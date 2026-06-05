using SignFabric.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Identity {
	public class AuthenticationSettingsManagementService : IAuthenticationSettingsManagementService {
		private readonly string _appSettingsPath;
		private readonly IConfiguration _configuration;

		public AuthenticationSettingsManagementService(IHostEnvironment environment, IConfiguration configuration) {
			_appSettingsPath = ResolveWritableAppSettingsPath(environment);
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		}

		public Task<AuthenticationSettingsConfiguration> GetSettingsAsync() {
			var oidcSection = _configuration.GetSection("Authentication:OpenIdConnect");
			var localOAuthSection = _configuration.GetSection("Authentication:LocalOAuth");
			var oidc = oidcSection.Get<OpenIdConnectAuthenticationSettings>() ?? new OpenIdConnectAuthenticationSettings();
			var localOAuth = localOAuthSection.Get<LocalOAuthSettings>() ?? new LocalOAuthSettings();
			var signerAccounts = _configuration.GetSection("SignerAccounts").Get<SignerAccountSettings>() ?? new SignerAccountSettings();

			oidc.HasClientSecret = !string.IsNullOrWhiteSpace(oidcSection["ClientSecret"]);
			oidc.ClientSecret = null;
			localOAuth.HasSigningKey = !string.IsNullOrWhiteSpace(localOAuthSection["SigningKey"]);
			localOAuth.SigningKey = null;

			return Task.FromResult(new AuthenticationSettingsConfiguration {
				Bearer = _configuration.GetSection("Authentication:Bearer").Get<BearerAuthenticationSettings>() ?? new BearerAuthenticationSettings(),
				OpenIdConnect = oidc,
				LocalOAuth = localOAuth,
				SignerAccounts = signerAccounts
			});
		}

		public async Task SaveSettingsAsync(AuthenticationSettingsConfiguration settings) {
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}

			var document = await ReadAppSettingsAsync();
			var authentication = EnsureObject(document, "Authentication");
			var signerAccounts = EnsureObject(document, "SignerAccounts");
			var bearer = EnsureObject(authentication, "Bearer");
			var oidc = EnsureObject(authentication, "OpenIdConnect");
			var localOAuth = EnsureObject(authentication, "LocalOAuth");

			signerAccounts["Enabled"] = settings.SignerAccounts?.Enabled ?? false;

			bearer["Authority"] = settings.Bearer?.Authority ?? string.Empty;
			bearer["Audience"] = settings.Bearer?.Audience ?? string.Empty;
			bearer["RequireHttpsMetadata"] = settings.Bearer?.RequireHttpsMetadata ?? true;

			oidc["Enabled"] = settings.OpenIdConnect?.Enabled ?? false;
			oidc["DisplayName"] = settings.OpenIdConnect?.DisplayName ?? "Single Sign-On";
			oidc["Authority"] = settings.OpenIdConnect?.Authority ?? string.Empty;
			oidc["ClientId"] = settings.OpenIdConnect?.ClientId ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(settings.OpenIdConnect?.ClientSecret)) {
				oidc["ClientSecret"] = settings.OpenIdConnect.ClientSecret;
			}
			else if (oidc["ClientSecret"] == null) {
				oidc["ClientSecret"] = string.Empty;
			}
			oidc["CallbackPath"] = settings.OpenIdConnect?.CallbackPath ?? "/signin-oidc";
			oidc["SignedOutCallbackPath"] = settings.OpenIdConnect?.SignedOutCallbackPath ?? "/signout-callback-oidc";
			oidc["ResponseType"] = settings.OpenIdConnect?.ResponseType ?? "code";
			oidc["SaveTokens"] = settings.OpenIdConnect?.SaveTokens ?? true;
			oidc["AutoProvisionUsers"] = settings.OpenIdConnect?.AutoProvisionUsers ?? false;
			oidc["Scopes"] = new JArray((settings.OpenIdConnect?.Scopes ?? new List<string>())
				.Where(value => !string.IsNullOrWhiteSpace(value))
				.Select(value => value.Trim()));

			localOAuth["Enabled"] = settings.LocalOAuth?.Enabled ?? false;
			localOAuth["Issuer"] = settings.LocalOAuth?.Issuer ?? "SignFabric";
			localOAuth["Audience"] = settings.LocalOAuth?.Audience ?? "signfabric-api";
			if (!string.IsNullOrWhiteSpace(settings.LocalOAuth?.SigningKey)) {
				localOAuth["SigningKey"] = settings.LocalOAuth.SigningKey;
			}
			else if (localOAuth["SigningKey"] == null) {
				localOAuth["SigningKey"] = string.Empty;
			}
			localOAuth["AccessTokenMinutes"] = settings.LocalOAuth?.AccessTokenMinutes ?? 60;
			localOAuth["Clients"] = JArray.FromObject(settings.LocalOAuth?.Clients ?? new List<LocalOAuthClientSettings>());

			await WriteAppSettingsAsync(document);
		}

		public async Task AddLocalOAuthClientAsync(LocalOAuthClientSettings client, string signingKeyIfMissing) {
			if (client == null) {
				throw new ArgumentNullException(nameof(client));
			}
			if (string.IsNullOrWhiteSpace(client.ClientId)) {
				throw new InvalidOperationException("The API client id is required.");
			}

			var document = await ReadAppSettingsAsync();
			var localOAuth = EnsureObject(EnsureObject(document, "Authentication"), "LocalOAuth");
			var clients = EnsureArray(localOAuth, "Clients");

			if (clients
				.OfType<JObject>()
				.Any(existing => string.Equals(existing.Value<string>("ClientId"), client.ClientId, StringComparison.OrdinalIgnoreCase))) {
				throw new InvalidOperationException("A local OAuth client with this client id already exists.");
			}

			localOAuth["Enabled"] = true;
			if (string.IsNullOrWhiteSpace(localOAuth.Value<string>("SigningKey")) && !string.IsNullOrWhiteSpace(signingKeyIfMissing)) {
				localOAuth["SigningKey"] = signingKeyIfMissing;
			}

			clients.Add(JObject.FromObject(client));
			await WriteAppSettingsAsync(document);
		}

		public async Task<bool> DeleteLocalOAuthClientAsync(string clientId) {
			if (string.IsNullOrWhiteSpace(clientId)) {
				return false;
			}

			var document = await ReadAppSettingsAsync();
			if (document["Authentication"] is not JObject authentication ||
				authentication["LocalOAuth"] is not JObject localOAuth ||
				localOAuth["Clients"] is not JArray clients) {
				return false;
			}

			var matches = clients
				.OfType<JObject>()
				.Where(existing => string.Equals(existing.Value<string>("ClientId"), clientId, StringComparison.OrdinalIgnoreCase))
				.ToList();

			if (!matches.Any()) {
				return false;
			}

			foreach (var match in matches) {
				match.Remove();
			}

			await WriteAppSettingsAsync(document);
			return true;
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

		private static JObject EnsureObject(JObject parent, string propertyName) {
			if (parent[propertyName] is JObject existing) {
				return existing;
			}

			var created = new JObject();
			parent[propertyName] = created;
			return created;
		}

		private static JArray EnsureArray(JObject parent, string propertyName) {
			if (parent[propertyName] is JArray existing) {
				return existing;
			}

			var created = new JArray();
			parent[propertyName] = created;
			return created;
		}

		private static string ResolveWritableAppSettingsPath(IHostEnvironment environment) {
			var fileName = environment.IsDevelopment()
				? "appsettings.Development.json"
				: "appsettings.json";

			return Path.Combine(environment.ContentRootPath, fileName);
		}
	}
}
