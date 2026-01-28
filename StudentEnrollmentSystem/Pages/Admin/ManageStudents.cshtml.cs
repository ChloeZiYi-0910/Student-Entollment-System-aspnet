using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;

namespace StudentEnrollmentSystem.Pages.Admin
{
    public class ManageStudentsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ManageStudentsModel(ApplicationDbContext context)
        {
            _context = context;
        }
        public class StudentViewModel
        {
            public string UserID { get; set; }
            public string StudentID { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string? Password { get; set; }
            public string Program { get; set; }
        }

        [BindProperty]
        public StudentViewModel NewStudent { get; set; } = new StudentViewModel();
        public List<User> Students { get; set; }
        public bool IsEditMode { get; set; }

        public async Task<IActionResult> OnGetAsync(string? id = null)
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            Students = await _context.Users
                .Where(u => u.Role == "Student")
                .ToListAsync();

            // If an ID is provided, we're in edit mode
            if (!string.IsNullOrEmpty(id))
            {
                IsEditMode = true;
                var studentUser = await _context.Users
                    .Include(u => u.StudentDetail)
                    .FirstOrDefaultAsync(u => u.UserID == id);

                if (studentUser != null && studentUser.StudentDetail != null)
                {
                    NewStudent = new StudentViewModel
                    {
                        UserID = studentUser.UserID,
                        StudentID = studentUser.StudentDetail.StudentID,
                        FirstName = studentUser.StudentDetail.FirstName,
                        LastName = studentUser.StudentDetail.LastName,
                        Program = studentUser.StudentDetail.Program,
                        // Don't set password as we don't want to display it
                    };
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAddStudentAsync()
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            if (_context.Users.Any(u => u.UserID == NewStudent.UserID))
            {
                TempData["Message"] = "User ID already exists.";
                TempData["MessageType"] = "danger";
                Students = _context.Users.Where(u => u.Role == "Student").ToList();
                return Page();
            }

            // Creating User record
            var studentUser = new User
            {
                UserID = NewStudent.UserID,
                FullName = NewStudent.LastName + " " + NewStudent.FirstName,
                Password = NewStudent.Password, // Remember to hash the password in production
                Role = "Student"
            };
            var passwordHasher = new PasswordHasher<User>();
            studentUser.Password = passwordHasher.HashPassword(studentUser, NewStudent.Password);

            try
            {
                _context.Users.Add(studentUser);
                await _context.SaveChangesAsync(); // Save User first to get the UserID assigned

                // Creating Student record
                var institutionalEmail = NewStudent.StudentID + "@university.edu";
                var student = new StudentDetail
                {
                    StudentID = NewStudent.StudentID,
                    UserID = NewStudent.UserID,
                    FirstName = NewStudent.FirstName,
                    LastName = NewStudent.LastName,
                    PersonalEmail = null, // Ensure this field is populated from the form
                    InstitutionalEmail = institutionalEmail,
                    PhoneNumber = null, // Ensure this field is populated from the form
                    //Address = null, // Ensure this field is populated from the form
                    Program = NewStudent.Program,
                    Status = "Active",
                    EnrollmentDate = DateTime.Now,
                    CGPA = 0.00m // Or set a default value
                };

                _context.StudentDetails.Add(student);
                await _context.SaveChangesAsync(); // Save Student record

                TempData["Message"] = $"Student {NewStudent.FirstName} {NewStudent.LastName} added successfully.";
                TempData["MessageType"] = "success";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                // Handle any exceptions that might occur during database operations
                TempData["Message"] = "Error adding student: " + ex.Message;
                TempData["MessageType"] = "danger";
                Students = _context.Users.Where(u => u.Role == "Student").ToList();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostEditStudentAsync()
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            try
            {
                // Disable triggers set up on Users or StudentDetails tables
                await _context.Database.ExecuteSqlRawAsync("DISABLE TRIGGER ALL ON Users");
                await _context.Database.ExecuteSqlRawAsync("DISABLE TRIGGER ALL ON StudentDetails");

                var studentUser = await _context.Users
                    .Include(u => u.StudentDetail)
                    .FirstOrDefaultAsync(u => u.UserID == NewStudent.UserID);

                if (studentUser == null)
                {
                    TempData["Message"] = "User not found.";
                    TempData["MessageType"] = "danger";
                    Students = await _context.Users.Where(u => u.Role == "Student").ToListAsync();
                    return Page();
                }

                studentUser.FullName = NewStudent.LastName + " " + NewStudent.FirstName;

                if (studentUser.StudentDetail != null)
                {
                    studentUser.StudentDetail.FirstName = NewStudent.FirstName;
                    studentUser.StudentDetail.LastName = NewStudent.LastName;
                    studentUser.StudentDetail.Program = NewStudent.Program;
                }

                // Only update password if a new one is provided
                if (!string.IsNullOrEmpty(NewStudent.Password))
                {
                    var passwordHasher = new PasswordHasher<User>();
                    studentUser.Password = passwordHasher.HashPassword(studentUser, NewStudent.Password);
                }

                try
                {
                    await _context.SaveChangesAsync();
                    TempData["Message"] = $"Student {NewStudent.FirstName} {NewStudent.LastName} updated successfully.";
                    TempData["MessageType"] = "success";
                    return RedirectToPage();
                }
                catch (Exception ex)
                {
                    TempData["Message"] = "Error updating student: " + ex.Message;
                    TempData["MessageType"] = "danger";
                    Students = await _context.Users.Where(u => u.Role == "Student").ToListAsync();
                    return Page();
                }

            }
            catch (Exception ex)
            {
                TempData["Message"] = "Error updating student: " + ex.Message;
                TempData["MessageType"] = "danger";
                Students = await _context.Users.Where(u => u.Role == "Student").ToListAsync();
                return Page();
            }
            finally
            {
                // Re-enable triggers
                await _context.Database.ExecuteSqlRawAsync("ENABLE TRIGGER ALL ON Users");
                await _context.Database.ExecuteSqlRawAsync("ENABLE TRIGGER ALL ON StudentDetails");
            }
        }

        public async Task<IActionResult> OnPostDeleteStudentAsync(string id)
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            var student = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id && u.Role == "Student");
            if (student == null) return NotFound();

            _context.Users.Remove(student);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"Student {student.FullName} deleted successfully.";
            TempData["MessageType"] = "success";
            return RedirectToPage();
        }

        public IActionResult OnPostCancelEdit()
        {
            return RedirectToPage();
        }

        private bool IsAdmin() => HttpContext.Session.GetString("Role") == "Admin" && !string.IsNullOrEmpty(HttpContext.Session.GetString("UserID"));
    }
}