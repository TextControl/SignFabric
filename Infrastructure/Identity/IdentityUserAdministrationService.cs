using SignFabric.Application.Identity;
using LiteDB.Identity.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Identity {
	public class IdentityUserAdministrationService : IUserAdministrationService {
		private readonly UserManager<LiteDbUser> _userManager;
		private readonly RoleManager<LiteDbRole> _roleManager;

		public IdentityUserAdministrationService(
			UserManager<LiteDbUser> userManager,
			RoleManager<LiteDbRole> roleManager) {
			_userManager = userManager;
			_roleManager = roleManager;
		}

		public async Task<IReadOnlyList<UserSummary>> GetUsersAsync() {
			var users = _userManager.Users
				.OrderBy(user => user.Email)
				.ToList();
			var result = new List<UserSummary>();

			foreach (var user in users) {
				var claims = await _userManager.GetClaimsAsync(user);
				result.Add(new UserSummary {
					Id = await _userManager.GetUserIdAsync(user),
					Email = await _userManager.GetEmailAsync(user),
					UserName = await _userManager.GetUserNameAsync(user),
					FirstName = claims.FirstOrDefault(claim => claim.Type == ClaimTypes.GivenName)?.Value,
					LastName = claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Surname)?.Value,
					EmailConfirmed = await _userManager.IsEmailConfirmedAsync(user),
					TwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user),
					IsDisabled = await _userManager.IsLockedOutAsync(user),
					Roles = (await _userManager.GetRolesAsync(user)).ToList()
				});
			}

			return result;
		}

		public async Task InviteUserAsync(string email, string firstName, string lastName, string temporaryPassword, string role, bool requireTwoFactor) {
			await EnsureRoleExistsAsync(role);

			var existingUser = await _userManager.FindByEmailAsync(email);

			if (existingUser != null) {
				throw new InvalidOperationException("A user with this e-mail address already exists.");
			}

			var user = new LiteDbUser {
				UserName = email,
				Email = email,
				EmailConfirmed = true
			};

			var createResult = await _userManager.CreateAsync(user, temporaryPassword);

			if (!createResult.Succeeded) {
				throw new InvalidOperationException(ToErrorMessage(createResult));
			}

			var roleResult = await _userManager.AddToRoleAsync(user, role);

			if (!roleResult.Succeeded) {
				throw new InvalidOperationException(ToErrorMessage(roleResult));
			}

			await SetProfileClaimsAsync(user, firstName, lastName);

			if (requireTwoFactor) {
				var twoFactorResult = await _userManager.SetTwoFactorEnabledAsync(user, true);

				if (!twoFactorResult.Succeeded) {
					throw new InvalidOperationException(ToErrorMessage(twoFactorResult));
				}
			}
		}

		public async Task SetRoleAsync(string userId, string role) {
			await EnsureRoleExistsAsync(role);

			var user = await _userManager.FindByIdAsync(userId);

			if (user == null) {
				throw new InvalidOperationException("User not found.");
			}

			var currentRoles = await _userManager.GetRolesAsync(user);

			if (currentRoles.Contains(AppRoles.Admin) && role != AppRoles.Admin) {
				var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);

				if (admins.Count <= 1) {
					throw new InvalidOperationException("At least one administrator must remain.");
				}
			}

			if (currentRoles.Count > 0) {
				var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);

				if (!removeResult.Succeeded) {
					throw new InvalidOperationException(ToErrorMessage(removeResult));
				}
			}

			var addResult = await _userManager.AddToRoleAsync(user, role);

			if (!addResult.Succeeded) {
				throw new InvalidOperationException(ToErrorMessage(addResult));
			}
		}

		public async Task SetEnabledAsync(string userId, bool enabled, string currentUserId) {
			var user = await _userManager.FindByIdAsync(userId);

			if (user == null) {
				throw new InvalidOperationException("User not found.");
			}

			if (!enabled && userId == currentUserId) {
				throw new InvalidOperationException("You cannot disable your own account.");
			}

			var roles = await _userManager.GetRolesAsync(user);

			if (!enabled && roles.Contains(AppRoles.Admin)) {
				var enabledAdmins = 0;
				var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);

				foreach (var admin in admins) {
					if (!await _userManager.IsLockedOutAsync(admin)) {
						enabledAdmins++;
					}
				}

				if (enabledAdmins <= 1) {
					throw new InvalidOperationException("At least one enabled administrator must remain.");
				}
			}

			var lockoutResult = await _userManager.SetLockoutEnabledAsync(user, true);

			if (!lockoutResult.Succeeded) {
				throw new InvalidOperationException(ToErrorMessage(lockoutResult));
			}

			DateTimeOffset? lockoutEnd = enabled ? null : DateTimeOffset.MaxValue;
			var result = await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);

			if (!result.Succeeded) {
				throw new InvalidOperationException(ToErrorMessage(result));
			}
		}

		public async Task SetTwoFactorEnabledAsync(string userId, bool enabled) {
			var user = await _userManager.FindByIdAsync(userId);

			if (user == null) {
				throw new InvalidOperationException("User not found.");
			}

			if (enabled && !await _userManager.IsEmailConfirmedAsync(user)) {
				throw new InvalidOperationException("The user's e-mail address must be confirmed before e-mail two-factor authentication can be enabled.");
			}

			var result = await _userManager.SetTwoFactorEnabledAsync(user, enabled);

			if (!result.Succeeded) {
				throw new InvalidOperationException(ToErrorMessage(result));
			}
		}

		public async Task DeleteUserAsync(string userId, string currentUserId) {
			var user = await _userManager.FindByIdAsync(userId);

			if (user == null) {
				throw new InvalidOperationException("User not found.");
			}

			if (userId == currentUserId) {
				throw new InvalidOperationException("You cannot delete your own account.");
			}

			var roles = await _userManager.GetRolesAsync(user);
			if (roles.Contains(AppRoles.Admin)) {
				var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);

				if (admins.Count <= 1) {
					throw new InvalidOperationException("At least one administrator must remain.");
				}
			}

			var result = await _userManager.DeleteAsync(user);

			if (!result.Succeeded) {
				throw new InvalidOperationException(ToErrorMessage(result));
			}
		}

		private async Task EnsureRoleExistsAsync(string role) {
			if (!AppRoles.All.Contains(role)) {
				throw new InvalidOperationException("Unknown role.");
			}

			if (!await _roleManager.RoleExistsAsync(role)) {
				var result = await _roleManager.CreateAsync(new LiteDbRole { Name = role });

				if (!result.Succeeded) {
					throw new InvalidOperationException(ToErrorMessage(result));
				}
			}
		}

		private static string ToErrorMessage(IdentityResult result) =>
			string.Join(" ", result.Errors.Select(error => error.Description));

		private async Task SetProfileClaimsAsync(LiteDbUser user, string firstName, string lastName) {
			await SetClaimAsync(user, ClaimTypes.GivenName, firstName);
			await SetClaimAsync(user, ClaimTypes.Surname, lastName);
		}

		private async Task SetClaimAsync(LiteDbUser user, string claimType, string value) {
			var claims = await _userManager.GetClaimsAsync(user);
			var existing = claims.FirstOrDefault(claim => claim.Type == claimType);
			var normalizedValue = (value ?? string.Empty).Trim();

			if (existing != null) {
				var removeResult = await _userManager.RemoveClaimAsync(user, existing);
				if (!removeResult.Succeeded) {
					throw new InvalidOperationException(ToErrorMessage(removeResult));
				}
			}

			if (!string.IsNullOrWhiteSpace(normalizedValue)) {
				var addResult = await _userManager.AddClaimAsync(user, new Claim(claimType, normalizedValue));
				if (!addResult.Succeeded) {
					throw new InvalidOperationException(ToErrorMessage(addResult));
				}
			}
		}
	}
}
