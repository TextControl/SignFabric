using SignFabric.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Security.Claims;

namespace SignFabric.Infrastructure.Identity {
	public class HttpCurrentUserContext : ICurrentUserContext {
		private readonly IHttpContextAccessor _httpContextAccessor;

		public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor) {
			_httpContextAccessor = httpContextAccessor;
		}

		public string UserId =>
			GetStableUserId() ?? "anonymous";

		public string UserName =>
			GetDisplayName() ??
			FindClaimValue(ClaimTypes.Name, "name", "preferred_username", "email", "client_id", "azp", "appid") ??
			"User";

		private string GetStableUserId() {
			var subject = FindClaimValue(ClaimTypes.NameIdentifier, "oid", "sub", "client_id", "azp", "appid", "email", "preferred_username");
			if (string.IsNullOrWhiteSpace(subject)) {
				return null;
			}

			var tenant = FindClaimValue("tid", "tenant_id");
			return string.IsNullOrWhiteSpace(tenant)
				? subject
				: $"{tenant}:{subject}";
		}

		private string GetDisplayName() {
			var user = _httpContextAccessor.HttpContext?.User;
			var firstName = user?.FindFirst(ClaimTypes.GivenName)?.Value;
			var lastName = user?.FindFirst(ClaimTypes.Surname)?.Value;
			var displayName = string.Join(" ", new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));

			return string.IsNullOrWhiteSpace(displayName)
				? null
				: displayName;
		}

		private string FindClaimValue(params string[] claimTypes) {
			var user = _httpContextAccessor.HttpContext?.User;
			return claimTypes
				.Select(type => user?.FindFirst(type)?.Value)
				.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
		}
	}
}
