using Intranet.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Pages.Procurement.Finance
{
    [Authorize(Roles = "Finance")]
    public class VerifyQueueModel : PageModel
    {
        private readonly AppDbContext _context;
        public VerifyQueueModel(AppDbContext context) => _context = context;

        public List<Request> PendingVerification { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                PendingVerification = await _context.Requests
                    .Include(r => r.Requester)
                    .Include(r => r.Quotes.Where(q => q.IsSelected))
                    .Where(r => r.Status == "Awaiting_Verification")
                    .OrderBy(r => r.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }
        }
    }
}