using SignFabric.Application.Abstractions;
using SignFabric.Infrastructure.Configuration;
using SignFabric.Infrastructure.Services.TextControl;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using TXTextControl;

namespace SignFabric.Infrastructure.Services {
	/// <summary>
	/// TX Text Control implementation of ITxDocumentService
	/// Wraps the existing TextControlHelpers for use in the new architecture
	/// </summary>
	public class TxDocumentService : ITxDocumentService, IDisposable {
		private readonly AppSettings _settings;
		private readonly AppSettingsPathResolver _paths;
		private readonly ISigningCertificateProvider _certificateProvider;

		public TxDocumentService(
			IOptions<AppSettings> settings,
			AppSettingsPathResolver paths,
			ISigningCertificateProvider certificateProvider) {
			_settings = settings.Value;
			_paths = paths;
			_certificateProvider = certificateProvider;
		}

		public byte[] GetInternalFormat(string base64Document) {
			using (var tx = new TextControlHelpers(base64Document)) {
				var ms = tx.GetInternalFormat();
				return ms?.ToArray();
			}
		}

		public byte[] CreateBlankInternalFormat() {
			using (var tx = new ServerTextControl()) {
				tx.Create();
				tx.Save(out byte[] data, BinaryStreamType.InternalUnicodeFormat);
				return data;
			}
		}

		public string GenerateThumbnail(string base64Document) {
			using (var tx = new TextControlHelpers(base64Document)) {
				return tx.GetThumbnail();
			}
		}

		public (byte[] PdfData, string ThumbnailSvg) CreateSignedPdf(Envelope envelope, string masterDocument) {
			using (var tx = new TextControlHelpers(masterDocument)) {
				var certificate = _certificateProvider.LoadSigningCertificateAsync(envelope.SigningCertificateId).GetAwaiter().GetResult();
				return tx.CreatePDF(envelope, envelope.UserID, _paths, certificate);
			}
		}

		public string GetDocumentAccessId(byte[] document) {
			using (var tx = new TextControlHelpers()) {
				return tx.GetDocumentAccessId(document);
			}
		}

		public List<FieldModel> GetMergeFields(string base64Document) {
			using (var tx = new TextControlHelpers(base64Document)) {
				return tx.GetMergeFields();
			}
		}

		public List<FieldAssignmentField> GetUnassignedRecipientFields(string base64Document, List<Signer> signers) {
			using (var tx = new TextControlHelpers(base64Document)) {
				return tx.GetUnassignedRecipientFields(signers);
			}
		}

		public List<SectionModel> GetSections(string base64Document) {
			using (var tx = new TextControlHelpers(base64Document)) {
				return tx.GetSubTextParts();
			}
		}

		public byte[] PrepareFormFields(string base64Document, Signer signer) {
			using (var tx = new TextControlHelpers(base64Document)) {
				return tx.PrepareFormFields(signer);
			}
		}

		public byte[] AssignRecipientFields(string base64Document, List<FieldAssignmentMapping> assignments) {
			using (var tx = new TextControlHelpers(base64Document)) {
				return tx.AssignRecipientFields(assignments);
			}
		}

		public bool HasTrackedChanges(string base64Document) {
			using (var tx = new TextControlHelpers(base64Document)) {
				return tx.HasTrackedChanges();
			}
		}

		public bool ContainsSignatureBoxes(string base64Document, List<Signer> signers) {
			using (var tx = new TextControlHelpers(base64Document)) {
				return tx.ContainsSignatureBox(signers);
			}
		}

		public byte[] SetFieldConditions(string base64Document, bool setConditions) {
			using (var tx = new TextControlHelpers(base64Document)) {
				return tx.ConditionToId(setConditions);
			}
		}

		public byte[] MergeJson(string base64Document, string jsonData) {
			using (var tx = new TextControlHelpers(base64Document)) {
				return tx.MergeJson(jsonData).ToArray();
			}
		}

		public void Dispose() {
			GC.SuppressFinalize(this);
		}
	}
}
