using SignFabric.Application.Services;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Signing;
using SignFabric.Application.Templates;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Threading.Tasks;

namespace SignFabric.Pages.Review {
	[AllowAnonymous]
	public class ThanksModel : PageModel {
		public SigningThanksInfo Thanks { get; set; }

		private readonly ISigningWorkflowService _signingWorkflowService;

		public ThanksModel(ISigningWorkflowService signingWorkflowService) {
			_signingWorkflowService = signingWorkflowService ?? throw new ArgumentNullException(nameof(signingWorkflowService));
		}

		public async Task OnGetAsync(string id) {
			try {
				Thanks = await _signingWorkflowService.GetSigningThanksAsync(id);
			} catch {
				Thanks = new SigningThanksInfo();
			}
		}
	}
}
