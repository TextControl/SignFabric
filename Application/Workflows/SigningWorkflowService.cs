using SignFabric.Application.Services;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Signing;
using SignFabric.Application.Templates;
using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TXTextControl.Web.MVC.DocumentViewer.Models;

namespace SignFabric.Application.Services {
	/// <summary>
	/// Application service for signing workflow
	/// Manages the document signing process and signer state
	/// </summary>
	public class SigningWorkflowService : ISigningWorkflowService {
		private const int EmailOtpLifetimeMinutes = 10;
		private const int EmailOtpMaxAttempts = 5;
		private const int EmailOtpAutoSendThrottleSeconds = 45;

		private readonly ITxDocumentService _txService;
		private readonly IEmailSender _emailSender;
		private readonly IAuditLogger _auditLogger;
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly ICertificateManagementService _certificateManagementService;
		private readonly string _userId;
		private readonly ILogger<SigningWorkflowService> _logger;

		public SigningWorkflowService(
			ITxDocumentService txService,
			IEmailSender emailSender,
			IAuditLogger auditLogger,
			IStoreRepositoryFactory storeFactory,
			ICertificateManagementService certificateManagementService,
			string userId,
			ILogger<SigningWorkflowService> logger) {
			_txService = txService ?? throw new ArgumentNullException(nameof(txService));
			_emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
			_auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_certificateManagementService = certificateManagementService ?? throw new ArgumentNullException(nameof(certificateManagementService));
			_userId = userId ?? throw new ArgumentNullException(nameof(userId));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		public async Task PrepareForSigningAsync(string envelopeId, string signerId) {
			try {
				var store = _storeFactory.CreateEnvelopeRepository(_userId);
				var envelope = store.GetEnvelopes(envelopeId).FirstOrDefault();
				
				if (envelope == null) {
					throw new InvalidOperationException($"Envelope {envelopeId} not found");
				}

				var signer = envelope.Signers.FirstOrDefault(s => s.Id == signerId);
				if (signer == null) {
					throw new InvalidOperationException($"Signer {signerId} not found in envelope");
				}

				// Mark as opened
				signer.SignerStatus = SignerStatus.Opened;
				store.Update(envelope.EnvelopeID, envelope);

				await _auditLogger.LogDocumentSignedAsync(envelope.EnvelopeID, signerId, DateTime.UtcNow);
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine($"Error preparing for signing: {ex.Message}");
				throw;
			}
		}

		public async Task<ExternalSigningPreparation> PrepareExternalSigningAsync(string accessId) {
			return await Task.Run(() => {
				var (envelopeId, ownerUserId, signerId) = DecodeSigningAccessId(accessId);
				var store = _storeFactory.CreateEnvelopeRepository(ownerUserId);
				Envelope envelope = store.GetEnvelopes(envelopeId).FirstOrDefault()
					?? throw new InvalidOperationException($"Envelope {envelopeId} not found");
				Signer signer = envelope.Signers.FirstOrDefault(item => item.Id == signerId)
					?? throw new InvalidOperationException($"Signer {signerId} not found in envelope {envelopeId}");

				if (signer.Role != RecipientRole.Signer) {
					throw new InvalidOperationException("This recipient is not configured to sign the document.");
				}

				if (signer.SignatureInformation != null) {
					return new ExternalSigningPreparation {
						AccessId = accessId,
						Envelope = envelope,
						Signer = signer,
						AlreadySigned = true
					};
				}

				NormalizeRouting(envelope);
				if (!signer.RoutingActive && envelope.Status == EnvelopeStatus.Sent) {
					return new ExternalSigningPreparation {
						AccessId = accessId,
						Envelope = envelope,
						Signer = signer,
						NotActiveYet = true
					};
				}

				if (signer.RequireEmailOtp && !signer.EmailOtpVerified) {
					return new ExternalSigningPreparation {
						AccessId = accessId,
						Envelope = envelope,
						Signer = signer,
						RequiresEmailOtp = true
					};
				}

				signer.SignerStatus = SignerStatus.Opened;
				store.Update(envelope.EnvelopeID, envelope);

				string document = MergeEnvelopeAutoFillFields(store.GetDocument(envelope.EnvelopeID), envelope);
				byte[] preparedDocument = _txService.PrepareFormFields(document, signer);

				return new ExternalSigningPreparation {
					AccessId = accessId,
					Document = Convert.ToBase64String(preparedDocument),
					Envelope = envelope,
					Signer = signer
				};
			});
		}

		public async Task RequestSignerEmailOtpAsync(string accessId, bool forceNewCode = false) {
			var (envelope, signer, ownerUserId) = LoadSigningContext(accessId);

			if (!signer.RequireEmailOtp || signer.EmailOtpVerified) {
				return;
			}

			var now = DateTime.UtcNow;
			if (!forceNewCode &&
				signer.EmailOtpSentAt.HasValue &&
				signer.EmailOtpSentAt.Value > now.AddSeconds(-EmailOtpAutoSendThrottleSeconds) &&
				signer.EmailOtpExpiresAt.HasValue &&
				signer.EmailOtpExpiresAt.Value > now) {
				return;
			}

			var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);
			signer.EmailOtpCodeHash = HashEmailOtp(envelope.EnvelopeID, signer.Id, code);
			signer.EmailOtpSentAt = now;
			signer.EmailOtpExpiresAt = now.AddMinutes(EmailOtpLifetimeMinutes);
			signer.EmailOtpAttempts = 0;
			_storeFactory.CreateEnvelopeRepository(ownerUserId).Update(envelope.EnvelopeID, envelope);

			await _emailSender.SendSignerEmailOtpAsync(envelope, signer, code);
		}

		public async Task<ExternalSigningPreparation> VerifySignerEmailOtpAsync(string accessId, string code) {
			var (envelope, signer, ownerUserId) = LoadSigningContext(accessId);

			if (!signer.RequireEmailOtp) {
				return await PrepareExternalSigningAsync(accessId);
			}

			if (signer.EmailOtpVerified) {
				return await PrepareExternalSigningAsync(accessId);
			}

			if (string.IsNullOrWhiteSpace(code) ||
				string.IsNullOrWhiteSpace(signer.EmailOtpCodeHash) ||
				!signer.EmailOtpExpiresAt.HasValue ||
				signer.EmailOtpExpiresAt.Value < DateTime.UtcNow) {
				throw new InvalidOperationException("The verification code is missing or has expired.");
			}

			if (signer.EmailOtpAttempts >= EmailOtpMaxAttempts) {
				throw new InvalidOperationException("Too many verification attempts. Request a new code and try again.");
			}

			signer.EmailOtpAttempts++;
			if (!string.Equals(
				signer.EmailOtpCodeHash,
				HashEmailOtp(envelope.EnvelopeID, signer.Id, code.Trim()),
				StringComparison.OrdinalIgnoreCase)) {
				_storeFactory.CreateEnvelopeRepository(ownerUserId).Update(envelope.EnvelopeID, envelope);
				throw new InvalidOperationException("The verification code is not correct.");
			}

			MarkSignerAuthenticated(signer, SignerAuthenticationMethod.EmailOtp);
			_storeFactory.CreateEnvelopeRepository(ownerUserId).Update(envelope.EnvelopeID, envelope);

			return await PrepareExternalSigningAsync(accessId);
		}

		public Task TrustAuthenticatedSignerAsync(string accessId) {
			var (envelope, signer, ownerUserId) = LoadSigningContext(accessId);
			if (signer.RequireEmailOtp && !signer.EmailOtpVerified) {
				MarkSignerAuthenticated(signer, SignerAuthenticationMethod.SignerAccount);
				_storeFactory.CreateEnvelopeRepository(ownerUserId).Update(envelope.EnvelopeID, envelope);
			}

			return Task.CompletedTask;
		}

		public async Task<SigningThanksInfo> GetSigningThanksAsync(string accessId) {
			return await Task.Run(() => {
				var (envelopeId, ownerUserId, signerId) = DecodeSigningAccessId(accessId);
				var store = _storeFactory.CreateEnvelopeRepository(ownerUserId);
				Envelope envelope = store.GetEnvelopes(envelopeId).FirstOrDefault()
					?? throw new InvalidOperationException($"Envelope {envelopeId} not found");

				return new SigningThanksInfo {
					Envelope = envelope,
					Signer = envelope.Signers.FirstOrDefault(item => item.Id == signerId)
				};
			});
		}

		public async Task<ValidatedDocument> ValidateSignedDocumentAsync(byte[] uploadedDocument) {
			return await Task.Run(() => {
				string accessId;
				try {
					accessId = _txService.GetDocumentAccessId(uploadedDocument);
				}
				catch {
					return InvalidValidationResult("The uploaded file could not be read as a signed SignFabric PDF.");
				}

				if (string.IsNullOrEmpty(accessId)) {
					return InvalidValidationResult("The uploaded PDF does not contain a SignFabric validation id.");
				}

				string[] parts;
				try {
					byte[] octets = Convert.FromBase64String(accessId);
					parts = Encoding.UTF8.GetString(octets).Split(':');
				}
				catch (FormatException) {
					return InvalidValidationResult("The embedded validation id is invalid.");
				}

				if (parts.Length < 2) {
					return InvalidValidationResult("The embedded validation id is incomplete.");
				}

				var ownerUserId = string.Join(":", parts.Skip(1));
				var store = _storeFactory.CreateEnvelopeRepository(ownerUserId);
				Envelope envelope = store.GetEnvelopes(parts[0]).FirstOrDefault();

				if (envelope == null) {
					return InvalidValidationResult("The envelope referenced by this document could not be found.");
				}

				if (envelope.Status != EnvelopeStatus.Signed) {
					return new ValidatedDocument {
						Envelope = envelope,
						Valid = false,
						ErrorMessage = "The envelope is not fully signed yet."
					};
				}

				try {
					string storedDocument = store.GetFinalSignedDocument(envelope.EnvelopeID);
					var storedBytes = Convert.FromBase64String(storedDocument);
					var storedHashSha256 = string.IsNullOrWhiteSpace(envelope.FinalDocumentHashSha256)
						? CalculateSha256(storedBytes)
						: envelope.FinalDocumentHashSha256;

					return new ValidatedDocument {
						Envelope = envelope,
						Valid = string.Equals(storedHashSha256, CalculateSha256(uploadedDocument), StringComparison.OrdinalIgnoreCase)
					};
				}
				catch {
					return new ValidatedDocument {
						Envelope = envelope,
						Valid = false,
						ErrorMessage = "The final signed document could not be loaded for validation."
					};
				}
			});
		}

		private static ValidatedDocument InvalidValidationResult(string message) =>
			new ValidatedDocument {
				Valid = false,
				ErrorMessage = message
			};

		public async Task CompleteSigningAsync(string envelopeId, string signerId) {
			try {
				var store = _storeFactory.CreateEnvelopeRepository(_userId);
				var envelope = store.GetEnvelopes(envelopeId).FirstOrDefault();
				
				if (envelope == null) {
					throw new InvalidOperationException($"Envelope {envelopeId} not found");
				}

				var signer = envelope.Signers.FirstOrDefault(s => s.Id == signerId);
				if (signer == null) {
					throw new InvalidOperationException($"Signer {signerId} not found in envelope");
				}

				signer.SignerStatus = SignerStatus.Signed;
				store.Update(envelope.EnvelopeID, envelope);

				// Send confirmation email
				await _emailSender.SendSignedConfirmationAsync(envelope, signer);

				await _auditLogger.LogDocumentSignedAsync(envelope.EnvelopeID, signerId, DateTime.UtcNow);
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine($"Error completing signing: {ex.Message}");
				throw;
			}
		}

		public async Task CompleteDocumentViewerSigningAsync(SignatureData data, string userId, string envelopeId, string signerId, string ipAddress, string userAgent, string host) {
			await Task.Run(async () => {
				if (data?.SignedDocument == null) {
					throw new InvalidOperationException("The signed document data was not received. Please reload the signing page and try again.");
				}

				var store = _storeFactory.CreateEnvelopeRepository(userId);
				Envelope envelope = store.GetEnvelopes(envelopeId).FirstOrDefault();
				if (envelope == null) {
					throw new InvalidOperationException("The signing envelope could not be found. Please request a new signing link.");
				}

				Signer currentSigner = envelope.Signers.FirstOrDefault(signer => signer.Id == signerId);

				if (currentSigner == null) {
					throw new InvalidOperationException("The signer could not be found for this envelope. Please request a new signing link.");
				}

				byte[] signedDocument = Convert.FromBase64String(data.SignedDocument.Document);
				var signatureLines = ExtractSignatureLines(data);

				currentSigner.SignatureInformation = new SignatureModel {
					Document = data.SignedDocument.Document,
					DocumentHashSha256 = CalculateSha256(signedDocument),
					IPAddress = ipAddress,
					NumPages = data.SignedDocument.NumPages,
					SignerInitials = data.SignedDocument.SignerInitials,
					SignerName = data.SignedDocument.SignerName,
					TimeStamp = data.SignedDocument.TimeStamp,
					UniqueId = data.SignedDocument.UniqueId,
					UserAgent = NormalizeEvidenceValue(userAgent),
					SignatureBoxName = ResolveSignatureBoxName(data.SignatureBoxName, signerId),
					SignatureLines = signatureLines,
					SignatureMethod = signatureLines.Count > 0 ? "Drawn signature" : "Electronic signature"
				};

				store.Update(envelope.EnvelopeID, envelope);

				using (var ms = new MemoryStream(signedDocument)) {
					store.UploadSignedDocument(envelope, ms, signerId);
				}

				var signatureBoxResult = data.SignedDocument.SignatureBoxMergeResults?.FirstOrDefault();
				if (!string.IsNullOrWhiteSpace(signatureBoxResult?.ImageResult)) {
					byte[] signatureImage = Convert.FromBase64String(signatureBoxResult.ImageResult);
					currentSigner.SignatureInformation.SignatureImageHashSha256 = CalculateSha256(signatureImage);

					using (var memStream = new MemoryStream(signatureImage, 0, signatureImage.Length, writable: false, publiclyVisible: true)) {
						store.UploadSignatureImage(envelope, memStream, signerId);
					}
				}
				else {
					currentSigner.SignatureInformation.SignatureMethod = "Electronic signature";
					_logger.LogWarning(
						"No signature image was included for signer {SignerId} in envelope {EnvelopeId}. The signed document will be finalized without a separate signature image artifact.",
						signerId,
						envelopeId);
				}

				currentSigner.SignerStatus = SignerStatus.Signed;
				currentSigner.CompletedAt = DateTime.UtcNow;
				store.Update(envelope.EnvelopeID, envelope);

				await TrySendSignedConfirmationAsync(envelope, currentSigner);

				if (IsEnvelopeWorkflowComplete(envelope)) {
					try {
						var signingRecipients = envelope.Signers.Where(item => item.Role == RecipientRole.Signer).ToList();
						string masterDocument = signingRecipients.Count > 1
							? MergeEnvelopeAutoFillFields(store.GetDocument(envelopeId), envelope)
							: store.GetSignedDocument(envelopeId, signingRecipients[0].Id);

						var createdPDF = _txService.CreateSignedPdf(envelope, masterDocument);

						using (var ms = new MemoryStream(createdPDF.PdfData)) {
							store.UploadFinalSignedDocument(envelope, ms);
						}

						envelope.FinalizedAt = DateTime.UtcNow;
						envelope.FinalDocumentHashSha256 = CalculateSha256(createdPDF.PdfData);
						envelope.FinalDocumentHashMD5 = CalculateMD5(createdPDF.PdfData);
						envelope.FinalDocumentSizeBytes = createdPDF.PdfData.LongLength;
						envelope.OriginalDocumentHashSha256 = CalculateSha256(Convert.FromBase64String(store.GetDocument(envelope.EnvelopeID)));
						envelope.ValidationId = Convert.ToBase64String(Encoding.UTF8.GetBytes(envelope.EnvelopeID + ":" + userId));
						envelope.SigningCertificate ??= GetSigningCertificateEvidence(envelope);

						if (!string.IsNullOrWhiteSpace(createdPDF.ThumbnailSvg)) {
							store.AddThumbnail(envelope, createdPDF.ThumbnailSvg);
						}

						envelope.Status = EnvelopeStatus.Signed;
						envelope.FaultMessage = null;
						store.Update(envelope.EnvelopeID, envelope);

						await TrySendFinalSignedNotificationAsync(envelope, createdPDF.PdfData);
						await _auditLogger.LogEnvelopeCompletedAsync(envelope.EnvelopeID, DateTime.UtcNow);
					} catch (Exception ex) {
						MarkFinalizationFault(store, envelope, ex);
						await TrySendFinalizationFaultNotificationAsync(envelope);
						throw new InvalidOperationException(envelope.FaultMessage, ex);
					}
				}
				else if (ActivateNextRoutingStep(envelope)) {
					store.Update(envelope.EnvelopeID, envelope);
					await TrySendRoutingInvitationsAsync(envelope, host, userId);
				}

				store.Update(envelope.EnvelopeID, envelope);
				await _auditLogger.LogDocumentSignedAsync(envelope.EnvelopeID, signerId, DateTime.UtcNow);
			});
		}

		public async Task<ExternalApprovalPreparation> PrepareExternalApprovalAsync(string accessId) {
			return await Task.Run(() => {
				var (envelope, approver, _) = LoadSigningContext(accessId);
				NormalizeRouting(envelope);

				if (approver.Role != RecipientRole.Approver) {
					throw new InvalidOperationException("This recipient is not configured as an approver.");
				}

				return new ExternalApprovalPreparation {
					AccessId = accessId,
					Envelope = envelope,
					Approver = approver,
					AlreadyCompleted = approver.SignerStatus == SignerStatus.Signed,
					NotActiveYet = approver.SignerStatus != SignerStatus.Signed &&
						envelope.Status == EnvelopeStatus.Sent &&
						!approver.RoutingActive
				};
			});
		}

		public async Task CompleteApprovalAsync(string accessId, bool approved, string comment, string ipAddress, string userAgent, string host) {
			var (envelope, approver, ownerUserId) = LoadSigningContext(accessId);
			if (approver.Role != RecipientRole.Approver) {
				throw new InvalidOperationException("This recipient is not configured as an approver.");
			}

			if (!approver.RoutingActive && envelope.Status == EnvelopeStatus.Sent) {
				throw new InvalidOperationException("This approval step is not active yet.");
			}

			var store = _storeFactory.CreateEnvelopeRepository(ownerUserId);
			approver.ApprovalComment = approved ? comment : null;
			approver.DeclineReason = approved ? null : comment;
			approver.ApprovalIPAddress = ipAddress;
			approver.ApprovalUserAgent = NormalizeEvidenceValue(userAgent);
			approver.CompletedAt = DateTime.UtcNow;
			approver.SignerStatus = approved ? SignerStatus.Signed : SignerStatus.None;

			if (!approved) {
				envelope.Status = EnvelopeStatus.Faulted;
				envelope.FaultMessage = string.IsNullOrWhiteSpace(comment)
					? "The envelope was declined by an approver."
					: comment;
				store.Update(envelope.EnvelopeID, envelope);
				return;
			}

			if (IsEnvelopeWorkflowComplete(envelope)) {
				await FinalizeEnvelopeAsync(store, envelope, ownerUserId);
			}
			else if (ActivateNextRoutingStep(envelope)) {
				store.Update(envelope.EnvelopeID, envelope);
				await TrySendRoutingInvitationsAsync(envelope, host, ownerUserId);
			}
			else {
				store.Update(envelope.EnvelopeID, envelope);
			}
		}

		public async Task<bool> IsFullySignedAsync(string envelopeId) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateEnvelopeRepository(_userId);
				var envelope = store.GetEnvelopes(envelopeId).FirstOrDefault();
				
				if (envelope == null) {
					return false;
				}

				return envelope.Signers.All(s => s.SignerStatus == SignerStatus.Signed);
			});
		}

		public async Task GenerateFinalDocumentAsync(string envelopeId) {
			try {
				var store = _storeFactory.CreateEnvelopeRepository(_userId);
				var envelope = store.GetEnvelopes(envelopeId).FirstOrDefault();
				
				if (envelope == null) {
					throw new InvalidOperationException($"Envelope {envelopeId} not found");
				}

				if (!IsEnvelopeWorkflowComplete(envelope)) {
					throw new InvalidOperationException("Not all signers have signed the document");
				}

				// Get the master document (logic from ReviewController)
				string masterDocument;
				var signingRecipients = envelope.Signers.Where(item => item.Role == RecipientRole.Signer).ToList();
				if (signingRecipients.Count > 1) {
					masterDocument = MergeEnvelopeAutoFillFields(store.GetDocument(envelopeId), envelope);
				} else {
					masterDocument = store.GetSignedDocument(envelopeId, signingRecipients[0].Id);
				}

				try {
					// Create final PDF with all signatures
					var (pdfData, thumbnailSvg) = _txService.CreateSignedPdf(envelope, masterDocument);

					// Store final document and thumbnail
					using (var ms = new MemoryStream(pdfData)) {
						store.UploadFinalSignedDocument(envelope, ms);
					}

					envelope.FinalizedAt = DateTime.UtcNow;
					envelope.FinalDocumentHashSha256 = CalculateSha256(pdfData);
					envelope.FinalDocumentHashMD5 = CalculateMD5(pdfData);
					envelope.FinalDocumentSizeBytes = pdfData.LongLength;
					envelope.OriginalDocumentHashSha256 = CalculateSha256(Convert.FromBase64String(store.GetDocument(envelope.EnvelopeID)));
					envelope.ValidationId = Convert.ToBase64String(Encoding.UTF8.GetBytes(envelope.EnvelopeID + ":" + _userId));
					envelope.SigningCertificate ??= GetSigningCertificateEvidence(envelope);

					if (!string.IsNullOrWhiteSpace(thumbnailSvg)) {
						store.AddThumbnail(envelope, thumbnailSvg);
					}

					envelope.Status = EnvelopeStatus.Signed;
					envelope.FaultMessage = null;
					store.Update(envelope.EnvelopeID, envelope);

					// Send notifications to all signers
					await TrySendFinalSignedNotificationAsync(envelope, pdfData);

					await _auditLogger.LogEnvelopeCompletedAsync(envelope.EnvelopeID, DateTime.UtcNow);
				} catch (Exception ex) {
					MarkFinalizationFault(store, envelope, ex);
					await TrySendFinalizationFaultNotificationAsync(envelope);
					throw new InvalidOperationException(envelope.FaultMessage, ex);
				}
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine($"Error generating final document: {ex.Message}");
				throw;
			}
		}

		private static void MarkFinalizationFault(IEnvelopeRepository store, Envelope envelope, Exception exception) {
			envelope.Status = EnvelopeStatus.Faulted;
			envelope.FaultMessage = exception is InvalidOperationException && !string.IsNullOrWhiteSpace(exception.Message)
				? exception.Message
				: "The final signed PDF could not be created. Please review the signing certificate and signature fields, then try again.";
			store.Update(envelope.EnvelopeID, envelope);
		}

		private string MergeEnvelopeAutoFillFields(string documentBase64, Envelope envelope) {
			string jsonData = JsonSerializer.Serialize(BuildEnvelopeAutoFillData(envelope));
			return Convert.ToBase64String(_txService.MergeJson(documentBase64, jsonData));
		}

		private static Dictionary<string, string> BuildEnvelopeAutoFillData(Envelope envelope) {
			var data = new Dictionary<string, string> {
				["current_date"] = DateTime.Now.ToString("d", CultureInfo.CurrentCulture),
				["current_datetime"] = DateTime.Now.ToString("g", CultureInfo.CurrentCulture),
				["document_name"] = envelope.Name ?? string.Empty,
				["envelope_id"] = envelope.EnvelopeID ?? string.Empty,
				["sender_name"] = envelope.Sender ?? string.Empty
			};

			if (envelope.Sent != default) {
				data["sent_date"] = envelope.Sent.ToString("d", CultureInfo.CurrentCulture);
			}

			var primarySigner = envelope.Signers.FirstOrDefault();
			if (primarySigner != null) {
				data["signer_name"] = primarySigner.Name ?? string.Empty;
				data["signer_email"] = primarySigner.Email ?? string.Empty;
			}

			foreach (var signer in envelope.Signers) {
				var signerKey = SanitizeMergeFieldName(signer.Id);
				data[$"signer_{signerKey}_name"] = signer.Name ?? string.Empty;
				data[$"signer_{signerKey}_email"] = signer.Email ?? string.Empty;
			}

			return data;
		}

		private static string SanitizeMergeFieldName(string value) {
			var builder = new StringBuilder();

			foreach (char character in value ?? string.Empty) {
				builder.Append(char.IsLetterOrDigit(character) ? character : '_');
			}

			return builder.ToString().Trim('_');
		}

		private static (string EnvelopeId, string OwnerUserId, string SignerId) DecodeSigningAccessId(string accessId) {
			if (string.IsNullOrWhiteSpace(accessId) || Path.HasExtension(accessId)) {
				throw new InvalidOperationException("Invalid signing access id.");
			}

			string decodedAccessId;
			if (accessId.Contains(':')) {
				decodedAccessId = accessId;
			}
			else {
				try {
					var normalized = accessId.Trim().Replace(' ', '+').Replace('-', '+').Replace('_', '/');
					normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
					byte[] octets = Convert.FromBase64String(normalized);
					decodedAccessId = Encoding.UTF8.GetString(octets);
				}
				catch (FormatException ex) {
					throw new InvalidOperationException("Invalid signing access id.", ex);
				}
			}

			string[] parts = decodedAccessId.Split(':');

			if (parts.Length < 3) {
				throw new InvalidOperationException("Invalid signing access id.");
			}

			var envelopeId = parts[0];
			var ownerUserId = string.Join(":", parts.Skip(1).Take(parts.Length - 2));
			var signerId = parts[^1];

			if (string.IsNullOrWhiteSpace(envelopeId) ||
				string.IsNullOrWhiteSpace(ownerUserId) ||
				string.IsNullOrWhiteSpace(signerId)) {
				throw new InvalidOperationException("Invalid signing access id.");
			}

			return (envelopeId, ownerUserId, signerId);
		}

		private (Envelope Envelope, Signer Signer, string OwnerUserId) LoadSigningContext(string accessId) {
			var (envelopeId, ownerUserId, signerId) = DecodeSigningAccessId(accessId);
			var store = _storeFactory.CreateEnvelopeRepository(ownerUserId);
			Envelope envelope = store.GetEnvelopes(envelopeId).FirstOrDefault()
				?? throw new InvalidOperationException($"Envelope {envelopeId} not found");
			Signer signer = envelope.Signers.FirstOrDefault(item => item.Id == signerId)
				?? throw new InvalidOperationException($"Signer {signerId} not found in envelope {envelopeId}");

			return (envelope, signer, ownerUserId);
		}

		private static void MarkSignerAuthenticated(Signer signer, SignerAuthenticationMethod method) {
			signer.EmailOtpVerified = true;
			signer.EmailOtpVerifiedAt = DateTime.UtcNow;
			signer.AuthenticationMethod = method;
			signer.EmailOtpCodeHash = null;
			signer.EmailOtpExpiresAt = null;
			signer.EmailOtpAttempts = 0;
		}

		private static string NormalizeEvidenceValue(string value) =>
			string.IsNullOrWhiteSpace(value) ? "Not available" : value.Trim();

		private static string ResolveSignatureBoxName(string signatureBoxName, string signerId) =>
			string.IsNullOrWhiteSpace(signatureBoxName) ? $"txsign_{signerId}" : signatureBoxName.Trim();

		private static string HashEmailOtp(string envelopeId, string signerId, string code) {
			var material = $"{envelopeId}:{signerId}:{code}";
			return CalculateSha256(Encoding.UTF8.GetBytes(material));
		}

		private async Task TrySendSignedConfirmationAsync(Envelope envelope, Signer signer) {
			try {
				await _emailSender.SendSignedConfirmationAsync(envelope, signer);
			} catch (Exception ex) {
				_logger.LogWarning(ex, "The signed confirmation e-mail for signer {SignerId} could not be sent.", signer.Id);
			}
		}

		private async Task TrySendFinalSignedNotificationAsync(Envelope envelope, byte[] finalDocument) {
			try {
				await _emailSender.SendFinalSignedNotificationAsync(envelope, finalDocument);
			} catch (Exception ex) {
				_logger.LogWarning(ex, "The final signed notification e-mail for envelope {EnvelopeId} could not be sent.", envelope.EnvelopeID);
			}
		}

		private async Task TrySendFinalizationFaultNotificationAsync(Envelope envelope) {
			try {
				await _emailSender.SendFinalizationFaultNotificationAsync(envelope);
			} catch (Exception ex) {
				_logger.LogWarning(ex, "The finalization fault notification e-mail for envelope {EnvelopeId} could not be sent.", envelope.EnvelopeID);
			}
		}

		private async Task TrySendRoutingInvitationsAsync(Envelope envelope, string host, string ownerUserId) {
			try {
				await _emailSender.SendEnvelopeInvitationsAsync(envelope, host, ownerUserId);
			} catch (Exception ex) {
				_logger.LogWarning(ex, "The next routing invitation e-mails for envelope {EnvelopeId} could not be sent.", envelope.EnvelopeID);
			}
		}

		private async Task FinalizeEnvelopeAsync(IEnvelopeRepository store, Envelope envelope, string ownerUserId) {
			try {
				var signingRecipients = envelope.Signers.Where(item => item.Role == RecipientRole.Signer).ToList();
				if (!signingRecipients.Any()) {
					throw new InvalidOperationException("The envelope has no signer recipients.");
				}

				string masterDocument = signingRecipients.Count > 1
					? MergeEnvelopeAutoFillFields(store.GetDocument(envelope.EnvelopeID), envelope)
					: store.GetSignedDocument(envelope.EnvelopeID, signingRecipients[0].Id);

				var createdPDF = _txService.CreateSignedPdf(envelope, masterDocument);

				using (var ms = new MemoryStream(createdPDF.PdfData)) {
					store.UploadFinalSignedDocument(envelope, ms);
				}

				envelope.FinalizedAt = DateTime.UtcNow;
				envelope.FinalDocumentHashSha256 = CalculateSha256(createdPDF.PdfData);
				envelope.FinalDocumentHashMD5 = CalculateMD5(createdPDF.PdfData);
				envelope.FinalDocumentSizeBytes = createdPDF.PdfData.LongLength;
				envelope.OriginalDocumentHashSha256 = CalculateSha256(Convert.FromBase64String(store.GetDocument(envelope.EnvelopeID)));
				envelope.ValidationId = Convert.ToBase64String(Encoding.UTF8.GetBytes(envelope.EnvelopeID + ":" + ownerUserId));
				envelope.SigningCertificate ??= GetSigningCertificateEvidence(envelope);

				if (!string.IsNullOrWhiteSpace(createdPDF.ThumbnailSvg)) {
					store.AddThumbnail(envelope, createdPDF.ThumbnailSvg);
				}

				envelope.Status = EnvelopeStatus.Signed;
				envelope.FaultMessage = null;
				store.Update(envelope.EnvelopeID, envelope);

				await TrySendFinalSignedNotificationAsync(envelope, createdPDF.PdfData);
				await _auditLogger.LogEnvelopeCompletedAsync(envelope.EnvelopeID, DateTime.UtcNow);
			} catch (Exception ex) {
				MarkFinalizationFault(store, envelope, ex);
				await TrySendFinalizationFaultNotificationAsync(envelope);
				throw new InvalidOperationException(envelope.FaultMessage, ex);
			}
		}

		private static void NormalizeRouting(Envelope envelope) {
			foreach (var recipient in envelope.Signers) {
				if (recipient.Role == RecipientRole.Observer) {
					recipient.RoutingOrder = 0;
				}
				else if (recipient.RoutingOrder <= 0) {
					recipient.RoutingOrder = 1;
				}
			}

			if (envelope.WorkflowMode == EnvelopeWorkflowMode.Simple) {
				foreach (var recipient in envelope.Signers) {
					recipient.Role = RecipientRole.Signer;
					recipient.RoutingOrder = 1;
				}
			}
		}

		private static bool ActivateNextRoutingStep(Envelope envelope) {
			NormalizeRouting(envelope);
			var previousActiveIds = envelope.Signers
				.Where(recipient => recipient.RoutingActive)
				.Select(recipient => recipient.Id)
				.ToHashSet();

			var nextBlockingOrder = envelope.Signers
				.Where(IsBlockingRecipient)
				.Where(recipient => !IsRecipientComplete(recipient))
				.Select(recipient => recipient.RoutingOrder <= 0 ? 1 : recipient.RoutingOrder)
				.DefaultIfEmpty(0)
				.Min();

			foreach (var recipient in envelope.Signers) {
				if (recipient.Role == RecipientRole.Observer) {
					recipient.RoutingActive = true;
					if (!recipient.RoutingActivatedAt.HasValue) {
						recipient.RoutingActivatedAt = DateTime.UtcNow;
					}
					continue;
				}

				var recipientOrder = recipient.RoutingOrder <= 0 ? 1 : recipient.RoutingOrder;
				recipient.RoutingActive = IsBlockingRecipient(recipient)
					? nextBlockingOrder > 0 && recipientOrder == nextBlockingOrder
					: nextBlockingOrder == 0 || recipientOrder <= nextBlockingOrder;

				if (recipient.RoutingActive && !recipient.RoutingActivatedAt.HasValue) {
					recipient.RoutingActivatedAt = DateTime.UtcNow;
				}
			}

			return envelope.Signers.Any(recipient => recipient.RoutingActive && !previousActiveIds.Contains(recipient.Id));
		}

		private static bool IsEnvelopeWorkflowComplete(Envelope envelope) =>
			envelope.Signers.Where(IsBlockingRecipient).All(IsRecipientComplete);

		private static bool IsBlockingRecipient(Signer recipient) =>
			recipient.Role == RecipientRole.Signer || recipient.Role == RecipientRole.Approver;

		private static bool IsRecipientComplete(Signer recipient) =>
			recipient.SignerStatus == SignerStatus.Signed;

		private static string CalculateMD5(byte[] document) {
			using (var md5 = MD5.Create()) {
				return BitConverter.ToString(md5.ComputeHash(document)).Replace("-", "").ToLowerInvariant();
			}
		}

		private static string CalculateSha256(byte[] document) {
			using (var sha256 = SHA256.Create()) {
				return BitConverter.ToString(sha256.ComputeHash(document)).Replace("-", "").ToLowerInvariant();
			}
		}

		private SigningCertificateEvidence GetSigningCertificateEvidence(Envelope envelope) {
			var certificates = _certificateManagementService.GetCertificatesAsync().GetAwaiter().GetResult();
			var certificate = certificates.FirstOrDefault(item =>
					string.Equals(item.Id, envelope.SigningCertificateId, StringComparison.OrdinalIgnoreCase)) ??
				certificates.FirstOrDefault(item => item.IsActive);

			if (certificate == null) {
				return null;
			}

			var configuration = _certificateManagementService.GetConfigurationAsync().GetAwaiter().GetResult();
			return new SigningCertificateEvidence {
				RecordId = envelope.SigningCertificateId ?? certificate.Id,
				DisplayName = certificate.DisplayName,
				Thumbprint = certificate.Thumbprint,
				Subject = certificate.Subject,
				Issuer = certificate.Issuer,
				NotBefore = certificate.NotBefore,
				NotAfter = certificate.NotAfter,
				Provider = configuration.Provider,
				CapturedAt = DateTime.UtcNow
			};
		}

		private static List<SignatureStroke> ExtractSignatureLines(object data) {
			var signatureLines = data?.GetType().GetProperty("SignatureLines")?.GetValue(data);
			var result = new List<SignatureStroke>();

			if (signatureLines is not IEnumerable lines) {
				return result;
			}

			var lineIndex = 1;
			foreach (var line in lines) {
				if (line is not IEnumerable points) {
					continue;
				}

				var stroke = new SignatureStroke {
					Index = lineIndex++
				};

				foreach (var point in points) {
					if (point == null) {
						continue;
					}

					var pointType = point.GetType();
					var timestamp = Convert.ToInt64(pointType.GetProperty("CreationTimeStamp")?.GetValue(point) ?? 0, CultureInfo.InvariantCulture);
					stroke.Points.Add(new SignaturePointModel {
						X = Convert.ToInt32(pointType.GetProperty("X")?.GetValue(point) ?? 0, CultureInfo.InvariantCulture),
						Y = Convert.ToInt32(pointType.GetProperty("Y")?.GetValue(point) ?? 0, CultureInfo.InvariantCulture),
						CreationTimeStamp = timestamp,
						CreatedAtUtc = timestamp > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime : null
					});
				}

				result.Add(stroke);
			}

			return result;
		}
	}
}
