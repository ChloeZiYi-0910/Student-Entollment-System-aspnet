using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace StudentEnrollmentSystem.Pages.Account
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet()
        {
            try
            {
                Debug.WriteLine("Logout: OnGet started");
                var userIDBefore = HttpContext.Session.GetString("UserID"); // Changed from StudentID
                Debug.WriteLine($"Logout: UserID before clear: {userIDBefore ?? "null"}");

                HttpContext.Session.Clear();
                Debug.WriteLine("Logout: Session cleared successfully");

                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                TempData["LogoutMessage"] = "You have been logged out successfully.";
                Debug.WriteLine("Logout: Redirecting to /Account/Login");
                return RedirectToPage("/Account/Login");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Logout: Error - {ex.Message}");
                Debug.WriteLine($"Logout: Stack Trace - {ex.StackTrace}");
                return Content($"Logout failed: {ex.Message}");
            }
        }
    }
}