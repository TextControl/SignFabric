using SignFabric.Application.Services;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Signing;
using SignFabric.Application.Templates;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Infrastructure.Configuration;
using SignFabric.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace SignFabric.Pages.Review {
	[AllowAnonymous]
	public class SignPageModel : PageModel {
		public string AccessId { get; set; }
		public string Document { get; set; }
		public Envelope Envelope { get; set; }
		public Signer Signer { get; set; }
		public bool CanCreateSignerAccount { get; set; }
		public bool SignerAccountExists { get; set; }

		private readonly ISigningWorkflowService _signingWorkflowService;
		private readonly UserManager<LiteDB.Identity.Models.LiteDbUser> _userManager;
		private readonly IOptionsMonitor<SignerAccountOptions> _signerAccountOptions;

		public SignPageModel(
			ISigningWorkflowService signingWorkflowService,
			UserManager<LiteDB.Identity.Models.LiteDbUser> userManager,
			IOptionsMonitor<SignerAccountOptions> signerAccountOptions) {
			_signingWorkflowService = signingWorkflowService ?? throw new ArgumentNullException(nameof(signingWorkflowService));
			_userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
			_signerAccountOptions = signerAccountOptions ?? throw new ArgumentNullException(nameof(signerAccountOptions));
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
				if (_signerAccountOptions.CurrentValue.Enabled && !string.IsNullOrWhiteSpace(Signer.Email)) {
					SignerAccountExists = await _userManager.FindByEmailAsync(Signer.Email) != null;
					CanCreateSignerAccount = !SignerAccountExists;
				}

				return Page();
			} catch {
				return RedirectToPage("Index", new { error = true });
			}
		}
	}
}
