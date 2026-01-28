using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace StudentEnrollmentSystem.Pages.Enrollment
{
    public class AddDropHistoryModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AddDropHistoryModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<EnrollmentRequestViewModel> History { get; set; }
        public string ErrorMessage { get; set; }

        public void OnGet()
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                Response.Redirect("/Account/Login");
                return;
            }

            var studentDetail = _context.StudentDetails.FirstOrDefault(sd => sd.UserID == userID);
            if (studentDetail == null)
            {
                ErrorMessage = "Student details not found.";
                return;
            }
            var studentId = studentDetail.StudentID;

            var sql = "SELECT RequestID, StudentID, CourseID, Action, Reason, RequestDate, Status " +
                      "FROM EnrollmentRequests WHERE StudentID = @StudentID " +
                      "ORDER BY RequestDate DESC";
            try
            {
                History = _context.Database.SqlQueryRaw<EnrollmentRequestViewModel>(sql,
                    new SqlParameter("@StudentID", studentId))
                    .ToList();

                if (History.Count == 0)
                {
                    ErrorMessage = "No add/drop request history found.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load history. Error: " + ex.Message;
                Console.WriteLine("SQL Error: " + ex.ToString());
                History = new List<EnrollmentRequestViewModel>();
            }
        }
    }

    public class EnrollmentRequestViewModel
    {
        public int RequestID { get; set; }
        public string StudentID { get; set; }
        public string CourseID { get; set; }
        public string Action { get; set; }
        public string Reason { get; set; }
        public DateTime? RequestDate { get; set; }
        public string Status { get; set; }
    }
}