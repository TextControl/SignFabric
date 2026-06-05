using SignFabric.Application.Abstractions;
using SignFabric.Infrastructure.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Security.Certificates {
	public class LocalPfxCertificateManagementService : ICertificateManagementService {
		private const string CertificatePasswordProtectorPurpose = "SignFabric.signing-certificates.local-pfx-password.v1";
		private readonly AppSettings _settings;
		private readonly AppSettingsPathResolver _paths;
		private readonly IDataProtector _passwordProtector;

		public LocalPfxCertificateManagementService(
			IOptions<AppSettings> settings,
			AppSettingsPathResolver paths,
			IDataProtectionProvider dataProtectionProvider) {
			_settings = settings.Value;
			_paths = paths;
			_passwordProtector = dataProtectionProvider.CreateProtector(CertificatePasswordProtectorPurpose);
		}

		public Task<IReadOnlyList<SigningCertificateSummary>> GetCertificatesAsync() =>
			Task.Run<IReadOnlyList<SigningCertificateSummary>>(() => {
				var manifest = ReadManifest();
				return manifest.Certificates
					.OrderBy(certificate => certificate.DisplayName)
					.Select(certificate => ToSummary(certificate, IsLocalPfxProvider(manifest) && certificate.Id == GetActiveCertificateId(manifest)))
					.ToList();
			});

		public Task<SigningCertificateConfiguration> GetConfigurationAsync() =>
			Task.Run(() => {
				var manifest = ReadManifest();
				var azureKeyVault = GetAzureKeyVaultConfiguration(manifest);
				return new SigningCertificateConfiguration {
					Provider = GetProvider(manifest),
					LocalCertificateDirectory = _paths.CertificateStorePath,
					AzureKeyVaultUri = azureKeyVault.VaultUri,
					AzureCertificateName = azureKeyVault.CertificateName,
					AzureUseDefaultAzureCredential = azureKeyVault.UseDefaultAzureCredential ?? _settings.SigningCertificate?.AzureKeyVault?.UseDefaultAzureCredential ?? true,
					AzureTenantId = azureKeyVault.TenantId,
					AzureClientId = azureKeyVault.ClientId
				};
			});

		public bool HasActiveSigningCertificate() {
			var manifest = ReadManifest();
			if (IsAzureKeyVaultProvider(manifest)) {
				var azureKeyVault = GetAzureKeyVaultConfiguration(manifest);
				return !string.IsNullOrWhiteSpace(azureKeyVault.VaultUri) &&
					!string.IsNullOrWhiteSpace(azureKeyVault.CertificateName);
			}

			var activeCertificateId = GetActiveCertificateId(manifest);
			return !string.IsNullOrWhiteSpace(activeCertificateId) &&
				manifest.Certificates.Any(certificate => certificate.Id == activeCertificateId);
		}

		public string GetDefaultLocalCertificateId() {
			var manifest = ReadManifest();
			var activeCertificateId = GetActiveCertificateId(manifest);
			return manifest.Certificates.Any(certificate => certificate.Id == activeCertificateId)
				? activeCertificateId
				: manifest.Certificates.FirstOrDefault()?.Id;
		}

		public bool IsLocalCertificateAvailable(string id) {
			if (string.IsNullOrWhiteSpace(id)) {
				return false;
			}

			var manifest = ReadManifest();
			return manifest.Certificates.Any(certificate => certificate.Id == id);
		}

		public Task ConfigureAzureKeyVaultAsync(
			string vaultUri,
			string certificateName,
			bool useDefaultAzureCredential,
			string tenantId,
			string clientId) =>
			Task.Run(() => {
				if (string.IsNullOrWhiteSpace(vaultUri)) {
					throw new InvalidOperationException("Vault URI is required.");
				}

				if (!Uri.TryCreate(vaultUri, UriKind.Absolute, out var parsedVaultUri) ||
					!parsedVaultUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
					throw new InvalidOperationException("Vault URI must be an absolute HTTPS URI.");
				}

				if (string.IsNullOrWhiteSpace(certificateName)) {
					throw new InvalidOperationException("Certificate name is required.");
				}

				if (!useDefaultAzureCredential) {
					throw new InvalidOperationException("Default Azure credential is required for Azure Key Vault access.");
				}

				var manifest = ReadManifest();
				manifest.Provider = "AzureKeyVault";
				manifest.AzureKeyVault = new AzureKeyVaultCertificateEntry {
					VaultUri = parsedVaultUri.ToString().TrimEnd('/'),
					CertificateName = certificateName.Trim(),
					UseDefaultAzureCredential = useDefaultAzureCredential,
					TenantId = tenantId?.Trim(),
					ClientId = clientId?.Trim()
				};

				WriteManifest(manifest);
			});

		public Task UseLocalPfxAsync() =>
			Task.Run(() => {
				var manifest = ReadManifest();
				if (string.IsNullOrWhiteSpace(GetActiveCertificateId(manifest))) {
					throw new InvalidOperationException("Upload and activate a local PFX certificate before switching to Local PFX.");
				}

				manifest.Provider = "LocalPfx";
				WriteManifest(manifest);
			});

		public async Task UploadLocalPfxAsync(string displayName, string fileName, Stream certificateStream, string password) {
			if (certificateStream == null) {
				throw new ArgumentNullException(nameof(certificateStream));
			}

			if (string.IsNullOrWhiteSpace(fileName) || !Path.GetExtension(fileName).Equals(".pfx", StringComparison.OrdinalIgnoreCase)) {
				throw new InvalidOperationException("Only .pfx certificate files are supported.");
			}

			using var ms = new MemoryStream();
			await certificateStream.CopyToAsync(ms);
			var data = ms.ToArray();
			var certificate = new X509Certificate2(data, password, X509KeyStorageFlags.Exportable);

			var manifest = ReadManifest();
			var id = Guid.NewGuid().ToString("N");
			var storedFileName = id + ".pfx";
			await File.WriteAllBytesAsync(Path.Combine(_paths.CertificateStorePath, storedFileName), data);

			manifest.Certificates.Add(new LocalCertificateEntry {
				Id = id,
				DisplayName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(fileName) : displayName.Trim(),
				FileName = storedFileName,
				Password = ProtectPassword(password),
				IsPasswordProtected = true,
				Thumbprint = certificate.Thumbprint,
				Subject = certificate.Subject,
				Issuer = certificate.Issuer,
				NotBefore = certificate.NotBefore.ToString("u"),
				NotAfter = certificate.NotAfter.ToString("u")
			});

			if (string.IsNullOrWhiteSpace(manifest.ActiveCertificateId)) {
				manifest.ActiveCertificateId = id;
			}

			WriteManifest(manifest);
		}

		public Task ActivateLocalPfxAsync(string id) =>
			Task.Run(() => {
				var manifest = ReadManifest();
				if (!manifest.Certificates.Any(certificate => certificate.Id == id)) {
					throw new InvalidOperationException("Certificate not found.");
				}

				manifest.ActiveCertificateId = id;
				WriteManifest(manifest);
			});

		public Task DeleteLocalPfxAsync(string id) =>
			Task.Run(() => {
				var manifest = ReadManifest();
				var certificate = manifest.Certificates.FirstOrDefault(item => item.Id == id);
				if (certificate == null) {
					return;
				}

				manifest.Certificates.Remove(certificate);
				if (manifest.ActiveCertificateId == id) {
					manifest.ActiveCertificateId = manifest.Certificates.FirstOrDefault()?.Id;
				}

				var path = Path.Combine(_paths.CertificateStorePath, certificate.FileName);
				if (File.Exists(path)) {
					File.Delete(path);
				}

				WriteManifest(manifest);
			});

		internal (string Path, string Password) GetActiveLocalCertificate() {
			var activeCertificateId = GetDefaultLocalCertificateId();
			return GetLocalCertificate(activeCertificateId);
		}

		internal (string Path, string Password) GetLocalCertificate(string certificateId) {
			var manifest = ReadManifest();
			var active = manifest.Certificates.FirstOrDefault(certificate => certificate.Id == certificateId);

			if (active != null) {
				return (Path.Combine(_paths.CertificateStorePath, active.FileName), UnprotectPassword(active));
			}

			throw new InvalidOperationException("The selected signing certificate is not available. Select another certificate or update the default certificate in the admin portal.");
		}

		internal SigningCertificateConfiguration GetActiveSigningConfiguration() =>
			GetConfigurationAsync().GetAwaiter().GetResult();

		private string GetProvider(LocalCertificateManifest manifest) {
			var configuredProvider = _settings.SigningCertificate?.Provider;
			return string.IsNullOrWhiteSpace(manifest.Provider)
				? string.IsNullOrWhiteSpace(configuredProvider) ? "LocalPfx" : configuredProvider
				: manifest.Provider;
		}

		private bool IsLocalPfxProvider(LocalCertificateManifest manifest) =>
			GetProvider(manifest).Equals("LocalPfx", StringComparison.OrdinalIgnoreCase);

		private bool IsAzureKeyVaultProvider(LocalCertificateManifest manifest) =>
			GetProvider(manifest).Equals("AzureKeyVault", StringComparison.OrdinalIgnoreCase);

		private AzureKeyVaultCertificateEntry GetAzureKeyVaultConfiguration(LocalCertificateManifest manifest) {
			var manifestConfiguration = manifest.AzureKeyVault ?? new AzureKeyVaultCertificateEntry();
			return new AzureKeyVaultCertificateEntry {
				VaultUri = string.IsNullOrWhiteSpace(manifestConfiguration.VaultUri)
					? _settings.SigningCertificate?.AzureKeyVault?.VaultUri
					: manifestConfiguration.VaultUri,
				CertificateName = string.IsNullOrWhiteSpace(manifestConfiguration.CertificateName)
					? _settings.SigningCertificate?.AzureKeyVault?.CertificateName
					: manifestConfiguration.CertificateName,
				UseDefaultAzureCredential = manifestConfiguration.UseDefaultAzureCredential ?? _settings.SigningCertificate?.AzureKeyVault?.UseDefaultAzureCredential,
				TenantId = string.IsNullOrWhiteSpace(manifestConfiguration.TenantId)
					? _settings.SigningCertificate?.AzureKeyVault?.TenantId
					: manifestConfiguration.TenantId,
				ClientId = string.IsNullOrWhiteSpace(manifestConfiguration.ClientId)
					? _settings.SigningCertificate?.AzureKeyVault?.ClientId
					: manifestConfiguration.ClientId
			};
		}

		private string GetActiveCertificateId(LocalCertificateManifest manifest) {
			var configuredActiveCertificateId = _settings.SigningCertificate?.LocalPfx?.ActiveCertificateId;
			return string.IsNullOrWhiteSpace(configuredActiveCertificateId)
				? manifest.ActiveCertificateId
				: configuredActiveCertificateId;
		}

		private LocalCertificateManifest ReadManifest() {
			var path = ManifestPath;
			if (!File.Exists(path)) {
				return new LocalCertificateManifest();
			}

			var json = File.ReadAllText(path);
			var manifest = JsonConvert.DeserializeObject<LocalCertificateManifest>(json) ?? new LocalCertificateManifest();
			if (ProtectLegacyPlainTextPasswords(manifest)) {
				WriteManifest(manifest);
			}
			return manifest;
		}

		private void WriteManifest(LocalCertificateManifest manifest) {
			ProtectLegacyPlainTextPasswords(manifest);
			Directory.CreateDirectory(_paths.CertificateStorePath);
			File.WriteAllText(ManifestPath, JsonConvert.SerializeObject(manifest, Formatting.Indented));
		}

		private bool ProtectLegacyPlainTextPasswords(LocalCertificateManifest manifest) {
			if (manifest?.Certificates == null) {
				return false;
			}

			var changed = false;
			foreach (var certificate in manifest.Certificates.Where(certificate =>
				!certificate.IsPasswordProtected &&
				!string.IsNullOrEmpty(certificate.Password))) {
				certificate.Password = ProtectPassword(certificate.Password);
				certificate.IsPasswordProtected = true;
				changed = true;
			}

			return changed;
		}

		private string ProtectPassword(string password) =>
			string.IsNullOrEmpty(password) ? password : _passwordProtector.Protect(password);

		private string UnprotectPassword(LocalCertificateEntry certificate) {
			if (certificate == null || string.IsNullOrEmpty(certificate.Password)) {
				return certificate?.Password;
			}

			if (!certificate.IsPasswordProtected) {
				return certificate.Password;
			}

			try {
				return _passwordProtector.Unprotect(certificate.Password);
			}
			catch (Exception ex) {
				throw new InvalidOperationException(
					$"The password for signing certificate '{certificate.DisplayName}' could not be decrypted. " +
					"Verify that the ASP.NET Core Data Protection keys for this deployment are available.",
					ex);
			}
		}

		private string ManifestPath => Path.Combine(_paths.CertificateStorePath, "certificates.json");

		private static SigningCertificateSummary ToSummary(LocalCertificateEntry certificate, bool isActive) =>
			new SigningCertificateSummary {
				Id = certificate.Id,
				DisplayName = certificate.DisplayName,
				FileName = certificate.FileName,
				Thumbprint = certificate.Thumbprint,
				Subject = certificate.Subject,
				Issuer = certificate.Issuer,
				NotBefore = certificate.NotBefore,
				NotAfter = certificate.NotAfter,
				IsActive = isActive
			};
	}
}
