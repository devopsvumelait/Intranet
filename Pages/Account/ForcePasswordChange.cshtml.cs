using Intranet.Models;
using Intranet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;


namespace Intranet.Pages.Account
{
    [Authorize]
    public class ForcePasswordChangeModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly PasswordService _passwordService;

        public ForcePasswordChangeModel(AppDbContext context, PasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        [BindProperty]
        [Required(ErrorMessage = "New password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "The password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Please confirm your new password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return Page();
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
                return RedirectToPage("/Account/Login");

            var userId = Guid.Parse(userIdClaim);

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            // ✅ FIXED PASSWORD HASH
            user.PasswordHash = _passwordService.HashPassword(NewPassword);
            user.MustChangePassword = false;
            user.PasswordChangedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await HttpContext.SignOutAsync();

            return RedirectToPage("/Account/Login");
        }
    }
}