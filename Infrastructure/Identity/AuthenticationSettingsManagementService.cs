using SignFabric.Application.Abstractions;
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

		public AuthenticationSettingsManagementService(IHostEnvironment environment) {
			_appSettingsPath = Path.Combine(environment.ContentRootPath, "appsettings.json");
		}

		public async Task<AuthenticationSettingsConfiguration> GetSettingsAsync() {
			var document = await ReadAppSettingsAsync();
			var authentication = document["Authentication"] as JObject ?? new JObject();
			var oidc = authentication["OpenIdConnect"] as JObject ?? new JObject();
			var localOAuth = authentication["LocalOAuth"] as JObject ?? new JObject();

			return new AuthenticationSettingsConfiguration {
				Bearer = (authentication["Bearer"] as JObject)?.ToObject<BearerAuthenticationSettings>() ?? new BearerAuthenticationSettings(),
				OpenIdConnect = new OpenIdConnectAuthenticationSettings {
					Enabled = oidc.Value<bool?>("Enabled") ?? false,
					DisplayName = oidc.Value<string>("DisplayName"),
					Authority = oidc.Value<string>("Authority"),
					ClientId = oidc.Value<string>("ClientId"),
					HasClientSecret = !string.IsNullOrWhiteSpace(oidc.Value<string>("ClientSecret")),
					CallbackPath = oidc.Value<string>("CallbackPath"),
					SignedOutCallbackPath = oidc.Value<string>("SignedOutCallbackPath"),
					ResponseType = oidc.Value<string>("ResponseType"),
					SaveTokens = oidc.Value<bool?>("SaveTokens") ?? true,
					AutoProvisionUsers = oidc.Value<bool?>("AutoProvisionUsers") ?? false,
					Scopes = ReadStringArray(oidc["Scopes"])
				},
				LocalOAuth = new LocalOAuthSettings {
					Enabled = localOAuth.Value<bool?>("Enabled") ?? false,
					Issuer = localOAuth.Value<string>("Issuer"),
					Audience = localOAuth.Value<string>("Audience"),
					HasSigningKey = !string.IsNullOrWhiteSpace(localOAuth.Value<string>("SigningKey")),
					AccessTokenMinutes = localOAuth.Value<int?>("AccessTokenMinutes") ?? 60,
					Clients = ReadClients(localOAuth["Clients"])
				}
			};
		}

		public async Task SaveSettingsAsync(AuthenticationSettingsConfiguration settings) {
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}

			var document = await ReadAppSettingsAsync();
			var authentication = EnsureObject(document, "Authentication");
			var bearer = EnsureObject(authentication, "Bearer");
			var oidc = EnsureObject(authentication, "OpenIdConnect");
			var localOAuth = EnsureObject(authentication, "LocalOAuth");

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

		private static List<string> ReadStringArray(JToken token) =>
			token is JArray array
				? array.Select(value => value?.ToString()).Where(value => !string.IsNullOrWhiteSpace(value)).ToList()
				: new List<string>();

		private static List<LocalOAuthClientSettings> ReadClients(JToken token) =>
			token?.ToObject<List<LocalOAuthClientSettings>>() ?? new List<LocalOAuthClientSettings>();
	}
}
