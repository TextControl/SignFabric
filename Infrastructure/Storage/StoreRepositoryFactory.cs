using SignFabric.Application.Abstractions;
using SignFabric.Infrastructure.Configuration;
using SignFabric.Infrastructure.Storage.LiteDb;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Collections.Generic;
using System.IO;

namespace SignFabric.Infrastructure.Storage {
	public class StoreRepositoryFactory : IStoreRepositoryFactory {
		private readonly AppSettingsPathResolver _paths;

		public StoreRepositoryFactory(AppSettingsPathResolver paths) {
			_paths = paths;
		}

		public IEnvelopeRepository CreateEnvelopeRepository(string userId) {
			return new EnvelopeRepository(new EnvelopeStore(userId, _paths));
		}

		public ITemplateRepository CreateTemplateRepository(string userId) {
			return new TemplateRepository(new TemplateStore(userId, _paths));
		}

		public IContractRepository CreateContractRepository(string userId) {
			return new ContractRepository(new ContractStore(userId, _paths));
		}
	}

	internal class EnvelopeRepository : IEnvelopeRepository {
		private readonly EnvelopeStore _store;

		public EnvelopeRepository(EnvelopeStore store) {
			_store = store;
		}

		public void Add(Envelope envelope, MemoryStream stream) => _store.Add(envelope, stream);
		public void Update(string envelopeId, Envelope envelope) => _store.Update(envelopeId, envelope);
		public void UpdateFile(Envelope envelope, MemoryStream stream) => _store.UpdateFile(envelope, stream);
		public List<Envelope> GetEnvelopes(string envelopeId = null) => _store.GetEnvelopes(envelopeId);
		public string GetDocument(string envelopeId) => _store.GetDocument(envelopeId);
		public string GetThumbnail(string envelopeId) => _store.GetThumbnail(envelopeId);
		public void AddThumbnail(Envelope envelope, string svgContent) => _store.AddThumbnail(envelope, svgContent);
		public string GetFinalSignedDocument(string envelopeId) => _store.GetFinalSignedDocument(envelopeId);
		public void UploadFinalSignedDocument(Envelope envelope, MemoryStream stream) => _store.UploadFinalSignedDocument(envelope, stream);
		public string GetSignedDocument(string envelopeId, string signerId) => _store.GetSignedDocument(envelopeId, signerId);
		public void UploadSignedDocument(Envelope envelope, MemoryStream stream, string signerId) => _store.UploadSignedDocument(envelope, stream, signerId);
		public string GetSignatureImage(string envelopeId, string signerId) => _store.GetSignatureImage(envelopeId, signerId);
		public string GetSignatureImageRaw(string envelopeId, string signerId) => _store.GetSignatureImageRaw(envelopeId, signerId);
		public void UploadSignatureImage(Envelope envelope, MemoryStream stream, string signerId) => _store.UploadSignatureImage(envelope, stream, signerId);
	}

	internal class TemplateRepository : ITemplateRepository {
		private readonly TemplateStore _store;

		public TemplateRepository(TemplateStore store) {
			_store = store;
		}

		public void Add(Template template, MemoryStream stream) => _store.Add(template, stream);
		public void Update(string templateId, Template template) => _store.Update(templateId, template);
		public void UpdateFile(Template template, MemoryStream stream) => _store.UpdateFile(template, stream);
		public void Delete(string templateId) => _store.Delete(templateId);
		public List<Template> GetTemplates(string templateId = null) => _store.GetTemplates(templateId);
		public string GetDocument(string templateId) => _store.GetDocument(templateId);
		public string GetThumbnail(string templateId) => _store.GetThumbnail(templateId);
		public void AddThumbnail(Template template, string svgContent) => _store.AddThumbnail(template, svgContent);
	}

	internal class ContractRepository : IContractRepository {
		private readonly ContractStore _store;

		public ContractRepository(ContractStore store) {
			_store = store;
		}

		public void Add(Contract contract, MemoryStream stream) => _store.Add(contract, stream);
		public void Update(string contractId, Contract contract) => _store.Update(contractId, contract);
		public void UpdateFile(Contract contract, MemoryStream stream) => _store.UpdateFile(contract, stream);
		public void Delete(string contractId) => _store.Delete(contractId);
		public List<Contract> GetContracts(string contractId = null) => _store.GetContracts(contractId);
		public string GetDocument(string contractId) => _store.GetDocument(contractId);
		public string GetThumbnail(string contractId) => _store.GetThumbnail(contractId);
		public void AddThumbnail(Contract contract, string svgContent) => _store.AddThumbnail(contract, svgContent);
	}
}
