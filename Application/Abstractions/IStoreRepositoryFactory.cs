using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Collections.Generic;
using System.IO;

namespace SignFabric.Application.Abstractions {
	public interface IStoreRepositoryFactory {
		IEnvelopeRepository CreateEnvelopeRepository(string userId);
		ITemplateRepository CreateTemplateRepository(string userId);
		IContractRepository CreateContractRepository(string userId);
	}

	public interface IEnvelopeRepository {
		void Add(Envelope envelope, MemoryStream stream);
		void Update(string envelopeId, Envelope envelope);
		void UpdateFile(Envelope envelope, MemoryStream stream);
		List<Envelope> GetEnvelopes(string envelopeId = null);
		string GetDocument(string envelopeId);
		string GetThumbnail(string envelopeId);
		void AddThumbnail(Envelope envelope, string svgContent);
		string GetFinalSignedDocument(string envelopeId);
		void UploadFinalSignedDocument(Envelope envelope, MemoryStream stream);
		string GetSignedDocument(string envelopeId, string signerId);
		void UploadSignedDocument(Envelope envelope, MemoryStream stream, string signerId);
		string GetSignatureImage(string envelopeId, string signerId);
		string GetSignatureImageRaw(string envelopeId, string signerId);
		void UploadSignatureImage(Envelope envelope, MemoryStream stream, string signerId);
	}

	public interface ITemplateRepository {
		void Add(Template template, MemoryStream stream);
		void Update(string templateId, Template template);
		void UpdateFile(Template template, MemoryStream stream);
		List<Template> GetTemplates(string templateId = null);
		string GetDocument(string templateId);
		string GetThumbnail(string templateId);
		void AddThumbnail(Template template, string svgContent);
	}

	public interface IContractRepository {
		void Add(Contract contract, MemoryStream stream);
		void Update(string contractId, Contract contract);
		void UpdateFile(Contract contract, MemoryStream stream);
		List<Contract> GetContracts(string contractId = null);
		string GetDocument(string contractId);
		string GetThumbnail(string contractId);
		void AddThumbnail(Contract contract, string svgContent);
	}
}
