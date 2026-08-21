using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Intranet.Models;

namespace Intranet.Pages.Procurement.Approvals
{
    [Authorize(Roles = "HOO,HOS,MD")]
    public class QueueModel : PageModel
    {
        private readonly AppDbContext _context;

        public QueueModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Request> PendingRequests { get; set; } = new();
        public string CurrentRole { get; set; } = "";

        public async Task OnGetAsync()
        {
            try
            {
                // 1. Determine the user's highest priority role for filtering
                if (User.IsInRole("MD")) CurrentRole = "MD";
                else if (User.IsInRole("HOS")) CurrentRole = "HOS";
                else if (User.IsInRole("HOO")) CurrentRole = "HOO";

                // 2. Map the role to the SQL Status string
                string targetStatus = CurrentRole switch
                {
                    "HOO" => "Pending_HOO",
                    "HOS" => "Pending_HOS",
                    "MD" => "Pending_MD",
                    _ => ""
                };

                // 3. Fetch requests sitting in this user's "court"
                PendingRequests = await _context.Requests
                    .Include(r => r.Requester)
                    .ThenInclude(u => u.Department)
                    .Include(r => r.Quotes)
                    .Where(r => r.Status == targetStatus)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }
        }
    }
}