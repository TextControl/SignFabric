using SignFabric.Application.Services;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Signing;
using SignFabric.Application.Templates;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Threading.Tasks;

namespace SignFabric.Pages.Review {
	[AllowAnonymous]
	public class SignPageModel : PageModel {
		public string AccessId { get; set; }
		public string Document { get; set; }
		public Envelope Envelope { get; set; }
		public Signer Signer { get; set; }

		private readonly ISigningWorkflowService _signingWorkflowService;

		public SignPageModel(ISigningWorkflowService signingWorkflowService) {
			_signingWorkflowService = signingWorkflowService ?? throw new ArgumentNullException(nameof(signingWorkflowService));
		}

		public async Task<IActionResult> OnGetAsync(string id) {
			try {
				if (string.IsNullOrEmpty(id)) {
					return RedirectToPage("Index", new { error = true });
				}

				var preparation = await _signingWorkflowService.PrepareExternalSigningAsync(id);

				if (preparation.AlreadySigned) {
					return RedirectToPage("FullySigned");
				}

				AccessId = preparation.AccessId;
				Document = preparation.Document;
				Envelope = preparation.Envelope;
				Signer = preparation.Signer;

				return Page();
			} catch {
				return RedirectToPage("Index", new { error = true });
			}
		}
	}
}
