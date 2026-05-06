using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	/// <summary>
	/// Service for document merging - handles JSON merging and data population
	/// Extracts merge logic from controllers
	/// </summary>
	public interface IDocumentMergeService {
		/// <summary>
		/// Merge JSON data into a template document
		/// </summary>
		Task<byte[]> MergeJsonAsync(string base64Document, string jsonData);

		/// <summary>
		/// Create an envelope instance from a template with merged data
		/// </summary>
		Task<(string EnvelopeId, MemoryStream Document)> CreateEnvelopeFromTemplateAsync(
			string templateId,
			string jsonData,
			string userId,
			string senderName);

	}
}
