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
	public class DetailsModel : PageModel {
		private readonly IDocumentPageService _pageService;
		private readonly ITemplateWorkflowService _templateWorkflowService;
		private readonly ICertificateManagementService _certificateManagementService;
		private readonly string _userId;

		public Template Template { get; set; }
		public string ThumbnailSvg { get; set; }
		public bool CanRequestSignatures { get; set; }

		[BindProperty]
		public RenameTemplateInput RenameTemplate { get; set; } = new();

		public DetailsModel(
			IDocumentPageService pageService,
			ITemplateWorkflowService templateWorkflowService,
			ICertificateManagementService certificateManagementService,
			ICurrentUserContext currentUserContext) {
			_pageService = pageService;
			_templateWorkflowService = templateWorkflowService;
			_certificateManagementService = certificateManagementService;
			_userId = currentUserContext.UserId;
		}

		public async Task<IActionResult> OnGetAsync(string id) {
			try {
				if (string.IsNullOrEmpty(id)) {
					return NotFound();
				}

				var model = await _pageService.GetTemplateDetailsAsync(_userId, id);
				Template = model.Template;
				ThumbnailSvg = model.ThumbnailSvg;
				CanRequestSignatures = _certificateManagementService.HasActiveSigningCertificate();
				RenameTemplate.Name = Template.Name;

				return Page();
			} catch (Exception) {
				return NotFound();
			}
		}

		public async Task<IActionResult> OnPostRenameAsync(string id) {
			if (string.IsNullOrWhiteSpace(RenameTemplate.Name)) {
				ModelState.AddModelError("RenameTemplate.Name", "Enter a document name.");
				await OnGetAsync(id);
				return Page();
			}

			await _templateWorkflowService.RenameAsync(_userId, id, RenameTemplate.Name);
			return RedirectToPage(new { id });
		}

		public class RenameTemplateInput {
			public string Name { get; set; }
		}
	}
}
