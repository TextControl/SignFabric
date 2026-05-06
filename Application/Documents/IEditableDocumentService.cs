using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public interface IEditableDocumentService {
		Task<string> GetEditableDocumentAsync(string userId, string documentType, string documentId);
		Task SaveDocumentAsync(string userId, string documentType, string documentId, string documentBase64);
	}
}
