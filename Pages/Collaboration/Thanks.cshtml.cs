using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SignFabric.Pages.Collaboration {
	[AllowAnonymous]
	public class ThanksModel : PageModel {
		public string DocumentId { get; set; }
		public string DocumentType { get; set; }

		public void OnGet(string id, string type = "contract") {
			DocumentId = id;
			DocumentType = type;
		}
	}
}
