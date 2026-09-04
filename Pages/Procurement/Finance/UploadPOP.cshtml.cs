using Intranet.Models;
using Intranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Intranet.Pages.Procurement.Finance
{
    [Authorize(Roles = "Finance")]
    public class UploadPOP : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IAzureBlobService _blobService; 
        private readonly NotificationService _notify;
        private readonly GeminiAgentService _gemini;

        public UploadPOP(AppDbContext context, IAzureBlobService blobService, NotificationService notify, GeminiAgentService gemini)
        {
            _context = context;
            _blobService = blobService;
            _notify = notify;
            _gemini = gemini;
        }

        private static DateTime GetSouthAfricanTime()
        {
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
        }

        [BindProperty]
        [ValidateNever]
        public Request RequestData { get; set; } = null!;

        [BindProperty] public IFormFile PopFile { get; set; } = null!;
        [BindProperty] public string ReferenceNumber { get; set; } = "";
        [BindProperty] public string PaymentMethod { get; set; } = "EFT";

        [BindProperty] public string FinanceComment { get; set; } = "";


        public List<Document> SupportingDocuments { get; set; } = new();

        private async Task LoadRequestDataAsync(int id)
        {
            RequestData = await _context.Requests
                .Include(r => r.Requester)
                .Include(r => r.Quotes)
                .Include(r => r.Approvals).ThenInclude(a => a.Approver)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            await LoadRequestDataAsync(id);
            if (RequestData == null) return NotFound();

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

            SupportingDocuments = await _context.Documents
                .Where(d => d.RequestId == id && d.DocType == "Supporting_Document")
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            if (SupportingDocuments != null)
            {
                foreach (var supDoc in SupportingDocuments)
                {
                    if (!string.IsNullOrEmpty(supDoc.BlobUrl))
                    {
                        try
                        {
                            if (Uri.IsWellFormedUriString(supDoc.BlobUrl, UriKind.Absolute))
                            {
                                var uri = new Uri(supDoc.BlobUrl);
                                string fileName = Path.GetFileName(uri.LocalPath);
                                supDoc.BlobUrl = _blobService.GetReadSasUrl("supporting", fileName);
                            }
                            else
                            {
                                supDoc.BlobUrl = _blobService.GetReadSasUrl("supporting", supDoc.BlobUrl);
                            }
                        }
                        catch
                        {
                            supDoc.BlobUrl = "#";
                        }
                    }

                    supDoc.FileName = GetCleanFileName(supDoc.FileName);
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            await LoadRequestDataAsync(id);
            if (RequestData == null) return NotFound();

            if (PopFile == null || string.IsNullOrWhiteSpace(ReferenceNumber))
            {
                ModelState.AddModelError("", "Please provide both the document file and the Reference Number.");
                await ReloadSupportingDocsAsync(id);
                return Page();
            }

            try
            {
                var req = await _context.Requests
                    .Include(r => r.Quotes)
                    .FirstOrDefaultAsync(m => m.Id == id);
                if (req == null) return NotFound();

                var selectedQuote = req.Quotes.FirstOrDefault(q => q.IsSelected);
                string approvedSupplier = selectedQuote?.SupplierName ?? "";

                // Logic Flags
                bool isPurchaseOrder = req.Status == "PO_Upload" || req.IsPoRequired;
                bool isSpecial = RequestData.RequestType == "Waybill" || RequestData.RequestType == "ONLINE";

                // AI Verification
                ComplianceResult aiResult = await _gemini.VerifyPopAsync(PopFile, ReferenceNumber, approvedSupplier, req.RequestType, isPurchaseOrder, isSpecial);

                if (!aiResult.IsValid)
                {
                    ModelState.AddModelError("", $"AI Verification Failed: {aiResult.ComparisonSummary}");
                    await ReloadSupportingDocsAsync(id);
                    return Page();
                }

                // File Processing & Azure Blob Storage Upload
                var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                string fileExt = Path.GetExtension(PopFile.FileName);
                string docPrefix = isPurchaseOrder ? "Purchase_Order" : "POP";
                string containerName = isPurchaseOrder ? "pos" : "pops";
                string fileName = $"{(isPurchaseOrder ? "PO" : "POP")}_{req.Id}_{Guid.NewGuid().ToString()[..6]}{fileExt}";

                string blobUrl;
                using (var stream = PopFile.OpenReadStream())
                {
                    blobUrl = await _blobService.UploadFileAsync(containerName, fileName, stream, PopFile.ContentType);
                }

                // Database Persistence
                if (!isPurchaseOrder)
                {
                    _context.Payments.Add(new Payment
                    {
                        RequestId = req.Id,
                        PaidById = currentUserId,
                        PaymentDate = GetSouthAfricanTime(),
                        AmountPaid = req.TotalAmount,
                        PaymentMethod = PaymentMethod,
                        ReferenceNumber = ReferenceNumber,
                        PopBlobUrl = blobUrl,
                        Status = "Completed",
                        Comments = FinanceComment
                    });
                }

                _context.Documents.Add(new Document
                {
                    RequestId = req.Id,
                    FileName = fileName,
                    BlobUrl = blobUrl,
                    DocType = docPrefix,
                    UploadedById = currentUserId,
                    UploadedAt = GetSouthAfricanTime()
                });

                // Status Routing Logic
                if (isPurchaseOrder)
                {
                    req.Status = "PO_Issued";
                }
                else if (isSpecial)
                {
                    req.Status = "Awaiting_Manager_Closure";
                }
                else
                {
                    req.Status = "Awaiting_Invoice";
                }

                req.UpdatedAt = GetSouthAfricanTime();

                _context.AuditLogs.Add(new AuditLog
                {
                    TableName = "Requests",
                    RecordId = req.Id.ToString(),
                    ActionBy = currentUserId,
                    ActionType = isPurchaseOrder ? "PO_UPLOAD" : "POP_UPLOAD",
                    NewValues = $"AI Verified {docPrefix}. Ref: {ReferenceNumber}. Notes: {aiResult.ComparisonSummary}"
                });

                await _context.SaveChangesAsync();

                // Notification
                try
                {
                    string msg = isPurchaseOrder
                        ? $"Purchase Order generated for Request #{req.Id}. Reference: {ReferenceNumber}."
                        : $"Payment released for Request #{req.Id}. Reference: {ReferenceNumber}.";

                    await _notify.NotifyUserAsync(req.RequesterId, msg, req.Id, isPurchaseOrder ? "PO Uploaded" : "POP Uploaded");
                }
                catch (Exception notifyEx)
                {
                    System.Diagnostics.Debug.WriteLine($"WARNING: Notification email failed to send: {notifyEx.Message}");
                }

                return RedirectToPage("./PaymentQueue");
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "A system error occurred while processing the AI verification.");
                await ReloadSupportingDocsAsync(id);
                return Page();
            }
        }
        private async Task ReloadSupportingDocsAsync(int id)
        {
            SupportingDocuments = await _context.Documents
                .Where(d => d.RequestId == id && d.DocType == "Supporting_Document")
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            if (SupportingDocuments != null)
            {
                foreach (var supDoc in SupportingDocuments)
                {
                    if (!string.IsNullOrEmpty(supDoc.BlobUrl))
                    {
                        try
                        {
                            if (Uri.IsWellFormedUriString(supDoc.BlobUrl, UriKind.Absolute))
                            {
                                var uri = new Uri(supDoc.BlobUrl);
                                string fileName = Path.GetFileName(uri.LocalPath);
                                supDoc.BlobUrl = _blobService.GetReadSasUrl("supporting", fileName);
                            }
                            else
                            {
                                supDoc.BlobUrl = _blobService.GetReadSasUrl("supporting", supDoc.BlobUrl);
                            }
                        }
                        catch
                        {
                            supDoc.BlobUrl = "#";
                        }
                    }
                    supDoc.FileName = GetCleanFileName(supDoc.FileName);
                }
            }
        }

        private string GetCleanFileName(string storedFileName)
        {
            if (string.IsNullOrEmpty(storedFileName)) return "Supporting_Document";
            var guidRegex = new Regex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}[-_]?");
            var cleaned = guidRegex.Replace(storedFileName, string.Empty);
            return string.IsNullOrEmpty(cleaned) ? storedFileName : cleaned;
        }
    }
}