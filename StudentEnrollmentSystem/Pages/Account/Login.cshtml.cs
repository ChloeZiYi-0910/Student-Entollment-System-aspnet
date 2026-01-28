using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using System.Diagnostics;

namespace StudentEnrollmentSystem.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;

        public LoginModel(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [BindProperty]
        public string UserID { get; set; }
        [BindProperty]
        public string Password { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Debug.WriteLine($"Login attempt: UserID = {UserID}, Password = {Password}");

            if (string.IsNullOrEmpty(UserID) || string.IsNullOrEmpty(Password))
            {
                ModelState.AddModelError("", "User ID and Password are required.");
                return Page();
            }

            var user = _context.Users.FirstOrDefault(u => u.UserID == UserID);

            if (user != null)
            {
                var passwordHasher = new PasswordHasher<User>();
                var result = passwordHasher.VerifyHashedPassword(user, user.Password, Password);
                if (result == PasswordVerificationResult.Success)
                {
                    Debug.WriteLine($"User found: {user.UserID}, {user.FullName}, Role: {user.Role}");
                    HttpContext.Session.SetString("UserID", UserID);
                    HttpContext.Session.SetString("FullName", user.FullName);
                    HttpContext.Session.SetString("Role", user.Role); // Store role in session
                    TempData["Username"] = user.FullName;
                    Debug.WriteLine("Session set, redirecting based on role");

                    // Role-based redirection
                    if (user.Role == "Admin")
                    {
                        Debug.WriteLine("Redirecting to Admin/Home");
                        return RedirectToPage("/Admin/AdminHome");
                    }
                    else // Default to Student (or any other role)
                    {

                        using (var connection = new SqlConnection(_connectionString))
                        {
                            await connection.OpenAsync();
                            // Simplified SQL query to check for NULL values in PersonalEmail
                            var studentSql = @"
                            SELECT 
                                CASE WHEN PersonalEmail IS NULL THEN 1 ELSE 0 END AS IsPersonalEmailNull
                            FROM StudentDetails
                            WHERE UserID = @UserID";

                            bool isPersonalEmailNull = false;

                            using (var command = new SqlCommand(studentSql, connection))
                            {
                                command.Parameters.AddWithValue("@UserID", UserID);
                                using (var reader = await command.ExecuteReaderAsync())
                                {
                                    if (reader.Read())
                                    {
                                        isPersonalEmailNull = reader.GetInt32(reader.GetOrdinal("IsPersonalEmailNull")) == 1;
                                    }
                                    else
                                    {
                                        return RedirectToPage("/Account/Login");
                                    }
                                }
                            }

                            // Check if any of the required fields are NULL
                            if (isPersonalEmailNull)
                            {
                                TempData["ErrorMessage"] = "Please complete your profile before enrolling.";
                                return RedirectToPage("/AccountDetails/StudentDetails");
                            }
                        }
                        Debug.WriteLine("Redirecting to /Home");
                        return RedirectToPage("/Home");
                    }
                }
            }

            Debug.WriteLine("No user found with matching credentials.");
            ModelState.AddModelError("", "Invalid User ID or Password.");
            return Page();
        }
    }
}