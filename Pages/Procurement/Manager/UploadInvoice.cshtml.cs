using Intranet.Models;
using Intranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Intranet.Pages.Procurement.Manager
{
    [Authorize(Roles = "Manager")]
    [EnableRateLimiting("AiValidationPolicy")]
    public class UploadInvoiceModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IAzureBlobService _blobService;
        private readonly NotificationService _notify;
        private readonly GeminiAgentService _ai;

        public UploadInvoiceModel(AppDbContext context, IAzureBlobService blobService, NotificationService notify, GeminiAgentService ai)
        {
            _context = context;
            _blobService = blobService;
            _notify = notify;
            _ai = ai;
        }

        private static DateTime GetSouthAfricanTime()
        {
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
        }
        [BindProperty] public Request RequestData { get; set; } = null!;
        [BindProperty] public IFormFile InvoiceFile { get; set; } = null!;
        [BindProperty] public IFormFile? PopFile { get; set; }

        private async Task<Request?> FetchFullRequestAsync(int id)
        {
            return await _context.Requests
                .Include(r => r.Quotes.Where(q => q.IsSelected))
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                RequestData = await FetchFullRequestAsync(id);
                if (RequestData == null) return NotFound();
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An error occurred while loading the request.");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var req = await FetchFullRequestAsync(id);
            if (req == null || InvoiceFile == null) return NotFound();

            var selectedQuote = req.Quotes.FirstOrDefault(q => q.IsSelected);
            if (selectedQuote == null)
            {
                ModelState.AddModelError("", "No approved quote found for this request.");
                RequestData = req;
                return Page();
            }

            try
            {
                // Temporary local file path for Gemini analysis since it expects a local file
                var tempInvoicePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{Path.GetExtension(InvoiceFile.FileName)}");
                using (var stream = InvoiceFile.OpenReadStream())
                {
                    using var fs = new System.IO.FileStream(tempInvoicePath, FileMode.Create);
                    await stream.CopyToAsync(fs);
                }

                string? tempPopPath = null;
                if (PopFile != null)
                {
                    tempPopPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{Path.GetExtension(PopFile.FileName)}");
                    using var popStream = PopFile.OpenReadStream();
                    using var popFs = new System.IO.FileStream(tempPopPath, FileMode.Create);
                    await popStream.CopyToAsync(popFs);
                }

                // AI Agent compliance check
                var compliance = await _ai.VerifyComplianceAsync(selectedQuote, InvoiceFile, PopFile);

                // Cleanup temp files safely
                try { if (System.IO.File.Exists(tempInvoicePath)) System.IO.File.Delete(tempInvoicePath); } catch { }
                if (tempPopPath != null)
                {
                    try { if (System.IO.File.Exists(tempPopPath)) System.IO.File.Delete(tempPopPath); } catch { }
                }

                if (!compliance.IsValid)
                {
                    ModelState.AddModelError("", "The uploaded invoice document failed AI cross-verification matching checks.");
                    RequestData = req;
                    return Page();
                }

                // File saving pipeline to Azure Blob Storage 'invoices' container
                string fileName = $"{Guid.NewGuid()}{Path.GetExtension(InvoiceFile.FileName)}";
                string invoiceUrl;
                using (var stream = InvoiceFile.OpenReadStream())
                {
                    invoiceUrl = await _blobService.UploadFileAsync("invoices", fileName, stream, InvoiceFile.ContentType);
                }

                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

                _context.Documents.Add(new Document
                {
                    RequestId = id,
                    FileName = InvoiceFile.FileName,
                    BlobUrl = invoiceUrl,
                    DocType = "Invoice",
                    UploadedById = Guid.Parse(userIdString!),
                    UploadedAt = GetSouthAfricanTime()
                });

                // --- ADAPTIVE STATUS ROUTING ---
                if (req.IsPoRequired)
                {
                    req.Status = "PO_Payment_Queue";
                }
                else
                {
                    req.Status = "Awaiting_Verification";
                }

                req.UpdatedAt = GetSouthAfricanTime();
                await _context.SaveChangesAsync();

                // Custom notifications depending on workflow track
                try
                {
                    if (req.IsPoRequired)
                    {
                        await _notify.NotifyApproversAsync(id, "Finance", $"Invoice uploaded for corporate account PO Request #{id}. Added to monthly payment execution queue.");
                    }
                    else
                    {
                        await _notify.NotifyApproversAsync(id, "Finance", $"Verified Invoice uploaded for cash Request #{id}. Awaiting final compliance audit.");
                    }
                }
                catch (Exception notifyEx)
                {
                    System.Diagnostics.Debug.WriteLine($"WARNING: Notification email failed to send: {notifyEx.Message}");
                }

                return RedirectToPage("./MyRequests");
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "AI Service timed out or failed. Please ensure the PDF is clear and legible.");
                RequestData = req;
                return Page();
            }
        }
    }
}