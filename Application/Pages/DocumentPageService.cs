using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public class DocumentPageService : IDocumentPageService {
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly ICertificateManagementService _certificateManagementService;

		public DocumentPageService(
			IStoreRepositoryFactory storeFactory,
			ICertificateManagementService certificateManagementService) {
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_certificateManagementService = certificateManagementService ?? throw new ArgumentNullException(nameof(certificateManagementService));
		}

		public Task<List<Envelope>> GetEnvelopesAsync(string userId) =>
			Task.Run(() => _storeFactory.CreateEnvelopeRepository(userId).GetEnvelopes());

		public Task<Envelope> GetEnvelopeAsync(string userId, string envelopeId) {
			return Task.Run(() => {
				var envelope = GetEnvelope(userId, envelopeId);
				EnsureOwner(envelope.UserID, userId);
				return envelope;
			});
		}

		public Task<EnvelopeDetailsView> GetEnvelopeDetailsAsync(string userId, string envelopeId) {
			return Task.Run(() => {
				var store = _storeFactory.CreateEnvelopeRepository(userId);
				var envelope = GetEnvelope(store, envelopeId);
				EnsureOwner(envelope.UserID, userId);
				EnsureAuditEvidence(store, envelope, GetSigningCertificateEvidence(envelope));

				var signatureImages = new Dictionary<string, string>();
				foreach (var signer in envelope.Signers) {
					if (signer.SignerStatus == SignerStatus.Signed && signer.SignatureInformation != null) {
						signatureImages[signer.Id] = store.GetSignatureImageRaw(envelopeId, signer.Id);
					}
				}

				return new EnvelopeDetailsView {
					Envelope = envelope,
					ThumbnailSvg = store.GetThumbnail(envelopeId),
					SignatureImages = signatureImages
				};
			});
		}

		public Task<SignModel> GetEnvelopeEditModelAsync(string userId, string envelopeId) {
			return Task.Run(() => {
				var store = _storeFactory.CreateEnvelopeRepository(userId);
				return new SignModel {
					Document = store.GetDocument(envelopeId),
					Envelope = GetEnvelope(store, envelopeId)
				};
			});
		}

		public Task<SignatureBoxModel> GetEnvelopeSignatureBoxModelAsync(string userId, string envelopeId) {
			return Task.Run(() => {
				var envelope = GetEnvelope(userId, envelopeId);
				return new SignatureBoxModel {
					ContainsSignatureBoxes = envelope.ContainsSignatureBoxes,
					EnvelopeID = envelope.EnvelopeID
				};
			});
		}

		public Task<(byte[] Document, string FileName)> DownloadEnvelopeAsync(string userId, string envelopeId) {
			return Task.Run(() => {
				var store = _storeFactory.CreateEnvelopeRepository(userId);
				var envelope = GetEnvelope(store, envelopeId);
				EnsureOwner(envelope.UserID, userId);

				if (envelope.Status != EnvelopeStatus.Signed) {
					throw new InvalidOperationException("Envelope must be fully signed before download.");
				}

				string pdfData = store.GetFinalSignedDocument(envelopeId);
				if (string.IsNullOrEmpty(pdfData)) {
					throw new InvalidOperationException("Error retrieving signed document.");
				}

				return (Convert.FromBase64String(pdfData), envelope.Name);
			});
		}

		public Task<List<Template>> GetTemplatesAsync(string userId) =>
			Task.Run(() => _storeFactory.CreateTemplateRepository(userId).GetTemplates());

		public Task<TemplateDetailsView> GetTemplateDetailsAsync(string userId, string templateId) {
			return Task.Run(() => {
				var store = _storeFactory.CreateTemplateRepository(userId);
				return new TemplateDetailsView {
					Template = GetTemplate(store, templateId),
					ThumbnailSvg = store.GetThumbnail(templateId)
				};
			});
		}

		public Task<TemplateEditModel> GetTemplateEditModelAsync(string userId, string templateId) {
			return Task.Run(() => {
				var store = _storeFactory.CreateTemplateRepository(userId);
				return new TemplateEditModel {
					Document = store.GetDocument(templateId),
					Template = GetTemplate(store, templateId)
				};
			});
		}

		public Task<List<Contract>> GetContractsAsync(string userId) =>
			Task.Run(() => _storeFactory.CreateContractRepository(userId).GetContracts());

		public Task<ContractDetailsView> GetContractDetailsAsync(string userId, string contractId) {
			return Task.Run(() => {
				var store = _storeFactory.CreateContractRepository(userId);
				return new ContractDetailsView {
					Contract = GetContract(store, contractId),
					ThumbnailSvg = store.GetThumbnail(contractId)
				};
			});
		}

		public Task<ContractEditModel> GetContractEditModelAsync(string userId, string contractId) {
			return Task.Run(() => {
				var store = _storeFactory.CreateContractRepository(userId);
				return new ContractEditModel {
					Document = store.GetDocument(contractId),
					Contract = GetContract(store, contractId)
				};
			});
		}

		private Envelope GetEnvelope(string userId, string envelopeId) =>
			GetEnvelope(_storeFactory.CreateEnvelopeRepository(userId), envelopeId);

		private static Envelope GetEnvelope(IEnvelopeRepository store, string envelopeId) =>
			store.GetEnvelopes(envelopeId).FirstOrDefault() ?? throw new InvalidOperationException("Envelope not found");

		private SigningCertificateEvidence GetSigningCertificateEvidence(Envelope envelope) {
			if (envelope.SigningCertificate != null) {
				return envelope.SigningCertificate;
			}

			var certificate = GetSigningCertificateSummary(envelope);
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

		private SigningCertificateSummary GetSigningCertificateSummary(Envelope envelope) {
			var certificates = _certificateManagementService.GetCertificatesAsync().GetAwaiter().GetResult();
			return certificates.FirstOrDefault(certificate =>
					string.Equals(certificate.Id, envelope.SigningCertificateId, StringComparison.OrdinalIgnoreCase)) ??
				certificates.FirstOrDefault(certificate => certificate.IsActive);
		}

		private static void EnsureAuditEvidence(IEnvelopeRepository store, Envelope envelope, SigningCertificateEvidence signingCertificate) {
			if (envelope.Status != EnvelopeStatus.Signed) {
				return;
			}

			var changed = false;

			if (string.IsNullOrWhiteSpace(envelope.ValidationId)) {
				envelope.ValidationId = Convert.ToBase64String(Encoding.UTF8.GetBytes(envelope.EnvelopeID + ":" + envelope.UserID));
				changed = true;
			}

			if (envelope.SigningCertificate == null && signingCertificate != null) {
				envelope.SigningCertificate = signingCertificate;
				changed = true;
			}

			if (string.IsNullOrWhiteSpace(envelope.OriginalDocumentHashSha256)) {
				try {
					envelope.OriginalDocumentHashSha256 = CalculateSha256(Convert.FromBase64String(store.GetDocument(envelope.EnvelopeID)));
					changed = true;
				}
				catch {
					// Best-effort backfill for existing envelopes.
				}
			}

			if (string.IsNullOrWhiteSpace(envelope.FinalDocumentHashSha256) || string.IsNullOrWhiteSpace(envelope.FinalDocumentHashMD5) || !envelope.FinalDocumentSizeBytes.HasValue) {
				try {
					var finalDocument = Convert.FromBase64String(store.GetFinalSignedDocument(envelope.EnvelopeID));
					envelope.FinalDocumentHashSha256 ??= CalculateSha256(finalDocument);
					envelope.FinalDocumentHashMD5 ??= CalculateMD5(finalDocument);
					envelope.FinalDocumentSizeBytes ??= finalDocument.LongLength;
					changed = true;
				}
				catch {
					// Best-effort backfill for existing envelopes.
				}
			}

			foreach (var signer in envelope.Signers.Where(item => item.SignatureInformation != null)) {
				if (string.IsNullOrWhiteSpace(signer.SignatureInformation.DocumentHashSha256)) {
					try {
						signer.SignatureInformation.DocumentHashSha256 = CalculateSha256(Convert.FromBase64String(store.GetSignedDocument(envelope.EnvelopeID, signer.Id)));
						changed = true;
					}
					catch {
						// Best-effort backfill for existing envelopes.
					}
				}

				if (string.IsNullOrWhiteSpace(signer.SignatureInformation.SignatureImageHashSha256)) {
					try {
						signer.SignatureInformation.SignatureImageHashSha256 = CalculateSha256(Convert.FromBase64String(store.GetSignatureImage(envelope.EnvelopeID, signer.Id)));
						changed = true;
					}
					catch {
						// Best-effort backfill for existing envelopes.
					}
				}
			}

			if (changed) {
				store.Update(envelope.EnvelopeID, envelope);
			}
		}

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

		private static Template GetTemplate(ITemplateRepository store, string templateId) =>
			store.GetTemplates(templateId).FirstOrDefault() ?? throw new InvalidOperationException("Template not found");

		private static Contract GetContract(IContractRepository store, string contractId) =>
			store.GetContracts(contractId).FirstOrDefault() ?? throw new InvalidOperationException("Contract not found");

		private static void EnsureOwner(string ownerUserId, string currentUserId) {
			if (ownerUserId != currentUserId) {
				throw new UnauthorizedAccessException();
			}
		}
	}
}
