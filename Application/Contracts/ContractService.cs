using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Application.ContractManagement {
	public class ContractService : IContractService {
		private readonly ITxDocumentService _txService;
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly string _userId;

		public ContractService(
			ITxDocumentService txService,
			IStoreRepositoryFactory storeFactory,
			string userId) {
			_txService = txService ?? throw new ArgumentNullException(nameof(txService));
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_userId = userId ?? throw new ArgumentNullException(nameof(userId));
		}

		public async Task<Contract> CreateAsync(Contract contract, MemoryStream documentStream) {
			return await Task.Run(() => {
				try {
					var store = _storeFactory.CreateContractRepository(_userId);
					contract.ContractID = Guid.NewGuid().ToString();
					contract.Created = DateTime.Now;
					
					store.Add(contract, documentStream);
					
					var documentBase64 = store.GetDocument(contract.ContractID);
					var thumbnail = _txService.GenerateThumbnail(documentBase64);
					store.AddThumbnail(contract, thumbnail);
					
					return contract;
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error creating contract: {ex.Message}");
					throw;
				}
			});
		}

		public async Task<Contract> GetAsync(string contractId) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateContractRepository(_userId);
				var contracts = store.GetContracts(contractId);
				return contracts.FirstOrDefault();
			});
		}

		public async Task<List<Contract>> GetAllAsync(string userId) {
			return await Task.Run(() => {
				var store = _storeFactory.CreateContractRepository(userId);
				return store.GetContracts();
			});
		}

		public async Task UpdateAsync(Contract contract) {
			await Task.Run(() => {
				var store = _storeFactory.CreateContractRepository(_userId);
				store.Update(contract.ContractID, contract);
			});
		}

		public async Task DeleteAsync(string contractId) {
			await Task.Run(() => {
				var store = _storeFactory.CreateContractRepository(_userId);
				store.Delete(contractId);
			});
		}

		public async Task<byte[]> GetSignedDocumentAsync(string contractId) {
			return await Task.Run(() => {
				try {
					var store = _storeFactory.CreateContractRepository(_userId);
					// Fallback if GetFinalSignedDocument doesn't exist
					var documentBase64 = store.GetDocument(contractId);
					if (!string.IsNullOrEmpty(documentBase64)) {
						return Convert.FromBase64String(documentBase64);
					}
					return new byte[0];
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error getting signed document: {ex.Message}");
					return new byte[0];
				}
			});
		}
	}
}
