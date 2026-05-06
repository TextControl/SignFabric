using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SignFabric.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SignFabric.Identity.Pages.Account {

   [AllowAnonymous]
   public class LoginModel : PageModel {
      private readonly SignInManager<LiteDB.Identity.Models.LiteDbUser> _signInManager;
      private readonly UserManager<LiteDB.Identity.Models.LiteDbUser> _userManager;
      private readonly IIdentityRedirectService _redirectService;
      private readonly IInitialUserRoleService _initialUserRoleService;
      private readonly IConfiguration _configuration;
      private readonly ILogger<LoginModel> _logger;

      public LoginModel(SignInManager<LiteDB.Identity.Models.LiteDbUser> signInManager,
         UserManager<LiteDB.Identity.Models.LiteDbUser> userManager,
         ILogger<LoginModel> logger,
         IIdentityRedirectService redirectService,
         IInitialUserRoleService initialUserRoleService,
         IConfiguration configuration) {
         _signInManager = signInManager;
         _userManager = userManager;
         _redirectService = redirectService;
         _initialUserRoleService = initialUserRoleService;
         _configuration = configuration;
         _logger = logger;
      }

      [BindProperty]
      public InputModel Input { get; set; }

      public IList<AuthenticationScheme> ExternalLogins { get; set; }

      public string ReturnUrl { get; set; }

      [TempData]
      public string ErrorMessage { get; set; }

      public class InputModel {
         [Required]
         [EmailAddress]
         [Display(Name = "E-Mail")]
         public string Email { get; set; }

         [Required]
         [DataType(DataType.Password)]
         public string Password { get; set; }

         [Display(Name = "Remember me?")]
         public bool RememberMe { get; set; }
      }

      public async Task OnGetAsync(string returnUrl = null) {
         if (!string.IsNullOrEmpty(ErrorMessage)) {
            ModelState.AddModelError(string.Empty, ErrorMessage);
         }

         returnUrl = returnUrl ?? Url.Content("~/");

         // Clear the existing external cookie to ensure a clean login process
         await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

         ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

         ReturnUrl = returnUrl;
      }

      public async Task<IActionResult> OnPostAsync(string returnUrl = null) {
         returnUrl = returnUrl ?? Url.Content("~/");
         ReturnUrl = returnUrl;
         ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

         if (ModelState.IsValid) {
            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true
            var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded) {
               _logger.LogInformation("User logged in.");
               var homePath = await _redirectService.GetHomePathByEmailAsync(Input.Email);
               return LocalRedirect(_redirectService.NormalizeReturnUrl(returnUrl, homePath, Url.IsLocalUrl(returnUrl)));
            }
            if (result.RequiresTwoFactor) {
               return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
            }
            if (result.IsLockedOut) {
               _logger.LogWarning("User account locked out.");
               return RedirectToPage("./Lockout");
            }
            else {
               ModelState.AddModelError(string.Empty, "Invalid login attempt.");
               return Page();
            }
         }

         // If we got this far, something failed, redisplay form
         return Page();
      }

      public IActionResult OnPostExternalLogin(string provider, string returnUrl = null) {
         if (string.IsNullOrWhiteSpace(provider)) {
            ModelState.AddModelError(string.Empty, "Select a sign-in provider.");
            return Page();
         }

         var redirectUrl = Url.Page("./Login", pageHandler: "ExternalLoginCallback", values: new { returnUrl });
         var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
         return new ChallengeResult(provider, properties);
      }

      public async Task<IActionResult> OnGetExternalLoginCallbackAsync(string returnUrl = null, string remoteError = null) {
         returnUrl ??= Url.Content("~/");

         if (!string.IsNullOrWhiteSpace(remoteError)) {
            ErrorMessage = $"External sign-in failed: {remoteError}";
            return RedirectToPage("./Login", new { returnUrl });
         }

         var info = await _signInManager.GetExternalLoginInfoAsync();
         if (info == null) {
            ErrorMessage = "External sign-in information could not be loaded.";
            return RedirectToPage("./Login", new { returnUrl });
         }

         var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);

         if (signInResult.Succeeded) {
            await _signInManager.UpdateExternalAuthenticationTokensAsync(info);
            _logger.LogInformation("User logged in with {Provider}.", info.LoginProvider);
            return await RedirectAfterExternalLoginAsync(returnUrl, await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey));
         }

         if (signInResult.IsLockedOut) {
            _logger.LogWarning("User account locked out.");
            return RedirectToPage("./Lockout");
         }

         var email = GetClaimValue(info.Principal, ClaimTypes.Email, "email", "preferred_username", "upn");
         if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email)) {
            ErrorMessage = "The external identity provider did not return a valid e-mail address.";
            return RedirectToPage("./Login", new { returnUrl });
         }

         var user = await _userManager.FindByEmailAsync(email);
         if (user == null) {
            if (!_configuration.GetValue("Authentication:OpenIdConnect:AutoProvisionUsers", false)) {
               ErrorMessage = "No local account is linked to this single sign-on user. Ask an administrator to create your account first.";
               return RedirectToPage("./Login", new { returnUrl });
            }

            var role = await _initialUserRoleService.GetInitialRoleAsync(email);
            user = new LiteDB.Identity.Models.LiteDbUser {
               UserName = email,
               Email = email,
               EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded) {
               ErrorMessage = ToErrorMessage(createResult);
               return RedirectToPage("./Login", new { returnUrl });
            }

            await SetProfileClaimsAsync(
               user,
               GetClaimValue(info.Principal, ClaimTypes.GivenName, "given_name"),
               GetClaimValue(info.Principal, ClaimTypes.Surname, "family_name"));

            await _initialUserRoleService.EnsureRoleExistsAsync(role);
            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded) {
               ErrorMessage = ToErrorMessage(roleResult);
               return RedirectToPage("./Login", new { returnUrl });
            }
         }

         if (await _userManager.IsLockedOutAsync(user)) {
            _logger.LogWarning("Locked out user attempted to sign in with {Provider}.", info.LoginProvider);
            return RedirectToPage("./Lockout");
         }

         var loginResult = await _userManager.AddLoginAsync(user, info);
         if (!loginResult.Succeeded && !loginResult.Errors.Any(error => error.Code == "LoginAlreadyAssociated")) {
            ErrorMessage = ToErrorMessage(loginResult);
            return RedirectToPage("./Login", new { returnUrl });
         }

         await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
         await _signInManager.UpdateExternalAuthenticationTokensAsync(info);
         _logger.LogInformation("User linked and logged in with {Provider}.", info.LoginProvider);

         return await RedirectAfterExternalLoginAsync(returnUrl, user);
      }

      private async Task<IActionResult> RedirectAfterExternalLoginAsync(string returnUrl, LiteDB.Identity.Models.LiteDbUser user) {
         var homePath = user == null
            ? "/dashboard"
            : await _redirectService.GetHomePathByEmailAsync(await _userManager.GetEmailAsync(user));
         return LocalRedirect(_redirectService.NormalizeReturnUrl(returnUrl, homePath, Url.IsLocalUrl(returnUrl)));
      }

      private async Task SetProfileClaimsAsync(LiteDB.Identity.Models.LiteDbUser user, string firstName, string lastName) {
         if (!string.IsNullOrWhiteSpace(firstName)) {
            await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.GivenName, firstName.Trim()));
         }

         if (!string.IsNullOrWhiteSpace(lastName)) {
            await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Surname, lastName.Trim()));
         }
      }

      private static string GetClaimValue(ClaimsPrincipal principal, params string[] claimTypes) =>
         claimTypes
            .Select(type => principal.FindFirst(type)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

      private static string ToErrorMessage(IdentityResult result) =>
         string.Join(" ", result.Errors.Select(error => error.Description));
   }

}
