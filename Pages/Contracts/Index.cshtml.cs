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

namespace SignFabric.Pages.Contracts {
	[Authorize(Roles = SignFabric.Application.Identity.AppRoles.EnvelopeCreators)]
	public class IndexModel : PageModel {
		private readonly IDocumentPageService _pageService;
		private readonly IContractService _contractService;
		private readonly string _userId;

		public List<Contract> Contracts { get; set; }
		[TempData]
		public string StatusMessage { get; set; }

		public IndexModel(
			IDocumentPageService pageService,
			IContractService contractService,
			ICurrentUserContext currentUserContext) {
			_pageService = pageService;
			_contractService = contractService;
			_userId = currentUserContext.UserId;
		}

		public async Task OnGetAsync() {
			Contracts = await _pageService.GetContractsAsync(_userId);
		}

		public async Task<IActionResult> OnPostDeleteAsync(string contractId) {
			if (string.IsNullOrWhiteSpace(contractId)) {
				StatusMessage = "Select a contract to remove.";
				return RedirectToPage();
			}

			await _contractService.DeleteAsync(contractId);
			StatusMessage = "Contract deleted.";
			return RedirectToPage();
		}

		public async Task<IActionResult> OnPostDeleteSelectedAsync(List<string> selectedContractIds) {
			var contractIds = (selectedContractIds ?? new List<string>())
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (!contractIds.Any()) {
				StatusMessage = "Select at least one contract to remove.";
				return RedirectToPage();
			}

			foreach (var contractId in contractIds) {
				await _contractService.DeleteAsync(contractId);
			}

			StatusMessage = contractIds.Count == 1
				? "1 contract deleted."
				: $"{contractIds.Count} contracts deleted.";

			return RedirectToPage();
		}
	}
}
