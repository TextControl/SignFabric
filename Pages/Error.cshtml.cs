using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace SignFabric.Pages {
	[AllowAnonymous]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class ErrorModel : PageModel {
		public string RequestId { get; set; }
		public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
		public int ResponseStatusCode { get; set; }
		public string ErrorMessage { get; set; }
		public string ErrorDetail { get; set; }

		public void OnGet(int? code = null) {
			RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
			ResponseStatusCode = code ?? HttpContext.Response.StatusCode;

			ErrorMessage = ResponseStatusCode switch {
				400 => "We could not process this request.",
				401 => "Please sign in to continue.",
				403 => "You do not have access to this item.",
				404 => "We could not find this page or document.",
				500 => "Something went wrong.",
				_ => "Something went wrong."
			};

			ErrorDetail = ResponseStatusCode switch {
				400 => "Check the entered data and try again.",
				401 => "Your session may have expired.",
				403 => "If you expected access, contact an administrator.",
				404 => "The link may be invalid, expired, or the item may have been removed.",
				500 => "The application could not complete the operation. Please try again.",
				_ => "Please try again."
			};
		}
	}
}
