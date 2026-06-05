using SignFabric.Application.Services;
using SignFabric.Application.Abstractions;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Signing;
using SignFabric.Application.Templates;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Application.Identity;
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
		private readonly ISignerDocumentService _signerDocumentService;
		private readonly string _userId;

		public List<Envelope> Envelopes { get; set; }
		public bool CanRequestSignatures { get; set; }
		public bool IsSignerAccount { get; set; }

		public IndexModel(
			IDocumentPageService pageService,
			ICertificateManagementService certificateManagementService,
			ISignerDocumentService signerDocumentService,
			ICurrentUserContext currentUserContext) {
			_pageService = pageService;
			_certificateManagementService = certificateManagementService;
			_signerDocumentService = signerDocumentService;
			_userId = currentUserContext.UserId;
		}

		public async Task OnGetAsync() {
			IsSignerAccount = User.IsInRole(AppRoles.Signer);
			if (IsSignerAccount) {
				var signerEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;
				Envelopes = await _signerDocumentService.GetSignedDocumentsAsync(signerEmail);
				CanRequestSignatures = false;
				return;
			}

			Envelopes = await _pageService.GetEnvelopesAsync(_userId);
			CanRequestSignatures = _certificateManagementService.HasActiveSigningCertificate();
		}
	}
}
