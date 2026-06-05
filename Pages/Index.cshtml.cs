using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SignFabric.Pages {
	[Authorize]
	public class IndexModel : PageModel {
		public IActionResult OnGet() {
			return RedirectToPage("/Dashboard/Index");
		}
	}
}
