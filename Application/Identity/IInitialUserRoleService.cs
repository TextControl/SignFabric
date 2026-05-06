using System.Threading.Tasks;

namespace SignFabric.Application.Identity {
	public interface IInitialUserRoleService {
		bool BootstrapAdminConfigured { get; }
		Task<string> GetInitialRoleAsync(string email);
		Task EnsureRoleExistsAsync(string role);
	}
}
