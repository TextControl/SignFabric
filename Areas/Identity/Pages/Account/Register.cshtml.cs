using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SignFabric.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace SignFabric.Identity.Pages.Account {

	[AllowAnonymous]
	public class RegisterModel : PageModel {
		private readonly SignInManager<LiteDB.Identity.Models.LiteDbUser> _signInManager;
		private readonly UserManager<LiteDB.Identity.Models.LiteDbUser> _userManager;
		private readonly IIdentityRedirectService _redirectService;
		private readonly IInitialUserRoleService _initialUserRoleService;
		private readonly ILogger<RegisterModel> _logger;

		public RegisterModel(
			 UserManager<LiteDB.Identity.Models.LiteDbUser> userManager,
			 SignInManager<LiteDB.Identity.Models.LiteDbUser> signInManager,
			 IIdentityRedirectService redirectService,
			 IInitialUserRoleService initialUserRoleService,
			 ILogger<RegisterModel> logger) {
			_userManager = userManager;
			_signInManager = signInManager;
			_redirectService = redirectService;
			_initialUserRoleService = initialUserRoleService;
			_logger = logger;
		}

		[BindProperty]
		public InputModel Input { get; set; }

		public string ReturnUrl { get; set; }
		public bool BootstrapAdminConfigured => _initialUserRoleService.BootstrapAdminConfigured;

		public IList<AuthenticationScheme> ExternalLogins { get; set; }

		public class InputModel {
			[Display(Name = "First Name")]
			public string FirstName { get; set; }

			[Display(Name = "Name")]
			public string LastName { get; set; }

			[Required]
			[EmailAddress]
			[Display(Name = "E-Mail")]
			public string Email { get; set; }

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

		public async Task OnGetAsync(string returnUrl = null) {
			ReturnUrl = returnUrl;
			ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
		}

		public async Task<IActionResult> OnPostAsync(string returnUrl = null) {
			returnUrl = returnUrl ?? Url.Content("~/");
			ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
			if (ModelState.IsValid) {
				var role = await _initialUserRoleService.GetInitialRoleAsync(Input.Email);
				
				var user = new LiteDB.Identity.Models.LiteDbUser { UserName = Input.Email, Email = Input.Email, EmailConfirmed = true };
				var result = await _userManager.CreateAsync(user, Input.Password);

				if (result.Succeeded) {
					_logger.LogInformation("User created a new account with password.");
					await SetProfileClaimsAsync(user, Input.FirstName, Input.LastName);

					await _initialUserRoleService.EnsureRoleExistsAsync(role);
					var roleResult = await _userManager.AddToRoleAsync(user, role);

					if (!roleResult.Succeeded) {
						foreach (var error in roleResult.Errors) {
							ModelState.AddModelError(string.Empty, error.Description);
						}

						return Page();
					}

					var twoFactorResult = await _userManager.SetTwoFactorEnabledAsync(user, true);

					if (!twoFactorResult.Succeeded) {
						foreach (var error in twoFactorResult.Errors) {
							ModelState.AddModelError(string.Empty, error.Description);
						}

						return Page();
					}

					var signInResult = await _signInManager.PasswordSignInAsync(
						Input.Email,
						Input.Password,
						isPersistent: false,
						lockoutOnFailure: false);

					if (signInResult.RequiresTwoFactor) {
						return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = false });
					}

					if (signInResult.Succeeded) {
						await _signInManager.SignOutAsync();
					}

					ModelState.AddModelError(string.Empty, "The account was created, but the verification flow could not be started. Please sign in to receive your verification code.");
					return Page();
				}
				foreach (var error in result.Errors) {
					ModelState.AddModelError(string.Empty, error.Description);
				}
			}

			// If we got this far, something failed, redisplay form
			return Page();
		}

		private async Task SetProfileClaimsAsync(LiteDB.Identity.Models.LiteDbUser user, string firstName, string lastName) {
			if (!string.IsNullOrWhiteSpace(firstName)) {
				await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.GivenName, firstName.Trim()));
			}

			if (!string.IsNullOrWhiteSpace(lastName)) {
				await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Surname, lastName.Trim()));
			}
		}
	}
}
