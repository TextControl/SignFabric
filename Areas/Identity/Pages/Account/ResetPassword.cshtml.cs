using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace SignFabric.Identity.Pages.Account {
	[AllowAnonymous]
	public class ResetPasswordModel : PageModel {
		private readonly UserManager<LiteDB.Identity.Models.LiteDbUser> _userManager;

		public ResetPasswordModel(UserManager<LiteDB.Identity.Models.LiteDbUser> userManager) {
			_userManager = userManager;
		}

		[BindProperty]
		public InputModel Input { get; set; } = new();

		public class InputModel {
			[Required]
			[EmailAddress]
			public string Email { get; set; }

			[Required]
			[StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
			[DataType(DataType.Password)]
			[Display(Name = "New password")]
			public string Password { get; set; }

			[DataType(DataType.Password)]
			[Display(Name = "Confirm password")]
			[Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
			public string ConfirmPassword { get; set; }

			[Required]
			public string Code { get; set; }
		}

		public IActionResult OnGet(string code = null, string email = null) {
			if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(email)) {
				return RedirectToPage("./Login");
			}

			Input = new InputModel {
				Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)),
				Email = email
			};

			return Page();
		}

		public async Task<IActionResult> OnPostAsync() {
			if (!ModelState.IsValid) {
				return Page();
			}

			var user = await _userManager.FindByEmailAsync(Input.Email);

			if (user == null) {
				return RedirectToPage("./ResetPasswordConfirmation");
			}

			var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);

			if (result.Succeeded) {
				return RedirectToPage("./ResetPasswordConfirmation");
			}

			foreach (var error in result.Errors) {
				ModelState.AddModelError(string.Empty, error.Description);
			}

			return Page();
		}
	}
}
