using SignFabric.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SignFabric.Pages {
	[Authorize]
	public class IndexModel : PageModel {
		public IActionResult OnGet() {
			if (User.IsInRole(AppRoles.Admin)) {
				return RedirectToPage("/Admin/Index");
			}

			return RedirectToPage("/Dashboard/Index");
		}
	}
}
