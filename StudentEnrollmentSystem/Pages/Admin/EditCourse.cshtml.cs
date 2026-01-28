using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;

namespace StudentEnrollmentSystem.Pages.Admin
{
    public class EditCourseModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;

        public EditCourseModel(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [BindProperty]
        public Course Course { get; set; }
        public string Message { get; set; }

        public IActionResult OnGet(string courseID)
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            var sql = @"
                SELECT c.CourseID, c.CourseName, c.DayOfWeek, 
                       ISNULL(c.StartTime, '00:00:00') AS StartTime, 
                       ISNULL(c.EndTime, '00:00:00') AS EndTime, 
                       c.Major, ISNULL(c.CreditHours, 0) AS CreditHours, 
                       c.Venue, c.Lecturer, c.Section,
                       ISNULL(c.TotalSeats, 0) AS TotalSeats,
                       ISNULL((SELECT COUNT(*) FROM Enrollments e WHERE e.CourseID = c.CourseID), 0) AS EnrolledCount,
                       ISNULL(c.TotalSeats, 0) - ISNULL((SELECT COUNT(*) FROM Enrollments e WHERE e.CourseID = c.CourseID), 0) AS AvailableSeats,
                       ISNULL(c.Cost, 0) AS Cost
                FROM Courses c
                WHERE c.CourseID = @CourseID";

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@CourseID", courseID);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            Course = new Course
                            {
                                CourseID = reader["CourseID"].ToString(),
                                CourseName = reader["CourseName"].ToString(),
                                DayOfWeek = reader["DayOfWeek"].ToString(),
                                StartTime = TimeSpan.TryParse(reader["StartTime"].ToString(), out TimeSpan st) ? st : TimeSpan.Zero,
                                EndTime = TimeSpan.TryParse(reader["EndTime"].ToString(), out TimeSpan et) ? et : TimeSpan.Zero,
                                Major = reader["Major"].ToString(),
                                CreditHours = Convert.ToInt32(reader["CreditHours"]),
                                Venue = reader["Venue"].ToString(),
                                Lecturer = reader["Lecturer"].ToString(),
                                Section = reader["Section"].ToString(),
                                TotalSeats = Convert.ToInt32(reader["TotalSeats"]),
                                EnrolledCount = Convert.ToInt32(reader["EnrolledCount"]),
                                AvailableSeats = Convert.ToInt32(reader["AvailableSeats"]),
                                Cost = Convert.ToDecimal(reader["Cost"])
                            };
                        }
                        else
                        {
                            return NotFound();
                        }
                    }
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            // Define time constraints
            TimeSpan minTime = new TimeSpan(8, 0, 0);  // 08:00 AM
            TimeSpan maxTime = new TimeSpan(18, 0, 0); // 18:00 PM

            // Validate StartTime and EndTime
            if (Course.StartTime < minTime || Course.StartTime > maxTime)
            {
                ModelState.AddModelError("Course.StartTime", "Class hours should be between 08:00 and 18:00 only.");
            }

            if (Course.EndTime < minTime || Course.EndTime > maxTime)
            {
                ModelState.AddModelError("Course.EndTime", "Class hours should be between 08:00 and 18:00 only.");
            }

            if (Course.EndTime <= Course.StartTime)
            {
                ModelState.AddModelError("Course.EndTime", "End time must be later than start time.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verify course exists
                    var checkSql = "SELECT COUNT(*) FROM Courses WHERE CourseID = @CourseID";
                    using (var command = new SqlCommand(checkSql, connection))
                    {
                        command.Parameters.AddWithValue("@CourseID", Course.CourseID);
                        var count = (int)await command.ExecuteScalarAsync();
                        if (count == 0)
                        {
                            TempData["Message"] = "Course not found.";
                            TempData["MessageType"] = "warning";
                            return RedirectToPage("/Admin/ManageCourses");
                        }
                    }

                    // Update course
                    var updateSql = @"
                        UPDATE Courses
                        SET CourseName = @CourseName,
                            DayOfWeek = @DayOfWeek,
                            StartTime = @StartTime,
                            EndTime = @EndTime,
                            Major = @Major,
                            CreditHours = @CreditHours,
                            Venue = @Venue,
                            Lecturer = @Lecturer,
                            Section = @Section,
                            TotalSeats = @TotalSeats,
                            Cost = @Cost
                        WHERE CourseID = @CourseID";
                    using (var command = new SqlCommand(updateSql, connection))
                    {
                        command.Parameters.AddWithValue("@CourseID", Course.CourseID);
                        command.Parameters.AddWithValue("@CourseName", Course.CourseName);
                        command.Parameters.AddWithValue("@DayOfWeek", Course.DayOfWeek);
                        command.Parameters.AddWithValue("@StartTime", Course.StartTime);
                        command.Parameters.AddWithValue("@EndTime", Course.EndTime);
                        command.Parameters.AddWithValue("@Major", Course.Major);
                        command.Parameters.AddWithValue("@CreditHours", Course.CreditHours);
                        command.Parameters.AddWithValue("@Venue", Course.Venue);
                        command.Parameters.AddWithValue("@Lecturer", Course.Lecturer);
                        command.Parameters.AddWithValue("@Section", Course.Section);
                        command.Parameters.AddWithValue("@TotalSeats", Course.TotalSeats);
                        command.Parameters.AddWithValue("@Cost", Course.Cost);

                        await command.ExecuteNonQueryAsync();
                    }
                }
                TempData["Message"] = "Course updated successfully.";
                TempData["MessageType"] = "success";
                return RedirectToPage("/Admin/ManageCourses");
            }
            catch (Exception ex)
            {
                TempData["Message"] = $"Failed to update course: {ex.Message}";
                TempData["MessageType"] = "danger";
                return Page();
            }
        }

        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("Role");
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UserID")) && role == "Admin";
        }
    }
}