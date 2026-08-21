using Intranet.Models;
using Intranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Intranet.Pages.Procurement.Finance
{
    [Authorize(Roles = "Finance")]
    public class AuditModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IAzureBlobService _blobService;
        public AuditModel(AppDbContext context, IAzureBlobService blobService)
        {
            _context = context;
            _blobService = blobService;
        }

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

        public List<Request> PaidRequests { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                var query = _context.Requests
        .Include(r => r.Requester)
        .Include(r => r.Quotes)
        .Include(r => r.Documents)
        .AsQueryable();

               /* PaidRequests = await _context.Requests
                    .Include(r => r.Quotes)
                    .Include(r => r.Documents)
                    .Where(r => r.Status == "Awaiting_Invoice" ||
                                r.Status == "PO_Issued" || 
                                r.Status == "Closed" ||
                                r.Status == "Awaiting_Verification" ||
                                r.Status == "Awaiting_Manager_Closure" ||
                                r.Status == "PO_Upload" ||
                                r.Status == "PO_Payment_Queue")
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync(); */


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

                        var pendingStatuses = new List<string> { "Awaiting_Payment", "Awaiting_Invoice", "Awaiting_Verification", "PO_Issued", "PO_Upload", "Awaiting_Manager_Closure", "Pending_HOO", "Pending_HOS", "Pending_MD" };
                        query = query.Where(r => pendingStatuses.Contains(r.Status));
                    }
                    else if (StatusFilter == "PO_Payment_Queue")
                    {
                        query = query.Where(r => r.Status == "PO_Payment_Queue");
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

                PaidRequests = await query
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync();

                foreach (var req in PaidRequests)
                {
                    // 1. Fix Winning Quote URL
                    var winningQuote = req.Quotes.FirstOrDefault(q => q.IsSelected);
                    if (winningQuote != null && !string.IsNullOrEmpty(winningQuote.BlobUrl))
                    {
                        try
                        {
                            var uri = new Uri(winningQuote.BlobUrl);
                            string fileName = Path.GetFileName(uri.LocalPath);
                            winningQuote.BlobUrl = _blobService.GetReadSasUrl("quotes", fileName);
                        }
                        catch { /* Fallback if malformed URI */ }
                    }

                    
                    foreach (var doc in req.Documents)
                    {
                        if (string.IsNullOrEmpty(doc.BlobUrl)) continue;

                        try
                        {
                            var uri = new Uri(doc.BlobUrl);
                            string fileName = Path.GetFileName(uri.LocalPath);

                            
                            if (doc.DocType == "Invoice")
                            {
                                doc.BlobUrl = _blobService.GetReadSasUrl("invoices", fileName);
                            }
                            else if (doc.DocType == "POP")
                            {
                                doc.BlobUrl = _blobService.GetReadSasUrl("pops", fileName);
                            }
                            
                        }
                        catch { /* Fallback if malformed URI */ }
                    }
                }

            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An unexpected fault trace occurred inside the audit log data streams.");
            }
        }
    }
}