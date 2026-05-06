using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using LiteDB.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SignFabric.Identity.Pages.Account.Manage
{
    public partial class AddEmailModel : PageModel
    {
      private readonly SignInManager<LiteDB.Identity.Models.LiteDbUser> _signInManager;
      private readonly UserManager<LiteDB.Identity.Models.LiteDbUser> _userManager;

      public AddEmailModel(
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
            [EmailAddress]
            [Display(Name = "E-Mail Address")]
            public string Email { get; set; }

      }

        private async Task LoadAsync(LiteDbUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var email ="";

            if(user.EmailConfirmed != false) {
               email = await _userManager.GetEmailAsync(user);
            }

            Username = userName;

            Input = new InputModel
            {
                Email = email
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
                return Page();
            }

            var email = await _userManager.GetEmailAsync(user);

            if (Input.Email != email)
            {
               var setUserNameResult = await _userManager.SetUserNameAsync(user, Input.Email);

               if (!setUserNameResult.Succeeded) {
                  StatusMessage = "E-Mail address has been already added. Use another e-mail address.";
                  return RedirectToPage();
               }

               var setEmailResult = await _userManager.SetEmailAsync(user, Input.Email);

               var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
               var confirmed = await _userManager.ConfirmEmailAsync(user, token);

               if (!setEmailResult.Succeeded)
                   {
                     StatusMessage = "Unexpected error when trying to set e-mail address.";
                       return RedirectToPage();
                   }
               }

               await _signInManager.RefreshSignInAsync(user);
               StatusMessage = "Your profile has been updated";
               return RedirectToPage("./Index");
           }
    }
}
