using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	/// <summary>
	/// Service for processing documents - handles uploads, conversions, thumbnail generation
	/// Extracts document processing logic from controllers
	/// </summary>
	public interface IDocumentProcessingService {
		/// <summary>
		/// Process a new document - generate thumbnail and extract metadata
		/// </summary>
		Task<(Template Template, string Thumbnail)> ProcessNewTemplateAsync(
			MemoryStream documentStream,
			string fileName,
			string userId);

		/// <summary>
		/// Process a new envelope document
		/// </summary>
		Task<(Envelope Envelope, string Thumbnail)> ProcessNewEnvelopeAsync(
			MemoryStream documentStream,
			string fileName,
			string userId,
			string senderName);

		/// <summary>
		/// Generate thumbnail for a document
		/// </summary>
		Task<string> GenerateThumbnailAsync(string base64Document);

		/// <summary>
		/// Update document file content
		/// </summary>
		Task UpdateDocumentAsync(string documentId, MemoryStream documentStream, string documentType, string userId);
	}
}
