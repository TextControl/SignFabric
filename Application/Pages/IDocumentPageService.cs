using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public interface IDocumentPageService {
		Task<List<Envelope>> GetEnvelopesAsync(string userId);
		Task<EnvelopeDetailsView> GetEnvelopeDetailsAsync(string userId, string envelopeId);
		Task<Envelope> GetEnvelopeAsync(string userId, string envelopeId);
		Task<SignModel> GetEnvelopeEditModelAsync(string userId, string envelopeId);
		Task<SignatureBoxModel> GetEnvelopeSignatureBoxModelAsync(string userId, string envelopeId);
		Task<(byte[] Document, string FileName)> DownloadEnvelopeAsync(string userId, string envelopeId);

		Task<List<Template>> GetTemplatesAsync(string userId);
		Task<TemplateDetailsView> GetTemplateDetailsAsync(string userId, string templateId);
		Task<TemplateEditModel> GetTemplateEditModelAsync(string userId, string templateId);

		Task<List<Contract>> GetContractsAsync(string userId);
		Task<ContractDetailsView> GetContractDetailsAsync(string userId, string contractId);
		Task<ContractEditModel> GetContractEditModelAsync(string userId, string contractId);
	}

	public class EnvelopeDetailsView {
		public Envelope Envelope { get; set; }
		public string ThumbnailSvg { get; set; }
		public Dictionary<string, string> SignatureImages { get; set; } = new Dictionary<string, string>();
	}

	public class TemplateDetailsView {
		public Template Template { get; set; }
		public string ThumbnailSvg { get; set; }
	}

	public class ContractDetailsView {
		public Contract Contract { get; set; }
		public string ThumbnailSvg { get; set; }
	}
}
