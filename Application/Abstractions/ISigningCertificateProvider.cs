using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace SignFabric.Application.Abstractions {
	public interface ISigningCertificateProvider {
		Task<X509Certificate2> LoadSigningCertificateAsync();
		Task<X509Certificate2> LoadSigningCertificateAsync(string certificateId);
	}
}
