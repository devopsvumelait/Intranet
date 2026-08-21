using Intranet.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Intranet.Pages.Procurement.Approvals
{
    [Authorize(Roles = "HOO,HOS,MD")]
    public class PendingQueueModel : PageModel
    {
        private readonly AppDbContext _context;

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
        [BindProperty(SupportsGet = true)] public string? RequestTypeFilter { get; set; }

        [BindProperty(SupportsGet = true)] public string? QuoteTypeFilter { get; set; }


        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        public PendingQueueModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Request> PendingRequests { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                var query = _context.Requests
        .Include(r => r.Requester)
        .Include(r => r.Quotes)
        .AsQueryable();

                // 1. Identify which status this specific role needs to see
                string statusToLookFor = "";

                if (User.IsInRole("HOO")) statusToLookFor = "Pending_HOO";
                else if (User.IsInRole("HOS")) statusToLookFor = "Pending_HOS";
                else if (User.IsInRole("MD")) statusToLookFor = "Pending_MD";

                if (!string.IsNullOrEmpty(statusToLookFor))
                {
                    query = query.Where(r => r.Status == statusToLookFor);
                }
                else
                {
                    
                    AllRequests = new List<Request>();
                    return;
                }

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

                        var pendingStatuses = new List<string> { "Awaiting_Payment", "Awaiting_Invoice", "Awaiting_Verification", "PO_Issued", "Awaiting_Manager_Closure", "Pending_HOO", "Pending_HOS", "Pending_MD" };
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
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync();

            }


            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }
        }
    }
}