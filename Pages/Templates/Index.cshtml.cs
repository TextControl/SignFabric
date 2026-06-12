using SignFabric.Application.Services;
using SignFabric.Application.Abstractions;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Signing;
using SignFabric.Application.Templates;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Pages.Templates {
	[Authorize(Roles = SignFabric.Application.Identity.AppRoles.EnvelopeCreators)]
	public class IndexModel : PageModel {
		private readonly IDocumentPageService _pageService;
		private readonly ITemplateService _templateService;
		private readonly string _userId;

		public List<Template> Templates { get; set; }
		[TempData]
		public string StatusMessage { get; set; }

		public IndexModel(
			IDocumentPageService pageService,
			ITemplateService templateService,
			ICurrentUserContext currentUserContext) {
			_pageService = pageService;
			_templateService = templateService;
			_userId = currentUserContext.UserId;
		}

		public async Task OnGetAsync() {
			Templates = await _pageService.GetTemplatesAsync(_userId);
		}

		public async Task<IActionResult> OnPostDeleteAsync(string templateId) {
			if (string.IsNullOrWhiteSpace(templateId)) {
				StatusMessage = "Select a template to remove.";
				return RedirectToPage();
			}

			await _templateService.DeleteAsync(templateId);
			StatusMessage = "Template deleted.";
			return RedirectToPage();
		}

		public async Task<IActionResult> OnPostDeleteSelectedAsync(List<string> selectedTemplateIds) {
			var templateIds = (selectedTemplateIds ?? new List<string>())
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (!templateIds.Any()) {
				StatusMessage = "Select at least one template to remove.";
				return RedirectToPage();
			}

			foreach (var templateId in templateIds) {
				await _templateService.DeleteAsync(templateId);
			}

			StatusMessage = templateIds.Count == 1
				? "1 template deleted."
				: $"{templateIds.Count} templates deleted.";

			return RedirectToPage();
		}
	}
}
