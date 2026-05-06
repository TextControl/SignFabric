using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.ContractManagement {
	/// <summary>
	/// Service for contract management
	/// </summary>
	public interface IContractService {
		Task<Contract> CreateAsync(Contract contract, MemoryStream documentStream);
		Task<Contract> GetAsync(string contractId);
		Task<List<Contract>> GetAllAsync(string userId);
		Task UpdateAsync(Contract contract);
		Task DeleteAsync(string contractId);
		Task<byte[]> GetSignedDocumentAsync(string contractId);
	}
}
