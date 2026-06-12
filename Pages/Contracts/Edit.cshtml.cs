using SignFabric.Application.Abstractions;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Services;
using SignFabric.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Threading.Tasks;

namespace SignFabric.Pages.Contracts {
	[Authorize(Roles = SignFabric.Application.Identity.AppRoles.EnvelopeCreators)]
	public class EditModel : PageModel {
		private readonly IContractService _contractService;
		private readonly IDocumentPageService _pageService;
		private readonly string _userId;

		public Contract Contract { get; set; }

		public EditModel(
			IContractService contractService,
			IDocumentPageService pageService,
			ICurrentUserContext currentUserContext) {
			_contractService = contractService ?? throw new ArgumentNullException(nameof(contractService));
			_pageService = pageService ?? throw new ArgumentNullException(nameof(pageService));
			_userId = currentUserContext.UserId;
		}

		public async Task<IActionResult> OnGetAsync(string id) {
			if (string.IsNullOrWhiteSpace(id)) {
				return NotFound();
			}

			Contract = await _contractService.GetAsync(id);
			if (Contract == null) {
				return NotFound();
			}

			if (Contract.UserID != _userId) {
				return Forbid();
			}

			return Page();
		}

		public async Task<IActionResult> OnGetEditPartialAsync(string id) {
			if (string.IsNullOrWhiteSpace(id)) {
				return NotFound();
			}

			var model = await _pageService.GetContractEditModelAsync(_userId, id);
			if (model?.Contract == null) {
				return NotFound();
			}

			return Partial("~/Pages/Shared/EditorPartials/_EditContract.cshtml", model);
		}
	}
}
