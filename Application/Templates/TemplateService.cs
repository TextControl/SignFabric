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
using System.Threading.Tasks;

namespace SignFabric.Application.Templates {
	/// <summary>
	/// Application service for template management
	/// Manages document templates that can be used for signing workflows
	/// </summary>
	public class TemplateService : ITemplateService {
		private readonly ITxDocumentService _txService;
		private readonly IAuditLogger _auditLogger;
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly string _userId;

		public TemplateService(
			ITxDocumentService txService,
			IAuditLogger auditLogger,
			IStoreRepositoryFactory storeFactory,
			string userId) {
			_txService = txService ?? throw new ArgumentNullException(nameof(txService));
			_auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_userId = userId ?? throw new ArgumentNullException(nameof(userId));
		}

		public async Task<Template> CreateAsync(Template template, MemoryStream documentStream) {
			try {
				var store = _storeFactory.CreateTemplateRepository(_userId);
				template.TemplateID = Guid.NewGuid().ToString();

				store.Add(template, documentStream);

				// Generate and store thumbnail
				var documentBase64 = store.GetDocument(template.TemplateID);
				var thumbnail = _txService.GenerateThumbnail(documentBase64);
				store.AddThumbnail(template, thumbnail);

				return template;
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine($"Error creating template: {ex.Message}");
				throw;
			}
		}

		public async Task<Template> GetAsync(string templateId) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateTemplateRepository(_userId);
				var templates = store.GetTemplates(templateId);
				return templates.FirstOrDefault();
			});
		}

		public async Task<List<Template>> GetAllAsync(string userId) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateTemplateRepository(userId);
				return store.GetTemplates();
			});
		}

		public async Task UpdateAsync(Template template) {
			await Task.Run(() => {
				var store = _storeFactory.CreateTemplateRepository(_userId);
				store.Update(template.TemplateID, template);
			});
		}

		public async Task DeleteAsync(string templateId) {
			await Task.Run(() => {
				var store = _storeFactory.CreateTemplateRepository(_userId);
				if (store.GetTemplates(templateId).Any()) {
					store.Delete(templateId);
				}
			});
		}
	}
}
