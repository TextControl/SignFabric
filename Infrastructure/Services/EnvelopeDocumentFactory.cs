using SignFabric.Application.Abstractions;
using SignFabric.Infrastructure.Configuration;
using SignFabric.Infrastructure.Services.TextControl;
using SignFabric.Infrastructure.Storage.LiteDb;
using System;
using System.IO;

namespace SignFabric.Infrastructure.Services {
	public class EnvelopeDocumentFactory : IEnvelopeDocumentFactory {
		private readonly AppSettingsPathResolver _paths;
		private readonly ICertificateManagementService _certificateManagementService;

		public EnvelopeDocumentFactory(
			AppSettingsPathResolver paths,
			ICertificateManagementService certificateManagementService) {
			_paths = paths;
			_certificateManagementService = certificateManagementService ?? throw new ArgumentNullException(nameof(certificateManagementService));
		}

		public string CreateEnvelopeFromDocument(string userId, string userName, MemoryStream documentStream, string fileName) =>
			CreateEnvelopeFromDocument(userId, userName, documentStream, fileName, null);

		public string CreateEnvelopeFromDocument(string userId, string userName, MemoryStream documentStream, string fileName, string signingCertificateId) {
			if (!_certificateManagementService.HasActiveSigningCertificate()) {
				throw new InvalidOperationException("No default signing certificate is configured. Upload and set a default certificate in the admin portal before creating envelopes.");
			}

			var resolvedCertificateId = string.IsNullOrWhiteSpace(signingCertificateId)
				? _certificateManagementService.GetDefaultLocalCertificateId()
				: signingCertificateId.Trim();

			if (!string.IsNullOrWhiteSpace(resolvedCertificateId) &&
				!_certificateManagementService.IsLocalCertificateAvailable(resolvedCertificateId)) {
				throw new InvalidOperationException("The selected signing certificate is not available.");
			}

			return EnvelopeDocument.ProcessNewDocument(documentStream, fileName, new EnvelopeStore(userId, _paths), userName, userId, resolvedCertificateId);
		}
	}
}
