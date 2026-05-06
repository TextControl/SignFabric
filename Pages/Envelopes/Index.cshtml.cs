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

namespace SignFabric.Pages.Envelopes {
	[Authorize]
	public class IndexModel : PageModel {
		private readonly IDocumentPageService _pageService;
		private readonly ICertificateManagementService _certificateManagementService;
		private readonly string _userId;

		public List<Envelope> Envelopes { get; set; }
		public bool CanRequestSignatures { get; set; }

		public IndexModel(
			IDocumentPageService pageService,
			ICertificateManagementService certificateManagementService,
			ICurrentUserContext currentUserContext) {
			_pageService = pageService;
			_certificateManagementService = certificateManagementService;
			_userId = currentUserContext.UserId;
		}

		public async Task OnGetAsync() {
			Envelopes = await _pageService.GetEnvelopesAsync(_userId);
			CanRequestSignatures = _certificateManagementService.HasActiveSigningCertificate();
		}
	}
}
