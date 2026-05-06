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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TXTextControl.Web.MVC.DocumentViewer.Models;

namespace SignFabric.Application.Services {
	/// <summary>
	/// Application service for signing workflow
	/// Manages the document signing process and signer state
	/// </summary>
	public class SigningWorkflowService : ISigningWorkflowService {
		private readonly ITxDocumentService _txService;
		private readonly IEmailSender _emailSender;
		private readonly IAuditLogger _auditLogger;
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly string _userId;
		private readonly ILogger<SigningWorkflowService> _logger;

		public SigningWorkflowService(
			ITxDocumentService txService,
			IEmailSender emailSender,
			IAuditLogger auditLogger,
			IStoreRepositoryFactory storeFactory,
			string userId,
			ILogger<SigningWorkflowService> logger) {
			_txService = txService ?? throw new ArgumentNullException(nameof(txService));
			_emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
			_auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
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

				if (signer.SignatureInformation != null) {
					return new ExternalSigningPreparation {
						AccessId = accessId,
						Envelope = envelope,
						Signer = signer,
						AlreadySigned = true
					};
				}

				signer.SignerStatus = SignerStatus.Opened;
				store.Update(envelope.EnvelopeID, envelope);

				string document = store.GetDocument(envelope.EnvelopeID);
				byte[] preparedDocument = _txService.PrepareFormFields(document, signer);

				return new ExternalSigningPreparation {
					AccessId = accessId,
					Document = Convert.ToBase64String(preparedDocument),
					Envelope = envelope,
					Signer = signer
				};
			});
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
					parts = Encoding.ASCII.GetString(octets).Split(':');
				}
				catch (FormatException) {
					return InvalidValidationResult("The embedded validation id is invalid.");
				}

				if (parts.Length < 2) {
					return InvalidValidationResult("The embedded validation id is incomplete.");
				}

				var store = _storeFactory.CreateEnvelopeRepository(parts[1]);
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

					return new ValidatedDocument {
						Envelope = envelope,
						Valid = CalculateMD5(Convert.FromBase64String(storedDocument)) == CalculateMD5(uploadedDocument)
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

		public async Task CompleteDocumentViewerSigningAsync(SignatureData data, string userId, string envelopeId, string signerId, string ipAddress) {
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

				currentSigner.SignatureInformation = new SignatureModel {
					Document = data.SignedDocument.Document,
					IPAddress = ipAddress,
					NumPages = data.SignedDocument.NumPages,
					SignerInitials = data.SignedDocument.SignerInitials,
					SignerName = data.SignedDocument.SignerName,
					TimeStamp = data.SignedDocument.TimeStamp,
					UniqueId = data.SignedDocument.UniqueId
				};

				store.Update(envelope.EnvelopeID, envelope);

				byte[] signedDocument = Convert.FromBase64String(data.SignedDocument.Document);

				using (var ms = new MemoryStream(signedDocument)) {
					store.UploadSignedDocument(envelope, ms, signerId);
				}

				var signatureBoxResult = data.SignedDocument.SignatureBoxMergeResults?.FirstOrDefault();
				if (string.IsNullOrWhiteSpace(signatureBoxResult?.ImageResult)) {
					throw new InvalidOperationException("The signature image was not received. Please reload the signing page and try again.");
				}

				byte[] signatureImage = Convert.FromBase64String(signatureBoxResult.ImageResult);

				using (var memStream = new MemoryStream(signatureImage, 0, signatureImage.Length, writable: false, publiclyVisible: true)) {
					store.UploadSignatureImage(envelope, memStream, signerId);
				}

				currentSigner.SignerStatus = SignerStatus.Signed;
				store.Update(envelope.EnvelopeID, envelope);

				await TrySendSignedConfirmationAsync(envelope, currentSigner);

				if (envelope.Signers.All(signer => signer.SignerStatus == SignerStatus.Signed)) {
					try {
						string masterDocument = envelope.Signers.Count > 1
							? store.GetDocument(envelopeId)
							: store.GetSignedDocument(envelopeId, envelope.Signers[0].Id);

						var createdPDF = _txService.CreateSignedPdf(envelope, masterDocument);

						using (var ms = new MemoryStream(createdPDF.PdfData)) {
							store.UploadFinalSignedDocument(envelope, ms);
						}

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

				store.Update(envelope.EnvelopeID, envelope);
				await _auditLogger.LogDocumentSignedAsync(envelope.EnvelopeID, signerId, DateTime.UtcNow);
			});
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

				if (!envelope.Signers.All(s => s.SignerStatus == SignerStatus.Signed)) {
					throw new InvalidOperationException("Not all signers have signed the document");
				}

				// Get the master document (logic from ReviewController)
				string masterDocument;
				if (envelope.Signers.Count > 1) {
					masterDocument = store.GetDocument(envelopeId);
				} else {
					masterDocument = store.GetSignedDocument(envelopeId, envelope.Signers[0].Id);
				}

				try {
					// Create final PDF with all signatures
					var (pdfData, thumbnailSvg) = _txService.CreateSignedPdf(envelope, masterDocument);

					// Store final document and thumbnail
					using (var ms = new MemoryStream(pdfData)) {
						store.UploadFinalSignedDocument(envelope, ms);
					}

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

		private static (string EnvelopeId, string OwnerUserId, string SignerId) DecodeSigningAccessId(string accessId) {
			byte[] octets = Convert.FromBase64String(accessId);
			string[] parts = Encoding.ASCII.GetString(octets).Split(':');

			if (parts.Length < 3) {
				throw new InvalidOperationException("Invalid signing access id.");
			}

			return (parts[0], parts[1], parts[2]);
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

		private static string CalculateMD5(byte[] document) {
			using (var md5 = MD5.Create()) {
				return BitConverter.ToString(md5.ComputeHash(document)).Replace("-", "").ToLowerInvariant();
			}
		}
	}
}
