using SignFabric.Application.Identity;
using LiteDB.Identity.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Identity {
	public class IdentityRedirectService : IIdentityRedirectService {
		private const string AdminHomePath = "/admin";
		private const string UserHomePath = "/dashboard";

		private readonly UserManager<LiteDbUser> _userManager;

		public IdentityRedirectService(UserManager<LiteDbUser> userManager) {
			_userManager = userManager;
		}

		public async Task<string> GetHomePathAsync(ClaimsPrincipal principal) {
			var user = await _userManager.GetUserAsync(principal);
			return await GetHomePathAsync(user);
		}

		public async Task<string> GetHomePathByEmailAsync(string email) {
			var user = await _userManager.FindByEmailAsync(email);
			return await GetHomePathAsync(user);
		}

		public string NormalizeReturnUrl(string returnUrl, string homePath, bool isLocalUrl) {
			if (string.IsNullOrWhiteSpace(returnUrl) ||
				returnUrl == "~/" ||
				returnUrl == "/" ||
				IsObsoleteHomeUrl(returnUrl)) {
				return homePath;
			}

			return isLocalUrl ? returnUrl : homePath;
		}

		private async Task<string> GetHomePathAsync(LiteDbUser user) {
			if (user != null && await _userManager.IsInRoleAsync(user, AppRoles.Admin)) {
				return AdminHomePath;
			}

			return UserHomePath;
		}

		private static bool IsObsoleteHomeUrl(string returnUrl) =>
			returnUrl.Equals("/Home", StringComparison.OrdinalIgnoreCase) ||
			returnUrl.Equals("/Home/Index", StringComparison.OrdinalIgnoreCase) ||
			returnUrl.Equals("/Home/Overview", StringComparison.OrdinalIgnoreCase) ||
			returnUrl.Equals("/Dashboard/Index", StringComparison.OrdinalIgnoreCase);
	}
}
