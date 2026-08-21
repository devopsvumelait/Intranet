using Intranet.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Finance,Admin")]
public class RegisterModel : PageModel
{
    private readonly AppDbContext _context;
    public RegisterModel(AppDbContext context) => _context = context;

    public List<Request> MasterList { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            MasterList = await _context.Requests
                .Include(r => r.Requester).ThenInclude(u => u.Department)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "An error occured");
        }
    }
}