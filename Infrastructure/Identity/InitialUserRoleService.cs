using SignFabric.Application.Identity;
using SignFabric.Infrastructure.Configuration;
using LiteDB.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Identity {
	public class InitialUserRoleService : IInitialUserRoleService {
		private readonly UserManager<LiteDbUser> _userManager;
		private readonly RoleManager<LiteDbRole> _roleManager;
		private readonly BootstrapAdminOptions _bootstrapAdminOptions;

		public InitialUserRoleService(
			UserManager<LiteDbUser> userManager,
			RoleManager<LiteDbRole> roleManager,
			IOptions<BootstrapAdminOptions> bootstrapAdminOptions) {
			_userManager = userManager;
			_roleManager = roleManager;
			_bootstrapAdminOptions = bootstrapAdminOptions.Value;
		}

		public bool BootstrapAdminConfigured => _bootstrapAdminOptions.HasEmail;

		public async Task<string> GetInitialRoleAsync(string email) {
			var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);

			if (admins.Count > 0) {
				return AppRoles.User;
			}

			if (_bootstrapAdminOptions.HasEmail) {
				return _bootstrapAdminOptions.Matches(email)
					? AppRoles.Admin
					: AppRoles.User;
			}

			return _userManager.Users.Any()
				? AppRoles.User
				: AppRoles.Admin;
		}

		public async Task EnsureRoleExistsAsync(string role) {
			if (!await _roleManager.RoleExistsAsync(role)) {
				await _roleManager.CreateAsync(new LiteDbRole { Name = role });
			}
		}
	}
}
