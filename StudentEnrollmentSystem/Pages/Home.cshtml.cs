using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace StudentEnrollmentSystem.Pages
{
    public class HomeModel : PageModel
    {
        public IActionResult OnGet()
        {
            var userID = HttpContext.Session.GetString("UserID");
            var role = HttpContext.Session.GetString("Role");
            Debug.WriteLine($"Home accessed. UserID: {userID}, Role: {role}");

            if (string.IsNullOrEmpty(userID))
            {
                Debug.WriteLine("No UserID in session, redirecting to Login");
                return RedirectToPage("/Account/Login");
            }

            Debug.WriteLine("Rendering Home page for user");
            return Page();
        }
    }
}