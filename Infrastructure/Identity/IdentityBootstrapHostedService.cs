using SignFabric.Application.Identity;
using SignFabric.Infrastructure.Configuration;
using LiteDB.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Identity {
	public class IdentityBootstrapHostedService : IHostedService {
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger<IdentityBootstrapHostedService> _logger;
		private readonly BootstrapAdminOptions _bootstrapAdminOptions;

		public IdentityBootstrapHostedService(
			IServiceProvider serviceProvider,
			ILogger<IdentityBootstrapHostedService> logger,
			IOptions<BootstrapAdminOptions> bootstrapAdminOptions) {
			_serviceProvider = serviceProvider;
			_logger = logger;
			_bootstrapAdminOptions = bootstrapAdminOptions.Value;
		}

		public async Task StartAsync(CancellationToken cancellationToken) {
			using var scope = _serviceProvider.CreateScope();
			var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<LiteDbRole>>();
			var userManager = scope.ServiceProvider.GetRequiredService<UserManager<LiteDbUser>>();

			foreach (var role in AppRoles.All) {
				if (!await roleManager.RoleExistsAsync(role)) {
					var result = await roleManager.CreateAsync(new LiteDbRole { Name = role });

					if (!result.Succeeded) {
						throw new InvalidOperationException($"Unable to create role '{role}': {string.Join(", ", result.Errors.Select(error => error.Description))}");
					}
				}
			}

			var admins = await userManager.GetUsersInRoleAsync(AppRoles.Admin);

			if (admins.Count == 0) {
				var initialAdmin = await FindInitialAdminAsync(userManager);

				if (initialAdmin != null) {
					var result = await userManager.AddToRoleAsync(initialAdmin, AppRoles.Admin);

					if (!result.Succeeded) {
						throw new InvalidOperationException($"Unable to assign first admin role: {string.Join(", ", result.Errors.Select(error => error.Description))}");
					}

					_logger.LogInformation("Promoted '{UserName}' to the initial Admin role.", initialAdmin.UserName);
				}
				else if (_bootstrapAdminOptions.HasEmail) {
					_logger.LogInformation("Bootstrap admin '{Email}' is configured but no matching user exists yet.", _bootstrapAdminOptions.Email);
				}
			}
		}

		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		private async Task<LiteDbUser> FindInitialAdminAsync(UserManager<LiteDbUser> userManager) {
			if (_bootstrapAdminOptions.HasEmail) {
				return await userManager.FindByEmailAsync(_bootstrapAdminOptions.Email.Trim());
			}

			return userManager.Users
				.OrderBy(user => user.UserName)
				.FirstOrDefault();
		}
	}
}
