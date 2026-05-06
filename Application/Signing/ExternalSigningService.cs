using SignFabric.Application.Abstractions;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Application.Signing {
	public class ExternalSigningService : IExternalSigningService {
		private readonly IEnvelopeService _envelopeService;
		private readonly IContractService _contractService;
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly string _userId;

		public ExternalSigningService(
			IEnvelopeService envelopeService,
			IContractService contractService,
			IStoreRepositoryFactory storeFactory,
			string userId) {
			_envelopeService = envelopeService ?? throw new ArgumentNullException(nameof(envelopeService));
			_contractService = contractService ?? throw new ArgumentNullException(nameof(contractService));
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_userId = userId ?? throw new ArgumentNullException(nameof(userId));
		}

		public async Task<Envelope> GetSigningLinkAsync(string encodedId) {
			return await Task.Run(() => {
				try {
					byte[] octets = Convert.FromBase64String(encodedId);
					var parts = System.Text.Encoding.ASCII.GetString(octets).Split(':');
					
					if (parts.Length < 2) {
						throw new InvalidOperationException("Invalid signing link format");
					}

					var envelopeId = parts[0];
					var userId = parts[1];
					
					return _storeFactory.CreateEnvelopeRepository(userId).GetEnvelopes(envelopeId).FirstOrDefault();
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error getting signing link: {ex.Message}");
					throw;
				}
			});
		}

		public async Task<Contract> GetContractReviewLinkAsync(string encodedId) {
			return await Task.Run(() => {
				try {
					byte[] octets = Convert.FromBase64String(encodedId);
					var parts = System.Text.Encoding.ASCII.GetString(octets).Split(':');
					
					if (parts.Length < 2) {
						throw new InvalidOperationException("Invalid review link format");
					}

					var contractId = parts[0];
					var userId = parts[1];
					
					return _storeFactory.CreateContractRepository(userId).GetContracts(contractId).FirstOrDefault();
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error getting contract review link: {ex.Message}");
					throw;
				}
			});
		}

		public async Task CompleteExternalSigningAsync(string envelopeId, string signerId, byte[] signedDocument) {
			await Task.Run(() => {
				try {
					var envelope = _envelopeService.GetAsync(envelopeId).Result;
					if (envelope == null) {
						throw new InvalidOperationException($"Envelope {envelopeId} not found");
					}

					var signer = envelope.Signers.Find(s => s.Id == signerId);
					if (signer == null) {
						throw new InvalidOperationException($"Signer {signerId} not found");
					}

					signer.SignerStatus = SignerStatus.Signed;
					_envelopeService.UpdateAsync(envelope).Wait();
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error completing external signing: {ex.Message}");
					throw;
				}
			});
		}

		public async Task CompleteExternalReviewAsync(string contractId, string reviewerId, string comments) {
			await Task.Run(() => {
				try {
					var contract = _contractService.GetAsync(contractId).Result;
					if (contract == null) {
						throw new InvalidOperationException($"Contract {contractId} not found");
					}

					_contractService.UpdateAsync(contract).Wait();
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error completing external review: {ex.Message}");
					throw;
				}
			});
		}

		public async Task<bool> ValidateSignatureAsync(string envelopeId, string signerId) {
			return await Task.Run(() => {
				try {
					var envelope = _envelopeService.GetAsync(envelopeId).Result;
					if (envelope == null) {
						return false;
					}

					var signer = envelope.Signers.Find(s => s.Id == signerId);
					return signer != null && signer.SignerStatus == SignerStatus.Signed;
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error validating signature: {ex.Message}");
					return false;
				}
			});
		}
	}
}
