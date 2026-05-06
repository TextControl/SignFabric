using System;
using System.Linq;
using System.Security.Claims;

namespace SignFabric.Application.Identity {
	public static class ApiAuthorization {
		public const string EnvelopeCreatePolicy = "Api.Envelopes.Create";
		public const string EnvelopeReadPolicy = "Api.Envelopes.Read";

		public const string EnvelopeCreatePermission = "envelopes:create";
		public const string EnvelopeReadPermission = "envelopes:read";

		public static bool HasPermission(ClaimsPrincipal user, string permission) {
			if (user == null || string.IsNullOrWhiteSpace(permission)) {
				return false;
			}

			return HasScope(user, permission) || HasRole(user, permission);
		}

		private static bool HasScope(ClaimsPrincipal user, string permission) {
			return user
				.FindAll(claim => string.Equals(claim.Type, "scp", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(claim.Type, "scope", StringComparison.OrdinalIgnoreCase))
				.SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				.Any(scope => string.Equals(scope, permission, StringComparison.OrdinalIgnoreCase));
		}

		private static bool HasRole(ClaimsPrincipal user, string permission) {
			return user
				.FindAll(claim => string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
				.Any(role => string.Equals(role.Value, permission, StringComparison.OrdinalIgnoreCase));
		}
	}
}
