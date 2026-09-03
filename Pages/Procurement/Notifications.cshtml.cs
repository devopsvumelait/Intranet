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

        public async Task<IActionResult> OnGetNavigateAsync(int notificationId)
        {
            var notif = await _context.Notifications.FindAsync(notificationId);
            if (notif == null)
            {
                return RedirectToPage("/Procurement/Notifications");
            }

            // Always mark notification as read upon clicking
            if (!notif.IsRead)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }

            if (!notif.RequestId.HasValue)
            {
                return RedirectToPage("/Procurement/Notifications");
            }

            // Retrieve live request status from DB to ensure action availability
            var request = await _context.Requests.FirstOrDefaultAsync(r => r.Id == notif.RequestId.Value);
            if (request == null)
            {
                TempData["ErrorMessage"] = "The referenced procurement request no longer exists.";
                return RedirectToPage("/Procurement/Notifications");
            }

            string status = request.Status ?? string.Empty;

            // ==========================================
            // 0. TERMINAL / FINISHED STATUS SAFETY CHECK
            // ==========================================
            if (status == "Completed" || status == "Closed" || status == "Cancelled" || status == "Rejected" || status == "Rejected_Acknowledged")
            {
                // If Finance clicks a finished request, send them to Audit view
                if (User.IsInRole("Finance"))
                {
                    TempData["StatusBubble"] = $"Request #{request.Id} is finalized ({status.Replace("_", " ")}).";
                    return RedirectToPage("/Procurement/Finance/Audit", new { searchId = request.Id });
                }

                // If a Manager clicks their own finished request, send them to Details to view it safely
                if (User.IsInRole("Manager"))
                {
                    TempData["StatusBubble"] = $"Request #{request.Id} is finalized ({status.Replace("_", " ")}).";
                    return RedirectToPage("/Procurement/Manager/Details", new { id = request.Id });
                }

                // For other roles, give a clean message and send back or to standard view
                TempData["StatusBubble"] = $"Request #{request.Id} has been finalized ({status.Replace("_", " ")}).";
                return RedirectToPage("/Procurement/Notifications");
            }

            // ==========================================
            // 1. STATUS-BASED ROUTING (Multi-Role Friendly)
            // ==========================================

            // --- HOO Approval Stage ---
            if (status == "Pending_HOO")
            {
                if (User.IsInRole("HOO"))
                {
                    return RedirectToPage("/Procurement/Approvals/Review", new { id = request.Id });
                }
            }

            // --- HOS Approval Stage ---
            if (status == "Pending_HOS")
            {
                if (User.IsInRole("HOS"))
                {
                    return RedirectToPage("/Procurement/Approvals/Review", new { id = request.Id });
                }
            }

            // --- Executive Approval Stage ---
            if (status == "Pending_Executive")
            {
                if (User.IsInRole("Executive"))
                {
                    return RedirectToPage("/Procurement/Approvals/Review", new { id = request.Id });
                }
            }

            // --- MD Approval Stage ---
            if (status == "Pending_MD")
            {
                if (User.IsInRole("MD"))
                {
                    return RedirectToPage("/Procurement/Approvals/Review", new { id = request.Id });
                }
            }

            // --- Finance Payment / POP Upload Stages ---
            if (status == "PO_Payment_Queue" || status == "Awaiting_Payment" || status == "PO_Upload" || status == "PO_Issued")
            {
                if (User.IsInRole("Finance"))
                {
                    return RedirectToPage("/Procurement/Finance/UploadPOP", new { id = request.Id });
                }
            }

            // --- Finance Verification Stage ---
            if (status == "Awaiting_Verification")
            {
                if (User.IsInRole("Finance"))
                {
                    return RedirectToPage("/Procurement/Finance/VerifyClosure", new { id = request.Id });
                }
                if (User.IsInRole("Manager"))
                {
                    return RedirectToPage("/Procurement/Manager/Details", new { id = request.Id });
                }
            }

            // --- Manager Invoice Upload Stages ---
            if (status == "Awaiting_Invoice" || status == "Resubmit_Invoice")
            {
                if (User.IsInRole("Manager"))
                {
                    return RedirectToPage("/Procurement/Manager/UploadInvoice", new { id = request.Id });
                }
            }

            // ==========================================
            // 2. GENERAL ROLE FALLBACKS (For active tracking/details)
            // ==========================================
            if (User.IsInRole("Manager"))
            {
                return RedirectToPage("/Procurement/Manager/Details", new { id = request.Id });
            }

            if (User.IsInRole("Finance"))
            {
                return RedirectToPage("/Procurement/Finance/PaymentQueue");
            }

            return RedirectToPage("/Procurement/Notifications");
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
        public async Task<IActionResult> OnPostMarkSingleReadAsync(int notificationId)
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