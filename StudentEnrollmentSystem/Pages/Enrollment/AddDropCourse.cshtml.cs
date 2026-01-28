using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StudentEnrollmentSystem.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace StudentEnrollmentSystem.Pages.Enrollment
{
    public class AddDropCourseModel : PageModel
    {
        private readonly string _connectionString;

        public AddDropCourseModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public List<Course> AvailableCourses { get; set; } = new();
        public List<Course> EnrolledCourses { get; set; } = new();
        public List<string> PendingAddCourses { get; set; } = new();
        public List<string> PendingDropCourses { get; set; } = new();
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public int TotalCreditHours { get; set; }
        public StudentDetail StudentDetail { get; set; }
        public bool IsEnrolled { get; set; }
        public int MaxCreditHours { get; } = 18;
        public string CurrentSemester { get; set; }

        [BindProperty]
        public string AddReason { get; set; }
        [BindProperty]
        public string DropReason { get; set; }

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

                    if (StudentDetail == null)
                    {
                        ErrorMessage = "Student details not found.";
                        return Page();
                    }

                    // Check if enrolled in current semester
                    CheckEnrollments(connection);

                    if (!IsEnrolled)
                    {
                        return Page(); // Modal will redirect
                    }

                    // Load available, enrolled, and pending courses
                    LoadAvailableCourses(connection);
                    LoadEnrolledCourses(connection);
                    LoadPendingRequests(connection);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load data: {ex.Message}";
                Console.WriteLine($"OnGet Error: {ex}");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAddAsync(string courseID)
        {
            var userID = GetUserID();
            OnGet();

            if (!IsEnrolled)
            {
                return Page(); // Modal will redirect
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Check if course is already pending
                    if (PendingAddCourses.Contains(courseID))
                    {
                        ErrorMessage = "This course is already pending approval.";
                        return Page();
                    }

                    // Load course details
                    var courseSql = @"
                        SELECT 
                            CourseName, 
                            CreditHours, 
                            DayOfWeek, 
                            StartTime, 
                            EndTime, 
                            ISNULL(TotalSeats, 30) AS TotalSeats,
                            ISNULL(TotalSeats, 30) - ISNULL((
                                SELECT COUNT(*) 
                                FROM Enrollments e 
                                WHERE e.CourseID = c.CourseID AND e.Semester = @Semester
                            ), 0) AS AvailableSeats
                        FROM Courses c
                        WHERE CourseID = @CourseID";
                    string courseName;
                    int creditHours;
                    int availableSeats;
                    TimeSpan startTime, endTime;
                    string dayOfWeek;
                    using (var command = new SqlCommand(courseSql, connection))
                    {
                        command.Parameters.AddWithValue("@CourseID", courseID);
                        command.Parameters.AddWithValue("@Semester", CurrentSemester);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (!reader.Read())
                            {
                                ErrorMessage = "Course not found.";
                                return Page();
                            }
                            courseName = reader.GetString("CourseName");
                            creditHours = reader.GetInt32("CreditHours");
                            availableSeats = reader.GetInt32("AvailableSeats");
                            dayOfWeek = reader.GetString("DayOfWeek");
                            startTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime"));
                            endTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime"));
                        }
                    }

                    if (availableSeats <= 0)
                    {
                        ErrorMessage = $"Course {courseName} is full. No seats available.";
                        return Page();
                    }

                    // Check total credit hours for current semester
                    if (TotalCreditHours + creditHours > MaxCreditHours)
                    {
                        ErrorMessage = $"Adding {courseName} exceeds the {MaxCreditHours}-credit limit for {CurrentSemester}.";
                        return Page();
                    }

                    // Check timetable conflict for current semester
                    if (HasScheduleConflict(StudentDetail.StudentID, courseID, connection))
                    {
                        ErrorMessage = $"The requested course {courseName} has a schedule conflict with your current timetable for {CurrentSemester}.";
                        return Page();
                    }

                    if (string.IsNullOrWhiteSpace(AddReason))
                    {
                        ErrorMessage = "Please provide a reason for adding the course.";
                        return Page();
                    }

                    // Submit add request with Semester
                    var requestSql = @"
                        INSERT INTO EnrollmentRequests (StudentID, CourseID, Action, Reason, RequestDate, Status, Semester)
                        VALUES (@StudentID, @CourseID, 'Add', @Reason, @RequestDate, 'Pending', @Semester)";
                    using (var command = new SqlCommand(requestSql, connection))
                    {
                        command.Parameters.AddWithValue("@StudentID", StudentDetail.StudentID);
                        command.Parameters.AddWithValue("@CourseID", courseID);
                        command.Parameters.AddWithValue("@Reason", AddReason);
                        command.Parameters.AddWithValue("@RequestDate", DateTime.Now);
                        command.Parameters.AddWithValue("@Semester", CurrentSemester);
                        await command.ExecuteNonQueryAsync();
                    }

                    SuccessMessage = $"Add request for {courseName} submitted for {CurrentSemester}. Status: Waiting for admin approval.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to submit add request: {ex.Message}";
                Console.WriteLine($"OnPostAdd Error: {ex}");
            }

            OnGet(); // Refresh data
            return Page();
        }

        public async Task<IActionResult> OnPostDropAsync(string courseID)
        {
            var userID = GetUserID();
            OnGet();

            if (!IsEnrolled)
            {
                return Page(); // Modal will redirect
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Check if course is already pending drop
                    if (PendingDropCourses.Contains(courseID))
                    {
                        ErrorMessage = "This course is already pending approval for dropping.";
                        return Page();
                    }

                    // Load course details
                    var courseSql = @"
                        SELECT CourseName
                        FROM Courses
                        WHERE CourseID = @CourseID";
                    string courseName;
                    using (var command = new SqlCommand(courseSql, connection))
                    {
                        command.Parameters.AddWithValue("@CourseID", courseID);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (!reader.Read())
                            {
                                ErrorMessage = "Course not found.";
                                return Page();
                            }
                            courseName = reader.GetString("CourseName");
                        }
                    }

                    // Check if enrolled in current semester
                    var checkSql = @"
                        SELECT COUNT(*) 
                        FROM Enrollments 
                        WHERE StudentID = @StudentID AND CourseID = @CourseID AND Semester = @Semester";
                    using (var command = new SqlCommand(checkSql, connection))
                    {
                        command.Parameters.AddWithValue("@StudentID", StudentDetail.StudentID);
                        command.Parameters.AddWithValue("@CourseID", courseID);
                        command.Parameters.AddWithValue("@Semester", CurrentSemester);
                        var count = (int)await command.ExecuteScalarAsync();
                        if (count == 0)
                        {
                            ErrorMessage = $"You are not enrolled in {courseName} for {CurrentSemester}.";
                            return Page();
                        }
                    }

                    if (string.IsNullOrWhiteSpace(DropReason))
                    {
                        ErrorMessage = "Please provide a reason for dropping the course.";
                        return Page();
                    }

                    // Submit drop request with Semester
                    var requestSql = @"
                        INSERT INTO EnrollmentRequests (StudentID, CourseID, Action, Reason, RequestDate, Status, Semester)
                        VALUES (@StudentID, @CourseID, 'Drop', @Reason, @RequestDate, 'Pending', @Semester)";
                    using (var command = new SqlCommand(requestSql, connection))
                    {
                        command.Parameters.AddWithValue("@StudentID", StudentDetail.StudentID);
                        command.Parameters.AddWithValue("@CourseID", courseID);
                        command.Parameters.AddWithValue("@Reason", DropReason);
                        command.Parameters.AddWithValue("@RequestDate", DateTime.Now);
                        command.Parameters.AddWithValue("@Semester", CurrentSemester);
                        await command.ExecuteNonQueryAsync();
                    }

                    SuccessMessage = $"Drop request for {courseName} submitted for {CurrentSemester}. Status: Waiting for admin approval.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to submit drop request: {ex.Message}";
                Console.WriteLine($"OnPostDrop Error: {ex}");
            }

            OnGet(); // Refresh data
            return Page();
        }

        private void LoadStudentDetails(SqlConnection connection)
        {
            var userID = GetUserID();
            var studentSql = @"
                SELECT StudentID, FirstName, LastName, Program
                FROM StudentDetails
                WHERE UserID = @UserID";
            using (var command = new SqlCommand(studentSql, connection))
            {
                command.Parameters.AddWithValue("@UserID", userID);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        StudentDetail = new StudentDetail
                        {
                            StudentID = reader.GetString("StudentID"),
                            FirstName = reader.GetString("FirstName"),
                            LastName = reader.GetString("LastName"),
                            Program = reader.GetString("Program")
                        };
                    }
                }
            }
        }

        private void LoadAvailableCourses(SqlConnection connection)
        {
            var availableSql = @"
                SELECT 
                    c.CourseID, 
                    c.CourseName, 
                    c.CreditHours, 
                    c.Major, 
                    c.DayOfWeek, 
                    c.StartTime, 
                    c.EndTime, 
                    c.Venue, 
                    c.Lecturer, 
                    c.Section,
                    ISNULL(c.TotalSeats, 30) AS TotalSeats,
                    ISNULL(c.TotalSeats, 30) - ISNULL((
                        SELECT COUNT(*) 
                        FROM Enrollments e 
                        WHERE e.CourseID = c.CourseID AND e.Semester = @Semester
                    ), 0) AS AvailableSeats
                FROM Courses c
                WHERE c.Major = @Major
                AND c.CourseID NOT IN (
                    SELECT e.CourseID 
                    FROM Enrollments e 
                    WHERE e.StudentID = @StudentID
                )";
            using (var command = new SqlCommand(availableSql, connection))
            {
                command.Parameters.AddWithValue("@Major", StudentDetail.Program);
                command.Parameters.AddWithValue("@StudentID", StudentDetail.StudentID);
                command.Parameters.AddWithValue("@Semester", CurrentSemester); // For seat count
                using (var reader = command.ExecuteReader())
                {
                    AvailableCourses = new List<Course>();
                    while (reader.Read())
                    {
                        AvailableCourses.Add(new Course
                        {
                            CourseID = reader.GetString("CourseID"),
                            CourseName = reader.GetString("CourseName"),
                            CreditHours = reader.GetInt32("CreditHours"),
                            Major = reader.GetString("Major"),
                            DayOfWeek = reader.GetString("DayOfWeek"),
                            StartTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime")),
                            EndTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime")),
                            Venue = reader.GetString("Venue"),
                            Lecturer = reader.GetString("Lecturer"),
                            Section = reader.GetString("Section"),
                            TotalSeats = reader.GetInt32("TotalSeats"),
                            AvailableSeats = reader.GetInt32("AvailableSeats")
                        });
                    }
                }
            }
        }

        private void LoadEnrolledCourses(SqlConnection connection)
        {
            var enrolledSql = @"
                SELECT 
                    c.CourseID, 
                    c.CourseName, 
                    c.CreditHours, 
                    c.Major, 
                    c.DayOfWeek, 
                    c.StartTime, 
                    c.EndTime, 
                    c.Venue, 
                    c.Lecturer, 
                    c.Section,
                    ISNULL(c.TotalSeats, 30) AS TotalSeats,
                    ISNULL(c.TotalSeats, 30) - ISNULL((
                        SELECT COUNT(*) 
                        FROM Enrollments e2 
                        WHERE e2.CourseID = c.CourseID AND e2.Semester = @Semester
                    ), 0) AS AvailableSeats
                FROM Enrollments e
                INNER JOIN Courses c ON e.CourseID = c.CourseID
                WHERE e.StudentID = @StudentID AND e.Semester = @Semester";
            using (var command = new SqlCommand(enrolledSql, connection))
            {
                command.Parameters.AddWithValue("@StudentID", StudentDetail.StudentID);
                command.Parameters.AddWithValue("@Semester", CurrentSemester);
                using (var reader = command.ExecuteReader())
                {
                    EnrolledCourses = new List<Course>();
                    TotalCreditHours = 0;
                    while (reader.Read())
                    {
                        EnrolledCourses.Add(new Course
                        {
                            CourseID = reader.GetString("CourseID"),
                            CourseName = reader.GetString("CourseName"),
                            CreditHours = reader.GetInt32("CreditHours"),
                            Major = reader.GetString("Major"),
                            DayOfWeek = reader.GetString("DayOfWeek"),
                            StartTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime")),
                            EndTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime")),
                            Venue = reader.GetString("Venue"),
                            Lecturer = reader.GetString("Lecturer"),
                            Section = reader.GetString("Section"),
                            TotalSeats = reader.GetInt32("TotalSeats"),
                            AvailableSeats = reader.GetInt32("AvailableSeats")
                        });
                        TotalCreditHours += reader.GetInt32("CreditHours");
                    }
                }
            }
        }

        private void LoadPendingRequests(SqlConnection connection)
        {
            var pendingSql = @"
                SELECT CourseID, Action
                FROM EnrollmentRequests
                WHERE StudentID = @StudentID  AND Status = 'Pending'";
            using (var command = new SqlCommand(pendingSql, connection))
            {
                command.Parameters.AddWithValue("@StudentID", StudentDetail.StudentID);
                command.Parameters.AddWithValue("@Semester", CurrentSemester);
                using (var reader = command.ExecuteReader())
                {
                    PendingAddCourses = new List<string>();
                    PendingDropCourses = new List<string>();
                    while (reader.Read())
                    {
                        var courseID = reader.GetString("CourseID");
                        var action = reader.GetString("Action");
                        if (action == "Add")
                            PendingAddCourses.Add(courseID);
                        else if (action == "Drop")
                            PendingDropCourses.Add(courseID);
                    }
                }
            }
        }

        private bool HasScheduleConflict(string studentId, string newCourseId, SqlConnection connection)
        {
            var courseSql = @"
                SELECT DayOfWeek, StartTime, EndTime
                FROM Courses
                WHERE CourseID = @CourseID";
            string newDayOfWeek = null;
            TimeSpan newStartTime = TimeSpan.Zero, newEndTime = TimeSpan.Zero;
            using (var command = new SqlCommand(courseSql, connection))
            {
                command.Parameters.AddWithValue("@CourseID", newCourseId);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        newDayOfWeek = reader.GetString("DayOfWeek");
                        newStartTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime"));
                        newEndTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime"));
                    }
                }
            }

            if (newDayOfWeek == null)
                return false;

            var enrolledSql = @"
                SELECT c.DayOfWeek, c.StartTime, c.EndTime
                FROM Enrollments e
                INNER JOIN Courses c ON e.CourseID = c.CourseID
                WHERE e.StudentID = @StudentID AND e.Semester = @Semester";
            using (var command = new SqlCommand(enrolledSql, connection))
            {
                command.Parameters.AddWithValue("@StudentID", studentId);
                command.Parameters.AddWithValue("@Semester", CurrentSemester);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string enrolledDay = reader.GetString("DayOfWeek");
                        TimeSpan enrolledStart = reader.GetTimeSpan(reader.GetOrdinal("StartTime"));
                        TimeSpan enrolledEnd = reader.GetTimeSpan(reader.GetOrdinal("EndTime"));

                        if (enrolledDay == newDayOfWeek &&
                            !(newEndTime <= enrolledStart || newStartTime >= enrolledEnd))
                        {
                            return true; // Conflict detected
                        }
                    }
                }
            }
            return false;
        }

        private void CheckEnrollments(SqlConnection connection)
        {
            string enrollmentSql = @"
                SELECT TOP 1 EnrollmentID
                FROM Enrollments
                WHERE StudentID = @StudentID AND Semester = @Semester";
            using (var command = new SqlCommand(enrollmentSql, connection))
            {
                command.Parameters.AddWithValue("@StudentID", StudentDetail.StudentID);
                command.Parameters.AddWithValue("@Semester", CurrentSemester);
                var result = command.ExecuteScalar();
                IsEnrolled = result != null;
            }
        }

        private string GetCurrentSemester()
        {
            var now = DateTime.Now;
            var currentYear = now.Year;
            var janStart = new DateTime(currentYear, 1, 1);
            var mayEnd = new DateTime(currentYear, 5, 31);
            var junStart = new DateTime(currentYear, 6, 1);
            var decEnd = new DateTime(currentYear, 12, 31);

            if (now >= janStart && now <= mayEnd)
            {
                return $"JAN{currentYear}";
            }
            else if (now >= junStart && now <= decEnd)
            {
                return $"JUN{currentYear}";
            }
            return $"JUN{currentYear}"; // Fallback
        }
    }
}