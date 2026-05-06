using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Threading.Tasks;

namespace SignFabric.Application.Signing {
	/// <summary>
	/// Service for external signing and reviewing
	/// </summary>
	public interface IExternalSigningService {
		Task<Envelope> GetSigningLinkAsync(string encodedId);
		Task<Contract> GetContractReviewLinkAsync(string encodedId);
		Task CompleteExternalSigningAsync(string envelopeId, string signerId, byte[] signedDocument);
		Task CompleteExternalReviewAsync(string contractId, string reviewerId, string comments);
		Task<bool> ValidateSignatureAsync(string envelopeId, string signerId);
	}
}
