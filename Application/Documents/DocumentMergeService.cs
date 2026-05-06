using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Application.Envelopes;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	/// <summary>
	/// Implementation of IDocumentMergeService
	/// Handles merging JSON data into templates and creating envelopes from templates
	/// </summary>
	public class DocumentMergeService : IDocumentMergeService {
		private readonly ITxDocumentService _txService;
		private readonly IEnvelopeService _envelopeService;
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly string _userId;

		public DocumentMergeService(
			ITxDocumentService txService,
			IEnvelopeService envelopeService,
			IStoreRepositoryFactory storeFactory,
			string userId) {
			_txService = txService ?? throw new ArgumentNullException(nameof(txService));
			_envelopeService = envelopeService ?? throw new ArgumentNullException(nameof(envelopeService));
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_userId = userId ?? throw new ArgumentNullException(nameof(userId));
		}

		public async Task<byte[]> MergeJsonAsync(string base64Document, string jsonData) {
			return await Task.Run(() => {
				try {
					return _txService.MergeJson(base64Document, jsonData);
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error merging JSON: {ex.Message}");
					throw;
				}
			});
		}

		public async Task<(string EnvelopeId, MemoryStream Document)> CreateEnvelopeFromTemplateAsync(
			string templateId,
			string jsonData,
			string userId,
			string senderName) {
			try {
				// Get template
				var templateStore = _storeFactory.CreateTemplateRepository(userId);
				var templates = templateStore.GetTemplates(templateId);
				if (templates.Count == 0) {
					throw new InvalidOperationException($"Template {templateId} not found");
				}

				var template = templates[0];
				var templateDocument = templateStore.GetDocument(templateId);

				// Merge JSON into template
				byte[] mergedDocument = await MergeJsonAsync(templateDocument, jsonData);

				// Create envelope from merged document
				var envelope = new Envelope {
					EnvelopeID = Guid.NewGuid().ToString(),
					Name = template.Name,
					UserID = userId,
					Sender = senderName,
					Status = EnvelopeStatus.New
				};

				// Save envelope
				var ms = new MemoryStream(mergedDocument);
				var envelopeStore = _storeFactory.CreateEnvelopeRepository(userId);
				envelopeStore.Add(envelope, ms);

				// Generate and store thumbnail
				var thumbnail = _txService.GenerateThumbnail(Convert.ToBase64String(mergedDocument));
				envelopeStore.AddThumbnail(envelope, thumbnail);

				return (envelope.EnvelopeID, new MemoryStream(mergedDocument));
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine($"Error creating envelope from template: {ex.Message}");
				throw;
			}
		}

	}
}
