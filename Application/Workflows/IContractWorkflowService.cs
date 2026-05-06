using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public interface IContractWorkflowService {
		Task<NewContractModel> CreateAsync(string userId, string userName, MemoryStream documentStream, string fileName);
		Task<Contract> AddRecipientAsync(string userId, string contractId, Signer signer);
		Task<Contract> SubmitAsync(string userId, string contractId, string host);
		Task<(byte[] Document, string FileName)> DownloadAsync(string userId, string contractId);
	}
}
