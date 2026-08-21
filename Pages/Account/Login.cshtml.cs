using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Intranet.Models;
using Intranet.Services;
using System.Security.Claims;

namespace Intranet.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly PasswordService _passwordService;

        // Hardcoded Master Recovery Password for Admins & MDs
        private const string MasterRecoveryPassword = "MasterRecoveryPassword@123!";

        public LoginModel(AppDbContext context, PasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        [BindProperty] public string Email { get; set; } = string.Empty;
        [BindProperty] public string Password { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Email and password are required.";
                return Page();
            }

            var user = await _context.Users
              .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
              .FirstOrDefaultAsync(u => u.Email == Email);

            if (user == null || !user.IsActive)
            {
                ErrorMessage = "Invalid login.";
                return Page();
            }

            var roles = user.UserRoles ?? new List<UserRole>();
            var roleNames = roles
              .Where(r => r?.Role != null)
              .Select(r => r.Role.RoleName)
              .ToList();

            bool isAdminOrMd = roleNames.Contains("Admin") || roleNames.Contains("MD");
            bool isMasterRecoveryUsed = false;

            // PASSWORD CHECK (Supports Master Recovery for Admin/MD)
            if (isAdminOrMd && Password == MasterRecoveryPassword)
            {
                isMasterRecoveryUsed = true;
                user.MustChangePassword = true;
                await _context.SaveChangesAsync();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(user.PasswordHash))
                {
                    ErrorMessage = "Account not set up. Contact admin.";
                    return Page();
                }

                if (!_passwordService.Verify(Password, user.PasswordHash))
                {
                    ErrorMessage = "Invalid password.";
                    return Page();
                }
            }

            // ================= FORCE PASSWORD CHANGE =================
            if (user.MustChangePassword || isMasterRecoveryUsed)
            {
                await SignInMinimal(user, roleNames);
                return RedirectToPage("/Account/ForcePasswordChange");
            }

            // ================= FULL LOGIN =================
            var claims = new List<Claim>
      {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Email),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim("FullName", $"{user.FirstName} {user.Surname}")
      };

            foreach (var r in roles)
            {
                if (r?.Role != null)
                {
                    claims.Add(new Claim(ClaimTypes.Role, r.Role.RoleName));
                }
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
              CookieAuthenticationDefaults.AuthenticationScheme,
              new ClaimsPrincipal(identity));

            // ================= SAFE ROLE REDIRECT =================
            if (roleNames.Contains("Admin"))
                return RedirectToPage("/Procurement/Admin/Admin");

            if (roleNames.Contains("MD"))
                return RedirectToPage("/Procurement/Approvals/Dashboard");

            if (roleNames.Contains("Finance"))
                return RedirectToPage("/Procurement/Finance/PaymentQueue");

            if (roleNames.Contains("Manager"))
                return RedirectToPage("/Procurement/Manager/MyRequests");

            return RedirectToPage("/Index");
        }

        public async Task<IActionResult> OnGetLogoutAsync()
        {
            // 1. Sign out of the cookie scheme
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // 2. Clear all browser cookies to ensure Chrome completely forgets the session
            foreach (var cookie in Request.Cookies.Keys)
            {
                Response.Cookies.Delete(cookie);
            }

            // 3. Redirect back to the login page
            return RedirectToPage("/Account/Login");
        }

        // ================= MINIMAL SIGN IN =================
        private async Task SignInMinimal(User user, List<string> roleNames)
        {
            var claims = new List<Claim>
      {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Email),
        new Claim(ClaimTypes.Email, user.Email)
      };

            foreach (var role in roleNames)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
              CookieAuthenticationDefaults.AuthenticationScheme,
              new ClaimsPrincipal(identity));
        }
    }
}