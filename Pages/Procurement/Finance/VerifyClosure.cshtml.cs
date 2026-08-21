using Intranet.Models;
using Intranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Intranet.Pages.Procurement.Finance
{
    [Authorize(Roles = "Finance")]
    public class VerifyClosureModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notify;
        private readonly RegisterService _registerService;
        private readonly IAzureBlobService _blobService;

        public VerifyClosureModel(AppDbContext context, NotificationService notify, RegisterService registerService, IAzureBlobService blobService)
        {
            _context = context;
            _notify = notify;
            _registerService = registerService;
            _blobService = blobService;
        }

        public Request Request { get; set; } = null!;
        public Quote WinningQuote { get; set; } = null!;
        public Document FinalInvoice { get; set; } = null!;
        public Document ProofOfPayment { get; set; } = null!;

        
        public Payment PaymentRecord { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                Request = await _context.Requests
                    .Include(r => r.Requester)
                    .Include(r => r.Quotes)
                    .Include(r => r.Documents)
                    .Include(r => r.Payments)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (Request == null) return NotFound();

                WinningQuote = Request.Quotes.FirstOrDefault(q => q.IsSelected)
                               ?? new Quote { SupplierName = "N/A", Price = 0 };

                FinalInvoice = Request.Documents
                    .Where(d => d.DocType == "Invoice")
                    .OrderByDescending(d => d.UploadedAt)
                    .FirstOrDefault();

                ProofOfPayment = Request.Documents.FirstOrDefault(d => d.DocType == "POP");

                // Fetch the payment record associated with this request
                PaymentRecord = await _context.Payments
                    .FirstOrDefaultAsync(p => p.RequestId == id);

                // Fix Winning Quote URL
                if (!string.IsNullOrEmpty(WinningQuote.BlobUrl))
                {
                    var quoteUri = new Uri(WinningQuote.BlobUrl);
                    string quoteFileName = Path.GetFileName(quoteUri.LocalPath);
                    WinningQuote.BlobUrl = _blobService.GetReadSasUrl("quotes", quoteFileName);
                }

                // Final Invoice URL
                if (FinalInvoice != null && !string.IsNullOrEmpty(FinalInvoice.BlobUrl))
                {
                    var invoiceUri = new Uri(FinalInvoice.BlobUrl);
                    string invoiceFileName = Path.GetFileName(invoiceUri.LocalPath);
                    FinalInvoice.BlobUrl = _blobService.GetReadSasUrl("invoices", invoiceFileName);
                }

                // Proof of Payment URL
                if (ProofOfPayment != null && !string.IsNullOrEmpty(ProofOfPayment.BlobUrl))
                {
                    var popUri = new Uri(ProofOfPayment.BlobUrl);
                    string popFileName = Path.GetFileName(popUri.LocalPath);
                    ProofOfPayment.BlobUrl = _blobService.GetReadSasUrl("pops", popFileName);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCloseRequestAsync(int id)
        {
            try
            {
                var req = await _context.Requests.FindAsync(id);
                if (req == null) return NotFound();

                req.Status = "Closed";
                await _registerService.AddToMonthlyRegisterAsync(id);

                _context.AuditLogs.Add(new AuditLog
                {
                    TableName = "Requests",
                    RecordId = id.ToString(),
                    ActionBy = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
                    ActionType = "CLOSE",
                    NewValues = "Final Invoice verified and added to Monthly Register."
                });

                await _context.SaveChangesAsync();
                await _notify.NotifyUserAsync(req.RequesterId, $"Request #{id} closed and registered.", id, "Request Closed");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }

            return RedirectToPage("./PaymentQueue");
        }

        public async Task<IActionResult> OnPostRejectInvoiceAsync(int id, string reason)
        {
            try
            {
                var req = await _context.Requests.FindAsync(id);
                if (req == null) return NotFound();

                req.Status = "Resubmit_Invoice";
                req.RejectionReason = reason;

                _context.AuditLogs.Add(new AuditLog
                {
                    TableName = "Requests",
                    RecordId = id.ToString(),
                    ActionBy = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
                    ActionType = "REJECT_INVOICE",
                    NewValues = $"Status: Resubmit_Invoice. Reason: {reason}"
                });

                await _context.SaveChangesAsync();
                await _notify.NotifyUserAsync(req.RequesterId, $"Invoice Rejected: {reason}", id, "Action Required");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }

            return RedirectToPage("./PaymentQueue");
        }
    }
}