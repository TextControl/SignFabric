using Microsoft.Extensions.DependencyInjection;

namespace SignFabric.Modules.Validation {
	/// <summary>
	/// Validation module - handles document and signature validation
	/// </summary>
	public class ValidationModule : IFeatureModule {
		public string Name => "Validation";
		public string Description => "Document validation, signature verification, and compliance checks";

		public void RegisterServices(IServiceCollection services) {
			// Validation services will be registered here
		}
	}
}
