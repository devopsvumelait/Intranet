using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Intranet.Services;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore; 
using Intranet.Models;               

namespace Intranet.Pages.Procurement
{
    public class NotificationsModel : PageModel
    {
        private readonly AppDbContext _context; 
        private readonly NotificationService _notify;

        public NotificationsModel(AppDbContext context, NotificationService notify)
        {
            _context = context;
            _notify = notify;
        }

        // Expose notifications list property to the UI template layer
        public List<Notification> Notifications { get; set; } = new();

        // Fetches your records when navigating to the page
        public async Task<IActionResult> OnGetAsync()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return RedirectToPage("/Account/Login");
            }

            Notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            foreach (var n in Notifications)
            {
                if (n.CreatedAt.HasValue)
                {
                    n.CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(n.CreatedAt.Value, saTimeZone);
                }
            }

            return Page();
        }

        // This handler will be called by the Navbar button
        public async Task<IActionResult> OnPostMarkAllReadAsync(string returnUrl)
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(userIdString, out Guid userId))
                {
                    // Call the central service
                    await _notify.MarkAllAsReadAsync(userId);
                }

                // Redirect back to the page the user was originally on
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred");
            }
            return RedirectToPage("/Procurement/Notifications"); // Redirect cleanly to fallback target
        }

        // Inline Single Item Update Handler Action
        public async Task<IActionResult> OnPostMarkSingleReadAsync(Guid notificationId)
        {
            var notif = await _context.Notifications.FindAsync(notificationId);
            if (notif != null)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}