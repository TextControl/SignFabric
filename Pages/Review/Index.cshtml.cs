using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SignFabric.Pages.Review {
	[AllowAnonymous]
	public class IndexModel : PageModel {
		[BindProperty]
		public string EnvelopeID { get; set; }

		public bool Error { get; set; }

		public void OnGet(bool error = false) {
			Error = error;
		}

		public IActionResult OnPost() {
			if (string.IsNullOrEmpty(EnvelopeID)) {
				Error = true;
				return Page();
			}

			return RedirectToPage("Sign", new { id = EnvelopeID });
		}
	}
}
