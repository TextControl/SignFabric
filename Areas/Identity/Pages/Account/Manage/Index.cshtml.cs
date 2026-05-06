using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using LiteDB.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SignFabric.Identity.Pages.Account.Manage
{
    public partial class IndexModel : PageModel
    {
      private readonly SignInManager<LiteDB.Identity.Models.LiteDbUser> _signInManager;
      private readonly UserManager<LiteDB.Identity.Models.LiteDbUser> _userManager;

      public IndexModel(
            UserManager<LiteDB.Identity.Models.LiteDbUser> userManager,
            SignInManager<LiteDB.Identity.Models.LiteDbUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string Username { get; set; }
        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Display(Name = "First Name")]
            public string FirstName { get; set; }

            [Display(Name = "Name")]
            public string LastName { get; set; }
        }

        private async Task LoadAsync(LiteDbUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);

            Username = userName;
            var claims = await _userManager.GetClaimsAsync(user);

            Input = new InputModel
            {
                FirstName = claims.FirstOrDefault(claim => claim.Type == ClaimTypes.GivenName)?.Value,
                LastName = claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Surname)?.Value
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                Input.FirstName = Request.Form["Input.FirstName"];
                Input.LastName = Request.Form["Input.LastName"];
                return Page();
            }

            await SetClaimAsync(user, ClaimTypes.GivenName, Input.FirstName);
            await SetClaimAsync(user, ClaimTypes.Surname, Input.LastName);

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Your profile has been updated";
            return RedirectToPage();
        }

        private async Task SetClaimAsync(LiteDbUser user, string claimType, string value) {
            var claims = await _userManager.GetClaimsAsync(user);
            var existing = claims.FirstOrDefault(claim => claim.Type == claimType);
            var normalizedValue = (value ?? string.Empty).Trim();

            if (existing != null) {
                await _userManager.RemoveClaimAsync(user, existing);
            }

            if (!string.IsNullOrWhiteSpace(normalizedValue)) {
                await _userManager.AddClaimAsync(user, new Claim(claimType, normalizedValue));
            }
        }
    }
}
