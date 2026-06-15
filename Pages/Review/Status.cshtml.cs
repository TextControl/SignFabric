using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SignFabric.Application.Services;
using SignFabric.Domain;
using System;
using System.Threading.Tasks;

namespace SignFabric.Pages.Review {
	[AllowAnonymous]
	public class StatusModel : PageModel {
		private readonly ISigningWorkflowService _signingWorkflowService;

		public Envelope Envelope { get; set; }
		public Signer Recipient { get; set; }

		public StatusModel(ISigningWorkflowService signingWorkflowService) {
			_signingWorkflowService = signingWorkflowService ?? throw new ArgumentNullException(nameof(signingWorkflowService));
		}

		public async Task<IActionResult> OnGetAsync(string id) {
			try {
				var info = await _signingWorkflowService.GetSigningThanksAsync(id);
				return RedirectToPage("/Envelopes/Details", new { id = info.Envelope.EnvelopeID, accessId = id });
			}
			catch {
				return RedirectToPage("Index", new { error = true });
			}
		}
	}
}
