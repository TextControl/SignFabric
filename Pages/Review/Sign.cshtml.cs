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
using System.Security.Claims;
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
		public bool RequiresEmailOtp { get; set; }
		public string OtpMessage { get; set; }
		public string OtpError { get; set; }
		public string SignInReturnUrl { get; set; }

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

				if (preparation.RequiresEmailOtp && IsAuthenticatedSigner(preparation.Signer)) {
					await _signingWorkflowService.TrustAuthenticatedSignerAsync(id);
					preparation = await _signingWorkflowService.PrepareExternalSigningAsync(id);
				}

				if (preparation.AlreadySigned) {
					return RedirectToPage("FullySigned");
				}

				await BindPreparationAsync(preparation);

				if (RequiresEmailOtp) {
					await _signingWorkflowService.RequestSignerEmailOtpAsync(id);
					OtpMessage = "We sent a verification code to your e-mail address.";
				}

				return Page();
			} catch {
				return RedirectToPage("Index", new { error = true });
			}
		}

		public async Task<IActionResult> OnPostVerifyOtpAsync(string id, string otpCode) {
			try {
				var preparation = await _signingWorkflowService.VerifySignerEmailOtpAsync(id, otpCode);
				if (preparation.AlreadySigned) {
					return RedirectToPage("FullySigned");
				}

				return RedirectToPage("Sign", new { id });
			}
			catch (Exception ex) {
				var preparation = await _signingWorkflowService.PrepareExternalSigningAsync(id);
				await BindPreparationAsync(preparation);
				OtpError = ex.Message;
				return Page();
			}
		}

		public async Task<IActionResult> OnPostSendOtpAsync(string id) {
			try {
				await _signingWorkflowService.RequestSignerEmailOtpAsync(id, forceNewCode: true);
				var preparation = await _signingWorkflowService.PrepareExternalSigningAsync(id);
				await BindPreparationAsync(preparation);
				OtpMessage = "A new verification code has been sent.";
				return Page();
			}
			catch {
				return RedirectToPage("Index", new { error = true });
			}
		}

		private async Task BindPreparationAsync(ExternalSigningPreparation preparation) {
			AccessId = preparation.AccessId;
			Document = preparation.Document;
			Envelope = preparation.Envelope;
			Signer = preparation.Signer;
			RequiresEmailOtp = preparation.RequiresEmailOtp;
			SignInReturnUrl = Url.Page("/Review/Sign", pageHandler: null, values: new { id = AccessId });

			if (_signerAccountOptions.CurrentValue.Enabled && !string.IsNullOrWhiteSpace(Signer.Email)) {
				SignerAccountExists = await _userManager.FindByEmailAsync(Signer.Email) != null;
				CanCreateSignerAccount = !SignerAccountExists;
			}
		}

		private bool IsAuthenticatedSigner(Signer signer) {
			if (signer == null || string.IsNullOrWhiteSpace(signer.Email) || User?.Identity?.IsAuthenticated != true) {
				return false;
			}

			var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity.Name;
			return string.Equals(email, signer.Email, StringComparison.OrdinalIgnoreCase);
		}
	}
}
