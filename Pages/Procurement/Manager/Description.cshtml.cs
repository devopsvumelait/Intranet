using Intranet.Models;
using Intranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace Intranet.Pages.Procurement.Manager
{
    [Authorize(Roles = "Manager")]
    public class DescriptionModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notify;

        public DescriptionModel(AppDbContext context, NotificationService notify)
        {
            _context = context;
            _notify = notify;
        }

        [BindProperty]
        public RequestInputModel Input { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int SelectedQuoteId { get; set; }

        [BindProperty]
        public decimal HiddenTotalAmount { get; set; }

        [BindProperty]
        public string RequestType { get; set; } = "Normal";

        [BindProperty]
        public string BlobUrl { get; set; } = string.Empty;

        [BindProperty]
        public string? SerializedSupportingDocs { get; set; }

        public List<string> DepartmentOptions { get; set; } = new() { "EUC", "Networks", "Cabling", "ADHOC", "Head Office", "Interns" };
        public List<string> QuoteTypeOptions { get; set; } = new() { "Accomodation", "Courier", "Flights", "Fuel", "Health And Safety", "Legal Fees", "Medicals", "Networking Expense", "PPE", "S&T", "Security Clearance", "Small Assets", "Staff Welfare", "Team Builds", "Telephone And Internet", "Tool Hire", "Training", "Vehicle Expense", "Vehicle Hire", "Office Expenses", "Materials" };
        public string SuggestedSupplierName { get; set; } = string.Empty;

        public class RequestInputModel
        {
            [Required(ErrorMessage = "A clear business description is required.")]
            [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
            public string Description { get; set; } = string.Empty;

            [Required]
            public string CostType { get; set; } = "Projects";

            [Required]
            public string PaymentTiming { get; set; } = "Immediate";

          
            public DateTime? FutureDate { get; set; }

            [Required(ErrorMessage = "Supplier Name must be confirmed before saving.")]
            [StringLength(100, ErrorMessage = "Supplier name cannot exceed 100 characters.")]
            public string SupplierName { get; set; } = string.Empty;

            public string DepartmentType { get; set; } = "None";
            public string CustomerName { get; set; } = "None";
            public string QuoteType { get; set; } = "None";
        }

        private static DateTime GetSouthAfricanTime()
        {
            // "South Africa Standard Time" is the standard Windows and IANA time zone ID for SAST
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
        }

        public async Task<IActionResult> OnGetAsync(string? description, decimal? amount, string? costType, string? timing, string? blobUrl, string? futureDate, string? departmentType, string? customerName, string? quoteType)
        {
            if (amount.HasValue && amount.Value > 0)
            {
                HiddenTotalAmount = amount.Value;
            }
            else if (TempData["HiddenAmountValue"] is string amountStr &&
                     decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedAmt))
            {
                HiddenTotalAmount = parsedAmt;
            }

            if (string.IsNullOrEmpty(RequestType))
            {
                RequestType = TempData["SelectedRequestType"]?.ToString() ?? "Normal";
            }

            if (string.IsNullOrEmpty(SerializedSupportingDocs))
            {
                SerializedSupportingDocs = TempData["SerializedSupportingDocs"]?.ToString() ?? string.Empty;
            }

            RequestType = TempData["SelectedRequestType"]?.ToString() ?? "Normal";
            TempData["HiddenAmountValue"] = HiddenTotalAmount.ToString("F2", CultureInfo.InvariantCulture);
            TempData.Keep("HiddenAmountValue");
            TempData.Keep("SerializedDraftQuotes");
            TempData.Keep("SerializedSupportingDocs");
            TempData.Keep("SelectedRequestType");

            // 1. Prioritize URL parameter, then TempData, then default to "None"
            Input.DepartmentType = departmentType ?? TempData["DepartmentType"]?.ToString() ?? "None";
            Input.CustomerName = customerName ?? TempData["CustomerName"]?.ToString() ?? "None";
            Input.QuoteType = quoteType ?? TempData["QuoteType"]?.ToString() ?? "None";

            // 2. Refresh TempData so it persists for the POST action
            TempData["DepartmentType"] = Input.DepartmentType;
            TempData["CustomerName"] = Input.CustomerName;
            TempData["QuoteType"] = Input.QuoteType;
            TempData.Keep("DepartmentType");
            TempData.Keep("CustomerName");
            TempData.Keep("QuoteType");

            if (!string.IsNullOrEmpty(blobUrl))
            {
                BlobUrl = blobUrl;
            }

            bool hasDbQuote = SelectedQuoteId > 0;
            bool hasUrlTrack = !string.IsNullOrEmpty(BlobUrl);
            bool hasCachedDrafts = TempData.Peek("SerializedDraftQuotes") is string draftJsonCheck && !string.IsNullOrEmpty(draftJsonCheck);

            if (!hasDbQuote && !hasUrlTrack && !hasCachedDrafts)
            {
                return RedirectToPage("./Create");
            }

            if (string.IsNullOrEmpty(Input.SupplierName))
            {
                Input.Description = description ?? string.Empty;
                Input.CostType = costType == "BAU" ? "BAU" : (costType == "Health And Safety" ? "Health And Safety" : "Projects");
                Input.PaymentTiming = (timing == "Immediate" || timing == "Present" || timing == "Present Dated") ? "Immediate" : "Future Dated";

                // Parse incoming future date parameters safely
                if (!string.IsNullOrEmpty(futureDate) && DateTime.TryParseExact(futureDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    Input.FutureDate = parsedDate;
                }

                if (SelectedQuoteId > 0)
                {
                    var q = await _context.Quotes.FindAsync(SelectedQuoteId);
                    if (q != null) SuggestedSupplierName = q.SupplierName;
                }
                else if (TempData["SerializedDraftQuotes"] is string draftJson)
                {
                    try
                    {
                        var drafts = JsonSerializer.Deserialize<List<Quote>>(draftJson);
                        var activeDraft = !string.IsNullOrEmpty(BlobUrl)
                            ? drafts?.Find(q => q.BlobUrl == blobUrl)
                            : drafts?.FirstOrDefault();

                        if (activeDraft != null)
                        {
                            SuggestedSupplierName = activeDraft.SupplierName;
                            if (string.IsNullOrEmpty(BlobUrl)) BlobUrl = activeDraft.BlobUrl;
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"AI Draft Parsing Error: {jsonEx.Message}");
                    }
                }

                Input.SupplierName = string.Empty;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveRequestAsync()
        {
            TempData.Keep("SerializedDraftQuotes");
            TempData.Keep("SerializedSupportingDocs");
            TempData.Keep("HiddenAmountValue");
            TempData.Keep("SelectedRequestType");
            TempData.Keep("DepartmentType");
            TempData.Keep("CustomerName");
            TempData.Keep("QuoteType");

            if (string.IsNullOrEmpty(RequestType))
            {
                RequestType = TempData["SelectedRequestType"]?.ToString() ?? "Normal";
            }

            if (string.IsNullOrEmpty(SerializedSupportingDocs))
            {
                SerializedSupportingDocs = TempData["SerializedSupportingDocs"]?.ToString() ?? string.Empty;
            }

            TempData["SelectedRequestType"] = RequestType;

            // Enforce validation constraints manually for the conditionally required date
            if (Input.PaymentTiming == "Future Dated" && !Input.FutureDate.HasValue)
            {
                ModelState.AddModelError("Input.FutureDate", "A target execution date must be assigned for future dated payments.");
            }

            if (!ModelState.IsValid)
            {
                if (HiddenTotalAmount == 0 && TempData["HiddenAmountValue"] is string amountStr &&
                    decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal savedAmt))
                {
                    HiddenTotalAmount = savedAmt;
                }

                // Repopulate suggested supplier name so the UI label doesn't go blank
                if (SelectedQuoteId > 0)
                {
                    var q = await _context.Quotes.FindAsync(SelectedQuoteId);
                    if (q != null) SuggestedSupplierName = q.SupplierName;
                }
                else if (TempData["SerializedDraftQuotes"] is string draftJson)
                {
                    try
                    {
                        var drafts = JsonSerializer.Deserialize<List<Quote>>(draftJson);
                        var activeDraft = !string.IsNullOrEmpty(BlobUrl)
                            ? drafts?.Find(qt => qt.BlobUrl == BlobUrl)
                            : drafts?.FirstOrDefault();

                        if (activeDraft != null)
                        {
                            SuggestedSupplierName = activeDraft.SupplierName;
                            if (string.IsNullOrEmpty(BlobUrl)) BlobUrl = activeDraft.BlobUrl;
                        }
                    }
                    catch { }
                }

                return Page();
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrEmpty(Input.DepartmentType) || Input.DepartmentType == "None")
                {
                    Input.DepartmentType = TempData["DepartmentType"]?.ToString() ?? Input.DepartmentType;
                }

                if (string.IsNullOrEmpty(Input.CustomerName) || Input.CustomerName == "None")
                {
                    Input.CustomerName = TempData["CustomerName"]?.ToString() ?? Input.CustomerName;
                }

                if (string.IsNullOrEmpty(Input.QuoteType) || Input.QuoteType == "None")
                {
                    Input.QuoteType = TempData["QuoteType"]?.ToString() ?? Input.QuoteType;
                }

                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdString, out Guid userId)) return Forbid();

                var recentThreshold = DateTime.UtcNow.AddSeconds(-5);
                bool isDuplicate = await _context.Requests.AnyAsync(r =>
                    r.RequesterId == userId &&
                    r.TotalAmount == HiddenTotalAmount &&
                    r.Description == Input.Description.Trim() &&
                    r.CreatedAt >= recentThreshold);

                if (isDuplicate)
                {
                    // Silently redirect or show a friendly message to stop duplicate entries
                    return RedirectToPage("./MyRequests");
                }

                if (HiddenTotalAmount == 0 && TempData["HiddenAmountValue"] is string amountStr &&
                    decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal savedAmt))
                {
                    HiddenTotalAmount = savedAmt;
                }

                string determinedStatus = "Pending_HOO";

                var localNow = GetSouthAfricanTime();

                var finalRequest = new Request
                {
                    RequesterId = userId,
                    Description = Input.Description.Trim(),
                    TotalAmount = HiddenTotalAmount,
                    CostType = Input.CostType,
                    PaymentTiming = Input.PaymentTiming,
                    IsPoRequired = (RequestType == "PO"),
                    FutureDate = (Input.PaymentTiming == "Future Dated") ? Input.FutureDate : null,
                    RequestType = RequestType,
                    DepartmentType = Input.DepartmentType,
                    CustomerName = Input.CustomerName,
                    QuoteType = Input.QuoteType,
                    Status = determinedStatus,
                    CreatedAt = localNow
                };

                _context.Requests.Add(finalRequest);
                await _context.SaveChangesAsync();

                // Handle quote persistence for both direct DB records and cached multi-quote JSON drafts
                if (SelectedQuoteId > 0)
                {
                    var targetQuote = await _context.Quotes.FirstOrDefaultAsync(q => q.Id == SelectedQuoteId);
                    if (targetQuote != null)
                    {
                        targetQuote.SupplierName = Input.SupplierName.Trim();
                        targetQuote.IsSelected = true;
                        targetQuote.RequestId = finalRequest.Id;
                        _context.Quotes.Update(targetQuote);
                    }
                }
                else if (TempData["SerializedDraftQuotes"] is string draftJson && !string.IsNullOrEmpty(draftJson))
                {
                    var drafts = JsonSerializer.Deserialize<List<Quote>>(draftJson);
                    if (drafts != null && drafts.Count > 0)
                    {
                        foreach (var draft in drafts)
                        {
                            draft.Id = 0; // Reset ID for new database insertion
                            draft.RequestId = finalRequest.Id;
                            draft.IsSelected = (draft.BlobUrl == BlobUrl);

                            // If this matches the primary active quote, update its supplier name if entered
                            if (draft.IsSelected && !string.IsNullOrEmpty(Input.SupplierName))
                            {
                                draft.SupplierName = Input.SupplierName.Trim();
                            }

                            _context.Quotes.Add(draft);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(BlobUrl))
                {
                    var targetQuote = await _context.Quotes.FirstOrDefaultAsync(q => q.BlobUrl == BlobUrl);
                    if (targetQuote != null)
                    {
                        targetQuote.SupplierName = Input.SupplierName.Trim();
                        targetQuote.IsSelected = true;
                        targetQuote.RequestId = finalRequest.Id;
                        _context.Quotes.Update(targetQuote);
                    }

                }

                if (!string.IsNullOrEmpty(SerializedSupportingDocs))
                {
                    var suppElements = JsonSerializer.Deserialize<List<JsonElement>>(SerializedSupportingDocs);
                    if (suppElements != null)
                    {
                        foreach (var elem in suppElements)
                        {
                            string url = elem.TryGetProperty("BlobUrl", out var urlProp) ? urlProp.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(url))
                            {
                                string extractedFileName = elem.TryGetProperty("FileName", out var nameProp) ? nameProp.GetString() ?? "" : "";
                                if (string.IsNullOrEmpty(extractedFileName))
                                {
                                    extractedFileName = Path.GetFileName(url);
                                }

                                _context.Documents.Add(new Document
                                {
                                    RequestId = finalRequest.Id,
                                    DocType = "Supporting_Document",
                                    FileName = string.IsNullOrEmpty(extractedFileName) ? "Supporting_Document" : extractedFileName,
                                    BlobUrl = url,
                                    UploadedAt = localNow,
                                    UploadedById = userId
                                });
                            }
                        }
                    }
                }

                        _context.Approvals.Add(new Approval
                {
                    RequestId = finalRequest.Id,
                    Stage = determinedStatus,
                    IsApproved = false,
                    DecisionDate = localNow,
                    Comments = $"Procurement managed requisition finalized for supplier: {Input.SupplierName}",
                    ApproverId = userId
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                try
                {
                    await _notify.NotifyApproversAsync(
                        finalRequest.Id,
                        "HOO",
                        $"New procurement request #{finalRequest.Id} has been submitted and requires your review."
                    );
                }
                catch (Exception notifyEx)
                {
                    System.Diagnostics.Debug.WriteLine($"WARNING: Notification email failed to send: {notifyEx.Message}");
                }

                return RedirectToPage("./MyRequests");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                string innerMessage = ex.InnerException != null ? $" | Inner Error: {ex.InnerException.Message}" : "";
                ModelState.AddModelError("", $"A critical data tracking pipeline failure occurred while saving your request: {ex.Message}{innerMessage}");
                return Page();
            }
        }
    }
}