namespace SignFabric.Modules {
	/// <summary>
	/// Base interface for all feature modules
	/// </summary>
	public interface IFeatureModule {
		string Name { get; }
		string Description { get; }
		void RegisterServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services);
	}
}
