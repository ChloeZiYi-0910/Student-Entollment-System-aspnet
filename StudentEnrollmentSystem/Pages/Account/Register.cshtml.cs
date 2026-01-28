using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace StudentEnrollmentSystem.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public RegisterModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public User User { get; set; }

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Check if UserID already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserID == User.UserID);
            if (existingUser != null)
            {
                ErrorMessage = "User ID already exists. Please choose a different one.";
                return Page();
            }

            try
            {
                _context.Users.Add(User);
                await _context.SaveChangesAsync();
                SuccessMessage = "Registration successful! You can now log in.";
                ModelState.Clear(); // Clear form after success
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred: {ex.Message}";
                return Page();
            }
        }
    }
}