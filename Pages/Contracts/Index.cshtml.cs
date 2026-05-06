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
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Pages.Contracts {
	[Authorize]
	public class IndexModel : PageModel {
		private readonly IDocumentPageService _pageService;
		private readonly string _userId;

		public List<Contract> Contracts { get; set; }

		public IndexModel(
			IDocumentPageService pageService,
			ICurrentUserContext currentUserContext) {
			_pageService = pageService;
			_userId = currentUserContext.UserId;
		}

		public async Task OnGetAsync() {
			Contracts = await _pageService.GetContractsAsync(_userId);
		}
	}
}
