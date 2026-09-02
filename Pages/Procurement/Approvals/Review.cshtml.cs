using Intranet.Models;
using Intranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Intranet.Pages.Procurement.Approvals
{
    public class ReviewModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notify;
        private readonly IAzureBlobService _blobService;
        private readonly ILogger<ReviewModel> _logger;

        public ReviewModel(AppDbContext context, NotificationService notify, IAzureBlobService blobService,ILogger<ReviewModel> logger)
        {
            _context = context;
            _notify = notify;
            _blobService = blobService;
            _logger = logger;
        }

        [BindProperty] public Request Request { get; set; } = null!;
        [BindProperty] public string DecisionComments { get; set; } = "";

        public List<Document> SupportingDocuments { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                Request = await _context.Requests
                    .Include(r => r.Requester)
                    .Include(r => r.Quotes)
                    .FirstOrDefaultAsync(m => m.Id == id.Value);

                if (Request == null) return NotFound();

                foreach (var quote in Request.Quotes)
                {
                    if (!string.IsNullOrEmpty(quote.BlobUrl))
                    {
                        try
                        {
                            if (Uri.IsWellFormedUriString(quote.BlobUrl, UriKind.Absolute))
                            {
                                var uri = new Uri(quote.BlobUrl);
                                string fileName = Path.GetFileName(uri.LocalPath);
                                quote.BlobUrl = _blobService.GetReadSasUrl("quotes", fileName);
                            }
                            else
                            {
                                quote.BlobUrl = _blobService.GetReadSasUrl("quotes", quote.BlobUrl);
                            }
                        }
                        catch
                        {
                            quote.BlobUrl = "#";
                        }
                    }
                }
                SupportingDocuments = await _context.Documents
                    .Where(d => d.RequestId == id.Value && d.DocType == "Supporting_Document")
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

                        // Clean up the tracking GUID prefix for clean UI presentation
                        supDoc.FileName = GetCleanFileName(supDoc.FileName);
                    }
                }

            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An error occurred while fetching the request.");
            }
            return Page();
        }

        private static DateTime GetSouthAfricanTime()
        {
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
        }

        public async Task<IActionResult> OnPostDecisionAsync(int? id, bool isApproved)
        {
            if (id == null) return NotFound();

            try
            {
                var req = await _context.Requests
                    .Include(r => r.Requester)
                    .FirstOrDefaultAsync(r => r.Id == id.Value);

                if (req == null) return NotFound();

                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
                var currentUserId = Guid.Parse(userIdString);

                // Determine the correct database stage identifier based on who is reviewing
                string evaluatingStage = User.IsInRole("MD") ? "MD" : "HOO";

                string nextNotificationTarget = string.Empty;
                string notificationMessage = string.Empty;
                Guid? userNotificationTarget = null;
                string userNotificationMessage = string.Empty;
                string userNotificationTitle = "Request Approved";

                if (isApproved)
                {
                    // Escalation Logic remains intact
                    if (req.TotalAmount > 15000 && req.Status == "Pending_HOO")
                    {
                        req.Status = "Pending_MD";

                        nextNotificationTarget = "MD";
                        notificationMessage = $"HOO approved Request #{req.Id}. Final MD authorization required.";
                    }
                    else
                    {

                        if (req.IsPoRequired)
                        {
                            req.Status = "PO_Upload";

                            nextNotificationTarget = "Finance";
                            notificationMessage = $"Request #{req.Id} approved. Please generate and upload the Purchase Order (PO).";
                            userNotificationTarget = req.RequesterId;
                            userNotificationMessage = $"Request #{req.Id} approved by Head. Finance is now compiling the official PO.";
                            userNotificationTitle = "Request Approved (PO Pending)";
                        }
                        else
                        {
                            // Original fallback behavior for standard non-PO transactions
                            req.Status = "Awaiting_Payment";

                            nextNotificationTarget = "Finance";
                            notificationMessage = $"Request #{req.Id} approved. Please upload Proof of Payment (POP).";
                            userNotificationTarget = req.RequesterId;
                            userNotificationMessage = $"Request #{req.Id} has been approved. Finance is processing payment.";
                        }
                    }
                }
                else
                {
                    req.Status = "Rejected";
                    userNotificationTarget = req.RequesterId;
                    userNotificationMessage = $"Request #{req.Id} was rejected. Reason: {DecisionComments}";
                    userNotificationTitle = "Request Rejected";
                }

                _context.Entry(req).Property(r => r.Status).IsModified = true;

                _context.Approvals.Add(new Approval
                {
                    RequestId = req.Id,
                    ApproverId = currentUserId,
                    IsApproved = isApproved,
                    Comments = DecisionComments,
                    DecisionDate = GetSouthAfricanTime(),
                    Stage = evaluatingStage
                });

                await _context.SaveChangesAsync();

                try
                {
                    if (!string.IsNullOrEmpty(nextNotificationTarget))
                    {
                        await _notify.NotifyApproversAsync(req.Id, nextNotificationTarget, notificationMessage);
                    }

                    if (userNotificationTarget.HasValue)
                    {
                        await _notify.NotifyUserAsync(userNotificationTarget.Value, userNotificationMessage, req.Id, userNotificationTitle);
                    }
                }
                catch (Exception notifyEx)
                {
                    _logger.LogError(notifyEx, "WARNING: Notification email failed to send for Request #{RequestId}", req.Id);
                }

                return RedirectToPage("./Dashboard");
            }
            catch (Exception ex)
            {
                Request = await LoadRequestDataAsync(id.Value);
                string detailedError = ex.InnerException != null ? $"{ex.Message} --> Inner: {ex.InnerException.Message}" : ex.Message;
                ModelState.AddModelError("", $"Error saving decision: {detailedError}");
                return Page();
            }


        }

        private async Task<Request?> LoadRequestDataAsync(int id)
        {
            var req = await _context.Requests
                .Include(r => r.Requester)
                .Include(r => r.Quotes)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (req == null) return null;

            foreach (var quote in req.Quotes)
            {
                if (!string.IsNullOrEmpty(quote.BlobUrl))
                {
                    try
                    {
                        if (Uri.IsWellFormedUriString(quote.BlobUrl, UriKind.Absolute))
                        {
                            var uri = new Uri(quote.BlobUrl);
                            string fileName = Path.GetFileName(uri.LocalPath);
                            quote.BlobUrl = _blobService.GetReadSasUrl("quotes", fileName);
                        }
                        else
                        {
                            quote.BlobUrl = _blobService.GetReadSasUrl("quotes", quote.BlobUrl);
                        }
                    }
                    catch
                    {
                        quote.BlobUrl = "#";
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

            return req;
        }

        private string GetCleanFileName(string storedFileName)
        {
            if (string.IsNullOrEmpty(storedFileName)) return "Supporting_Document";

            // Matches standard 36-character GUIDs at the start of the string
            var guidRegex = new Regex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}[-_]?");
            var cleaned = guidRegex.Replace(storedFileName, string.Empty);
            return string.IsNullOrEmpty(cleaned) ? storedFileName : cleaned;
        }
    }
}