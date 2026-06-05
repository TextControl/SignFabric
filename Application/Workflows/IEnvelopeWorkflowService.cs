using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public interface IEnvelopeWorkflowService {
		Task<Envelope> AddRecipientAsync(string userId, string envelopeId, Signer signer);
		Task<Envelope> GetRecipientsAsync(string userId, string envelopeId);
		Task<Envelope> RemoveRecipientAsync(string userId, string envelopeId, Signer signer);
		Task<Envelope> UpdateAsync(string userId, Envelope envelope);
		Task<Envelope> SubmitAsync(string userId, string envelopeId, string host);
		Task<string> CreateAsync(string userId, string userName, MemoryStream documentStream, string fileName);
		Task<string> CreateAsync(string userId, string userName, MemoryStream documentStream, string fileName, string signingCertificateId);
	}
}
