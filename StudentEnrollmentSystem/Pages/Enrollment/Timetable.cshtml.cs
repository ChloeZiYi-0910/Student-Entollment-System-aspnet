using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StudentEnrollmentSystem.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace StudentEnrollmentSystem.Pages.Enrollment
{
    public class TimetableModel : PageModel
    {
        private readonly string _connectionString;

        public TimetableModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public List<Course> EnrolledCourses { get; set; } = new();
        public StudentDetail Student { get; set; }
        public string SuccessMessage { get; set; }
        public string CurrentSemester { get; set; }
        public List<string> DaysOfWeek { get; } = new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
        public List<TimeSpan> TimeSlots { get; } = Enumerable.Range(8, 11).Select(h => TimeSpan.FromHours(h)).ToList();

        private string GetUserID()
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                Response.Redirect("/Account/Login");
            }
            return userID;
        }

        public IActionResult OnGet()
        {
            var userID = GetUserID();
            CurrentSemester = GetCurrentSemester();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    // Load student details
                    LoadStudentDetails(connection);

                    if (Student == null)
                    {
                        TempData["ErrorMessage"] = "Please complete your student profile first.";
                        return RedirectToPage("/AccountDetails/StudentDetails");
                    }

                    // Load enrolled courses for the current semester
                    LoadEnrolledCourses(connection);

                    // Check for success message from enrollment
                    if (TempData["SuccessMessage"] != null)
                    {
                        SuccessMessage = TempData["SuccessMessage"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to load timetable: {ex.Message}";
                Console.WriteLine($"OnGet Error: {ex}");
            }
            return Page();
        }

        private void LoadStudentDetails(SqlConnection connection)
        {
            var userID = GetUserID();

            var studentSql = @"
                SELECT StudentID, FirstName, LastName, Program, PersonalEmail, InstitutionalEmail
                FROM StudentDetails
                WHERE UserID = @UserID";
            using (var command = new SqlCommand(studentSql, connection))
            {
                command.Parameters.AddWithValue("@UserID", userID);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Student = new StudentDetail
                        {
                            StudentID = reader.GetString("StudentID"),
                            FirstName = reader.GetString("FirstName"),
                            LastName = reader.GetString("LastName"),
                            Program = reader.GetString("Program"),
                            PersonalEmail = reader.IsDBNull(reader.GetOrdinal("PersonalEmail")) ? null : reader.GetString("PersonalEmail"),
                            InstitutionalEmail = reader.IsDBNull(reader.GetOrdinal("InstitutionalEmail")) ? null : reader.GetString("InstitutionalEmail")
                        };
                    }
                }
            }
        }

        private void LoadEnrolledCourses(SqlConnection connection)
        {
            EnrolledCourses = new List<Course>();
            string studentId = Student.StudentID;

            // Load courses enrolled in the current semester only
            var coursesSql = @"
                SELECT 
                    c.CourseID, 
                    c.CourseName, 
                    c.DayOfWeek, 
                    c.StartTime, 
                    c.EndTime, 
                    c.Venue, 
                    c.Lecturer, 
                    c.Section
                FROM Enrollments e
                INNER JOIN Courses c ON e.CourseID = c.CourseID
                WHERE e.StudentID = @StudentID AND e.Semester = @Semester";
            using (var command = new SqlCommand(coursesSql, connection))
            {
                command.Parameters.AddWithValue("@StudentID", studentId);
                command.Parameters.AddWithValue("@Semester", CurrentSemester);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        EnrolledCourses.Add(new Course
                        {
                            CourseID = reader.GetString("CourseID"),
                            CourseName = reader.GetString("CourseName"),
                            DayOfWeek = reader.GetString("DayOfWeek"),
                            StartTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime")),
                            EndTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime")),
                            Venue = reader.GetString("Venue"),
                            Lecturer = reader.GetString("Lecturer"),
                            Section = reader.GetString("Section")
                        });
                    }
                }
            }
        }

        private string GetCurrentSemester()
        {
            var now = DateTime.Now;
            var currentYear = now.Year;
            var janStart = new DateTime(currentYear, 1, 1);
            var janEnd = new DateTime(currentYear, 2, 1);
            var junStart = new DateTime(currentYear, 6, 1);
            var junEnd = new DateTime(currentYear, 7, 1);

            if (now >= janStart && now <= janEnd)
            {
                return $"JAN{currentYear}";
            }
            else if (now >= junStart && now <= junEnd)
            {
                return $"JUN{currentYear}";
            }
            else
            {
                // Default to the most recent semester if outside enrollment periods
                return now.Month < 6 ? $"JAN{currentYear}" : $"JUN{currentYear}";
            }
        }
    }
}