using Intranet.Models;
using Intranet.Services;
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
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IAzureBlobService _blobService;
        public DetailsModel(AppDbContext context, IAzureBlobService blobService) 
        {
            _context = context;
            _blobService = blobService;
        }

        public Request RequestData { get; set; } = null!;
        public List<Approval> ApprovalSteps { get; set; } = new();
        public Document? ProofOfPayment { get; set; }
        public Document? GetInvoice { get; set; }

        public Document? GetPO { get; set; }
        public List<Document> SupersededInvoices { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                RequestData = await _context.Requests
                    .Include(r => r.Quotes)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (RequestData == null) return NotFound();

                if (string.IsNullOrEmpty(ReturnUrl))
                {
                    ReturnUrl = Request.Headers["Referer"].ToString();
                }

                ApprovalSteps = await _context.Approvals
                    .Where(a => a.RequestId == id)
                    .OrderByDescending(a => a.DecisionDate)
                    .ToListAsync();

                ProofOfPayment = await _context.Documents
                    .Where(d => d.RequestId == id && d.DocType == "POP")
                    .OrderByDescending(d => d.UploadedAt)
                    .FirstOrDefaultAsync();

                GetInvoice = await _context.Documents
                    .Where(d => d.RequestId == id && d.DocType == "Invoice")
                    .OrderByDescending(d => d.UploadedAt)
                    .FirstOrDefaultAsync();

                SupersededInvoices = await _context.Documents
                    .Where(d => d.RequestId == id && d.DocType == "Superseded_Invoice")
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync();

                GetPO = await _context.Documents
                    .Where(d => d.RequestId == id && (d.DocType == "PO" || d.DocType == "Purchase_Order"))
                    .OrderByDescending(d => d.UploadedAt)
                    .FirstOrDefaultAsync();

                if (RequestData.Quotes != null)
                {
                    foreach (var quote in RequestData.Quotes)
                    {
                        if (!string.IsNullOrEmpty(quote.BlobUrl))
                        {
                            var qUri = new Uri(quote.BlobUrl);
                            quote.BlobUrl = _blobService.GetReadSasUrl("quotes", Path.GetFileName(qUri.LocalPath));
                        }
                    }
                }

                if (ProofOfPayment != null && !string.IsNullOrEmpty(ProofOfPayment.BlobUrl))
                {
                    var popUri = new Uri(ProofOfPayment.BlobUrl);
                    ProofOfPayment.BlobUrl = _blobService.GetReadSasUrl("pops", Path.GetFileName(popUri.LocalPath));
                }

                
                if (GetInvoice != null && !string.IsNullOrEmpty(GetInvoice.BlobUrl))
                {
                    var invUri = new Uri(GetInvoice.BlobUrl);
                    GetInvoice.BlobUrl = _blobService.GetReadSasUrl("invoices", Path.GetFileName(invUri.LocalPath));
                }

                //  Superseded Invoices URLs
                if (SupersededInvoices != null)
                {
                    foreach (var supInv in SupersededInvoices)
                    {
                        if (!string.IsNullOrEmpty(supInv.BlobUrl))
                        {
                            var supUri = new Uri(supInv.BlobUrl);
                            supInv.BlobUrl = _blobService.GetReadSasUrl("invoices", Path.GetFileName(supUri.LocalPath));
                        }
                    }
                }

                // Purchase Order URL
                if (GetPO != null && !string.IsNullOrEmpty(GetPO.BlobUrl))
                {
                    var poUri = new Uri(GetPO.BlobUrl);
                    GetPO.BlobUrl = _blobService.GetReadSasUrl("pos", Path.GetFileName(poUri.LocalPath)); // Adjust container name if your PO container is named differently (e.g., "documents" or "pos")
                }

            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An error occurred while loading details.");
            }

            return Page();
        }

        // Handler allowing the manager to authorize sign-off execution pipelines
        public async Task<IActionResult> OnPostSignOffAsync(int id)
        {
            var request = await _context.Requests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(currentUserIdStr, out Guid currentUserId);

            // Access Control Enforcement
            if (request.RequesterId != currentUserId || request.Status != "PO_Issued")
            {
                return Forbid();
            }

            
            request.Status = "PO_Payment_Queue";

            // Append an audit log trace tracking transaction
            _context.Approvals.Add(new Approval
            {
                RequestId = id,
                Stage = "ManagerFinalSignOff",
                IsApproved = true,
                DecisionDate = DateTime.Now,
                Comments = "Manager confirmed delivery of services/goods. Request shifted to Finance Payment Queue.",

                
                ApproverId = currentUserId
            });

            await _context.SaveChangesAsync();
            return RedirectToPage("./Details", new { id, returnUrl = ReturnUrl });
        }

        public async Task<IActionResult> OnPostAcknowledgeRejectedAsync(int id)
        {
            var request = await _context.Requests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(currentUserIdStr, out Guid currentUserId);

            // Enforcement: Only owner can acknowledge, and only for specific types
            if (request.RequesterId != currentUserId || request.Status != "Rejected")
            {
                return Forbid();
            }

            // Transition to Closed
            request.Status = "Rejected_Acknowledged";

            // Audit log
            _context.Approvals.Add(new Approval
            {
                RequestId = id,
                Stage = "Manager_Closed",
                IsApproved = true,
                DecisionDate = DateTime.Now,
                Comments = "Manager acknowledged. Request archived.",
                ApproverId = currentUserId
            });

            await _context.SaveChangesAsync();
            return RedirectToPage("./Details", new { id });
        }

        public async Task<IActionResult> OnPostAcknowledgeAsync(int id)
        {
            var request = await _context.Requests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(currentUserIdStr, out Guid currentUserId);

            // Enforcement: Only owner can acknowledge, and only for specific types
            if (request.RequesterId != currentUserId ||
                (request.RequestType != "Waybill" && request.RequestType != "ONLINE"))
            {
                return Forbid();
            }

            // Transition to Closed
            request.Status = "Closed";

            // Audit log
            _context.Approvals.Add(new Approval
            {
                RequestId = id,
                Stage = "Manager_Closed",
                IsApproved = true,
                DecisionDate = DateTime.Now,
                Comments = "Manager acknowledged. Request archived.",
                ApproverId = currentUserId
            });

            await _context.SaveChangesAsync();
            return RedirectToPage("./Details", new { id });
        }
    }
}