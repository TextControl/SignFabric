using SignFabric.Application.Abstractions;
using SignFabric.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignFabric.Pages.New {
	[Authorize(Roles = AppRoles.EnvelopeCreators)]
	public class IndexModel : PageModel {
		private readonly ICertificateManagementService _certificateManagementService;
		private readonly IUploadPolicy _uploadPolicy;

		public bool CanRequestSignatures { get; set; }
		public string AcceptedFileTypes => _uploadPolicy.AcceptAttribute;
		public IReadOnlyList<SigningCertificateSummary> Certificates { get; set; } = new List<SigningCertificateSummary>();
		public string DefaultCertificateId { get; set; }

		public IndexModel(
			ICertificateManagementService certificateManagementService,
			IUploadPolicy uploadPolicy) {
			_certificateManagementService = certificateManagementService;
			_uploadPolicy = uploadPolicy;
		}

		public async Task<IActionResult> OnGetAsync() {
			CanRequestSignatures = _certificateManagementService.HasActiveSigningCertificate();
			Certificates = await _certificateManagementService.GetCertificatesAsync();
			DefaultCertificateId = _certificateManagementService.GetDefaultLocalCertificateId();
			return Page();
		}
	}
}
