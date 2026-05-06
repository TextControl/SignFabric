using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public class TemplateWorkflowService : ITemplateWorkflowService {
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly ITxDocumentService _txService;
		private readonly IEnvelopeDocumentFactory _envelopeDocumentFactory;

		public TemplateWorkflowService(
			IStoreRepositoryFactory storeFactory,
			ITxDocumentService txService,
			IEnvelopeDocumentFactory envelopeDocumentFactory) {
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_txService = txService ?? throw new ArgumentNullException(nameof(txService));
			_envelopeDocumentFactory = envelopeDocumentFactory ?? throw new ArgumentNullException(nameof(envelopeDocumentFactory));
		}

		public Task<NewTemplateModel> CreateAsync(string userId, MemoryStream documentStream, string fileName) =>
			Task.Run(() => CreateTemplateCore(userId, documentStream, fileName));

		public async Task<NewTemplateModel> CreateBlankAsync(string userId, string documentName) {
			return await Task.Run(() => {
				var name = NormalizeTemplateName(documentName);
				using var stream = new MemoryStream(_txService.CreateBlankInternalFormat());
				return CreateTemplateCore(userId, stream, name);
			});
		}

		public async Task RenameAsync(string userId, string templateId, string documentName) {
			await Task.Run(() => {
				var store = _storeFactory.CreateTemplateRepository(userId);
				var template = store.GetTemplates(templateId).First();
				template.Name = NormalizeTemplateName(documentName);
				store.Update(template.TemplateID, template);
			});
		}

		public async Task<List<FieldModel>> GetFieldsAsync(string userId, string templateId) =>
			await Task.Run(() => _txService.GetMergeFields(_storeFactory.CreateTemplateRepository(userId).GetDocument(templateId)));

		public async Task<string> CreateEnvelopeFromTemplateAsync(string userId, string userName, string templateId, IDictionary<string, string> fields) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateTemplateRepository(userId);
				var template = store.GetTemplates(templateId).First();
				string json = "{" + string.Join(",", fields.Select(field => $"\"{field.Key}\":\"{field.Value}\"")) + "}";
				using var data = new MemoryStream(_txService.MergeJson(store.GetDocument(templateId), json));
				return _envelopeDocumentFactory.CreateEnvelopeFromDocument(userId, userName, data, template.Name);
			});
		}

		private NewTemplateModel CreateTemplateCore(string userId, MemoryStream stream, string fileName) {
			byte[] data = stream.ToArray();
			string image = _txService.GenerateThumbnail(Convert.ToBase64String(data));
			stream = new MemoryStream(_txService.GetInternalFormat(Convert.ToBase64String(data)));
			var template = new Template { Created = DateTime.Now, UserID = userId, Name = fileName, TemplateID = Guid.NewGuid().ToString() };
			var store = _storeFactory.CreateTemplateRepository(userId);
			store.Add(template, stream);
			store.AddThumbnail(template, image);
			return new NewTemplateModel { Template = template, Thumbnail = Convert.ToBase64String(Encoding.UTF8.GetBytes(image)) };
		}

		private static string NormalizeTemplateName(string documentName) {
			var name = (documentName ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(name)) {
				throw new InvalidOperationException("Enter a document name.");
			}

			return name;
		}
	}
}
