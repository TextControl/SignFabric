using SignFabric.Infrastructure.Configuration;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Email {
	public interface IEmailCredentialsProvider {
		Task<Credentials> GetCredentialsAsync();
	}
}
