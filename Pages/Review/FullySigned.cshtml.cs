using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SignFabric.Pages.Review {
	[AllowAnonymous]
	public class FullySignedModel : PageModel {
		public void OnGet() {
		}
	}
}
