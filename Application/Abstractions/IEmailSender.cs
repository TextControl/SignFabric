using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Threading.Tasks;

namespace SignFabric.Application.Abstractions {
	/// <summary>
	/// Abstraction for email sending
	/// </summary>
	public interface IEmailSender {
		Task SendSigningInvitationAsync(Envelope envelope, Signer signer, string signingUrl);
		Task SendEnvelopeInvitationsAsync(Envelope envelope, string host, string userId);
		Task SendSignerEmailOtpAsync(Envelope envelope, Signer signer, string code);
		Task SendSignedConfirmationAsync(Envelope envelope, Signer signer);
		Task SendFinalSignedNotificationAsync(Envelope envelope, byte[] finalDocument);
		Task SendFinalizationFaultNotificationAsync(Envelope envelope);
		Task SendContractReviewAsync(Contract contract, string host, string userId);
		Task SendContractReviewedOwnerAsync(Contract contract, string host);
		Task SendUserInvitationAsync(string email, string temporaryPassword, string loginUrl);
		Task SendTwoFactorCodeAsync(string email, string code);
		Task SendPasswordResetAsync(string email, string resetUrl);
	}
}
