using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using System.Diagnostics;

namespace StudentEnrollmentSystem.Pages.Enquiry
{
    public class EvaluationDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EvaluationDetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public int EnrollmentID { get; set; }
        public string CourseID { get; set; }
        public string CourseName { get; set; }
        public string Lecturer { get; set; }
        public string Status { get; set; }
        public DateTime? FilledUpDate { get; set; }
        [BindProperty]
        public EvaluationResponse EvaluationResponse { get; set; } = new EvaluationResponse();

        public async Task<IActionResult> OnGetAsync(int enrollmentID)
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                return RedirectToPage("/Account/Login");
            }

            var studentID = await _context.StudentDetails
                .Where(s => s.UserID == userID)
                .Select(s => s.StudentID)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(studentID))
            {
                return RedirectToPage("/Account/Login");
            }

            var courseID = await _context.Enrollments
                .Where(e => e.EnrollmentID == enrollmentID && e.StudentID == studentID)
                .Select(e => e.CourseID)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(courseID))
            {
                return RedirectToPage("/Enquiry/StudentEvaluation");
            }

            var course = await _context.Courses
                .Where(c => c.CourseID == courseID)
                .Select(c => new Course
                {
                    CourseID = c.CourseID,
                    CourseName = c.CourseName,
                    Lecturer = c.Lecturer
                })
                .FirstOrDefaultAsync();

            if (course == null)
            {
                return NotFound();
            }

            EnrollmentID = enrollmentID;
            CourseID = course.CourseID;
            CourseName = course.CourseName;
            Lecturer = course.Lecturer;

            var evaluationStatus = await _context.EvaluationStatuses
                .Where(es => es.EnrollmentID == enrollmentID)
                .FirstOrDefaultAsync();

            if (evaluationStatus != null)
            {
                Status = evaluationStatus.Status;
                FilledUpDate = evaluationStatus.FilledUpDate;

                // Check if there's an existing response and load it
                var existingResponse = await _context.EvaluationResponses
                    .Where(er => er.EvaluationStatusID == evaluationStatus.EvaluationStatusID)
                    .FirstOrDefaultAsync();

                if (existingResponse != null)
                {
                    EvaluationResponse = existingResponse;
                }
            }
            else
            {
                Status = "Pending";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => new { x.Key, x.Value.Errors })
                    .ToList();

                foreach (var error in errors)
                {
                    Console.WriteLine($"Error in {error.Key}: {string.Join(", ", error.Errors.Select(e => e.ErrorMessage))}");
                }
                OnGetAsync(EnrollmentID);
                return Page();
            }
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                return RedirectToPage("/Account/Login");
            }

            var studentID = await _context.StudentDetails
                .Where(s => s.UserID == userID)
                .Select(s => s.StudentID)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(studentID))
            {
                return RedirectToPage("/Account/Login");
            }

            // Update or create the evaluation status
            var evaluationStatus = await _context.EvaluationStatuses
                .Where(es => es.EnrollmentID == EnrollmentID)
                .FirstOrDefaultAsync();

            if (evaluationStatus == null)
            {
                // Create a new evaluation status
                evaluationStatus = new EvaluationStatus
                {
                    EnrollmentID = EnrollmentID,
                    Status = "Completed",
                    FilledUpDate = DateTime.Now
                };

                _context.EvaluationStatuses.Add(evaluationStatus);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Update existing evaluation status
                evaluationStatus.Status = "Completed";
                evaluationStatus.FilledUpDate = DateTime.Now;

                _context.EvaluationStatuses.Update(evaluationStatus);
                await _context.SaveChangesAsync();
            }

            // Check if there's an existing response
            var existingResponse = await _context.EvaluationResponses
                .Where(er => er.EvaluationStatusID == evaluationStatus.EvaluationStatusID)
                .FirstOrDefaultAsync();

            // In the OnPostAsync method after you get or create the evaluationStatus

            if (existingResponse == null)
            {
                // Add new response
                EvaluationResponse.EvaluationStatusID = evaluationStatus.EvaluationStatusID;
                EvaluationResponse.EvaluationStatus = evaluationStatus; // Add this line
                _context.EvaluationResponses.Add(EvaluationResponse);
            }
            else
            {
                // Update existing response
                existingResponse.Q1 = EvaluationResponse.Q1;
                existingResponse.Q2 = EvaluationResponse.Q2;
                existingResponse.Q3 = EvaluationResponse.Q3;
                existingResponse.Q4 = EvaluationResponse.Q4;
                _context.EvaluationResponses.Update(existingResponse);
            }

            await _context.SaveChangesAsync();

            return RedirectToPage("/Enquiry/StudentEvaluation"); // Redirect back to evaluation list
        }
    }
}
