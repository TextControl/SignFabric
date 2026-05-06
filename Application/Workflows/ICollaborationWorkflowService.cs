using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public interface ICollaborationWorkflowService {
		Task<ContractReviewInfo> GetContractReviewAsync(string accessId, string currentUserId, string currentUserName);
		Task<string> GetDocumentAsync(string accessId);
		Task<string> SaveDocumentAsync(string accessId, string documentBase64, bool owner, string currentUserId, string host);
	}

	public class ContractReviewInfo {
		public string AccessId { get; set; }
		public Contract Contract { get; set; }
		public string EditorUser { get; set; }
		public bool Owner { get; set; }
		public bool IsUnavailable { get; set; }
	}
}
