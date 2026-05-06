using SignFabric.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace SignFabric.Modules.Templates {
	/// <summary>
	/// Templates module - handles document templates
	/// </summary>
	public class TemplatesModule : IFeatureModule {
		public string Name => "Templates";
		public string Description => "Document template creation and management";

		public void RegisterServices(IServiceCollection services) {
			// ITemplateService is registered globally, but this shows the module structure
		}
	}
}
