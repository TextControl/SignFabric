using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Threading.Tasks;
using TXTextControl.Web.MVC.DocumentViewer.Models;

namespace SignFabric.Application.Services {
	/// <summary>
	/// Application service for signing workflow
	/// </summary>
	public interface ISigningWorkflowService {
		Task PrepareForSigningAsync(string envelopeId, string signerId);
		Task<ExternalSigningPreparation> PrepareExternalSigningAsync(string accessId);
		Task RequestSignerEmailOtpAsync(string accessId, bool forceNewCode = false);
		Task<ExternalSigningPreparation> VerifySignerEmailOtpAsync(string accessId, string code);
		Task TrustAuthenticatedSignerAsync(string accessId);
		Task<SigningThanksInfo> GetSigningThanksAsync(string accessId);
		Task<ValidatedDocument> ValidateSignedDocumentAsync(byte[] uploadedDocument);
		Task CompleteSigningAsync(string envelopeId, string signerId);
		Task CompleteDocumentViewerSigningAsync(SignatureData data, string userId, string envelopeId, string signerId, string ipAddress, string userAgent);
		Task<bool> IsFullySignedAsync(string envelopeId);
		Task GenerateFinalDocumentAsync(string envelopeId);
	}

	public class ExternalSigningPreparation {
		public string AccessId { get; set; }
		public string Document { get; set; }
		public Envelope Envelope { get; set; }
		public Signer Signer { get; set; }
		public bool AlreadySigned { get; set; }
		public bool RequiresEmailOtp { get; set; }
	}

	public class SigningThanksInfo {
		public Envelope Envelope { get; set; }
		public Signer Signer { get; set; }
	}
}
