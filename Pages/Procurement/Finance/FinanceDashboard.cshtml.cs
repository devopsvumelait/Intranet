using Intranet.Models;
using Intranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Intranet.Pages.Procurement.Finance
{
    [Authorize(Roles = "Finance")]
    public class FinanceDashboardModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notify;

        public FinanceDashboardModel(AppDbContext context, NotificationService notify)
        {
            _context = context;
            _notify = notify;
        }

        public string FullName { get; set; } = "";

        // Key Metrics
        public int PresentDatedCount { get; set; }
        public int FutureDatedCount { get; set; }
        public int AwaitingInvoicesCount { get; set; }
        public int AuditReviewCount { get; set; }

        public List<Request> PendingPayments { get; set; } = new();
        public List<Notification> Notifications { get; set; } = new();

        private static DateTime GetSouthAfricanTime()
        {
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
        }

        public DateTime CurrentSaTime => GetSouthAfricanTime();

        public async Task OnGetAsync()
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(userIdString, out Guid userId))
                {
                    var currentUser = await _context.Users.FindAsync(userId);
                    FullName = currentUser != null ? $"{currentUser.FirstName} {currentUser.Surname}" : "Finance Officer";

                    // 1. Present Dated / Action Required: Standard cash awaiting payment + brand new approved items needing a PO upload
                    PresentDatedCount = await _context.Requests
                        .CountAsync(r => (r.PaymentTiming == "Present" && r.Status == "Awaiting_Payment") || r.Status == "PO_Upload");

                    // 2. Future Dated: Approved items scheduled for a future date (Standard workflow fallback)
                    FutureDatedCount = await _context.Requests
                        .CountAsync(r => r.PaymentTiming == "Future" && r.Status == "Awaiting_Payment");

                    // 3. NEW ADJUSTMENT: Count standard cash accounts awaiting invoices PLUS corporate PO tracks sitting with the Manager
                    AwaitingInvoicesCount = await _context.Requests
                        .CountAsync(r => r.Status == "Awaiting_Invoice" || (r.Status == "PO_Issued" && r.IsPoRequired));

                    // 4. Audit Review: Tracking closed files or items undergoing verification cycles
                    AuditReviewCount = await _context.Requests
                        .CountAsync(r => r.Status == "Awaiting_Verification" || r.Status == "Closed");

                    // Keep queue focused exclusively on items requiring immediate Finance interaction (Filters out already issued POs)
                    PendingPayments = await _context.Requests
                        .Include(r => r.Requester)
                        .Include(r => r.Quotes)
                        .Where(r => r.Status == "Awaiting_Payment" || r.Status == "PO_Upload")
                        .OrderBy(r => r.CreatedAt)
                        .ThenByDescending(r => r.TotalAmount)
                        .Take(10)
                        .ToListAsync();

                    // Unread Notifications
                    Notifications = await _context.Notifications
                        .Where(n => n.UserId == userId && !n.IsRead)
                        .OrderByDescending(n => n.CreatedAt)
                        .ToListAsync();
                }
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An error occurred while loading the control metrics.");
            }
        }

        public async Task<IActionResult> OnPostMarkAllReadAsync()
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(userIdString, out Guid userId))
                {
                    await _notify.MarkAllAsReadAsync(userId);
                }
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An error occurred while clearing alerts.");
            }
            return RedirectToPage();
        }
    }
}