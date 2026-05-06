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
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Pages.Review {
	[AllowAnonymous]
	public class ValidateModel : PageModel {
		[BindProperty]
		public SignFabric.Presentation.ViewModels.ValidateModel Input { get; set; } = new SignFabric.Presentation.ViewModels.ValidateModel();

		public ValidatedDocument Result { get; set; }

		private readonly ISigningWorkflowService _signingWorkflowService;

		public ValidateModel(ISigningWorkflowService signingWorkflowService) {
			_signingWorkflowService = signingWorkflowService ?? throw new ArgumentNullException(nameof(signingWorkflowService));
		}

		public void OnGet(bool error = false) {
			Input.Error = error;
		}

		public async Task<IActionResult> OnPostAsync() {
			try {
				if (Input.Document == null) {
					Input.Error = true;
					return Page();
				}

				using (var ms = new MemoryStream()) {
					Input.Document.CopyTo(ms);
					Result = await _signingWorkflowService.ValidateSignedDocumentAsync(ms.ToArray());
				}

				if (Result.Valid == false && Result.Envelope == null) {
					Input.Error = true;
					Input.ErrorMessage = Result.ErrorMessage;
					Result = null;
				}

				return Page();
			} catch {
				Input.Error = true;
				Input.ErrorMessage = "The document could not be validated. Please check the selected PDF and try again.";
				return Page();
			}
		}
	}
}
