using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public class EnvelopeWorkflowService : IEnvelopeWorkflowService {
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly IEmailSender _emailSender;
		private readonly IEnvelopeDocumentFactory _envelopeDocumentFactory;

		public EnvelopeWorkflowService(
			IStoreRepositoryFactory storeFactory,
			IEmailSender emailSender,
			IEnvelopeDocumentFactory envelopeDocumentFactory) {
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
			_envelopeDocumentFactory = envelopeDocumentFactory ?? throw new ArgumentNullException(nameof(envelopeDocumentFactory));
		}

		public async Task<Envelope> AddRecipientAsync(string userId, string envelopeId, Signer signer) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateEnvelopeRepository(userId);
				var envelope = store.GetEnvelopes(envelopeId).FirstOrDefault() ?? throw new InvalidOperationException("Envelope not found");
				if (envelope.Signers.Any(p => p.Email.ToLower() == signer.Email.ToLower())) throw new InvalidOperationException("List already contains this recipient");
				envelope.Signers.Add(new Signer {
					Name = signer.Name,
					Email = signer.Email,
					Id = Guid.NewGuid().ToString(),
					Role = signer.Role,
					RoutingOrder = signer.RoutingOrder <= 0 ? 1 : signer.RoutingOrder,
					RequireEmailOtp = signer.RequireEmailOtp,
					AuthenticationMethod = signer.RequireEmailOtp
						? SignerAuthenticationMethod.EmailOtp
						: SignerAuthenticationMethod.EmailLink
				});
				envelope.Status = EnvelopeStatus.New;
				store.Update(envelope.EnvelopeID, envelope);
				return envelope;
			});
		}

		public async Task<Envelope> UpdateWorkflowAsync(string userId, string envelopeId, EnvelopeWorkflowUpdate request) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateEnvelopeRepository(userId);
				var envelope = store.GetEnvelopes(envelopeId).FirstOrDefault() ?? throw new InvalidOperationException("Envelope not found");
				if (request == null) {
					throw new ArgumentNullException(nameof(request));
				}

				envelope.WorkflowMode = request.WorkflowMode;
				foreach (var update in request.Recipients ?? Enumerable.Empty<EnvelopeRecipientWorkflowUpdate>()) {
					var recipient = envelope.Signers.FirstOrDefault(item => item.Id == update.Id);
					if (recipient == null) {
						continue;
					}

					recipient.Role = update.Role;
					recipient.RoutingOrder = update.Role == RecipientRole.Observer
						? 0
						: update.RoutingOrder <= 0 ? 1 : update.RoutingOrder;
					recipient.RequireEmailOtp = update.RequireEmailOtp;
					recipient.AuthenticationMethod = update.RequireEmailOtp
						? SignerAuthenticationMethod.EmailOtp
						: SignerAuthenticationMethod.EmailLink;
				}

				if (envelope.WorkflowMode == EnvelopeWorkflowMode.Simple) {
					foreach (var recipient in envelope.Signers) {
						recipient.Role = RecipientRole.Signer;
						recipient.RoutingOrder = 1;
					}
				}

				store.Update(envelope.EnvelopeID, envelope);
				return envelope;
			});
		}

		public async Task<Envelope> GetRecipientsAsync(string userId, string envelopeId) =>
			await Task.Run(() => _storeFactory.CreateEnvelopeRepository(userId).GetEnvelopes(envelopeId).FirstOrDefault() ?? throw new InvalidOperationException("Envelope not found"));

		public async Task<Envelope> RemoveRecipientAsync(string userId, string envelopeId, Signer signer) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateEnvelopeRepository(userId);
				var envelope = store.GetEnvelopes(envelopeId).FirstOrDefault() ?? throw new InvalidOperationException("Envelope not found");
				var signerToRemove = envelope.Signers.FirstOrDefault(p => p.Email.ToLower() == signer.Email.ToLower()) ?? throw new InvalidOperationException("Recipient not found");
				envelope.Signers.Remove(signerToRemove);
				envelope.Status = EnvelopeStatus.New;
				store.Update(envelope.EnvelopeID, envelope);
				return envelope;
			});
		}

		public async Task<Envelope> UpdateAsync(string userId, Envelope envelope) {
			return await Task.Run(() => {
				if (envelope == null) {
					throw new ArgumentNullException(nameof(envelope));
				}

				var store = _storeFactory.CreateEnvelopeRepository(userId);
				store.Update(envelope.EnvelopeID, envelope);
				return envelope;
			});
		}

		public async Task<Envelope> SubmitAsync(string userId, string envelopeId, string host) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateEnvelopeRepository(userId);
				var envelope = store.GetEnvelopes(envelopeId).FirstOrDefault() ?? throw new InvalidOperationException("Envelope not found");
				NormalizeRouting(envelope);
				envelope.Status = EnvelopeStatus.Sent;
				envelope.Sent = DateTime.Now;
				ActivateNextRoutingStep(envelope);
				store.Update(envelope.EnvelopeID, envelope);
				_emailSender.SendEnvelopeInvitationsAsync(envelope, host, userId).GetAwaiter().GetResult();
				store.Update(envelope.EnvelopeID, envelope);
				return envelope;
			});
		}

		public Task<string> CreateAsync(string userId, string userName, MemoryStream documentStream, string fileName) =>
			Task.Run(() => _envelopeDocumentFactory.CreateEnvelopeFromDocument(userId, userName, documentStream, fileName));

		public Task<string> CreateAsync(string userId, string userName, MemoryStream documentStream, string fileName, string signingCertificateId) =>
			Task.Run(() => _envelopeDocumentFactory.CreateEnvelopeFromDocument(userId, userName, documentStream, fileName, signingCertificateId));

		private static void NormalizeRouting(Envelope envelope) {
			foreach (var recipient in envelope.Signers) {
				if (recipient.Role == RecipientRole.Observer) {
					recipient.RoutingOrder = 0;
				}
				else if (recipient.RoutingOrder <= 0) {
					recipient.RoutingOrder = 1;
				}
			}

			if (envelope.WorkflowMode == EnvelopeWorkflowMode.Simple) {
				foreach (var recipient in envelope.Signers) {
					recipient.Role = RecipientRole.Signer;
					recipient.RoutingOrder = 1;
				}
			}
		}

		private static void ActivateNextRoutingStep(Envelope envelope) {
			var nextBlockingOrder = envelope.Signers
				.Where(IsBlockingRecipient)
				.Where(recipient => !IsRecipientComplete(recipient))
				.Select(recipient => recipient.RoutingOrder <= 0 ? 1 : recipient.RoutingOrder)
				.DefaultIfEmpty(0)
				.Min();

			foreach (var recipient in envelope.Signers) {
				if (recipient.Role == RecipientRole.Observer) {
					recipient.RoutingActive = true;
					if (!recipient.RoutingActivatedAt.HasValue) {
						recipient.RoutingActivatedAt = DateTime.UtcNow;
					}
					continue;
				}

				var recipientOrder = recipient.RoutingOrder <= 0 ? 1 : recipient.RoutingOrder;
				recipient.RoutingActive = IsBlockingRecipient(recipient)
					? nextBlockingOrder > 0 && recipientOrder == nextBlockingOrder
					: nextBlockingOrder == 0 || recipientOrder <= nextBlockingOrder;

				if (recipient.RoutingActive && !recipient.RoutingActivatedAt.HasValue) {
					recipient.RoutingActivatedAt = DateTime.UtcNow;
				}
			}
		}

		private static bool IsBlockingRecipient(Signer recipient) =>
			recipient.Role == RecipientRole.Signer || recipient.Role == RecipientRole.Approver;

		private static bool IsRecipientComplete(Signer recipient) =>
			recipient.SignerStatus == SignerStatus.Signed;
	}
}
