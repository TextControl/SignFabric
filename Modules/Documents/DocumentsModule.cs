using SignFabric.Application.Services;
using SignFabric.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace SignFabric.Modules.Documents {
	/// <summary>
	/// Documents module - handles document management
	/// </summary>
	public class DocumentsModule : IFeatureModule {
		public string Name => "Documents";
		public string Description => "Document upload, storage, and management";

		public void RegisterServices(IServiceCollection services) {
			// Services are already registered globally, but this shows the module structure
		}
	}
}
