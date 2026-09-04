using Intranet.Models;
using Intranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Pages.Procurement.Manager
{
    [Authorize(Roles = "Manager")]
    [RequestSizeLimit(104857600)]
    [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IAzureBlobService _blobService;
        private readonly GeminiAgentService _ai;

        public CreateModel(AppDbContext context, IAzureBlobService blobService, GeminiAgentService ai)
        {
            _context = context;
            _blobService = blobService;
            _ai = ai;
        }

        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }

        [BindProperty] public string Description { get; set; } = "";
        [BindProperty] public string CostType { get; set; } = "Projects";
        [BindProperty] public string PaymentTiming { get; set; } = "Present";
        [BindProperty] public bool IsPoRequired { get; set; } = false;

        [BindProperty] public DateTime? FutureDate { get; set; }

        [BindProperty] public List<IFormFile> QuoteFiles { get; set; } = new();

        [BindProperty] public List<IFormFile> SupportingFiles { get; set; } = new();
        [BindProperty] public int FormStep { get; set; } = 1;
        [BindProperty] public string SerializedQuotes { get; set; } = "";
        [BindProperty] public string SerializedSupportingDocs { get; set; } = "";
        [BindProperty] public string SelectedQuoteUrl { get; set; } = "";
        [BindProperty] public string RemovedQuoteIds { get; set; } = "";

        [BindProperty] public string RemovedSupportingDocIds { get; set; } = "";

        [BindProperty]
        public List<IFormFile> QuoteUploads { get; set; } = new List<IFormFile>();

        [BindProperty]
        public string RequestType { get; set; } = "Normal";

        

        [BindProperty] public string DepartmentType { get; set; } = "None";
        [BindProperty] public string CustomerName { get; set; } = "None";
        [BindProperty] public string QuoteType { get; set; } = "None";

        public List<Quote> ExtractedQuotes { get; set; } = new();

        private DateTime GetSouthAfricanTime()
        {
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
        }
        public bool IsEditMode => Id.HasValue && Id.Value > 0;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id.HasValue)
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                Guid.TryParse(userIdString, out Guid userId);

                var request = await _context.Requests
                    .Include(r => r.Quotes)
                    .FirstOrDefaultAsync(m => m.Id == id.Value);

                if (request == null) return NotFound();
                if (request.RequesterId != userId) return Forbid();

                if (request.Status != "Pending_HOO" && request.Status != "Pending_HOS" && request.Status != "Submitted")
                {
                    TempData["ErrorMessage"] = "This request has already been processed by management and can no longer be modified.";
                    return RedirectToPage("./MyRequests");
                }

                Id = request.Id;
                Description = request.Description;
                CostType = request.CostType;
                DepartmentType = request.DepartmentType;
                CustomerName = request.CustomerName;
                QuoteType = request.QuoteType;

                PaymentTiming = (request.PaymentTiming == "Immediate") ? "Present" : "Future";
                IsPoRequired = request.IsPoRequired;
                FutureDate = request.FutureDate;
                RequestType = request.RequestType;

                ExtractedQuotes = request.Quotes.ToList();
                SerializedQuotes = JsonSerializer.Serialize(ExtractedQuotes);
                SelectedQuoteUrl = request.Quotes.FirstOrDefault(q => q.IsSelected)?.BlobUrl ?? "";

                var existingSuppDocs = await _context.Documents
                    .Where(d => d.RequestId == id.Value && d.DocType == "Supporting_Document")
                    .ToListAsync();

                SerializedSupportingDocs = JsonSerializer.Serialize(existingSuppDocs.Select(d => new {
                    d.Id,
                    FileName = string.IsNullOrEmpty(d.FileName) ? Path.GetFileName(d.BlobUrl) : d.FileName,
                    d.BlobUrl
                }));

                FormStep = 1;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!string.IsNullOrEmpty(SerializedQuotes))
            {
                ExtractedQuotes = JsonSerializer.Deserialize<List<Quote>>(SerializedQuotes) ?? new();
            }

            ModelState.Remove(nameof(QuoteFiles));
            ModelState.Remove(nameof(SupportingFiles));

            // STEP 2 RE-ROUTING PIPELINE
            if (FormStep == 2)
            {
                if (!string.IsNullOrEmpty(SerializedQuotes))
                {
                    ExtractedQuotes = JsonSerializer.Deserialize<List<Quote>>(SerializedQuotes) ?? new();
                }

                var selectedRadioUrl = Request.Form["SelectedQuoteUrl"].ToString();
                if (!string.IsNullOrEmpty(selectedRadioUrl)) SelectedQuoteUrl = selectedRadioUrl;

                if (IsEditMode) return await FinalizeAndSaveOldBehavior();

                var selectedQuote = ExtractedQuotes.FirstOrDefault(q => q.BlobUrl == SelectedQuoteUrl);
                decimal finalPrice = selectedQuote?.Price ?? 0.00m;

                TempData["SerializedDraftQuotes"] = SerializedQuotes;
                TempData["SerializedSupportingDocs"] = SerializedSupportingDocs;
                TempData["HiddenAmountValue"] = finalPrice.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                TempData["SelectedRequestType"] = RequestType;
                TempData["DepartmentType"] = DepartmentType;
                TempData["CustomerName"] = CustomerName;
                TempData["QuoteType"] = QuoteType;
                TempData.Keep();

                return RedirectToPage("./Description", new
                {
                    selectedQuoteId = 0,
                    description = Description,
                    amount = finalPrice,
                    costType = CostType == "BAU" ? "BAU" : (CostType == "Health And Safety" ? "Health And Safety" : "Projects"),
                    timing = (PaymentTiming == "Present" || PaymentTiming == "Immediate") ? "Immediate" : "Future Dated",
                    blobUrl = SelectedQuoteUrl,
                    futureDate = FutureDate?.ToString("yyyy-MM-dd"),
                    requestType = RequestType,
                    departmentType = DepartmentType,
                    customerName = CustomerName,
                    quoteType = QuoteType
                });
            }

            // Capture Files
            var uploadedFiles = (QuoteFiles != null && QuoteFiles.Count > 0)
                ? QuoteFiles
                : Request.Form.Files.GetFiles("QuoteFiles").ToList();

            var uploadedSuppFiles = (SupportingFiles != null && SupportingFiles.Count > 0)
                ? SupportingFiles
                : Request.Form.Files.GetFiles("SupportingFiles").ToList();

            var removedIds = !string.IsNullOrEmpty(RemovedQuoteIds)
                ? RemovedQuoteIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                : new List<int>();

            IsPoRequired = RequestType == "PO";

            try
            {
                List<Document> currentSuppList = new();
                if (!string.IsNullOrEmpty(SerializedSupportingDocs))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<JsonElement>>(SerializedSupportingDocs);
                        if (parsed != null)
                        {
                            foreach (var elem in parsed)
                            {
                                int docId = elem.TryGetProperty("Id", out var idProp) ? idProp.GetInt32() : 0;
                                string url = elem.TryGetProperty("BlobUrl", out var urlProp) ? urlProp.GetString() ?? "" : "";
                                string name = elem.TryGetProperty("FileName", out var nameProp) ? nameProp.GetString() ?? "" : Path.GetFileName(url);
                                currentSuppList.Add(new Document { Id = docId, FileName = name, BlobUrl = url, DocType = "Supporting_Document" });
                            }
                        }
                    }
                    catch { }
                }

                if (uploadedSuppFiles != null && uploadedSuppFiles.Count > 0)
                {
                    foreach (var suppFile in uploadedSuppFiles)
                    {
                        string safeOriginalName = Path.GetFileName(suppFile.FileName);
                        string blobFileName = $"{Guid.NewGuid()}{Path.GetExtension(suppFile.FileName)}";
                        string suppBlobUrl;

                        using (var stream = suppFile.OpenReadStream())
                        {
                            suppBlobUrl = await _blobService.UploadFileAsync("supporting", blobFileName, stream, suppFile.ContentType);
                        }

                        currentSuppList.Add(new Document
                        {
                            Id = 0,
                            FileName = safeOriginalName,
                            BlobUrl = suppBlobUrl,
                            DocType = "Supporting_Document",
                            UploadedAt = GetSouthAfricanTime()
                        });
                    }
                }

                SerializedSupportingDocs = JsonSerializer.Serialize(currentSuppList.Select(d => new {
                    d.Id,
                    FileName = string.IsNullOrEmpty(d.FileName) ? Path.GetFileName(d.BlobUrl) : d.FileName,
                    d.BlobUrl
                }));


                if (uploadedFiles != null)
                    foreach (var f in uploadedFiles)
                        Console.WriteLine($"[DEBUG]    -> File: {f.FileName}, Size: {f.Length}");

                // 1. Get existing data
                List<Quote> currentList = new();
                if (IsEditMode)
                {
                    var dbQuotes = await _context.Quotes.Where(q => q.RequestId == Id.Value).ToListAsync();
                    currentList = dbQuotes.Where(q => !removedIds.Contains(q.Id)).ToList();
                    Console.WriteLine($"[DEBUG] EditMode - dbQuotes loaded: {currentList.Count}");
                }
                else if (!string.IsNullOrEmpty(SerializedQuotes))
                {
                    currentList = JsonSerializer.Deserialize<List<Quote>>(SerializedQuotes) ?? new();
                    Console.WriteLine($"[DEBUG] Deserialized existing quotes: {currentList.Count}");
                }

                // 2. Process NEW files sequentially to avoid Gemini API rate limiting (max 3 total)
                if (uploadedFiles != null && uploadedFiles.Count > 0)
                {
                    int slotsRemaining = 3 - currentList.Count;
                    if (slotsRemaining > 0)
                    {
                        var filesToProcess = uploadedFiles.Take(slotsRemaining).ToList();

                        foreach (var file in filesToProcess)
                        {
                            string fileHash = GetFileSha256Hash(file);

                            bool isDuplicate = await _context.Quotes
                            .Include(q => q.Request)
                            .AnyAsync(q => q.FileHash == fileHash
                            && q.Request.Status != "Cancelled"
                            && q.Request.Status != "Rejected_Acknowledged"
                            && (!IsEditMode || q.RequestId != Id.Value));

                            if (isDuplicate)
                            {
                                Console.WriteLine($"[SECURITY] Rejected duplicate file upload exploit variant: {file.FileName}");
                                ModelState.AddModelError("", $"The file '{file.FileName}' has already been uploaded previously. Duplicate entries are restricted.");

                                ExtractedQuotes = currentList;
                                SerializedQuotes = JsonSerializer.Serialize(ExtractedQuotes);
                                FormStep = 1;
                                return Page();
                            }

                            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

                            // Upload file to Azure Blob Storage 'quotes' container
                            string blobUrl;
                            using (var stream = file.OpenReadStream())
                            {
                                blobUrl = await _blobService.UploadFileAsync("quotes", fileName, stream, file.ContentType);
                            }

                            
                            var tempPath = Path.Combine(Path.GetTempPath(), fileName);
                            using (var stream = file.OpenReadStream())
                            {
                                using var fs = new FileStream(tempPath, FileMode.Create);
                                await stream.CopyToAsync(fs);
                            }

                            var aiData = await _ai.AnalyzeQuoteAsync(tempPath, file.FileName);
                            try { if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); } catch { }

                            currentList.Add(new Quote
                            {
                                SupplierName = aiData.SupplierName,
                                Price = aiData.TotalPrice,
                                AiExtractedVat = aiData.VatRegistration,
                                AiAnalysisNotes = aiData.Notes,
                                AiConfidenceScore = aiData.ConfidenceScore,
                                BlobUrl = blobUrl,
                                DocType = "Quote",
                                FileHash = fileHash
                            });
                        }
                    }
                }

                if (currentList.Count == 0)
                {
                    ModelState.AddModelError("", "You must leave or upload at least 1 supplier quote.");
                    FormStep = 1;
                    return Page();
                }

                // 4. Update State for UI
                ExtractedQuotes = currentList;
                SerializedQuotes = JsonSerializer.Serialize(ExtractedQuotes);

                if (string.IsNullOrEmpty(SelectedQuoteUrl) || !ExtractedQuotes.Any(q => q.BlobUrl == SelectedQuoteUrl))
                {
                    SelectedQuoteUrl = ExtractedQuotes.OrderBy(q => q.Price).FirstOrDefault()?.BlobUrl ?? "";
                }

                FormStep = 2;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"AI Sourcing Engine Failure: {ex.Message}");
                FormStep = 1;
                return Page();
            }

            return Page();
        }

        private async Task<IActionResult> FinalizeAndSaveOldBehavior()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out Guid userId)) return Page();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                Request request = await _context.Requests.Include(r => r.Quotes).FirstOrDefaultAsync(r => r.Id == Id.Value);
                if (request == null) return NotFound();

                var removedIds = new List<int>();
                if (!string.IsNullOrEmpty(RemovedQuoteIds))
                {
                    removedIds = RemovedQuoteIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                }

                var removedSuppIds = !string.IsNullOrEmpty(RemovedSupportingDocIds)
                    ? RemovedSupportingDocIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                    : new List<int>();

                request.Description = Description;
                request.CostType = CostType;
                request.DepartmentType = DepartmentType;
                request.CustomerName = CustomerName;
                request.QuoteType = QuoteType;
                request.RequestType = RequestType;
                request.PaymentTiming = (PaymentTiming == "Present" || PaymentTiming == "Immediate") ? "Immediate" : "Future Dated";
                request.IsPoRequired = (RequestType == "PO");

                request.FutureDate = (PaymentTiming == "Future") ? FutureDate : null;
                request.UpdatedAt = GetSouthAfricanTime();

                if (removedIds.Count > 0)
                {
                    var targets = request.Quotes.Where(q => removedIds.Contains(q.Id)).ToList();
                    _context.Quotes.RemoveRange(targets);
                }

                if (removedSuppIds.Count > 0)
                {
                    var suppTargets = await _context.Documents.Where(d => removedSuppIds.Contains(d.Id)).ToListAsync();
                    _context.Documents.RemoveRange(suppTargets);
                }

                var selected = ExtractedQuotes.FirstOrDefault(q => q.BlobUrl == SelectedQuoteUrl);
                request.TotalAmount = selected?.Price ?? 0;
                request.Status = "Pending_HOO";

                await _context.SaveChangesAsync();

                foreach (var q in ExtractedQuotes)
                {
                    if (q.Id == 0)
                    {
                        q.RequestId = request.Id;
                        q.IsSelected = (q.BlobUrl == SelectedQuoteUrl);
                        _context.Quotes.Add(q);
                    }
                    else
                    {
                        var match = await _context.Quotes.FindAsync(q.Id);
                        if (match != null)
                        {
                            match.IsSelected = (match.BlobUrl == SelectedQuoteUrl);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(SerializedSupportingDocs))
                {
                    try
                    {
                        var suppElements = JsonSerializer.Deserialize<List<JsonElement>>(SerializedSupportingDocs);
                        if (suppElements != null)
                        {
                            foreach (var elem in suppElements)
                            {
                                int docId = elem.TryGetProperty("Id", out var idProp) ? idProp.GetInt32() : 0;
                                string url = elem.TryGetProperty("BlobUrl", out var urlProp) ? urlProp.GetString() ?? "" : "";

                                if (docId == 0 && !string.IsNullOrEmpty(url))
                                {
                                    string extractedFileName = elem.TryGetProperty("FileName", out var nameProp) ? nameProp.GetString() ?? "" : "";
                                    if (string.IsNullOrEmpty(extractedFileName))
                                    {
                                        extractedFileName = Path.GetFileName(url);
                                    }

                                    _context.Documents.Add(new Document
                                    {
                                        RequestId = request.Id,
                                        DocType = "Supporting_Document",
                                        FileName = string.IsNullOrEmpty(extractedFileName) ? "Supporting_Document" : extractedFileName,
                                        BlobUrl = url,
                                        UploadedAt = GetSouthAfricanTime()
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "Request updated successfully.";
                return RedirectToPage("./MyRequests");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Database Commit Error: {ex.Message}");
                ExtractedQuotes = new();
                FormStep = 1;
                return Page();
            }
        }

        private string GetFileSha256Hash(IFormFile file)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = file.OpenReadStream())
                {
                    byte[] hashBytes = sha256.ComputeHash(stream);
                    return Convert.ToHexString(hashBytes).ToLower();
                }
            }
        }
    }
}