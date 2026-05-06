using System;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.Abstractions {
	/// <summary>
	/// Abstraction for document storage operations (LiteDB, file system, etc.)
	/// </summary>
	public interface IDocumentStorage {
		Task<string> GetDocumentAsync(string documentId);
		Task<string> GetThumbnailAsync(string documentId);
		Task UploadDocumentAsync(string documentId, MemoryStream stream);
		Task UploadThumbnailAsync(string documentId, string svgContent);
		Task<bool> DocumentExistsAsync(string documentId);
	}
}
