using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;

namespace StudentEnrollmentSystem.Pages.AccountDetails
{
    public class ChangePasswordModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<User> _passwordHasher;

        public ChangePasswordModel(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = new PasswordHasher<User>();
        }

        [BindProperty]
        public ChangePasswordViewModel Input { get; set; } = new();

        public string SuccessMessage { get; set; }
        public string ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                return RedirectToPage("/Account/Login");
            }
            return Page();
        }
        public IActionResult OnPost()
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                return RedirectToPage("/Account/Login");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserID == userID);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            // Use ASP.NET Identity's password hasher to verify the password
            var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(user, user.Password, Input.CurrentPassword);

            if (passwordVerificationResult == PasswordVerificationResult.Failed)
            {
                ErrorMessage = "Incorrect current password.";
                return Page();
            }
            var isNewPasswordSame = _passwordHasher.VerifyHashedPassword(user, user.Password, Input.NewPassword) == PasswordVerificationResult.Success
                        || _passwordHasher.VerifyHashedPassword(user, user.Password, Input.ConfirmNewPassword) == PasswordVerificationResult.Success;

            if (isNewPasswordSame)
            {
                ErrorMessage = "New password cannot be the same as the current password.";
                return Page();
            }

            // Check new password match
            if (Input.NewPassword != Input.ConfirmNewPassword)
            {
                ErrorMessage = "New password and confirmation do not match.";
                return Page();
            }
            if (string.IsNullOrEmpty(Input.NewPassword) || string.IsNullOrEmpty(Input.NewPassword) || string.IsNullOrEmpty(Input.ConfirmNewPassword))
            {
                ErrorMessage = "Please Input all Field";
                return Page();
            }
            // Temporarily store new password & user info in TempData
            TempData["NewPassword"] = Input.NewPassword;
            TempData["UserID"] = userID;

            // Set session variable to track that change password flow is in progress
            HttpContext.Session.SetString("AuthFlow", "ChangePassword");

            // Redirect to email/TAC verification page
            return RedirectToPage("/VerifyTAC");
        }
    }
}