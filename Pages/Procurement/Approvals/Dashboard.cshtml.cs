using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Intranet.Models;
using Intranet.Services;
using System.Security.Claims;

namespace Intranet.Pages.Procurement.Approvals
{
    [Authorize(Roles = "HOO,HOS,MD")]
    public class DashboardModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notify;

        public DashboardModel(AppDbContext context, NotificationService notify)
        {
            _context = context;
            _notify = notify;
        }

        public string UserFullName { get; set; }

        public List<Request> PendingQueue { get; set; } = new();
        public List<Notification> Notifications { get; set; } = new();

        public decimal TotalPendingValue { get; set; }
        public int ApprovedThisMonth { get; set; }
        public int RejectedThisMonth { get; set; } 
        public decimal TotalSpendThisMonth { get; set; }

        public string CurrentMonthYear { get; set; } = string.Empty;
        private static DateTime GetSouthAfricanTime()
        {
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
        }

        public async Task OnGetAsync()
        {
            try
            {
                CurrentMonthYear = GetSouthAfricanTime().ToString("MMMM yyyy");
                var userId1 = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var currentUser = await _context.Users.FindAsync(userId1);
                UserFullName = currentUser != null ? $"{currentUser.FirstName} {currentUser.Surname}" : "User";

                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdString, out Guid userId)) return;

                // 1. Fetch Personal Notifications
                Notifications = await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                // 2. Identify Role-Based Status
                string statusToLookFor = "";
                if (User.IsInRole("HOO")) statusToLookFor = "Pending_HOO";
                else if (User.IsInRole("HOS")) statusToLookFor = "Pending_HOS";
                else if (User.IsInRole("MD")) statusToLookFor = "Pending_MD";

                // 3. Fetch Personal Queue
                PendingQueue = await _context.Requests
                    .Include(r => r.Requester)
                    .Include(r => r.Quotes)
                    .Where(r => r.Status == statusToLookFor)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                // 4. Calculate Analytics
                TotalPendingValue = PendingQueue.Sum(r => r.TotalAmount);
                var localNow = GetSouthAfricanTime();
                var firstDayOfMonth = new DateTime(localNow.Year, localNow.Month, 1);

                // Fetch all decisions (Approved AND Rejected) for the current month by this user
                var monthlyDecisions = await _context.Approvals
                    .Include(a => a.Request)
                    .Where(a => a.ApproverId == userId && a.DecisionDate >= firstDayOfMonth)
                    .ToListAsync();

                // Filter for Approved
                var approved = monthlyDecisions.Where(a => a.IsApproved).ToList();
                ApprovedThisMonth = approved.Count;
                TotalSpendThisMonth = approved.Sum(a => a.Request.TotalAmount);

                // Filter for Rejected
                RejectedThisMonth = monthlyDecisions.Count(a => !a.IsApproved);

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }
        }

        public async Task<IActionResult> OnPostAcknowledgeAsync(int noteId)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(noteId);
                if (notification != null)
                {
                    notification.IsRead = true;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }
            return RedirectToPage();
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
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }
            return RedirectToPage();
        }
    }
}