using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SignFabric.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace SignFabric.Identity.Pages.Account {

	[AllowAnonymous]
	public class LogoutModel : PageModel {
		private readonly SignInManager<LiteDB.Identity.Models.LiteDbUser> _signInManager;
		private readonly IIdentityRedirectService _redirectService;
		private readonly ILogger<LogoutModel> _logger;

		public LogoutModel(
			SignInManager<LiteDB.Identity.Models.LiteDbUser> signInManager,
			IIdentityRedirectService redirectService,
			ILogger<LogoutModel> logger) {
			_signInManager = signInManager;
			_redirectService = redirectService;
			_logger = logger;
		}

		public string RedirectUrl { get; private set; }

		public async Task OnGetAsync(string returnUrl = null) {
			RedirectUrl = _redirectService.NormalizeReturnUrl(returnUrl, "/", Url.IsLocalUrl(returnUrl));
			await _signInManager.SignOutAsync();
			_logger.LogInformation("User logged out.");
		}
	}
}
