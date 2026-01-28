using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StudentEnrollmentSystem.Models;
using System.Data;

namespace StudentEnrollmentSystem.Pages.Admin
{
    public class ViewEnrollmentsModel : PageModel
    {
        private readonly string _connectionString;

        public ViewEnrollmentsModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public List<StudentDetail> Students { get; set; } = new();
        public StudentDetail SelectedStudent { get; set; }
        public List<StudentEnrollment> Enrollments { get; set; } = new();

        public IActionResult OnGet(string? studentID = null)
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    // Load all students with enrollments
                    var studentsSql = @"
                        SELECT 
                            sd.StudentID, 
                            sd.FirstName, 
                            sd.LastName,
                            COUNT(se.EnrollmentID) AS TotalCourses,
                            ISNULL(SUM(c.CreditHours), 0) AS TotalCreditHours
                        FROM StudentDetails sd
                        INNER JOIN Enrollments se ON sd.StudentID = se.StudentID
                        LEFT JOIN Courses c ON se.CourseID = c.CourseID
                        GROUP BY sd.StudentID, sd.FirstName, sd.LastName
                        ORDER BY sd.StudentID";

                    using (var command = new SqlCommand(studentsSql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Students.Add(new StudentDetail
                                {
                                    StudentID = reader.GetString("StudentID"),
                                    FirstName = reader.GetString("FirstName"),
                                    LastName = reader.GetString("LastName"),
                                    TotalCourses = reader.GetInt32("TotalCourses"),
                                    TotalCreditHours = reader.GetInt32("TotalCreditHours")
                                });
                            }
                        }
                    }

                    // If a specific studentID is provided, load their details
                    if (!string.IsNullOrEmpty(studentID))
                    {
                        var studentSql = @"
                            SELECT StudentID, FirstName, LastName
                            FROM StudentDetails
                            WHERE StudentID = @StudentID";

                        using (var studentCommand = new SqlCommand(studentSql, connection))
                        {
                            studentCommand.Parameters.AddWithValue("@StudentID", studentID);

                            using (var reader = studentCommand.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    SelectedStudent = new StudentDetail
                                    {
                                        StudentID = reader.GetString("StudentID"),
                                        FirstName = reader.GetString("FirstName"),
                                        LastName = reader.GetString("LastName")
                                    };
                                }
                            }
                        }

                        if (SelectedStudent != null)
                        {
                            var enrollmentSql = @"
                                SELECT 
                                    se.EnrollmentID, 
                                    se.StudentID, 
                                    se.CourseID, 
                                    se.LastAction, 
                                    se.ActionDate,
                                    se.Semester,
                                    c.CourseName, 
                                    c.CreditHours
                                FROM Enrollments se
                                INNER JOIN Courses c ON se.CourseID = c.CourseID
                                WHERE se.StudentID = @StudentID
                                ORDER BY se.ActionDate";

                            using (var enrollmentCommand = new SqlCommand(enrollmentSql, connection))
                            {
                                enrollmentCommand.Parameters.AddWithValue("@StudentID", studentID);

                                using (var reader = enrollmentCommand.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        Enrollments.Add(new StudentEnrollment
                                        {
                                            EnrollmentID = reader.GetInt32("EnrollmentID"),
                                            StudentID = reader.GetString("StudentID"),
                                            CourseID = reader.GetString("CourseID"),
                                            Semester = reader.GetString("Semester"),
                                            LastAction = reader.IsDBNull("LastAction") ? null : reader.GetString("LastAction"),
                                            ActionDate = reader.IsDBNull("ActionDate") ? null : reader.GetDateTime("ActionDate"),
                                            Course = new Course
                                            {
                                                CourseID = reader.GetString("CourseID"),
                                                CourseName = reader.GetString("CourseName"),
                                                CreditHours = reader.GetInt32("CreditHours")
                                            }
                                        });
                                    }
                                }
                            }

                            // Set totals for the selected student
                            SelectedStudent.TotalCourses = Enrollments.Count;
                            SelectedStudent.TotalCreditHours = Enrollments.Sum(e => e.Course.CreditHours);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnGet: Failed to load data - {ex.Message}");
            }

            return Page();
        }

        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetString("UserID");
            return role == "Admin" && !string.IsNullOrEmpty(userId);
        }
    }
}