using SignFabric.Infrastructure.Configuration;
using SignFabric.Infrastructure.Email.Legacy;
using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Email {
	/// <summary>
	/// Email sender implementation using the existing ConfirmationEmail helper
	/// </summary>
	public class EmailSender : IEmailSender {
		private readonly IEmailCredentialsProvider _credentialsProvider;
		private readonly AppSettingsPathResolver _paths;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly ILogger<EmailSender> _logger;

		public EmailSender(
			IEmailCredentialsProvider credentialsProvider,
			AppSettingsPathResolver paths,
			IHttpContextAccessor httpContextAccessor,
			ILogger<EmailSender> logger) {
			_credentialsProvider = credentialsProvider ?? throw new ArgumentNullException(nameof(credentialsProvider));
			_paths = paths ?? throw new ArgumentNullException(nameof(paths));
			_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		public async Task SendSigningInvitationAsync(Envelope envelope, Signer signer, string signingUrl) {
			var host = ResolveHost(signingUrl);
			await SendAsync(async () => {
				var email = new ConfirmationEmail(await _credentialsProvider.GetCredentialsAsync(), _paths);
				email.SendSigningInvitationEmail(envelope, signer, host, envelope.UserID);
			}, "signing invitation", signer.Email);
		}

		public async Task SendEnvelopeInvitationsAsync(Envelope envelope, string host, string userId) {
			await SendAsync(async () => {
				var email = new ConfirmationEmail(await _credentialsProvider.GetCredentialsAsync(), _paths);
				email.SendConfirmationEmail(envelope, host, userId);
			}, "envelope invitations", string.Join(", ", envelope.Signers.ConvertAll(signer => signer.Email)));
		}

		public async Task SendSignedConfirmationAsync(Envelope envelope, Signer signer) {
			var host = ResolveCurrentHost();
			await SendAsync(async () => {
				var email = new ConfirmationEmail(await _credentialsProvider.GetCredentialsAsync(), _paths);
				email.SendSignedEmail(envelope, signer, host);
			}, "signed confirmation", signer.Email);
		}

		public async Task SendFinalSignedNotificationAsync(Envelope envelope, byte[] finalDocument) {
			var host = ResolveCurrentHost();
			await SendAsync(async () => {
				var email = new ConfirmationEmail(await _credentialsProvider.GetCredentialsAsync(), _paths);
				using var ms = new MemoryStream(finalDocument);
				foreach (var signer in envelope.Signers) {
					email.SendFinalSignedEmail(envelope, ms, signer, host);
					ms.Position = 0;
				}
			}, "final signed notification", string.Join(", ", envelope.Signers.ConvertAll(signer => signer.Email)));
		}

		public async Task SendFinalizationFaultNotificationAsync(Envelope envelope) {
			var host = ResolveCurrentHost();
			await SendAsync(async () => {
				var email = new ConfirmationEmail(await _credentialsProvider.GetCredentialsAsync(), _paths);
				email.SendFinalizationFaultEmail(envelope, host);
			}, "finalization fault notification", envelope.Sender);
		}

		public async Task SendContractReviewAsync(Contract contract, string host, string userId) {
			await SendAsync(async () => {
				var email = new ConfirmationEmail(await _credentialsProvider.GetCredentialsAsync(), _paths);
				email.SendReviewEmail(contract, host, userId);
			}, "contract review", contract.Signer.Email);
		}

		public async Task SendContractReviewedOwnerAsync(Contract contract, string host) {
			await SendAsync(async () => {
				var email = new ConfirmationEmail(await _credentialsProvider.GetCredentialsAsync(), _paths);
				email.SendReviewOwnerEmail(contract, host);
			}, "contract reviewed owner notification", contract.Signer.Email);
		}

		public async Task SendUserInvitationAsync(string email, string temporaryPassword, string loginUrl) {
			var host = ResolveHost(loginUrl);
			await SendAsync(async () => {
				var emailMessage = new ConfirmationEmail(await _credentialsProvider.GetCredentialsAsync(), _paths);
				emailMessage.SendUserInvitationEmail(email, temporaryPassword, loginUrl, host);
			}, "user invitation", email);
		}

		public async Task SendTwoFactorCodeAsync(string email, string code) {
			var host = ResolveCurrentHost();
			await SendAsync(async () => {
				var emailMessage = new ConfirmationEmail(await _credentialsProvider.GetCredentialsAsync(), _paths);
				emailMessage.SendTwoFactorCodeEmail(email, code, host);
			}, "two-factor authentication code", email);
		}

		public async Task SendPasswordResetAsync(string email, string resetUrl) {
			var host = ResolveHost(resetUrl);
			await SendAsync(async () => {
				var emailMessage = new ConfirmationEmail(await _credentialsProvider.GetCredentialsAsync(), _paths);
				emailMessage.SendPasswordResetEmail(email, resetUrl, host);
			}, "password reset", email);
		}

		private async Task SendAsync(Func<Task> send, string emailKind, string destination) {
			try {
				await Task.Run(send);
			} catch (Exception ex) {
				_logger.LogError(ex, "Failed to send {EmailKind} e-mail to {Destination}.", emailKind, destination);
				throw;
			}
		}

		private string ResolveHost(string url) {
			if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri)) {
				return absoluteUri.GetLeftPart(UriPartial.Authority);
			}

			return ResolveCurrentHost();
		}

		private string ResolveCurrentHost() {
			var request = _httpContextAccessor.HttpContext?.Request;
			if (request == null) {
				throw new InvalidOperationException("Cannot resolve the public application URL for a relative e-mail link.");
			}

			return $"{request.Scheme}://{request.Host}";
		}
	}
}
