using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SignFabric.Infrastructure.Configuration {
	public class AppSettingsPathResolver {
		private static readonly char[] InvalidPathChars = Path.GetInvalidFileNameChars();
		private readonly IWebHostEnvironment _environment;
		private readonly AppSettings _settings;

		public AppSettingsPathResolver(IWebHostEnvironment environment, IOptions<AppSettings> settings) {
			_environment = environment ?? throw new ArgumentNullException(nameof(environment));
			_settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
		}

		public string DataDirectory => ResolveDirectory(_settings.DataDirectory, "App_Data");
		public string DataProtectionKeysPath => ResolveDirectory(_settings.DataProtectionKeysPath, Path.Combine("App_Data", "data-protection-keys"));
		public string DatabaseDirectory => ResolveDirectory(_settings.DatabaseDirectory, "Data");
		public string AuditLogsPath => ResolveDirectory(_settings.AuditLogsPath, Path.Combine("Data", "audit"));
		public string EmailTemplatesPath => ResolveDirectory(_settings.EmailTemplatesPath, "App_Data");
		public string CertificateStorePath => ResolveDirectory(_settings.SigningCertificate?.LocalPfx?.Directory, Path.Combine("App_Data", "certificates"));

		public string GetUserDatabasePath(string prefix, string userId) {
			var userDirectory = GetUserDatabaseDirectory(userId);
			var databasePath = Path.Combine(userDirectory, $"{prefix}.db");
			MigrateLegacyUserDatabase(prefix, userId, databasePath);
			return databasePath;
		}

		public string GetUserDatabaseDirectory(string userId) {
			var directory = Path.Combine(DatabaseDirectory, "users", GetUserDirectoryName(userId));
			Directory.CreateDirectory(directory);
			return directory;
		}

		public string GetLegacyUserDatabasePath(string prefix, string userId) =>
			Path.Combine(DatabaseDirectory, $"{prefix}_{userId}.db");

		private string ResolveDirectory(string configuredPath, string fallbackPath) {
			var path = ResolvePath(configuredPath, fallbackPath);
			Directory.CreateDirectory(path);
			return path;
		}

		private string ResolvePath(string configuredPath, string fallbackPath) {
			var path = string.IsNullOrWhiteSpace(configuredPath)
				? fallbackPath
				: configuredPath;

			return Path.IsPathRooted(path)
				? Path.GetFullPath(path)
				: Path.GetFullPath(Path.Combine(_environment.ContentRootPath, path));
		}

		private void MigrateLegacyUserDatabase(string prefix, string userId, string databasePath) {
			var legacyPath = GetLegacyUserDatabasePath(prefix, userId);
			if (!File.Exists(legacyPath) || File.Exists(databasePath)) {
				return;
			}

			File.Move(legacyPath, databasePath);
		}

		private string GetUserDirectoryName(string userId) {
			if (string.IsNullOrWhiteSpace(userId)) {
				throw new InvalidOperationException("A user id is required to resolve the user database path.");
			}

			var sanitized = new string(userId
				.Select(character => InvalidPathChars.Contains(character) ? '_' : character)
				.ToArray())
				.Trim('.');

			if (string.IsNullOrWhiteSpace(sanitized)) {
				sanitized = "user";
			}

			return sanitized == userId
				? sanitized
				: $"{sanitized}_{ComputeHashSuffix(userId)}";
		}

		private static string ComputeHashSuffix(string value) {
			var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
			return Convert.ToHexString(hash).Substring(0, 12).ToLowerInvariant();
		}
	}
}
