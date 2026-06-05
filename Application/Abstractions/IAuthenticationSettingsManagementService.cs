using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignFabric.Application.Abstractions {
	public interface IAuthenticationSettingsManagementService {
		Task<AuthenticationSettingsConfiguration> GetSettingsAsync();
		Task SaveSettingsAsync(AuthenticationSettingsConfiguration settings);
		Task AddLocalOAuthClientAsync(LocalOAuthClientSettings client, string signingKeyIfMissing);
		Task<bool> DeleteLocalOAuthClientAsync(string clientId);
	}

	public class AuthenticationSettingsConfiguration {
		public BearerAuthenticationSettings Bearer { get; set; } = new();
		public OpenIdConnectAuthenticationSettings OpenIdConnect { get; set; } = new();
		public LocalOAuthSettings LocalOAuth { get; set; } = new();
		public SignerAccountSettings SignerAccounts { get; set; } = new();
	}

	public class BearerAuthenticationSettings {
		public string Authority { get; set; }
		public string Audience { get; set; }
		public bool RequireHttpsMetadata { get; set; } = true;
	}

	public class OpenIdConnectAuthenticationSettings {
		public bool Enabled { get; set; }
		public string DisplayName { get; set; }
		public string Authority { get; set; }
		public string ClientId { get; set; }
		public string ClientSecret { get; set; }
		public bool HasClientSecret { get; set; }
		public string CallbackPath { get; set; }
		public string SignedOutCallbackPath { get; set; }
		public string ResponseType { get; set; }
		public bool SaveTokens { get; set; } = true;
		public bool AutoProvisionUsers { get; set; }
		public List<string> Scopes { get; set; } = new();
	}

	public class LocalOAuthSettings {
		public bool Enabled { get; set; }
		public string Issuer { get; set; }
		public string Audience { get; set; }
		public string SigningKey { get; set; }
		public bool HasSigningKey { get; set; }
		public int AccessTokenMinutes { get; set; } = 60;
		public List<LocalOAuthClientSettings> Clients { get; set; } = new();
	}

	public class LocalOAuthClientSettings {
		public string ClientId { get; set; }
		public string DisplayName { get; set; }
		public string SecretSha256 { get; set; }
		public string UserId { get; set; }
		public List<string> Scopes { get; set; } = new();
	}

	public class SignerAccountSettings {
		public bool Enabled { get; set; }
	}
}
