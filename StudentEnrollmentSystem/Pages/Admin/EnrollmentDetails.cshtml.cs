using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace StudentEnrollmentSystem.Pages.Admin
{
    public class EnrollmentDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EnrollmentDetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public User Student { get; set; }
        public List<StudentEnrollment> Enrollments { get; set; }
        [BindProperty(SupportsGet = true)]
        public string StudentID { get; set; }

        public IActionResult OnGet()
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            Student = _context.Users
                .FirstOrDefault(u => u.UserID == StudentID && u.Role == "Student");

            if (Student == null)
            {
                return Page(); // Shows "Student not found" message
            }

            Enrollments = _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.StudentID == StudentID)
                .OrderBy(e => e.CourseID)
                .ToList();

            return Page();
        }

        private bool IsAdmin() => HttpContext.Session.GetString("Role") == "Admin" && !string.IsNullOrEmpty(HttpContext.Session.GetString("UserID"));
    }
}