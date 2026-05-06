using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignFabric.Application.Identity {
	public interface IUserAdministrationService {
		Task<IReadOnlyList<UserSummary>> GetUsersAsync();
		Task InviteUserAsync(string email, string firstName, string lastName, string temporaryPassword, string role, bool requireTwoFactor);
		Task SetRoleAsync(string userId, string role);
		Task SetEnabledAsync(string userId, bool enabled, string currentUserId);
		Task SetTwoFactorEnabledAsync(string userId, bool enabled);
		Task DeleteUserAsync(string userId, string currentUserId);
	}
}
