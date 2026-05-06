using SignFabric.Infrastructure.Configuration;
using SignFabric.Application.Abstractions;
using SignFabric.Infrastructure.Email;
using SignFabric.Infrastructure.Identity;
using SignFabric.Infrastructure.Logging;
using SignFabric.Infrastructure.Security.Certificates;
using SignFabric.Infrastructure.Services;
using SignFabric.Infrastructure.Storage;
using SignFabric.Application.Identity;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SignFabric.Infrastructure {
	public static class InfrastructureServiceCollectionExtensions {
		public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration) {
			services.Configure<Credentials>(configuration.GetSection("Credentials"));
			services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
			services.Configure<BootstrapAdminOptions>(configuration.GetSection("BootstrapAdmin"));
			services.Configure<LocalOAuthOptions>(configuration.GetSection("Authentication:LocalOAuth"));

			services.AddDataProtection();
			services.AddSingleton<AppSettingsPathResolver>();
			services.AddHttpContextAccessor();
			services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
			services.AddScoped<IIdentityRedirectService, IdentityRedirectService>();
			services.AddScoped<IInitialUserRoleService, InitialUserRoleService>();
			services.AddScoped<IUserAdministrationService, IdentityUserAdministrationService>();
			services.AddSingleton<IAuthenticationSettingsManagementService, AuthenticationSettingsManagementService>();
			services.AddSingleton<ILocalOAuthTokenService, LocalOAuthTokenService>();
			services.AddScoped<ITxDocumentService, TxDocumentService>();
			services.AddSingleton<LocalPfxCertificateManagementService>();
			services.AddSingleton<ICertificateManagementService>(provider => provider.GetRequiredService<LocalPfxCertificateManagementService>());
			services.AddSingleton<ISigningCertificateProvider, SigningCertificateProvider>();
			services.AddSingleton<EmailSettingsManagementService>();
			services.AddSingleton<IEmailSettingsManagementService>(provider => provider.GetRequiredService<EmailSettingsManagementService>());
			services.AddSingleton<IEmailCredentialsProvider>(provider => provider.GetRequiredService<EmailSettingsManagementService>());
			services.AddScoped<IEmailSender, EmailSender>();
			services.AddScoped<IAuditLogger, AuditLogger>();
			services.AddScoped<IUploadPolicy, ConfiguredUploadPolicy>();
			services.AddScoped<IStoreRepositoryFactory, StoreRepositoryFactory>();
			services.AddScoped<IUserDataStoreCleaner, UserDataStoreCleaner>();
			services.AddScoped<IEnvelopeDocumentFactory, EnvelopeDocumentFactory>();
			services.AddScoped<ISampleDocumentProvider, AppDataSampleDocumentProvider>();

			return services;
		}
	}
}
