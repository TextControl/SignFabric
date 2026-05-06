using SignFabric.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace SignFabric.Controllers {
	[AllowAnonymous]
	[ApiController]
	[Route("api/v1/oauth")]
	public class LocalOAuthController : ControllerBase {
		private readonly ILocalOAuthTokenService _tokenService;

		public LocalOAuthController(ILocalOAuthTokenService tokenService) {
			_tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
		}

		[HttpPost("token")]
		public async Task<IActionResult> Token() {
			var request = await ReadRequestAsync();
			if (!string.Equals(request.GrantType, "client_credentials", StringComparison.Ordinal)) {
				return BadRequest(new {
					error = "unsupported_grant_type",
					error_description = "Only the client_credentials grant type is supported."
				});
			}

			var token = await _tokenService.CreateTokenAsync(
				request.ClientId,
				request.ClientSecret,
				new[] { request.Scope });

			if (!token.Success) {
				return Unauthorized(new {
					error = token.Error
				});
			}

			return Ok(new {
				access_token = token.AccessToken,
				token_type = token.TokenType,
				expires_in = token.ExpiresIn,
				scope = token.Scope
			});
		}

		[HttpPost("secret-hash")]
		public async Task<IActionResult> SecretHash() {
			var request = await ReadSecretHashRequestAsync();
			if (string.IsNullOrWhiteSpace(request.ClientSecret)) {
				return BadRequest(new {
					error = "invalid_request",
					error_description = "client_secret is required."
				});
			}

			return Ok(new {
				secret_sha256 = _tokenService.ComputeSecretHash(request.ClientSecret)
			});
		}

		private async Task<TokenRequest> ReadRequestAsync() {
			if (Request.HasFormContentType) {
				var form = await Request.ReadFormAsync();
				return new TokenRequest {
					GrantType = form["grant_type"],
					ClientId = form["client_id"],
					ClientSecret = form["client_secret"],
					Scope = form["scope"]
				};
			}

			var json = await JsonSerializer.DeserializeAsync<TokenRequest>(Request.Body, new JsonSerializerOptions {
				PropertyNameCaseInsensitive = true
			});

			return json ?? new TokenRequest();
		}

		private async Task<SecretHashRequest> ReadSecretHashRequestAsync() {
			if (Request.HasFormContentType) {
				var form = await Request.ReadFormAsync();
				return new SecretHashRequest {
					ClientSecret = form["client_secret"]
				};
			}

			var json = await JsonSerializer.DeserializeAsync<SecretHashRequest>(Request.Body, new JsonSerializerOptions {
				PropertyNameCaseInsensitive = true
			});

			return json ?? new SecretHashRequest();
		}

		public class TokenRequest {
			public string GrantType { get; set; }
			public string ClientId { get; set; }
			public string ClientSecret { get; set; }
			public string Scope { get; set; }
		}

		public class SecretHashRequest {
			public string ClientSecret { get; set; }
		}
	}
}
