using Intranet.Models;
using Intranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

public class ProcRegister : PageModel
{
    private readonly AppDbContext _context;
    public ProcRegister(AppDbContext context)
    {
        _context = context;
        
    }

    public List<Request> MasterList { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int SelectedMonth { get; set; } = DateTime.Now.Month;

    [BindProperty(SupportsGet = true)]
    public int SelectedYear { get; set; } = DateTime.Now.Year;

    // Generates the filename based on selection for the download button
    public string TargetFileName =>
        $"Procurement_Register_{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(SelectedMonth)}_{SelectedYear}.xlsx";

    public async Task OnGetAsync()
    {
        try
        {

            // Filter database results to only show the selected month/year
            MasterList = await _context.Requests
                .Include(r => r.Requester).ThenInclude(u => u.Department)
                .Include(r => r.Quotes)
                .Where(r => r.Status == "Closed" &&
                            r.CreatedAt.HasValue &&
                            r.CreatedAt.Value.Month == SelectedMonth &&
                            r.CreatedAt.Value.Year == SelectedYear)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "An error occured");
        }
    }
}