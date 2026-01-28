using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using StudentEnrollmentSystem.Utilities;

namespace StudentEnrollmentSystem.Pages.Enquiry
{
    public class StudentEvaluationModel : PageModel
    {
        public class EnrolledCourseWithEvaluationStatus
        {
            public int EnrollmentID { get; set; }
            public string CourseID { get; set; }
            public string CourseName { get; set; }
            public string Section { get; set; }
            public string Lecturer { get; set; }
            public string Venue { get; set; }
            public string DayOfWeek { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public string Status { get; set; }  // "Pending" or "Completed"
            public DateTime? FilledUpDate { get; set; }
            public string Semester { get; set; }
            public bool IsCurrentSemester { get; set; }

        }

        public List<EnrolledCourseWithEvaluationStatus> EnrolledCoursesWithEvaluationStatus { get; set; }

        private readonly ApplicationDbContext _context;

        public StudentEvaluationModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                return RedirectToPage("/Account/Login");
            }

            var studentInfo = GetStudentInfo(userID);
            if (studentInfo == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var studentID = studentInfo.StudentID;
            var studentProgram = studentInfo.Program;

            // Fetch enrolled courses with evaluation status
            EnrolledCoursesWithEvaluationStatus = GetEnrolledCoursesWithEvaluationStatus(studentID);
            return Page();
        }
        private dynamic GetStudentInfo(string userID)
        {
            return _context.StudentDetails
                .Where(s => s.UserID == userID)
                .Select(s => new { s.StudentID, s.Program })
                .FirstOrDefault();
        }
        private List<EnrolledCourseWithEvaluationStatus> GetEnrolledCoursesWithEvaluationStatus(string studentID)
        {
            var semester = SemesterHelper.GetCurrentSemester();
            return _context.Enrollments
            .Where(e => e.StudentID == studentID)
            .Join(_context.Courses,
                e => e.CourseID,
                c => c.CourseID,
                (e, c) => new { e.EnrollmentID, e.Semester, c.CourseID, c.CourseName, c.Section, c.Lecturer, c.Venue, c.DayOfWeek, c.StartTime, c.EndTime })
            .Join(_context.EvaluationStatuses,
                ec => ec.EnrollmentID,
                es => es.EnrollmentID,
                (ec, es) => new EnrolledCourseWithEvaluationStatus
                {
                    EnrollmentID = ec.EnrollmentID,
                    Semester = ec.Semester,
                    CourseID = ec.CourseID,
                    CourseName = ec.CourseName,
                    Section = ec.Section,
                    Lecturer = ec.Lecturer,
                    Venue = ec.Venue,
                    DayOfWeek = ec.DayOfWeek,
                    StartTime = ec.StartTime,
                    EndTime = ec.EndTime,
                    Status = es.Status,
                    FilledUpDate = es.FilledUpDate,
                    IsCurrentSemester = ec.Semester == semester
                })
            .OrderByDescending(x => x.IsCurrentSemester) // current semester on top
            .AsNoTracking()
            .ToList();

        }
    }
}
