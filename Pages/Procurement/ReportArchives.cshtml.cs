using Intranet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Intranet.Pages.Procurement 
{
    public class ReportArchivesModel : PageModel
    {
        private readonly AppDbContext _context;
        public ReportArchivesModel(AppDbContext context) => _context = context;

        public List<Document> ArchivedReports { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? SelectedMonth { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedYear { get; set; }

        public async Task OnGetAsync()
        {
            try { 
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(userIdString, out Guid userId))
                {
                    var query = _context.Documents
                    .Where(d => (d.RequestId == null || d.RequestId == -1) && d.UploadedById == userId)
                    .AsQueryable();

                    // Apply Month Filter
                    if (SelectedMonth.HasValue && SelectedMonth > 0)
                    {
                        query = query.Where(d => d.UploadedAt.HasValue && d.UploadedAt.Value.Month == SelectedMonth);
                    }

                    // Apply Year Filter
                    if (SelectedYear.HasValue)
                    {
                        query = query.Where(d => d.UploadedAt.HasValue && d.UploadedAt.Value.Year == SelectedYear);
                    }

                    ArchivedReports = await query.OrderByDescending(d => d.UploadedAt).ToListAsync();
                }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }
        }
    }
}