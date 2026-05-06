using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.Templates {
	/// <summary>
	/// Application service for template management
	/// </summary>
	public interface ITemplateService {
		Task<Template> CreateAsync(Template template, MemoryStream documentStream);
		Task<Template> GetAsync(string templateId);
		Task<List<Template>> GetAllAsync(string userId);
		Task UpdateAsync(Template template);
		Task DeleteAsync(string templateId);
	}
}
