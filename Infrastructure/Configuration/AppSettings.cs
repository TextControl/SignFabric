using System.Collections.Generic;

namespace SignFabric.Infrastructure.Configuration {
	/// <summary>
	/// Application settings configuration
	/// Loaded from appsettings.json
	/// </summary>
	public class AppSettings {
		public string DataDirectory { get; set; }
		public string DatabaseDirectory { get; set; }
		public string AuditLogsPath { get; set; }
		public SigningCertificateSettings SigningCertificate { get; set; } = new();
		public string EmailTemplatesPath { get; set; }
		public long MaxFileSize { get; set; }
		public List<string> AllowedFileTypes { get; set; } = new();
	}

	public class SigningCertificateSettings {
		public string Provider { get; set; } = "LocalPfx";
		public LocalPfxCertificateSettings LocalPfx { get; set; } = new();
		public AzureKeyVaultCertificateSettings AzureKeyVault { get; set; } = new();
	}

	public class LocalPfxCertificateSettings {
		public string Directory { get; set; } = "App_Data/certificates";
		public string ActiveCertificateId { get; set; }
	}

	public class AzureKeyVaultCertificateSettings {
		public string VaultUri { get; set; }
		public string CertificateName { get; set; }
		public bool UseDefaultAzureCredential { get; set; } = true;
		public string TenantId { get; set; }
		public string ClientId { get; set; }
	}
}
