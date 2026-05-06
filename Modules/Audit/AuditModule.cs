using SignFabric.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace SignFabric.Modules.Audit {
	/// <summary>
	/// Audit module - handles audit logging and compliance tracking
	/// </summary>
	public class AuditModule : IFeatureModule {
		public string Name => "Audit";
		public string Description => "Audit logging, compliance tracking, and event history";

		public void RegisterServices(IServiceCollection services) {
			// IAuditLogger is registered globally, but this shows the module structure
		}
	}
}
