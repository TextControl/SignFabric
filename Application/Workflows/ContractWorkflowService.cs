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
	public class ContractWorkflowService : IContractWorkflowService {
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly ITxDocumentService _txService;
		private readonly IEmailSender _emailSender;

		public ContractWorkflowService(
			IStoreRepositoryFactory storeFactory,
			ITxDocumentService txService,
			IEmailSender emailSender) {
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_txService = txService ?? throw new ArgumentNullException(nameof(txService));
			_emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
		}

		public Task<NewContractModel> CreateAsync(string userId, string userName, MemoryStream documentStream, string fileName) =>
			Task.Run(() => CreateContractCore(userId, userName, documentStream, fileName));

		public async Task<Contract> AddRecipientAsync(string userId, string contractId, Signer signer) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateContractRepository(userId);
				var contract = store.GetContracts(contractId).FirstOrDefault() ?? throw new InvalidOperationException("Contract not found");
				contract.Signer = new Signer { Name = signer.Name, Email = signer.Email, Id = Guid.NewGuid().ToString() };
				contract.Status = ContractStatus.New;
				store.Update(contract.ContractID, contract);
				return contract;
			});
		}

		public async Task<Contract> SubmitAsync(string userId, string contractId, string host) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateContractRepository(userId);
				var contract = store.GetContracts(contractId).FirstOrDefault() ?? throw new InvalidOperationException("Contract not found");
				contract.Status = ContractStatus.Sent;
				contract.Sent = DateTime.Now;
				store.Update(contract.ContractID, contract);
				_emailSender.SendContractReviewAsync(contract, host, userId).GetAwaiter().GetResult();
				return contract;
			});
		}

		public async Task<(byte[] Document, string FileName)> DownloadAsync(string userId, string contractId) =>
			await Task.Run(() => {
				var store = _storeFactory.CreateContractRepository(userId);
				var contract = store.GetContracts(contractId).FirstOrDefault() ?? throw new InvalidOperationException("Contract not found");
				return (Convert.FromBase64String(store.GetDocument(contractId)), contract.Name);
			});

		private NewContractModel CreateContractCore(string userId, string userName, MemoryStream stream, string fileName) {
			byte[] data = stream.ToArray();
			string image = _txService.GenerateThumbnail(Convert.ToBase64String(data));
			stream = new MemoryStream(_txService.GetInternalFormat(Convert.ToBase64String(data)));
			var contract = new Contract { Created = DateTime.Now, Status = ContractStatus.New, Sender = userName, UserID = userId, Name = fileName, ContractID = Guid.NewGuid().ToString() };
			var store = _storeFactory.CreateContractRepository(userId);
			store.Add(contract, stream);
			store.AddThumbnail(contract, image);
			return new NewContractModel { Contract = contract, Thumbnail = Convert.ToBase64String(Encoding.UTF8.GetBytes(image)) };
		}
	}
}
