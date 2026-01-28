using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using StudentEnrollmentSystem.Utilities;
using System.Threading.Tasks;

namespace StudentEnrollmentSystem.Pages.AccountDetails
{
    public class StudentDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public StudentDetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public StudentDetail StudentDetails { get; set; }

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public string UniqueEmailMessage { get; set; }
        public string UniquePhoneNumberMessage { get; set; }
        public bool IsFirstSemester { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var UserID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(UserID))
            {
                return RedirectToPage("/Account/Login");
            }

            // Get user creation date to determine if first semester
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == UserID);
            if (user != null)
            {
                IsFirstSemester = SemesterHelper.IsFirstSemester(user.CreatedDate);
                ViewData["IsFirstSemester"] = IsFirstSemester;
            }

            StudentDetails = await _context.StudentDetails
                .Where(sd => sd.UserID == UserID)
                .SingleOrDefaultAsync() ?? new StudentDetail();

            // For first semester students, we could display a message or handle CGPA differently
            if (IsFirstSemester)
            {
                ViewData["CGPAMessage"] = "CGPA is not applicable for first semester students.";    
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var UserID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(UserID))
            {
                return RedirectToPage("/Account/Login");
            }

            // Check if student is in first semester
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == UserID);
            if (user != null)
            {
                IsFirstSemester = SemesterHelper.IsFirstSemester(user.CreatedDate);

                // For first semester students, CGPA set to null or 0.0
                if (IsFirstSemester)
                {
                    StudentDetails.CGPA = null; // or you could use 0.0m
                }
            }
            // Prevent validation of unrelated navigation properties
            ModelState.Remove("StudentDetails.User");
            ModelState.Remove("StudentDetails.Status");
            ModelState.Remove("StudentDetails.UserID");
            ModelState.Remove("StudentDetails.StudentID");
            ModelState.Remove("StudentDetails.Program");
            ModelState.Remove("StudentDetails.InstitutionalEmail");
            ModelState.Remove("StudentDetails.Invoices");
            ModelState.Remove("StudentDetails.Enquiries");
            ModelState.Remove("StudentDetails.BankDetail");
            ModelState.Remove("StudentDetails.Enrollments");
            ModelState.Remove("StudentDetails.PaymentHistories");
            ModelState.Remove("StudentDetails.ContactInformation");
            ModelState.Remove("StudentDetails.EnrollmentRequests");
            ModelState.Remove("StudentDetails.EnrollmentHistories");

            if (!ModelState.IsValid)
            {
                foreach (var modelError in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine(modelError.ErrorMessage);
                }
                ErrorMessage = "Please check the form for errors.";
                return Page();
            }
            // Check if PersonalEmail is already used by another student
            var existingEmail = await _context.StudentDetails
                .Where(sd => sd.UserID != UserID && sd.PersonalEmail == StudentDetails.PersonalEmail)
                .FirstOrDefaultAsync();
            if (existingEmail != null)
            {
                UniqueEmailMessage = "This email is already in use by another student.";
            }

            // Check if PhoneNumber is already used by another student
            var existingPhoneNumber = await _context.StudentDetails
                .Where(sd => sd.UserID != UserID && sd.PhoneNumber == StudentDetails.PhoneNumber)
                .FirstOrDefaultAsync();
            if (existingPhoneNumber != null)
            {
                UniquePhoneNumberMessage = "This phone number is already in use by another student.";   
            }

            //allow to update or save personal details if email and phone number is unique
            if (string.IsNullOrEmpty(UniqueEmailMessage) && string.IsNullOrEmpty(UniquePhoneNumberMessage))
            {
                try
                {
                    // Check if StudentDetails record exists
                    var studentdetailsExist = await _context.StudentDetails.AnyAsync(sd => sd.UserID == UserID);

                    if (studentdetailsExist)
                    {
                        // Update StudentDetails using SQL
                        string updateDetailsSql = @"
                        UPDATE StudentDetails 
                        SET 
                            FirstName = @FirstName,
                            LastName = @LastName,
                            DateOfBirth = @DateOfBirth,
                            PersonalEmail = @PersonalEmail,
                            PhoneNumber = @PhoneNumber,
                            CGPA = @CGPA
                       WHERE UserID = @UserID";
                        if (IsFirstSemester)
                        {
                            StudentDetails.CGPA = 0.0m;
                        }

                        int detailsRowsUpdated = await _context.Database.ExecuteSqlRawAsync(updateDetailsSql,
                            new SqlParameter("@FirstName", StudentDetails.FirstName),
                            new SqlParameter("@LastName", StudentDetails.LastName),
                            new SqlParameter("@DateOfBirth", StudentDetails.DateOfBirth),
                            new SqlParameter("@PersonalEmail", StudentDetails.PersonalEmail),
                            new SqlParameter("@PhoneNumber", StudentDetails.PhoneNumber),
                            new SqlParameter("@CGPA", StudentDetails.CGPA),
                            new SqlParameter("@UserID", UserID));

                        // Update User's FullName
                        string updateUserSql = @"
                        UPDATE Users 
                        SET 
                            FullName = @FullName
                       WHERE UserID = @UserID";

                        int userRowsUpdated = await _context.Database.ExecuteSqlRawAsync(updateUserSql,
                            new SqlParameter("@FullName", StudentDetails.LastName + " " + StudentDetails.FirstName),
                            new SqlParameter("@UserID", UserID));
                    }

                    SuccessMessage = "Your changes have been saved successfully.";
                }
                catch (SqlException ex)
                {
                    // Log the specific SQL error
                    ErrorMessage = "Database error occurred. Please try again." + ex.Message;
                }
                catch (Exception ex)
                {
                    // Log the general error
                    ErrorMessage = "An error occurred while saving your changes.";
                }
            }

            // Reload the data
            await OnGetAsync();

            return Page();
        }
    }
}
