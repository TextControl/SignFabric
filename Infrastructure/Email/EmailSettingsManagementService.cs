using SignFabric.Application.Abstractions;
using SignFabric.Infrastructure.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Email {
	public class EmailSettingsManagementService : IEmailSettingsManagementService, IEmailCredentialsProvider {
		private const string ProtectorPurpose = "SignFabric.email.smtp-password.v1";
		private const string TemplateMetadataFileName = "email-template-settings.json";
		private static readonly Regex PlaceholderRegex = new(@"%%%[A-Za-z0-9_]+%%%", RegexOptions.Compiled);
		private readonly string _appSettingsPath;
		private readonly AppSettingsPathResolver _paths;
		private readonly IDataProtector _protector;

		public EmailSettingsManagementService(
			IHostEnvironment environment,
			AppSettingsPathResolver paths,
			IDataProtectionProvider dataProtectionProvider) {
			_appSettingsPath = Path.Combine(environment.ContentRootPath, "appsettings.json");
			_paths = paths ?? throw new ArgumentNullException(nameof(paths));
			_protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
		}

		public async Task<EmailSettingsConfiguration> GetSettingsAsync() {
			var credentials = await ReadCredentialsAsync(migratePlainTextPassword: false, throwOnProtectedPasswordFailure: false);
			var email = credentials.Email ?? new Configuration.Email();
			var hasPassword = !string.IsNullOrEmpty(email.Password);
			var passwordRequiresReset = email.PasswordProtected && hasPassword && !CanUnprotect(email.Password);

			return new EmailSettingsConfiguration {
				Username = email.Username,
				From = email.From,
				Bcc = email.Bcc,
				Server = email.Server,
				Port = email.Port,
				HasPassword = hasPassword && !passwordRequiresReset,
				IsPasswordProtected = email.PasswordProtected,
				PasswordRequiresReset = passwordRequiresReset
			};
		}

		public async Task SaveSettingsAsync(EmailSettingsConfiguration settings) {
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}

			var document = await ReadAppSettingsAsync();
			var credentials = EnsureObject(document, "Credentials");
			var email = EnsureObject(credentials, "EMail");

			email["Username"] = settings.Username ?? string.Empty;
			email["From"] = settings.From ?? string.Empty;
			email["Bcc"] = settings.Bcc ?? string.Empty;
			email["Server"] = settings.Server ?? string.Empty;
			email["Port"] = settings.Port;

			if (!string.IsNullOrWhiteSpace(settings.Password)) {
				email["Password"] = Protect(settings.Password);
				email["PasswordProtected"] = true;
			}
			else if (email["PasswordProtected"] == null) {
				email["PasswordProtected"] = false;
			}

			await WriteAppSettingsAsync(document);
		}

		public Task<IReadOnlyList<EmailTemplateSummary>> GetTemplatesAsync() {
			var directory = ResolveTemplateDirectory();
			var metadata = ReadTemplateMetadata();
			IReadOnlyList<EmailTemplateSummary> templates = Directory
				.EnumerateFiles(directory, "*.html", SearchOption.TopDirectoryOnly)
				.OrderBy(path => string.Equals(Path.GetFileName(path), "email-layout.html", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
				.ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
				.Select(path => {
					var html = File.ReadAllText(path);
					var fileName = Path.GetFileName(path);
					var settings = GetTemplateMetadata(metadata, fileName);
					return new EmailTemplateSummary {
						FileName = fileName,
						DisplayName = Path.GetFileNameWithoutExtension(path),
						Subject = settings.Subject,
						Placeholders = ExtractPlaceholders(html, settings.Subject, settings.Preheader)
					};
				})
				.ToList();

			return Task.FromResult(templates);
		}

		public Task<EmailTemplateDetails> GetTemplateAsync(string fileName) {
			var path = ResolveTemplatePath(fileName);
			var html = File.ReadAllText(path);
			var settings = GetTemplateMetadata(ReadTemplateMetadata(), Path.GetFileName(path));
			return Task.FromResult(new EmailTemplateDetails {
				FileName = Path.GetFileName(path),
				Html = html,
				Subject = settings.Subject,
				Preheader = settings.Preheader,
				Placeholders = ExtractPlaceholders(html, settings.Subject, settings.Preheader)
			});
		}

		public async Task SaveTemplateAsync(string fileName, string html, string subject, string preheader) {
			var path = ResolveTemplatePath(fileName);
			await File.WriteAllTextAsync(path, html ?? string.Empty);
			await SaveTemplateMetadataAsync(Path.GetFileName(path), subject, preheader);
		}

		public async Task<Credentials> GetCredentialsAsync() {
			return await ReadCredentialsAsync(migratePlainTextPassword: true, throwOnProtectedPasswordFailure: true);
		}

		private async Task<Credentials> ReadCredentialsAsync(bool migratePlainTextPassword, bool throwOnProtectedPasswordFailure) {
			var document = await ReadAppSettingsAsync();
			var credentialsToken = document["Credentials"];
			var credentials = credentialsToken?.ToObject<Credentials>() ?? new Credentials();
			credentials.Email ??= new Configuration.Email();

			if (!string.IsNullOrEmpty(credentials.Email.Password)) {
				if (credentials.Email.PasswordProtected) {
					if (throwOnProtectedPasswordFailure) {
						credentials.Email.Password = Unprotect(credentials.Email.Password);
					}
				}
				else if (migratePlainTextPassword) {
					var email = EnsureObject(EnsureObject(document, "Credentials"), "EMail");
					email["Password"] = Protect(credentials.Email.Password);
					email["PasswordProtected"] = true;
					await WriteAppSettingsAsync(document);
					credentials.Email.PasswordProtected = true;
				}
			}

			return credentials;
		}

		private async Task<JObject> ReadAppSettingsAsync() {
			if (!File.Exists(_appSettingsPath)) {
				throw new FileNotFoundException("The application settings file could not be found.", _appSettingsPath);
			}

			var json = await File.ReadAllTextAsync(_appSettingsPath);
			return JObject.Parse(json);
		}

		private async Task WriteAppSettingsAsync(JObject document) {
			var json = document.ToString(Formatting.Indented);
			await File.WriteAllTextAsync(_appSettingsPath, json);
		}

		private JObject EnsureObject(JObject parent, string propertyName) {
			if (parent[propertyName] is JObject existing) {
				return existing;
			}

			var created = new JObject();
			parent[propertyName] = created;
			return created;
		}

		private string ResolveTemplateDirectory() {
			var directory = Path.GetFullPath(_paths.EmailTemplatesPath);
			if (!Directory.Exists(directory)) {
				throw new DirectoryNotFoundException($"The e-mail template directory does not exist: {directory}");
			}

			return directory;
		}

		private string ResolveTemplatePath(string fileName) {
			if (string.IsNullOrWhiteSpace(fileName)) {
				throw new InvalidOperationException("Select an e-mail template.");
			}

			var requestedFileName = Path.GetFileName(fileName);
			if (!string.Equals(requestedFileName, fileName, StringComparison.Ordinal) ||
				!string.Equals(Path.GetExtension(requestedFileName), ".html", StringComparison.OrdinalIgnoreCase)) {
				throw new InvalidOperationException("Invalid e-mail template file.");
			}

			var directory = ResolveTemplateDirectory();
			var path = Path.GetFullPath(Path.Combine(directory, requestedFileName));
			if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
				throw new InvalidOperationException("Invalid e-mail template file.");
			}

			if (!File.Exists(path)) {
				throw new FileNotFoundException("The e-mail template could not be found.", path);
			}

			return path;
		}

		private IReadOnlyList<string> ExtractPlaceholders(params string[] values) =>
			values
				.SelectMany(value => PlaceholderRegex.Matches(value ?? string.Empty).Cast<Match>())
				.Select(match => match.Value)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
				.ToList();

		private string ResolveTemplateMetadataPath() =>
			Path.Combine(ResolveTemplateDirectory(), TemplateMetadataFileName);

		private Dictionary<string, EmailTemplateMetadata> ReadTemplateMetadata() {
			var path = ResolveTemplateMetadataPath();

			if (!File.Exists(path)) {
				File.WriteAllText(path, "{}");
			}

			try {
				var json = File.ReadAllText(path);
				var stored = JsonConvert.DeserializeObject<Dictionary<string, EmailTemplateMetadata>>(json)
					?? new Dictionary<string, EmailTemplateMetadata>(StringComparer.OrdinalIgnoreCase);

				return new Dictionary<string, EmailTemplateMetadata>(stored, StringComparer.OrdinalIgnoreCase);
			}
			catch {
				return new Dictionary<string, EmailTemplateMetadata>(StringComparer.OrdinalIgnoreCase);
			}
		}

		private async Task SaveTemplateMetadataAsync(string fileName, string subject, string preheader) {
			var metadata = ReadTemplateMetadata();
			metadata[fileName] = new EmailTemplateMetadata {
				Subject = string.IsNullOrWhiteSpace(subject) ? fileName : subject.Trim(),
				Preheader = preheader?.Trim() ?? string.Empty
			};

			var json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
			await File.WriteAllTextAsync(ResolveTemplateMetadataPath(), json);
		}

		private static EmailTemplateMetadata GetTemplateMetadata(IDictionary<string, EmailTemplateMetadata> metadata, string fileName) {
			if (metadata.TryGetValue(fileName, out var settings) && settings != null) {
				return new EmailTemplateMetadata {
					Subject = string.IsNullOrWhiteSpace(settings.Subject) ? fileName : settings.Subject,
					Preheader = settings.Preheader ?? string.Empty
				};
			}

			return new EmailTemplateMetadata {
				Subject = fileName,
				Preheader = string.Empty
			};
		}

		private class EmailTemplateMetadata {
			public string Subject { get; set; }
			public string Preheader { get; set; }
		}

		private string Protect(string password) => _protector.Protect(password);

		private bool CanUnprotect(string protectedPassword) {
			try {
				_protector.Unprotect(protectedPassword);
				return true;
			}
			catch {
				return false;
			}
		}

		private string Unprotect(string protectedPassword) {
			try {
				return _protector.Unprotect(protectedPassword);
			}
			catch (Exception ex) {
				throw new InvalidOperationException("The stored SMTP password could not be decrypted. Re-enter it in the admin portal.", ex);
			}
		}
	}
}
