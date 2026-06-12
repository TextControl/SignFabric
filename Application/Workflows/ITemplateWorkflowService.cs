using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public interface ITemplateWorkflowService {
		Task<NewTemplateModel> CreateAsync(string userId, MemoryStream documentStream, string fileName);
		Task<NewTemplateModel> CreateBlankAsync(string userId, string documentName);
		Task RenameAsync(string userId, string templateId, string documentName);
		Task<List<FieldModel>> GetFieldsAsync(string userId, string templateId);
		Task<string> CreateEnvelopeFromTemplateAsync(string userId, string userName, string templateId, IDictionary<string, string> fields);
		Task<string> CreateContractFromTemplateAsync(string userId, string userName, string templateId, IDictionary<string, string> fields);
	}
}
