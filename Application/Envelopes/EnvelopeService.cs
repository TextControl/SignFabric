using SignFabric.Application.Services;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Signing;
using SignFabric.Application.Templates;
using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Application.Envelopes {
	/// <summary>
	/// Application service for envelope (signing request) management
	/// Orchestrates envelope operations using domain services
	/// </summary>
	public class EnvelopeService : IEnvelopeService {
		private readonly ITxDocumentService _txService;
		private readonly IEmailSender _emailSender;
		private readonly IAuditLogger _auditLogger;
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly string _userId;

		public EnvelopeService(
			ITxDocumentService txService,
			IEmailSender emailSender,
			IAuditLogger auditLogger,
			IStoreRepositoryFactory storeFactory,
			string userId) {
			_txService = txService ?? throw new ArgumentNullException(nameof(txService));
			_emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
			_auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_userId = userId ?? throw new ArgumentNullException(nameof(userId));
		}

		public async Task<Envelope> CreateAsync(Envelope envelope, MemoryStream documentStream) {
			try {
				var store = _storeFactory.CreateEnvelopeRepository(_userId);
				envelope.EnvelopeID = Guid.NewGuid().ToString();
				envelope.UserID = _userId;
				envelope.Status = EnvelopeStatus.New;
				envelope.Created = DateTime.Now;

				store.Add(envelope, documentStream);

				// Generate and store thumbnail
				var documentBase64 = store.GetDocument(envelope.EnvelopeID);
				var thumbnail = _txService.GenerateThumbnail(documentBase64);
				store.AddThumbnail(envelope, thumbnail);

				await _auditLogger.LogEnvelopeCreatedAsync(envelope.EnvelopeID, _userId);

				return envelope;
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine($"Error creating envelope: {ex.Message}");
				throw;
			}
		}

		public async Task<Envelope> GetAsync(string envelopeId) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateEnvelopeRepository(_userId);
				var envelopes = store.GetEnvelopes(envelopeId);
				return envelopes.FirstOrDefault();
			});
		}

		public async Task<List<Envelope>> GetAllAsync(string userId) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateEnvelopeRepository(userId);
				return store.GetEnvelopes();
			});
		}

		public async Task UpdateAsync(Envelope envelope) {
			await Task.Run(() => {
				var store = _storeFactory.CreateEnvelopeRepository(_userId);
				store.Update(envelope.EnvelopeID, envelope);
			});
		}

		public async Task SendAsync(string envelopeId) {
			try {
				var store = _storeFactory.CreateEnvelopeRepository(_userId);
				var envelope = store.GetEnvelopes(envelopeId).FirstOrDefault();
				
				if (envelope == null) {
					throw new InvalidOperationException($"Envelope {envelopeId} not found");
				}

				envelope.Status = EnvelopeStatus.Sent;
				envelope.Sent = DateTime.Now;

				foreach (var signer in envelope.Signers) {
					signer.SignerStatus = SignerStatus.Sent;
					var accessId = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(envelopeId + ":" + _userId + ":" + signer.Id))
						.TrimEnd('=')
						.Replace('+', '-')
						.Replace('/', '_');
					var signingUrl = $"/review/sign?id={Uri.EscapeDataString(accessId)}";
					await _emailSender.SendSigningInvitationAsync(envelope, signer, signingUrl);
				}

				store.Update(envelope.EnvelopeID, envelope);
				await _auditLogger.LogEnvelopeSentAsync(envelope.EnvelopeID, _userId);
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine($"Error sending envelope: {ex.Message}");
				throw;
			}
		}

		public async Task CompleteSigningAsync(string envelopeId) {
			try {
				var store = _storeFactory.CreateEnvelopeRepository(_userId);
				var envelope = store.GetEnvelopes(envelopeId).FirstOrDefault();
				
				if (envelope == null) {
					throw new InvalidOperationException($"Envelope {envelopeId} not found");
				}

				var allSigned = envelope.Signers.All(s => s.SignerStatus == SignerStatus.Signed);
				if (!allSigned) {
					throw new InvalidOperationException("Not all signers have signed the document");
				}

				envelope.Status = EnvelopeStatus.Signed;

				store.Update(envelope.EnvelopeID, envelope);
				await _auditLogger.LogEnvelopeCompletedAsync(envelope.EnvelopeID, DateTime.UtcNow);
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine($"Error completing signing: {ex.Message}");
				throw;
			}
		}

		public async Task<byte[]> GetSignedDocumentAsync(string envelopeId) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateEnvelopeRepository(_userId);
				var documentBase64 = store.GetFinalSignedDocument(envelopeId);
				return Convert.FromBase64String(documentBase64);
			});
		}
	}
}
