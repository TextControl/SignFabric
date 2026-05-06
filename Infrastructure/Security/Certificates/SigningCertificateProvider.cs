using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using SignFabric.Application.Abstractions;
using SignFabric.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Security.Certificates {
	public class SigningCertificateProvider : ISigningCertificateProvider {
		private readonly AppSettings _settings;
		private readonly LocalPfxCertificateManagementService _localCertificates;

		public SigningCertificateProvider(
			IOptions<AppSettings> settings,
			LocalPfxCertificateManagementService localCertificates) {
			_settings = settings.Value;
			_localCertificates = localCertificates;
		}

		public Task<X509Certificate2> LoadSigningCertificateAsync() {
			var configuration = _localCertificates.GetActiveSigningConfiguration();
			var provider = configuration.Provider ?? _settings.SigningCertificate?.Provider ?? "LocalPfx";

			if (provider.Equals("AzureKeyVault", StringComparison.OrdinalIgnoreCase)) {
				return LoadAzureKeyVaultCertificateAsync(configuration);
			}

			var certificate = _localCertificates.GetActiveLocalCertificate();
			return Task.FromResult(new X509Certificate2(
				certificate.Path,
				certificate.Password,
				X509KeyStorageFlags.Exportable));
		}

		private async Task<X509Certificate2> LoadAzureKeyVaultCertificateAsync(SigningCertificateConfiguration configuration) {
			if (string.IsNullOrWhiteSpace(configuration.AzureKeyVaultUri)) {
				throw new InvalidOperationException("Azure Key Vault URI is not configured.");
			}

			if (string.IsNullOrWhiteSpace(configuration.AzureCertificateName)) {
				throw new InvalidOperationException("Azure Key Vault certificate name is not configured.");
			}

			var credentialOptions = new DefaultAzureCredentialOptions();
			if (!string.IsNullOrWhiteSpace(configuration.AzureTenantId)) {
				credentialOptions.TenantId = configuration.AzureTenantId;
			}

			if (!string.IsNullOrWhiteSpace(configuration.AzureClientId)) {
				credentialOptions.ManagedIdentityClientId = configuration.AzureClientId;
			}

			var client = new SecretClient(new Uri(configuration.AzureKeyVaultUri), new DefaultAzureCredential(credentialOptions));
			var secret = await client.GetSecretAsync(configuration.AzureCertificateName);
			var certificateData = GetCertificateData(secret.Value.Value);

			return new X509Certificate2(
				certificateData,
				(string)null,
				X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
		}

		private static byte[] GetCertificateData(string secretValue) {
			try {
				return Convert.FromBase64String(secretValue);
			}
			catch (FormatException) {
				return Encoding.UTF8.GetBytes(secretValue);
			}
		}
	}
}
