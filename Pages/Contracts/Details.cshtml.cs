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

namespace SignFabric.Pages.Contracts {
	[Authorize]
	public class DetailsModel : PageModel {
		private readonly IDocumentPageService _pageService;
		private readonly ICertificateManagementService _certificateManagementService;
		private readonly string _userId;

		public Contract Contract { get; set; }
		public string ThumbnailSvg { get; set; }
		public bool CanRequestSignatures { get; set; }

		public DetailsModel(
			IDocumentPageService pageService,
			ICertificateManagementService certificateManagementService,
			ICurrentUserContext currentUserContext) {
			_pageService = pageService;
			_certificateManagementService = certificateManagementService;
			_userId = currentUserContext.UserId;
		}

		public async Task<IActionResult> OnGetAsync(string id) {
			try {
				if (string.IsNullOrEmpty(id)) {
					return NotFound();
				}

				var model = await _pageService.GetContractDetailsAsync(_userId, id);
				Contract = model.Contract;
				ThumbnailSvg = model.ThumbnailSvg;
				CanRequestSignatures = _certificateManagementService.HasActiveSigningCertificate();

				return Page();
			} catch (Exception) {
				return NotFound();
			}
		}
	}
}
