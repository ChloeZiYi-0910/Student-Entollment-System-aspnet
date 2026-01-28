using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using System.Diagnostics;

namespace StudentEnrollmentSystem.Pages.Admin
{
    public class ManageCoursesModel : PageModel
    {
        private readonly string _connectionString;
        private readonly ApplicationDbContext _context;

        public ManageCoursesModel(IConfiguration configuration, ApplicationDbContext context)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _context = context;
        }

        public List<Course> Courses { get; set; } = new List<Course>();
        [BindProperty]
        public Course NewCourse { get; set; }
        public string Message { get; set; }

        public IActionResult OnGet()
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            LoadCourses();
            return Page();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            // Ensure StartTime and EndTime are within 08:00 - 18:00
            TimeSpan minTime = new TimeSpan(8, 0, 0);  // 08:00 AM
            TimeSpan maxTime = new TimeSpan(18, 0, 0); // 18:00 PM

            if (NewCourse.StartTime < minTime || NewCourse.StartTime > maxTime)
            {
                ModelState.AddModelError("NewCourse.StartTime", "Class hours should be between 0800 to 1800 only");
            }

            if (NewCourse.EndTime < minTime || NewCourse.EndTime > maxTime)
            {
                ModelState.AddModelError("NewCourse.EndTime", "Class hours should be between 0800 to 1800 only");
            }

            // Ensure end time is later than start time
            if (NewCourse.EndTime <= NewCourse.StartTime)
            {
                ModelState.AddModelError("NewCourse.EndTime", "End time is earlier than start time.");
            }
            if (!ModelState.IsValid)
            {
                LoadCourses();
                return Page();
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var checkSql = "SELECT COUNT(*) FROM Courses WHERE CourseID = @CourseID";
                    using (var command = new SqlCommand(checkSql, connection))
                    {
                        command.Parameters.AddWithValue("@CourseID", NewCourse.CourseID);
                        var count = (int)await command.ExecuteScalarAsync();
                        if (count > 0)
                        {
                            TempData["Message"] = "Course ID already exists.";
                            TempData["MessageType"] = "danger";
                            LoadCourses();
                            return Page();
                        }
                    }

                    if (!TimeSpan.TryParse(NewCourse.StartTime.ToString(), out TimeSpan startTime) ||
                        !TimeSpan.TryParse(NewCourse.EndTime.ToString(), out TimeSpan endTime))
                    {
                        TempData["Message"] = "Invalid time format for Start Time or End Time.";
                        TempData["MessageType"] = "danger";
                        LoadCourses();
                        return Page();
                    }

                    var insertSql = @"
                        INSERT INTO Courses (CourseID, CourseName, DayOfWeek, StartTime, EndTime, Major, CreditHours, Venue, Lecturer, Section, TotalSeats, Cost)
                        VALUES (@CourseID, @CourseName, @DayOfWeek, @StartTime, @EndTime, @Major, @CreditHours, @Venue, @Lecturer, @Section, @TotalSeats, @Cost)";
                    using (var command = new SqlCommand(insertSql, connection))
                    {
                        command.Parameters.AddWithValue("@CourseID", NewCourse.CourseID);
                        command.Parameters.AddWithValue("@CourseName", NewCourse.CourseName);
                        command.Parameters.AddWithValue("@DayOfWeek", NewCourse.DayOfWeek);
                        command.Parameters.AddWithValue("@StartTime", startTime);
                        command.Parameters.AddWithValue("@EndTime", endTime);
                        command.Parameters.AddWithValue("@Major", NewCourse.Major);
                        command.Parameters.AddWithValue("@CreditHours", NewCourse.CreditHours);
                        command.Parameters.AddWithValue("@Venue", NewCourse.Venue);
                        command.Parameters.AddWithValue("@Lecturer", NewCourse.Lecturer);
                        command.Parameters.AddWithValue("@Section", NewCourse.Section);
                        command.Parameters.AddWithValue("@TotalSeats", NewCourse.TotalSeats);
                        command.Parameters.AddWithValue("@Cost", NewCourse.Cost);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                TempData["Message"] = "Course added successfully.";
                TempData["MessageType"] = "success";
            }
            catch (Exception ex)
            {
                TempData["Message"] = $"Failed to delete course: {ex.Message}";
                TempData["MessageType"] = "danger";
                LoadCourses();
                return Page();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string courseID)
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            // Check if there are any enrollments for this course
            var enrollmentCount = await _context.Enrollments
                .CountAsync(e => e.CourseID == courseID);

            if (enrollmentCount > 0)
            {
                TempData["Message"] = "Cannot delete course. Students are still enrolled.";
                TempData["MessageType"] = "danger"; // To style the alert
                return RedirectToPage(); // Redirect to reload page and display message
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var checkSql = "SELECT COUNT(*) FROM Courses WHERE CourseID = @CourseID";
                    using (var command = new SqlCommand(checkSql, connection))
                    {
                        command.Parameters.AddWithValue("@CourseID", courseID);
                        var count = (int)await command.ExecuteScalarAsync();
                        if (count == 0)
                        {
                            TempData["Message"] = "Course not found.";
                            TempData["MessageType"] = "warning";
                            return RedirectToPage();
                        }
                    }

                    var deleteSql = "DELETE FROM Courses WHERE CourseID = @CourseID";
                    using (var command = new SqlCommand(deleteSql, connection))
                    {
                        command.Parameters.AddWithValue("@CourseID", courseID);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                TempData["Message"] = "Course deleted successfully.";
                TempData["MessageType"] = "success";
            }
            catch (Exception ex)
            {
                TempData["Message"] = $"Failed to delete course: {ex.Message}";
                TempData["MessageType"] = "danger";
            }

            return RedirectToPage();
        }

        private void LoadCourses()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var sql = @"
                        SELECT 
                            c.CourseID, 
                            c.CourseName, 
                            c.DayOfWeek, 
                            ISNULL(c.StartTime, '00:00:00') AS StartTime, 
                            ISNULL(c.EndTime, '00:00:00') AS EndTime, 
                            c.Major, 
                            ISNULL(c.CreditHours, 0) AS CreditHours, 
                            c.Venue, 
                            c.Lecturer, 
                            c.Section,
                            ISNULL(c.TotalSeats, 0) AS TotalSeats,
                            ISNULL((
                                SELECT COUNT(*) 
                                FROM Enrollments e 
                                WHERE e.CourseID = c.CourseID
                            ), 0) AS EnrolledCount,
                            ISNULL(c.Cost, 0.00) AS Cost
                        FROM Courses c";
                    using (var command = new SqlCommand(sql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                try
                                {
                                    string startTimeStr = reader["StartTime"]?.ToString() ?? "00:00:00";
                                    string endTimeStr = reader["EndTime"]?.ToString() ?? "00:00:00";
                                    TimeSpan startTime = TimeSpan.TryParse(startTimeStr, out TimeSpan st) ? st : TimeSpan.Zero;
                                    TimeSpan endTime = TimeSpan.TryParse(endTimeStr, out TimeSpan et) ? et : TimeSpan.Zero;

                                    int creditHours = reader["CreditHours"] != DBNull.Value && int.TryParse(reader["CreditHours"].ToString(), out int ch) ? ch : 0;
                                    int totalSeats = reader["TotalSeats"] != DBNull.Value && int.TryParse(reader["TotalSeats"].ToString(), out int ts) ? ts : 0;
                                    int enrolledCount = reader["EnrolledCount"] != DBNull.Value && int.TryParse(reader["EnrolledCount"].ToString(), out int ec) ? ec : 0;
                                    decimal cost = reader["Cost"] != DBNull.Value && decimal.TryParse(reader["Cost"].ToString(), out decimal c) ? c : 0;

                                    Courses.Add(new Course
                                    {
                                        CourseID = reader["CourseID"]?.ToString() ?? "",
                                        CourseName = reader["CourseName"]?.ToString() ?? "",
                                        DayOfWeek = reader["DayOfWeek"]?.ToString() ?? "",
                                        StartTime = startTime,
                                        EndTime = endTime,
                                        Major = reader["Major"]?.ToString() ?? "",
                                        CreditHours = creditHours,
                                        Venue = reader["Venue"]?.ToString() ?? "",
                                        Lecturer = reader["Lecturer"]?.ToString() ?? "",
                                        Section = reader["Section"]?.ToString() ?? "",
                                        TotalSeats = totalSeats,
                                        EnrolledCount = enrolledCount,
                                        Cost = cost
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error parsing course data for CourseID {reader["CourseID"]}: {ex.Message}");
                                    Message += $"Error loading course {reader["CourseID"]}: {ex.Message}; ";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadCourses failed: {ex.Message}");
                Message = $"Failed to load courses: {ex.Message}";
            }
        }

        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("Role");
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UserID")) && role == "Admin";
        }
    }
}