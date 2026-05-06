using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Application.Identity {
	public interface IIdentityRedirectService {
		Task<string> GetHomePathAsync(ClaimsPrincipal principal);
		Task<string> GetHomePathByEmailAsync(string email);
		string NormalizeReturnUrl(string returnUrl, string homePath, bool isLocalUrl);
	}
}
