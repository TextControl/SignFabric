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
				envelope.Signers.Add(new Signer { Name = signer.Name, Email = signer.Email, Id = Guid.NewGuid().ToString() });
				envelope.Status = EnvelopeStatus.New;
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
				envelope.Status = EnvelopeStatus.Sent;
				envelope.Sent = DateTime.Now;
				store.Update(envelope.EnvelopeID, envelope);
				_emailSender.SendEnvelopeInvitationsAsync(envelope, host, userId).GetAwaiter().GetResult();
				return envelope;
			});
		}

		public Task<string> CreateAsync(string userId, string userName, MemoryStream documentStream, string fileName) =>
			Task.Run(() => _envelopeDocumentFactory.CreateEnvelopeFromDocument(userId, userName, documentStream, fileName));

		public Task<string> CreateAsync(string userId, string userName, MemoryStream documentStream, string fileName, string signingCertificateId) =>
			Task.Run(() => _envelopeDocumentFactory.CreateEnvelopeFromDocument(userId, userName, documentStream, fileName, signingCertificateId));
	}
}
