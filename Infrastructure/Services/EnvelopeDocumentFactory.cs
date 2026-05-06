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

		public string CreateEnvelopeFromDocument(string userId, string userName, MemoryStream documentStream, string fileName) {
			if (!_certificateManagementService.HasActiveSigningCertificate()) {
				throw new InvalidOperationException("No active signing certificate is configured. Upload and activate a certificate in the admin portal before creating envelopes.");
			}

			return EnvelopeDocument.ProcessNewDocument(documentStream, fileName, new EnvelopeStore(userId, _paths), userName, userId);
		}
	}
}
