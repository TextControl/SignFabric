using SignFabric.Application.Identity;
using SignFabric.Application.Services;
using SignFabric.Domain;
using SignFabric.Infrastructure.Configuration;
using LiteDB.Identity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Pages.Review {
	[AllowAnonymous]
	public class CreateAccountModel : PageModel {
		private readonly ISigningWorkflowService _signingWorkflowService;
		private readonly UserManager<LiteDbUser> _userManager;
		private readonly SignInManager<LiteDbUser> _signInManager;
		private readonly IInitialUserRoleService _initialUserRoleService;
		private readonly IOptionsMonitor<SignerAccountOptions> _signerAccountOptions;

		public CreateAccountModel(
			ISigningWorkflowService signingWorkflowService,
			UserManager<LiteDbUser> userManager,
			SignInManager<LiteDbUser> signInManager,
			IInitialUserRoleService initialUserRoleService,
			IOptionsMonitor<SignerAccountOptions> signerAccountOptions) {
			_signingWorkflowService = signingWorkflowService ?? throw new ArgumentNullException(nameof(signingWorkflowService));
			_userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
			_signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
			_initialUserRoleService = initialUserRoleService ?? throw new ArgumentNullException(nameof(initialUserRoleService));
			_signerAccountOptions = signerAccountOptions ?? throw new ArgumentNullException(nameof(signerAccountOptions));
		}

		[BindProperty]
		public InputModel Input { get; set; } = new();

		public string SignerEmail { get; set; }
		public string SignerName { get; set; }
		public string EnvelopeName { get; set; }

		public class InputModel {
			[Required]
			public string AccessId { get; set; }

			[Required]
			[StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
			[DataType(DataType.Password)]
			[Display(Name = "Password")]
			public string Password { get; set; }

			[DataType(DataType.Password)]
			[Display(Name = "Confirm password")]
			[Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
			public string ConfirmPassword { get; set; }
		}

		public async Task<IActionResult> OnGetAsync(string id) {
			Input.AccessId = id;
			return await LoadAsync(id) ? Page() : NotFound();
		}

		public async Task<IActionResult> OnPostAsync() {
			if (!await LoadAsync(Input.AccessId)) {
				return NotFound();
			}

			if (!ModelState.IsValid) {
				return Page();
			}

			var existingUser = await _userManager.FindByEmailAsync(SignerEmail);
			if (existingUser != null) {
				ModelState.AddModelError(string.Empty, "An account already exists for this e-mail address. Please sign in instead.");
				return Page();
			}

			var user = new LiteDbUser {
				UserName = SignerEmail,
				Email = SignerEmail,
				EmailConfirmed = true
			};

			var createResult = await _userManager.CreateAsync(user, Input.Password);
			if (!createResult.Succeeded) {
				AddErrors(createResult);
				return Page();
			}

			await SetProfileClaimsAsync(user, SignerName);
			await _initialUserRoleService.EnsureRoleExistsAsync(AppRoles.Signer);
			var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Signer);
			if (!roleResult.Succeeded) {
				AddErrors(roleResult);
				return Page();
			}

			await _signInManager.SignInAsync(user, isPersistent: false);
			return RedirectToPage("/Dashboard/Index");
		}

		private async Task<bool> LoadAsync(string accessId) {
			if (!_signerAccountOptions.CurrentValue.Enabled || string.IsNullOrWhiteSpace(accessId)) {
				return false;
			}

			SigningThanksInfo thanks;
			try {
				thanks = await _signingWorkflowService.GetSigningThanksAsync(accessId);
			}
			catch {
				return false;
			}

			if (thanks?.Signer?.SignerStatus != SignerStatus.Signed || string.IsNullOrWhiteSpace(thanks.Signer.Email)) {
				return false;
			}

			SignerEmail = thanks.Signer.Email.Trim();
			SignerName = thanks.Signer.Name;
			EnvelopeName = thanks.Envelope?.Name;
			return true;
		}

		private async Task SetProfileClaimsAsync(LiteDbUser user, string signerName) {
			if (string.IsNullOrWhiteSpace(signerName)) {
				return;
			}

			var parts = signerName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (parts.Length > 0) {
				await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.GivenName, parts[0]));
			}
			if (parts.Length > 1) {
				await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Surname, parts[1]));
			}
		}

		private void AddErrors(IdentityResult result) {
			foreach (var error in result.Errors) {
				ModelState.AddModelError(string.Empty, error.Description);
			}
		}
	}
}
