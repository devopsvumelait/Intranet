using DocumentFormat.OpenXml.Office2010.Excel;
using Intranet.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Intranet.Pages.Procurement.Finance
{
    [Authorize(Roles = "Finance")]
    public class PaymentQueue : PageModel
    {
        private readonly AppDbContext _context;
        public PaymentQueue(AppDbContext context) => _context = context;


        public string UserFullName { get; set; }
        public List<Request> PresentDatedQueue { get; set; } = new();
        public List<Request> FutureDatedQueue { get; set; } = new();
        public List<Request> WaybillQueue { get; set; } = new();
        public List<Request> OnlineQueue { get; set; } = new();
        public List<Request> PoUploadQueue { get; set; } = new();
        public List<Request> AwaitingInvoiceQueue { get; set; } = new();
        public List<Request> AwaitingVerificationQueue { get; set; } = new();
        public List<Request> PoActiveTable { get; set; } = new();

        public async Task OnGetAsync()
        {
            var data = await _context.Requests
                .Include(r => r.Requester)
                .Include(r => r.Quotes.Where(q => q.IsSelected))
                .ToListAsync();


            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var currentUser = await _context.Users.FindAsync(userId);
            UserFullName = currentUser != null ? $"{currentUser.FirstName} {currentUser.Surname}" : "User";

            PresentDatedQueue = data.Where(r => r.Status == "Awaiting_Payment" && r.PaymentTiming == "Immediate").ToList();
            FutureDatedQueue = data.Where(r => r.Status == "Awaiting_Payment" && r.PaymentTiming == "Future Dated").ToList();

            WaybillQueue = data.Where(r => r.RequestType == "Waybill" && r.Status != "Closed" && r.Status != "Pending_HOO" && r.Status != "Pending_MD").ToList();
            OnlineQueue = data.Where(r => r.RequestType == "ONLINE" && r.Status != "Closed" && r.Status != "Pending_HOO" && r.Status != "Pending_MD").ToList();
            
            PoUploadQueue = data.Where(r => r.Status == "PO_Upload").ToList();
            AwaitingInvoiceQueue = data.Where(r => r.Status == "Awaiting_Invoice" || r.Status == "PO_Issued").ToList();
            AwaitingVerificationQueue = data.Where(r => r.Status == "Awaiting_Verification").ToList();
            PoActiveTable = data.Where(r => r.Status == "PO_Payment_Queue").ToList();
        }
    }
}