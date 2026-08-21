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

namespace Intranet.Pages.Procurement
{
    [Authorize(Roles = "Manager, Finance, MD, HOO, HOS")]
    public class ArchiveModel : PageModel
    {
        private readonly AppDbContext _context;
        public ArchiveModel(AppDbContext context) => _context = context;

        public List<Request> ArchivedRequests { get; set; } = new();

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

        public async Task OnGetAsync()
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdString, out Guid userId)) return;

                var query = _context.Requests
                    .Include(r => r.Requester)
                    .Include(r => r.Quotes)
                    .AsNoTracking()
                    .Where(r => r.Status == "Closed" || r.Status == "Rejected" || r.Status == "Cancelled" || r.Status == "Rejected_Acknowledged")
                    .AsQueryable();

                // Role-based security containment context checks
                if (User.IsInRole("Manager") && !User.IsInRole("Finance") && !User.IsInRole("MD") && !User.IsInRole("HOO") && !User.IsInRole("HOS"))
                {
                    query = query.Where(r => r.RequesterId == userId);
                }

                // Keyword Search Queries
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    string searchLower = SearchTerm.Trim().ToLower();
                    query = query.Where(r =>
                        r.Id.ToString().Contains(searchLower) ||
                        r.Description.ToLower().Contains(searchLower) ||
                        r.Requester.FirstName.ToLower().Contains(searchLower) ||
                        r.Requester.Surname.ToLower().Contains(searchLower) ||
                        r.CustomerName.ToLower().Contains(searchLower) ||
                        r.Quotes.Any(q => q.IsSelected && q.SupplierName.ToLower().Contains(searchLower)));
                }

                // Status Filter Selection Handling
                if (!string.IsNullOrEmpty(StatusFilter))
                {
                    // Intercepts 'Rejected' search parameters to safely return both standard and acknowledged rows
                    if (StatusFilter == "Rejected")
                    {
                        query = query.Where(r => r.Status == "Rejected" || r.Status == "Rejected_Acknowledged" || r.Status == "Cancelled");
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

                // Date Filter Pipelines
                if (StartDate.HasValue)
                {
                    query = query.Where(r => r.CreatedAt >= StartDate.Value);
                }
                if (EndDate.HasValue)
                {
                    var endOfRange = EndDate.Value.Date.AddDays(1);
                    query = query.Where(r => r.CreatedAt < endOfRange);
                }

                ArchivedRequests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An unexpected system fault occurred while fetching archived data pipelines.");
            }
        }
    }
}