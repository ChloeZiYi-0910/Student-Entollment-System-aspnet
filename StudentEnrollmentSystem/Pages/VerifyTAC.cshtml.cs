using System;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using System.Net.Mail;
using System.Net;
using System.ComponentModel.DataAnnotations;

namespace StudentEnrollmentSystem.Pages
{
    public class VerifyTACModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<User> _passwordHasher;

        public VerifyTACModel(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = new PasswordHasher<User>();
        }

        [BindProperty]
        public string EnteredTAC { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        public bool CodeSent { get; set; }
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public string? AuthFlow { get; set; }

        public IActionResult OnGet()
        {
            AuthFlow = HttpContext.Session.GetString("AuthFlow");
            var userID = AuthFlow == "ChangePassword"
                ? HttpContext.Session.GetString("UserID") // Already logged in
                : HttpContext.Session.GetString("AuthFlowUserID"); // Temporarily stored for reset


            // Validate we have a proper authentication flow in progress
            if (string.IsNullOrEmpty(userID) || string.IsNullOrEmpty(AuthFlow) ||
                (AuthFlow != "ChangePassword" && AuthFlow != "ForgotPassword"))
            {
                // Redirect based on auth flow
                if (AuthFlow == "ChangePassword")
                    return RedirectToPage("/AccountDetails/ChangePassword");
                else
                    return RedirectToPage("/Account/ForgotPassword");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserID == userID);
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            // Pre-fill email if it exists in session but allow user to change it
            Email = HttpContext.Session.GetString("UserEmail");

            // Check if there's already an active TAC for this user
            var existingTAC = _context.VerifyTAC
                .FirstOrDefault(t => t.UserID == userID &&
                                 !t.IsVerify &&
                                 t.ExpiryTime > DateTime.Now);

            if (existingTAC != null)
            {
                CodeSent = true;
                ErrorMessage = "A verification code has already been sent. Please check your email or wait until it expires.";
            }
            else
            {
                CodeSent = false;
            }


            return Page();
        }
        public IActionResult OnGetCancel()
        {
            AuthFlow = HttpContext.Session.GetString("AuthFlow");

            // Decide redirect based on the flow
            if (AuthFlow == "ChangePassword")
            {
                return RedirectToPage("/AccountDetails/ChangePassword");
            }
            else if (AuthFlow == "ForgotPassword")
            {
                return RedirectToPage("/Account/ForgotPassword");
            }

            // Fallback
            return RedirectToPage("/Index");
        }

        public IActionResult OnPostSendTAC()
        {
            AuthFlow = HttpContext.Session.GetString("AuthFlow");
            var userID = AuthFlow == "ChangePassword"
                ? HttpContext.Session.GetString("UserID") // Already logged in
                : HttpContext.Session.GetString("AuthFlowUserID"); // Temporarily stored for reset

            if (string.IsNullOrEmpty(userID))
            {
                return RedirectToPage("/Account/Login");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserID == userID);
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            if (string.IsNullOrEmpty(Email))
            {
                ErrorMessage = "Email is required to send verification code.";
                return Page();
            }

            if (!IsValidEmail(Email))
            {
                ErrorMessage = "Please enter a valid email address.";
                return Page();
            }

            // Store user's email in session for reference
            HttpContext.Session.SetString("VerificationEmail", Email);

            // Generate a 6-digit TAC
            string tac = GenerateRandomTAC();

            // Save TAC to database
            SaveTACToDatabase(userID, Email, tac);

            // Send TAC via email
            bool emailSent = SendTACEmail(Email, tac);

            if (!emailSent)
            {
                ErrorMessage = "Failed to send verification code. Please try again.";
                return Page();
            }

            SuccessMessage = "Verification code sent successfully!";
            CodeSent = true;
            return Page();
        }
        public IActionResult OnPostVerify()
        {
            AuthFlow = HttpContext.Session.GetString("AuthFlow");
            var userID = AuthFlow == "ChangePassword"
                ? HttpContext.Session.GetString("UserID") 
                : HttpContext.Session.GetString("AuthFlowUserID"); // Temporarily stored for reset

            if (string.IsNullOrEmpty(userID))
            {
                return RedirectToPage("/Account/Login");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserID == userID);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                CodeSent = true;
                return Page();
            }

            // Get the email that was used for verification
            Email = HttpContext.Session.GetString("VerificationEmail");
            if (string.IsNullOrEmpty(Email))
            {
                ErrorMessage = "Email information missing. Please restart the verification process.";
                CodeSent = false;
                return Page();
            }

            CodeSent = true;

            // Validate TAC
            var tacRecord = _context.VerifyTAC
                .FirstOrDefault(t => t.UserID == userID &&
                                  t.TACcode == EnteredTAC &&
                                  t.Email == Email &&
                                  t.ExpiryTime > DateTime.Now &&
                                  !t.IsVerify);

            if (tacRecord == null)
            {
                ErrorMessage = "Invalid or expired verification code.";
                return Page();
            }

            // Mark TAC as used
            tacRecord.IsVerify = true;
            _context.SaveChanges();

            string newPassword = TempData["NewPassword"]?.ToString();
            if (string.IsNullOrEmpty(newPassword))
            {
                ErrorMessage = "Password information is missing. Please try again.";
                return Page();
            }

            // Update the password using ASP.NET Identity's password hasher
            user.Password = _passwordHasher.HashPassword(user, newPassword);
            _context.SaveChanges();

            // Determine redirect path based on auth flow
            string redirectPath;
            string successMessage;

            if (AuthFlow == "ChangePassword")
            {
                redirectPath = "/AccountDetails/ChangePassword";
                successMessage = "Password changed successfully!";
            }
            else // ForgotPassword flow
            {
                redirectPath = "/Account/Login";
                successMessage = "Password reset successfully! You can now log in with your new password.";
            }

            // Clear session variables
            HttpContext.Session.Remove("AuthFlow");
            HttpContext.Session.Remove("VerificationEmail");
            HttpContext.Session.Remove("AuthFlowUserID");

            TempData["SuccessMessage"] = successMessage;
            return RedirectToPage(redirectPath);
        }

        private string GenerateRandomTAC()
        {
            // Use a cryptographically secure random number generator
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                byte[] data = new byte[4];
                rng.GetBytes(data);
                int value = Math.Abs(BitConverter.ToInt32(data, 0));
                return (value % 900000 + 100000).ToString();
            }
        }

        private void SaveTACToDatabase(string userID, string email, string tac)
        {
            // Remove any existing TACs for this user
            var existingTACs = _context.VerifyTAC.Where(t => t.UserID == userID);
            if (existingTACs.Any())
            {
                _context.VerifyTAC.RemoveRange(existingTACs);
            }

            // Add new TAC
            _context.VerifyTAC.Add(new VerifyTAC
            {
                UserID = userID,
                Email = email,
                TACcode = tac,
                CreatedTime = DateTime.Now,
                ExpiryTime = DateTime.Now.AddMinutes(10), // TAC valid for 10 minutes
                IsVerify = false
            });

            _context.SaveChanges();
        }

        private bool SendTACEmail(string email, string tac)
        {
            try
            {
                string smtpServer = _configuration["EmailSettings:SmtpServer"];
                int smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]);
                string smtpUsername = _configuration["EmailSettings:SmtpUsername"];
                string smtpPassword = _configuration["EmailSettings:SmtpPassword"];
                string senderEmail = _configuration["EmailSettings:SenderEmail"];


                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                    client.EnableSsl = true;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(smtpUsername, "Student Enrollment System - No Reply"),
                        Subject = "Password Change Verification Code",
                        Body = $"Your verification code for password change is: {tac}. This code will expire in 10 minutes.",
                        IsBodyHtml = false
                    };

                    mailMessage.To.Add(email);
                    client.Send(mailMessage);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
                return false;
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}