using SignFabric.Application.Abstractions;
using SignFabric.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Identity {
	public class LocalOAuthTokenService : ILocalOAuthTokenService {
		private readonly LocalOAuthOptions _options;

		public LocalOAuthTokenService(IOptions<LocalOAuthOptions> options) {
			_options = options.Value ?? new LocalOAuthOptions();
		}

		public Task<LocalOAuthTokenResult> CreateTokenAsync(string clientId, string clientSecret, IEnumerable<string> requestedScopes) {
			if (!_options.Enabled) {
				return Task.FromResult(Failed("local_oauth_disabled"));
			}

			if (string.IsNullOrWhiteSpace(_options.SigningKey) || Encoding.UTF8.GetByteCount(_options.SigningKey) < 32) {
				return Task.FromResult(Failed("local_oauth_signing_key_invalid"));
			}

			var client = _options.Clients.FirstOrDefault(candidate =>
				string.Equals(candidate.ClientId, clientId, StringComparison.Ordinal));

			if (client == null || string.IsNullOrWhiteSpace(client.SecretSha256) || string.IsNullOrWhiteSpace(client.UserId)) {
				return Task.FromResult(Failed("invalid_client"));
			}

			var submittedHash = ComputeSecretHash(clientSecret ?? string.Empty);
			if (!FixedTimeEquals(submittedHash, client.SecretSha256)) {
				return Task.FromResult(Failed("invalid_client"));
			}

			var allowedScopes = (client.Scopes ?? new List<string>())
				.Where(scope => !string.IsNullOrWhiteSpace(scope))
				.Select(scope => scope.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			var requested = (requestedScopes ?? Array.Empty<string>())
				.Where(scope => !string.IsNullOrWhiteSpace(scope))
				.SelectMany(scope => scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			var grantedScopes = requested.Any() ? requested : allowedScopes;
			if (!grantedScopes.Any() || grantedScopes.Any(scope => !allowedScopes.Contains(scope, StringComparer.OrdinalIgnoreCase))) {
				return Task.FromResult(Failed("invalid_scope"));
			}

			var expires = DateTime.UtcNow.AddMinutes(Math.Max(1, _options.AccessTokenMinutes));
			var scopeValue = string.Join(" ", grantedScopes);
			var claims = new List<Claim> {
				new(JwtRegisteredClaimNames.Sub, client.UserId),
				new(ClaimTypes.NameIdentifier, client.UserId),
				new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
				new("client_id", client.ClientId),
				new("name", string.IsNullOrWhiteSpace(client.DisplayName) ? client.ClientId : client.DisplayName),
				new("scope", scopeValue),
				new("scp", scopeValue)
			};

			claims.AddRange(grantedScopes.Select(scope => new Claim("roles", scope)));

			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
			var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
			var token = new JwtSecurityToken(
				issuer: _options.Issuer,
				audience: _options.Audience,
				claims: claims,
				notBefore: DateTime.UtcNow,
				expires: expires,
				signingCredentials: credentials);

			return Task.FromResult(new LocalOAuthTokenResult {
				Success = true,
				AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
				ExpiresIn = (int)Math.Round((expires - DateTime.UtcNow).TotalSeconds),
				Scope = scopeValue
			});
		}

		public string ComputeSecretHash(string clientSecret) {
			var hash = SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret ?? string.Empty));
			return Convert.ToHexString(hash).ToLowerInvariant();
		}

		private static LocalOAuthTokenResult Failed(string error) =>
			new() {
				Success = false,
				Error = error
			};

		private static bool FixedTimeEquals(string left, string right) {
			var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
			var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
			return leftBytes.Length == rightBytes.Length &&
				CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
		}
	}
}
