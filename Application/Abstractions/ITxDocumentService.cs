using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Collections.Generic;

namespace SignFabric.Application.Abstractions {
	/// <summary>
	/// Abstraction for TX Text Control document operations
	/// </summary>
	public interface ITxDocumentService {
		byte[] GetInternalFormat(string base64Document);
		byte[] CreateBlankInternalFormat();
		string GenerateThumbnail(string base64Document);
		(byte[] PdfData, string ThumbnailSvg) CreateSignedPdf(Envelope envelope, string masterDocument);
		string GetDocumentAccessId(byte[] document);
		List<FieldModel> GetMergeFields(string base64Document);
		List<FieldAssignmentField> GetUnassignedRecipientFields(string base64Document, List<Signer> signers);
		List<SectionModel> GetSections(string base64Document);
		byte[] AssignRecipientFields(string base64Document, List<FieldAssignmentMapping> assignments);
		byte[] PrepareFormFields(string base64Document, Signer signer);
		bool HasTrackedChanges(string base64Document);
		bool ContainsSignatureBoxes(string base64Document, List<Signer> signers);
		byte[] SetFieldConditions(string base64Document, bool setConditions);
		byte[] MergeJson(string base64Document, string jsonData);
	}
}
