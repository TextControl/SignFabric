using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignFabric.Application.Abstractions {
	public interface ILocalOAuthTokenService {
		Task<LocalOAuthTokenResult> CreateTokenAsync(string clientId, string clientSecret, IEnumerable<string> requestedScopes);
		string ComputeSecretHash(string clientSecret);
	}

	public class LocalOAuthTokenResult {
		public bool Success { get; set; }
		public string Error { get; set; }
		public string AccessToken { get; set; }
		public string TokenType { get; set; } = "Bearer";
		public int ExpiresIn { get; set; }
		public string Scope { get; set; }
	}
}
