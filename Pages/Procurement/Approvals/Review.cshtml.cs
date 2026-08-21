using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Intranet.Models;
using Intranet.Services;
using System.Security.Claims;

namespace Intranet.Pages.Procurement.Approvals
{
    public class ReviewModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notify;
        private readonly IAzureBlobService _blobService;

        public ReviewModel(AppDbContext context, NotificationService notify, IAzureBlobService blobService)
        {
            _context = context;
            _notify = notify;
            _blobService = blobService;
        }

        [BindProperty] public Request Request { get; set; } = null!;
        [BindProperty] public string DecisionComments { get; set; } = "";

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                Request = await _context.Requests
                    .Include(r => r.Requester)
                    .Include(r => r.Quotes)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (Request == null) return NotFound();

                foreach (var quote in Request.Quotes)
                {
                    if (!string.IsNullOrEmpty(quote.BlobUrl))
                    {
                        // Extract the file name from the stored BlobUrl 
                        var uri = new Uri(quote.BlobUrl);
                        string fileName = Path.GetFileName(uri.LocalPath);

                        // Overwrite the BlobUrl with a temporary secure SAS URL (expires in 30 mins)
                        quote.BlobUrl = _blobService.GetReadSasUrl("quotes", fileName);
                    }
                }

            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An error occurred while fetching the request.");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostDecisionAsync(int id, bool isApproved)
        {
            try
            {
                var req = await _context.Requests
                    .Include(r => r.Requester)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (req == null) return NotFound();

                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
                var currentUserId = Guid.Parse(userIdString);

                if (isApproved)
                {
                    // Escalation Logic remains intact
                    if (req.TotalAmount > 15000 && req.Status == "Pending_HOO")
                    {
                        req.Status = "Pending_MD";

                        await _notify.NotifyApproversAsync(
                            req.Id,
                            "MD",
                            $"HOO approved Request #{req.Id}. Final MD authorization required."
                        );
                    }
                    else
                    {
                        
                        if (req.IsPoRequired)
                        {
                            req.Status = "PO_Upload";

                            // NOTIFY FINANCE: PO Generation Needed
                            await _notify.NotifyApproversAsync(req.Id, "Finance", $"Request #{req.Id} approved. Please generate and upload the Purchase Order (PO).");

                            // NOTIFY MANAGER: Moved to PO Stage
                            await _notify.NotifyUserAsync(req.RequesterId, $"Request #{req.Id} approved by Head. Finance is now compiling the official PO.", req.Id, "Request Approved (PO Pending)");
                        }
                        else
                        {
                            // Original fallback behavior for standard non-PO transactions
                            req.Status = "Awaiting_Payment";

                            // NOTIFY FINANCE: POP needed
                            await _notify.NotifyApproversAsync(req.Id, "Finance", $"Request #{req.Id} approved. Please upload Proof of Payment (POP).");

                            // NOTIFY MANAGER: Approved
                            await _notify.NotifyUserAsync(req.RequesterId, $"Request #{req.Id} has been approved. Finance is processing payment.", req.Id, "Request Approved");
                        }
                    }
                }
                else
                {
                    req.Status = "Rejected";
                    await _notify.NotifyUserAsync(req.RequesterId, $"Request #{req.Id} was rejected. Reason: {DecisionComments}", req.Id);
                }

                
                _context.Approvals.Add(new Approval
                {
                    RequestId = req.Id,
                    ApproverId = currentUserId,
                    IsApproved = isApproved,
                    Comments = DecisionComments,
                    DecisionDate = DateTime.Now,
                    Stage = User.IsInRole("MD") ? "MD" : "HOO"
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An error occurred while saving the approval decision.");
                return Page();
            }

            return RedirectToPage("./Dashboard");
        }
    }
}