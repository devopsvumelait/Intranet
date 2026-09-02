using Intranet.Models;
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
    public class MyRequestsModel : PageModel
    {
        private readonly AppDbContext _context;
        public MyRequestsModel(AppDbContext context) => _context = context;

        public string UserFullName { get; set; }

        public List<Request> AllRequests { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DepartmentFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? CostTypeFilter { get; set; }
        [BindProperty(SupportsGet = true)]  public string? RequestTypeFilter { get; set; }

        [BindProperty(SupportsGet = true)] public string? QuoteTypeFilter { get; set; }


        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        private static DateTime GetSouthAfricanTime()
        {
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
        }
        public async Task OnGetAsync()
        {
            try
            {
                var userId1 = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var currentUser = await _context.Users.FindAsync(userId1);
                UserFullName = currentUser != null ? $"{currentUser.FirstName} {currentUser.Surname}" : "User";

                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdString, out Guid userId)) return;

                // Start with the base query for the current user
                var query = _context.Requests
                    .Include(r => r.Requester)
                    .Include(r => r.Quotes)
                    .AsNoTracking()
                    .Where(r => r.RequesterId == userId)
                    .AsQueryable();

                // Keyword Search
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    string searchLower = SearchTerm.Trim().ToLower();
                    query = query.Where(r =>
                        r.Id.ToString().Contains(searchLower) ||
                        r.Description.ToLower().Contains(searchLower) ||
                        r.Quotes.Any(q => q.IsSelected && q.SupplierName.ToLower().Contains(searchLower)) ||
                        r.CustomerName.ToLower().Contains(searchLower));

                }

                // Status Filter
                if (!string.IsNullOrEmpty(StatusFilter))
                {
                    if (StatusFilter == "Pending")
                    {
                        
                        var pendingStatuses = new List<string> { "Awaiting_Payment", "Awaiting_Invoice", "Awaiting_Verification", "PO_Issued" , "Awaiting_Manager_Closure", "Pending_HOO", "Pending_HOS", "Pending_MD" };
                        query = query.Where(r => pendingStatuses.Contains(r.Status));
                    }

                    else if (StatusFilter == "Rejected")
                    {
                        query = query.Where(r => r.Status == "Rejected" || r.Status == "Rejected_Acknowledged");
                    }
                    else
                    {
                        query = query.Where(r => r.Status == StatusFilter);
                    }
                }

                if (!string.IsNullOrEmpty(DepartmentFilter))
                {
                    query = query.Where(r => r.DepartmentType == DepartmentFilter);
                }

                if (!string.IsNullOrEmpty(RequestTypeFilter))
                {
                    query = query.Where(r => r.RequestType == RequestTypeFilter);
                }

                if (!string.IsNullOrEmpty(QuoteTypeFilter))
                {
                    query = query.Where(r => r.QuoteType == QuoteTypeFilter);
                }

                if (!string.IsNullOrEmpty(CostTypeFilter))
                {
                    query = query.Where(r => r.CostType == CostTypeFilter);
                }

                // Date Filters
                if (StartDate.HasValue)
                {
                    query = query.Where(r => r.CreatedAt >= StartDate.Value);
                }
                if (EndDate.HasValue)
                {
                    var endOfRange = EndDate.Value.Date.AddDays(1);
                    query = query.Where(r => r.CreatedAt < endOfRange);
                }

                AllRequests = await query
                    .OrderBy(r => r.Status == "Closed" || r.Status == "PO_Payment_Queue" || r.Status == "Rejected_Acknowledged" ? 1 : 0)
                    .ThenByDescending(r => r.CreatedAt)
                    .ToListAsync();

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }
        }

        public async Task<IActionResult> OnPostAcknowledgeRejectedAsync(int id)
        {
            try
            {
                var request = await _context.Requests.FindAsync(id);
                if (request != null && request.Status == "Rejected")
                {
                // Change the status to a new state that indicates it has been acknowledged
                request.Status = "Rejected_Acknowledged";
                request.UpdatedAt = GetSouthAfricanTime();
                await _context.SaveChangesAsync();
                }

                return RedirectToPage();

            }


            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
                return Page();
            }
            
        }
    }
}