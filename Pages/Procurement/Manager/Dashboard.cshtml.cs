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

namespace Intranet.Pages.Procurement.Manager
{
    [Authorize(Roles = "Manager")]
    public class DashboardModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notify;

        public DashboardModel(AppDbContext context, NotificationService notify)
        {
            _context = context;
            _notify = notify;
        }

        public string FullName { get; set; } = "";
        public int PendingCount { get; set; }
        public int RejectedCount { get; set; }


        public int InvoicesToUploadCount { get; set; }


        public int PosToReviewCount { get; set; }
        public int TotalActiveCount { get; set; }

        public List<Request> ActiveRequests { get; set; } = new();
        public List<AuditLog> RecentAlerts { get; set; } = new();
        public List<Notification> Notifications { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(userIdString, out Guid userId))
                {
                    var currentUser = await _context.Users.FindAsync(userId);
                    if (currentUser != null)
                    {
                        FullName = $"{currentUser.FirstName} {currentUser.Surname}";

                        var baseQuery = _context.Requests.Where(r => r.RequesterId == userId);

                        // Metric Calculations
                        PendingCount = await baseQuery.CountAsync(r => r.Status.Contains("Pending"));
                        RejectedCount = await baseQuery.CountAsync(r => r.Status == "Rejected");


                        InvoicesToUploadCount = await baseQuery.CountAsync(r => r.Status == "Awaiting_Invoice" || r.Status == "Resubmit_Invoice");


                        PosToReviewCount = await baseQuery.CountAsync(r => r.Status == "PO_Issued" && r.IsPoRequired);

                        TotalActiveCount = await baseQuery.CountAsync(r => r.Status != "Closed" && r.Status != "Rejected_Acknowledged");


                        ActiveRequests = await baseQuery
                        .Include(r => r.Quotes)
                        .Include(r => r.Documents) 
                        .Where(r => r.Status != "Closed" && r.Status != "Rejected_Acknowledged")
                        .OrderByDescending(r => r.CreatedAt)
                        .Take(5)
                        .ToListAsync();

                        // Fetch unread notifications for the sidebar
                        Notifications = await _context.Notifications
                            .Where(n => n.UserId == userId && !n.IsRead)
                            .OrderByDescending(n => n.CreatedAt)
                            .ToListAsync();

                        // Audit Logs (System Activity)
                        RecentAlerts = await _context.AuditLogs
                            .Where(a => a.ActionBy == userId)
                            .OrderByDescending(a => a.Timestamp)
                            .Take(5)
                            .ToListAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured while compiling dashboard analytics.");
            }
        }

        // ================= ACKNOWLEDGE REJECTION =================
        public async Task<IActionResult> OnPostAcknowledgeRejectedAsync(int id)
        {
            try
            {
                var request = await _context.Requests.FindAsync(id);

                if (request != null && request.Status == "Rejected")
                {
                    request.Status = "Rejected_Acknowledged";

                    _context.AuditLogs.Add(new AuditLog
                    {
                        TableName = "Requests",
                        RecordId = request.Id.ToString(),
                        ActionBy = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
                        ActionType = "UPDATE",
                        NewValues = "Manager acknowledged rejection. Workflow archived.",
                        Timestamp = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }

            return RedirectToPage();
        }


        public async Task<IActionResult> OnPostCloseRequestAsync(int id)
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdString, out Guid userId)) return Forbid();

                var request = await _context.Requests.FindAsync(id);
                if (request == null) return NotFound();

                // Security Isolation Check: Verify this belongs to the logged-in manager
                if (request.RequesterId != userId) return Forbid();

                // Safety Rule: Only allow direct button closure if it went through the PO track
                if (request.Status == "PO_Issued" && request.IsPoRequired)
                {
                    request.Status = "Closed";
                    request.UpdatedAt = DateTime.Now;

                    _context.AuditLogs.Add(new AuditLog
                    {
                        TableName = "Requests",
                        RecordId = request.Id.ToString(),
                        ActionBy = userId,
                        ActionType = "UPDATE",
                        NewValues = "Manager reviewed issued Purchase Order. Request officially finalized and closed.",
                        Timestamp = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while closing the procurement file.");
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