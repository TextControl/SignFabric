using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public class EditableDocumentService : IEditableDocumentService {
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly ITxDocumentService _txService;

		public EditableDocumentService(
			IStoreRepositoryFactory storeFactory,
			ITxDocumentService txService) {
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_txService = txService ?? throw new ArgumentNullException(nameof(txService));
		}

		public async Task<string> GetEditableDocumentAsync(string userId, string documentType, string documentId) {
			return await Task.Run(() => {
				string document = documentType switch {
					"envelope" => _storeFactory.CreateEnvelopeRepository(userId).GetDocument(documentId),
					"template" => _storeFactory.CreateTemplateRepository(userId).GetDocument(documentId),
					"contract" => _storeFactory.CreateContractRepository(userId).GetDocument(documentId),
					_ => throw new InvalidOperationException($"Unknown document type {documentType}")
				};

				if (documentType == "contract") {
					return document;
				}

				return Convert.ToBase64String(_txService.SetFieldConditions(document, false));
			});
		}

		public async Task SaveDocumentAsync(string userId, string documentType, string documentId, string documentBase64) {
			await Task.Run(() => {
				byte[] document = Convert.FromBase64String(documentBase64);
				string normalizedDocumentBase64 = Convert.ToBase64String(document);
				document = _txService.SetFieldConditions(normalizedDocumentBase64, true);
				string savedDocumentBase64 = Convert.ToBase64String(document);
				string thumbnail = _txService.GenerateThumbnail(savedDocumentBase64);

				using (var stream = new MemoryStream(document)) {
					switch (documentType) {
						case "envelope":
							var envelopeStore = _storeFactory.CreateEnvelopeRepository(userId);
							var envelope = envelopeStore.GetEnvelopes(documentId).FirstOrDefault() ?? throw new InvalidOperationException("Envelope not found");
							envelope.ContainsSignatureBoxes = _txService.ContainsSignatureBoxes(savedDocumentBase64, envelope.Signers);
							envelopeStore.UpdateFile(envelope, stream);
							envelopeStore.AddThumbnail(envelope, thumbnail);
							envelopeStore.Update(envelope.EnvelopeID, envelope);
							break;
						case "template":
							var templateStore = _storeFactory.CreateTemplateRepository(userId);
							var template = templateStore.GetTemplates(documentId).FirstOrDefault() ?? throw new InvalidOperationException("Template not found");
							templateStore.UpdateFile(template, stream);
							templateStore.AddThumbnail(template, thumbnail);
							templateStore.Update(template.TemplateID, template);
							break;
						case "contract":
							var contractStore = _storeFactory.CreateContractRepository(userId);
							var contract = contractStore.GetContracts(documentId).FirstOrDefault() ?? throw new InvalidOperationException("Contract not found");
							contractStore.UpdateFile(contract, stream);
							contractStore.AddThumbnail(contract, thumbnail);
							contractStore.Update(contract.ContractID, contract);
							break;
						default:
							throw new InvalidOperationException($"Unknown document type {documentType}");
					}
				}
			});
		}
	}
}
