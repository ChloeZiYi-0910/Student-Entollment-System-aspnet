using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;

namespace StudentEnrollmentSystem.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<User> _passwordHasher;

        public ForgotPasswordModel(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = new PasswordHasher<User>();
        }
        [BindProperty]
        public string UserID { get; set; }

        [BindProperty]
        public ChangePasswordViewModel Input { get; set; } = new();


        public string SuccessMessage { get; set; }
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please fix the errors and try again.";
                return Page();
            }

            var user = _context.Users.FirstOrDefault(u => u.UserID == UserID);
            if (user == null)
            {
                ErrorMessage = "User not found. Please check your UserID and try again.";
                return Page();
            }
            var isNewPasswordSame = _passwordHasher.VerifyHashedPassword(user, user.Password, Input.NewPassword) == PasswordVerificationResult.Success
                                    || _passwordHasher.VerifyHashedPassword(user, user.Password, Input.ConfirmNewPassword) == PasswordVerificationResult.Success;

            if (isNewPasswordSame)
            {
                ErrorMessage = "New password cannot be the same as the current password.";
                return Page();
            }
            if (Input.NewPassword != Input.ConfirmNewPassword)
            {
                ErrorMessage = "New password and confirmation do not match.";
                return Page();
            }

            // Store user information in session for the verification process
            HttpContext.Session.SetString("AuthFlowUserID", user.UserID);

            // Temporarily store new password in TempData
            TempData["NewPassword"] = Input.NewPassword;
            TempData["UserID"] = UserID;

            HttpContext.Session.SetString("AuthFlow", "ForgotPassword");

            return RedirectToPage("/VerifyTAC");
        }

    }
}
