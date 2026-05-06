using SignFabric.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace SignFabric.Modules.Signing {
	/// <summary>
	/// Signing module - handles document signing workflows
	/// </summary>
	public class SigningModule : IFeatureModule {
		public string Name => "Signing";
		public string Description => "Document signing workflow and signature management";

		public void RegisterServices(IServiceCollection services) {
			// ISigningWorkflowService is registered globally, but this shows the module structure
		}
	}
}
