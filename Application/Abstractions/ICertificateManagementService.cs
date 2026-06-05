using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.Abstractions {
	public interface ICertificateManagementService {
		Task<IReadOnlyList<SigningCertificateSummary>> GetCertificatesAsync();
		Task<SigningCertificateConfiguration> GetConfigurationAsync();
		bool HasActiveSigningCertificate();
		string GetDefaultLocalCertificateId();
		bool IsLocalCertificateAvailable(string id);
		Task ConfigureAzureKeyVaultAsync(string vaultUri, string certificateName, bool useDefaultAzureCredential, string tenantId, string clientId);
		Task UseLocalPfxAsync();
		Task UploadLocalPfxAsync(string displayName, string fileName, Stream certificateStream, string password);
		Task ActivateLocalPfxAsync(string id);
		Task DeleteLocalPfxAsync(string id);
	}

	public class SigningCertificateSummary {
		public string Id { get; set; }
		public string DisplayName { get; set; }
		public string FileName { get; set; }
		public string Thumbprint { get; set; }
		public string Subject { get; set; }
		public string Issuer { get; set; }
		public string NotBefore { get; set; }
		public string NotAfter { get; set; }
		public bool IsActive { get; set; }
	}

	public class SigningCertificateConfiguration {
		public string Provider { get; set; }
		public string LocalCertificateDirectory { get; set; }
		public string AzureKeyVaultUri { get; set; }
		public string AzureCertificateName { get; set; }
		public bool AzureUseDefaultAzureCredential { get; set; } = true;
		public string AzureTenantId { get; set; }
		public string AzureClientId { get; set; }
	}
}
