using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Intranet.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                {
                    return RedirectToPage("/Account/Login");
                }

                // If logged in, send them to their specific dashboard
                if (User.IsInRole("Finance")) return RedirectToPage("/Procurement/Finance/PaymentQueue");
                if (User.IsInRole("MD") || User.IsInRole("HOO") || User.IsInRole("HOS"))
                    return RedirectToPage("/Procurement/Approvals/Dashboard");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured");
            }
            return RedirectToPage("/Procurement/Manager/MyRequests");
        }
    }
}
