using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO;

namespace SignFabric.Pages.Review {
	[AllowAnonymous]
	public class SignLegacyModel : PageModel {
		public IActionResult OnGet(string id) {
			if (string.IsNullOrWhiteSpace(id) || Path.HasExtension(id)) {
				return NotFound();
			}

			return RedirectToPage("/Review/Sign", new { id });
		}
	}
}
