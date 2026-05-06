using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	/// <summary>
	/// Implementation of IFieldExtractionService
	/// Handles extraction of merge fields and signature boxes from documents
	/// </summary>
	public class FieldExtractionService : IFieldExtractionService {
		private readonly ITxDocumentService _txService;

		public FieldExtractionService(ITxDocumentService txService) {
			_txService = txService ?? throw new ArgumentNullException(nameof(txService));
		}

		public async Task<List<FieldModel>> GetMergeFieldsAsync(string base64Document) {
			return await Task.Run(() => {
				try {
					return _txService.GetMergeFields(base64Document);
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error getting merge fields: {ex.Message}");
					throw;
				}
			});
		}

		public async Task<List<SectionModel>> GetSectionsAsync(string base64Document) {
			return await Task.Run(() => {
				try {
					return _txService.GetSections(base64Document);
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error getting sections: {ex.Message}");
					throw;
				}
			});
		}

		public async Task<bool> ContainsSignatureBoxesAsync(string base64Document, List<Signer> signers) {
			return await Task.Run(() => {
				try {
					return _txService.ContainsSignatureBoxes(base64Document, signers);
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error checking signature boxes: {ex.Message}");
					throw;
				}
			});
		}

		public async Task<byte[]> UpdateFieldConditionsAsync(string base64Document, bool setConditions) {
			return await Task.Run(() => {
				try {
					return _txService.SetFieldConditions(base64Document, setConditions);
				} catch (Exception ex) {
					System.Diagnostics.Debug.WriteLine($"Error updating field conditions: {ex.Message}");
					throw;
				}
			});
		}
	}
}
