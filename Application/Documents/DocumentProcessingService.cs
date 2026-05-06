using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Templates;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	/// <summary>
	/// Implementation of IDocumentProcessingService
	/// Handles document upload, conversion, and thumbnail generation
	/// </summary>
	public class DocumentProcessingService : IDocumentProcessingService {
		private readonly ITxDocumentService _txService;
		private readonly ITemplateService _templateService;
		private readonly IEnvelopeService _envelopeService;
		private readonly IStoreRepositoryFactory _storeFactory;

		public DocumentProcessingService(
			ITxDocumentService txService,
			ITemplateService templateService,
			IEnvelopeService envelopeService,
			IStoreRepositoryFactory storeFactory) {
			_txService = txService ?? throw new ArgumentNullException(nameof(txService));
			_templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
			_envelopeService = envelopeService ?? throw new ArgumentNullException(nameof(envelopeService));
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
		}

		public async Task<(Template Template, string Thumbnail)> ProcessNewTemplateAsync(
			MemoryStream documentStream,
			string fileName,
			string userId) {
			try {
				documentStream.Position = 0;
				byte[] data = documentStream.ToArray();
				string base64Document = Convert.ToBase64String(data);

				// Generate thumbnail
				string thumbnail = _txService.GenerateThumbnail(base64Document);

				// Create template
				var template = new Template {
					TemplateID = Guid.NewGuid().ToString(),
					Name = fileName,
					UserID = userId
				};

				// Save to storage
				documentStream.Position = 0;
				await _templateService.CreateAsync(template, documentStream);

				return (template, thumbnail);
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine($"Error processing template: {ex.Message}");
				throw;
			}
		}

		public async Task<(Envelope Envelope, string Thumbnail)> ProcessNewEnvelopeAsync(
			MemoryStream documentStream,
			string fileName,
			string userId,
			string senderName) {
			try {
				documentStream.Position = 0;
				byte[] data = documentStream.ToArray();
				string base64Document = Convert.ToBase64String(data);

				// Generate thumbnail
				string thumbnail = _txService.GenerateThumbnail(base64Document);

				// Create envelope
				var envelope = new Envelope {
					EnvelopeID = Guid.NewGuid().ToString(),
					Name = fileName,
					UserID = userId,
					Sender = senderName,
					Status = EnvelopeStatus.New
				};

				// Save to storage
				documentStream.Position = 0;
				await _envelopeService.CreateAsync(envelope, documentStream);

				return (envelope, thumbnail);
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine($"Error processing envelope: {ex.Message}");
				throw;
			}
		}

		public async Task<string> GenerateThumbnailAsync(string base64Document) {
			return await Task.Run(() => {
				try {
					return _txService.GenerateThumbnail(base64Document);
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error generating thumbnail: {ex.Message}");
					throw;
				}
			});
		}

		public async Task UpdateDocumentAsync(
			string documentId,
			MemoryStream documentStream,
			string documentType,
			string userId) {
			await Task.Run(() => {
				try {
					switch (documentType.ToLower()) {
						case "template":
							var templateStore = _storeFactory.CreateTemplateRepository(userId);
							var template = new Template { TemplateID = documentId };
							templateStore.UpdateFile(template, documentStream);
							break;
						case "envelope":
							var envelopeStore = _storeFactory.CreateEnvelopeRepository(userId);
							var envelope = new Envelope { EnvelopeID = documentId };
							envelopeStore.UpdateFile(envelope, documentStream);
							break;
						default:
							throw new InvalidOperationException($"Unknown document type: {documentType}");
					}
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error updating document: {ex.Message}");
					throw;
				}
			});
		}
	}
}
