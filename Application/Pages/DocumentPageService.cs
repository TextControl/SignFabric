using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public class DocumentPageService : IDocumentPageService {
		private readonly IStoreRepositoryFactory _storeFactory;

		public DocumentPageService(IStoreRepositoryFactory storeFactory) {
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
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

		private Envelope GetEnvelope(string userId, string envelopeId) =>
			GetEnvelope(_storeFactory.CreateEnvelopeRepository(userId), envelopeId);

		private static Envelope GetEnvelope(IEnvelopeRepository store, string envelopeId) =>
			store.GetEnvelopes(envelopeId).FirstOrDefault() ?? throw new InvalidOperationException("Envelope not found");

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
