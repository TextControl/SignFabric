using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using SignFabric.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace SignFabric.Identity.Pages.Account {
	[AllowAnonymous]
	public class ForgotPasswordModel : PageModel {
		private readonly UserManager<LiteDB.Identity.Models.LiteDbUser> _userManager;
		private readonly IEmailSender _emailSender;

		public ForgotPasswordModel(
			UserManager<LiteDB.Identity.Models.LiteDbUser> userManager,
			IEmailSender emailSender) {
			_userManager = userManager;
			_emailSender = emailSender;
		}

		[BindProperty]
		public InputModel Input { get; set; } = new();

		public class InputModel {
			[Required]
			[EmailAddress]
			[Display(Name = "E-Mail")]
			public string Email { get; set; }
		}

		public async Task<IActionResult> OnPostAsync() {
			if (!ModelState.IsValid) {
				return Page();
			}

			var user = await _userManager.FindByEmailAsync(Input.Email);

			if (user != null && await _userManager.IsEmailConfirmedAsync(user)) {
				var token = await _userManager.GeneratePasswordResetTokenAsync(user);
				var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
				var resetUrl = Url.Page(
					"./ResetPassword",
					pageHandler: null,
					values: new { area = "Identity", code = encodedToken, email = Input.Email },
					protocol: Request.Scheme,
					host: Request.Host.ToString());

				try {
					await _emailSender.SendPasswordResetAsync(Input.Email, resetUrl);
				}
				catch (Exception ex) {
					ModelState.AddModelError(string.Empty, ex.Message);
					return Page();
				}
			}

			return RedirectToPage("./ForgotPasswordConfirmation");
		}
	}
}
