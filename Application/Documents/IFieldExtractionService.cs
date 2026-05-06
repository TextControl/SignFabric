using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	/// <summary>
	/// Service for field extraction - handles getting merge fields and signature boxes
	/// Extracts field extraction logic from controllers
	/// </summary>
	public interface IFieldExtractionService {
		/// <summary>
		/// Get all merge fields from a document
		/// </summary>
		Task<List<FieldModel>> GetMergeFieldsAsync(string base64Document);

		/// <summary>
		/// Get all sections/parts from a document
		/// </summary>
		Task<List<SectionModel>> GetSectionsAsync(string base64Document);

		/// <summary>
		/// Check if document contains signature boxes
		/// </summary>
		Task<bool> ContainsSignatureBoxesAsync(string base64Document, List<Signer> signers);

		/// <summary>
		/// Update form field conditions (valid/invalid)
		/// </summary>
		Task<byte[]> UpdateFieldConditionsAsync(string base64Document, bool setConditions);
	}
}
