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
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Pages.Templates {
	[Authorize(Roles = SignFabric.Application.Identity.AppRoles.EnvelopeCreators)]
	public class EditModel : PageModel {
		private readonly ITemplateService _templateService;
		private readonly IDocumentPageService _pageService;
		private readonly string _userId;

		public Template Template { get; set; }
		public string DocumentContent { get; set; }
		public string ThumbnailSvg { get; set; }
		public string ErrorMessage { get; set; }

		public EditModel(
			ITemplateService templateService,
			IDocumentPageService pageService,
			ICurrentUserContext currentUserContext) {
			_templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
			_pageService = pageService ?? throw new ArgumentNullException(nameof(pageService));
			_userId = currentUserContext.UserId;
		}

		public async Task<IActionResult> OnGetAsync(string id) {
			try {
				if (string.IsNullOrEmpty(id)) {
					return NotFound();
				}

				Template = await _templateService.GetAsync(id);
				if (Template == null) {
					return NotFound();
				}

				// TODO: Load document content and thumbnail
				DocumentContent = null;
				ThumbnailSvg = null;

				return Page();
			} catch (Exception ex) {
				ErrorMessage = $"Error loading template: {ex.Message}";
				return Page();
			}
		}

		public async Task<IActionResult> OnPostSaveAsync(string id) {
			try {
				if (string.IsNullOrEmpty(id)) {
					return NotFound();
				}

			Template = await _templateService.GetAsync(id);
			if (Template == null) {
				return NotFound();
			}

			// Save template changes
			await _templateService.UpdateAsync(Template);

			return RedirectToPage("Index");
		} catch (Exception ex) {
			ErrorMessage = $"Error saving template: {ex.Message}";
			return Page();
		}
	}

	public async Task<IActionResult> OnGetEditTemplatePartialAsync(string id) {
		if (string.IsNullOrEmpty(id)) {
			return NotFound();
		}

		var model = await _pageService.GetTemplateEditModelAsync(_userId, id);

		return Partial("~/Pages/Shared/EditorPartials/_EditTemplate.cshtml", model);
	}
	}
}
