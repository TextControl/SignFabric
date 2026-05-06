using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using SignFabric.Application.Abstractions;
using SignFabric.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace SignFabric.Identity.Pages.Account {
	[AllowAnonymous]
	public class LoginWith2faModel : PageModel {
		private const string EmailProvider = "Email";
		private readonly SignInManager<LiteDB.Identity.Models.LiteDbUser> _signInManager;
		private readonly UserManager<LiteDB.Identity.Models.LiteDbUser> _userManager;
		private readonly IEmailSender _emailSender;
		private readonly IIdentityRedirectService _redirectService;
		private readonly ILogger<LoginWith2faModel> _logger;

		public LoginWith2faModel(
			SignInManager<LiteDB.Identity.Models.LiteDbUser> signInManager,
			UserManager<LiteDB.Identity.Models.LiteDbUser> userManager,
			IEmailSender emailSender,
			IIdentityRedirectService redirectService,
			ILogger<LoginWith2faModel> logger) {
			_signInManager = signInManager;
			_userManager = userManager;
			_emailSender = emailSender;
			_redirectService = redirectService;
			_logger = logger;
		}

		[BindProperty]
		public InputModel Input { get; set; } = new();

		[BindProperty]
		public bool RememberMe { get; set; }

		[BindProperty]
		public string ReturnUrl { get; set; }

		public class InputModel {
			[Required]
			[StringLength(12, MinimumLength = 4)]
			[DataType(DataType.Text)]
			[Display(Name = "Verification code")]
			public string Code { get; set; }

		}

		public async Task<IActionResult> OnGetAsync(bool rememberMe, string returnUrl = null) {
			ReturnUrl = returnUrl ?? Url.Content("~/");
			RememberMe = rememberMe;

			var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();

			if (user == null) {
				return RedirectToPage("./Login");
			}

			var providers = await _userManager.GetValidTwoFactorProvidersAsync(user);

			if (!providers.Contains(EmailProvider)) {
				ModelState.AddModelError(string.Empty, "E-mail two-factor authentication is not available for this account.");
				return Page();
			}

			var code = await _userManager.GenerateTwoFactorTokenAsync(user, EmailProvider);
			var email = await _userManager.GetEmailAsync(user);

			try {
				await _emailSender.SendTwoFactorCodeAsync(email, code);
			}
			catch {
				ModelState.AddModelError(string.Empty, "The verification code could not be sent. Please contact an administrator.");
			}

			return Page();
		}

		public async Task<IActionResult> OnPostAsync() {
			var result = await TryCompleteTwoFactorSignInAsync();

			if (result.Success) {
				return LocalRedirect(result.RedirectUrl);
			}

			ModelState.AddModelError(string.Empty, result.Error);
			return Page();
		}

		public async Task<IActionResult> OnPostValidateCodeAsync() {
			var result = await TryCompleteTwoFactorSignInAsync();

			if (result.Success) {
				return new JsonResult(new {
					success = true,
					redirectUrl = result.RedirectUrl
				});
			}

			return new JsonResult(new {
				success = false,
				error = result.Error
			});
		}

		private async Task<(bool Success, string RedirectUrl, string Error)> TryCompleteTwoFactorSignInAsync() {
			ReturnUrl = ReturnUrl ?? Url.Content("~/");

			if (!ModelState.IsValid) {
				return (false, null, "Enter the verification code.");
			}

			var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();

			if (user == null) {
				return (false, Url.Page("./Login"), "The sign-in session expired. Please sign in again.");
			}

			var code = (Input.Code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
			var result = await _signInManager.TwoFactorSignInAsync(
				EmailProvider,
				code,
				RememberMe,
				rememberClient: false);

			if (result.Succeeded) {
				_logger.LogInformation("User completed e-mail two-factor authentication.");
				var email = await _userManager.GetEmailAsync(user);
				var homePath = await _redirectService.GetHomePathByEmailAsync(email);
				return (true, _redirectService.NormalizeReturnUrl(ReturnUrl, homePath, Url.IsLocalUrl(ReturnUrl)), null);
			}

			if (result.IsLockedOut) {
				_logger.LogWarning("User account locked out.");
				return (false, Url.Page("./Lockout"), "This account has been locked out.");
			}

			return (false, null, "Invalid verification code.");
		}
	}
}
