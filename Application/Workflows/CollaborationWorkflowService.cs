using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public class CollaborationWorkflowService : ICollaborationWorkflowService {
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly ITxDocumentService _txService;
		private readonly IEmailSender _emailSender;

		public CollaborationWorkflowService(
			IStoreRepositoryFactory storeFactory,
			ITxDocumentService txService,
			IEmailSender emailSender) {
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_txService = txService ?? throw new ArgumentNullException(nameof(txService));
			_emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
		}

		public async Task<ContractReviewInfo> GetContractReviewAsync(string accessId, string currentUserId, string currentUserName) {
			return await Task.Run(() => {
				var (contractId, ownerId) = DecodeContractAccessId(accessId);
				var store = _storeFactory.CreateContractRepository(ownerId);
				var contract = store.GetContracts(contractId).First();
				bool owner = !string.IsNullOrEmpty(currentUserId) && currentUserId == contract.UserID;

				return new ContractReviewInfo {
					AccessId = accessId,
					Contract = contract,
					Owner = owner,
					EditorUser = owner ? currentUserName : contract.Signer?.Email,
					IsUnavailable = contract.Status == ContractStatus.Closed || contract.Status == ContractStatus.Accepted
				};
			});
		}

		public async Task<string> GetDocumentAsync(string accessId) {
			return await Task.Run(() => {
				var (contractId, ownerId) = DecodeContractAccessId(accessId);
				return _storeFactory.CreateContractRepository(ownerId).GetDocument(contractId);
			});
		}

		public async Task<string> SaveDocumentAsync(string accessId, string documentBase64, bool owner, string currentUserId, string host) {
			return await Task.Run(() => {
				var (contractId, ownerId) = DecodeContractAccessId(accessId);
				var store = _storeFactory.CreateContractRepository(ownerId);
				var contract = store.GetContracts(contractId).First();
				var ownerSave = currentUserId == contract.UserID;
				byte[] savedDocument = Convert.FromBase64String(documentBase64);

				using (var stream = new MemoryStream(savedDocument)) {
					store.UpdateFile(contract, stream);
				}

				contract.HasTrackedChanges = _txService.HasTrackedChanges(Convert.ToBase64String(savedDocument));
				contract.Status = contract.HasTrackedChanges ? ContractStatus.Changed : ContractStatus.Accepted;
				store.Update(contract.ContractID, contract);

				if (ownerSave) {
					_emailSender.SendContractReviewAsync(contract, host, ownerId).GetAwaiter().GetResult();
				}
				else {
					_emailSender.SendContractReviewedOwnerAsync(contract, host).GetAwaiter().GetResult();
				}

				return ownerSave ? "/contracts" : "/collaboration/thanks/" + accessId;
			});
		}

		private static (string ContractId, string OwnerId) DecodeContractAccessId(string accessId) {
			byte[] octets = Convert.FromBase64String(accessId);
			string[] parts = Encoding.ASCII.GetString(octets).Split(':');

			if (parts.Length < 2) {
				throw new InvalidOperationException("Invalid contract access id.");
			}

			return (parts[0], parts[1]);
		}
	}
}
