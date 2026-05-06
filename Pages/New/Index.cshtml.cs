using SignFabric.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SignFabric.Pages.New {
	[Authorize]
	public class IndexModel : PageModel {
		private readonly ICertificateManagementService _certificateManagementService;
		private readonly IUploadPolicy _uploadPolicy;

		public bool CanRequestSignatures { get; set; }
		public string AcceptedFileTypes => _uploadPolicy.AcceptAttribute;

		public IndexModel(
			ICertificateManagementService certificateManagementService,
			IUploadPolicy uploadPolicy) {
			_certificateManagementService = certificateManagementService;
			_uploadPolicy = uploadPolicy;
		}

		public IActionResult OnGet() {
			CanRequestSignatures = _certificateManagementService.HasActiveSigningCertificate();
			return Page();
		}
	}
}
