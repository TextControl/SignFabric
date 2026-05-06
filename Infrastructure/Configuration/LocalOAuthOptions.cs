using System.Collections.Generic;

namespace SignFabric.Infrastructure.Configuration {
	public class LocalOAuthOptions {
		public bool Enabled { get; set; }
		public string Issuer { get; set; } = "SignFabric";
		public string Audience { get; set; } = "signfabric-api";
		public string SigningKey { get; set; }
		public int AccessTokenMinutes { get; set; } = 60;
		public List<LocalOAuthClientOptions> Clients { get; set; } = new();
	}

	public class LocalOAuthClientOptions {
		public string ClientId { get; set; }
		public string DisplayName { get; set; }
		public string SecretSha256 { get; set; }
		public string UserId { get; set; }
		public List<string> Scopes { get; set; } = new();
	}
}
