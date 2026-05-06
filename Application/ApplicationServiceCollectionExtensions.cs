using SignFabric.Application.Abstractions;
using SignFabric.Application.Services;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Signing;
using SignFabric.Application.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace SignFabric.Application {
	public static class ApplicationServiceCollectionExtensions {
		public static IServiceCollection AddApplicationServices(this IServiceCollection services) {
			services.AddScoped<IEnvelopeService>(sp => {
				var currentUser = sp.GetRequiredService<ICurrentUserContext>();
				return new EnvelopeService(
					sp.GetRequiredService<ITxDocumentService>(),
					sp.GetRequiredService<IEmailSender>(),
					sp.GetRequiredService<IAuditLogger>(),
					sp.GetRequiredService<IStoreRepositoryFactory>(),
					currentUser.UserId);
			});

			services.AddScoped<ISigningWorkflowService>(sp => {
				var currentUser = sp.GetRequiredService<ICurrentUserContext>();
				return new SigningWorkflowService(
					sp.GetRequiredService<ITxDocumentService>(),
					sp.GetRequiredService<IEmailSender>(),
					sp.GetRequiredService<IAuditLogger>(),
					sp.GetRequiredService<IStoreRepositoryFactory>(),
					currentUser.UserId,
					sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SigningWorkflowService>>());
			});

			services.AddScoped<ITemplateService>(sp => {
				var currentUser = sp.GetRequiredService<ICurrentUserContext>();
				return new TemplateService(
					sp.GetRequiredService<ITxDocumentService>(),
					sp.GetRequiredService<IAuditLogger>(),
					sp.GetRequiredService<IStoreRepositoryFactory>(),
					currentUser.UserId);
			});

			services.AddScoped<IDocumentMergeService>(sp => {
				var currentUser = sp.GetRequiredService<ICurrentUserContext>();
				return new DocumentMergeService(
					sp.GetRequiredService<ITxDocumentService>(),
					sp.GetRequiredService<IEnvelopeService>(),
					sp.GetRequiredService<IStoreRepositoryFactory>(),
					currentUser.UserId);
			});

			services.AddScoped<IContractService>(sp => {
				var currentUser = sp.GetRequiredService<ICurrentUserContext>();
				return new ContractService(
					sp.GetRequiredService<ITxDocumentService>(),
					sp.GetRequiredService<IStoreRepositoryFactory>(),
					currentUser.UserId);
			});

			services.AddScoped<IExternalSigningService>(sp => {
				var currentUser = sp.GetRequiredService<ICurrentUserContext>();
				return new ExternalSigningService(
					sp.GetRequiredService<IEnvelopeService>(),
					sp.GetRequiredService<IContractService>(),
					sp.GetRequiredService<IStoreRepositoryFactory>(),
					currentUser.UserId);
			});

			services.AddScoped<IEditableDocumentService, EditableDocumentService>();
			services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
			services.AddScoped<IDocumentPageService, DocumentPageService>();
			services.AddScoped<IEnvelopeWorkflowService, EnvelopeWorkflowService>();
			services.AddScoped<ITemplateWorkflowService, TemplateWorkflowService>();
			services.AddScoped<IContractWorkflowService, ContractWorkflowService>();
			services.AddScoped<ICollaborationWorkflowService, CollaborationWorkflowService>();
			services.AddScoped<IFieldExtractionService, FieldExtractionService>();

			return services;
		}
	}
}
