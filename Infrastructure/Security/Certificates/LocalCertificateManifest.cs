using System.Collections.Generic;

namespace SignFabric.Infrastructure.Security.Certificates {
	internal class LocalCertificateManifest {
		public string Provider { get; set; }
		public string ActiveCertificateId { get; set; }
		public AzureKeyVaultCertificateEntry AzureKeyVault { get; set; } = new();
		public List<LocalCertificateEntry> Certificates { get; set; } = new();
	}

	internal class AzureKeyVaultCertificateEntry {
		public string VaultUri { get; set; }
		public string CertificateName { get; set; }
		public bool? UseDefaultAzureCredential { get; set; }
		public string TenantId { get; set; }
		public string ClientId { get; set; }
	}

	internal class LocalCertificateEntry {
		public string Id { get; set; }
		public string DisplayName { get; set; }
		public string FileName { get; set; }
		public string Password { get; set; }
		public bool IsPasswordProtected { get; set; }
		public string Thumbprint { get; set; }
		public string Subject { get; set; }
		public string Issuer { get; set; }
		public string NotBefore { get; set; }
		public string NotAfter { get; set; }
	}
}
