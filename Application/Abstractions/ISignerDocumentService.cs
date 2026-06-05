using SignFabric.Application.Services;
using SignFabric.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignFabric.Application.Abstractions {
	public interface ISignerDocumentService {
		Task<List<Envelope>> GetSignedDocumentsAsync(string signerEmail);
		Task<EnvelopeDetailsView> GetSignedDocumentDetailsAsync(string signerEmail, string envelopeId);
		Task<(byte[] Document, string FileName)> DownloadSignedDocumentAsync(string signerEmail, string envelopeId);
	}
}
