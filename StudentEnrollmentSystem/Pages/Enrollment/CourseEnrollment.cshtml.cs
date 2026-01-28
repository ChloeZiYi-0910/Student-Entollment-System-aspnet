using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StudentEnrollmentSystem.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace StudentEnrollmentSystem.Pages.Enrollment
{
    public class CourseEnrollmentModel : PageModel
    {
        private readonly string _connectionString;

        public CourseEnrollmentModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public List<Course> Courses { get; set; } = new();
        public List<string> EnrolledCourseIDs { get; set; } = new();
        public bool IsEnrollmentPeriod { get; set; } = true; // Always true for this setup
        public bool IsEnrolled { get; set; }
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public int TotalCreditHours { get; set; }
        public StudentDetail StudentDetail { get; set; }
        public int MaxCreditHours { get; } = 18;

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

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    loadStudentDetails(connection);

                    if (StudentDetail == null)
                    {
                        TempData["ErrorMessage"] = "Please complete your student profile first.";
                        return RedirectToPage("/AccountDetails/StudentDetails");
                    }

                    CheckEnrollments();
                    LoadCourses(connection);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load data: {ex.Message}";
                Console.WriteLine($"OnGet Error: {ex}");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(List<string> selectedCourses)
        {
            var userID = GetUserID();
            OnGet(); // Reload data

            if (IsEnrolled)
            {
                ErrorMessage = "You have already enrolled for this semester.";
                return Page();
            }

            if (selectedCourses == null || selectedCourses.Count == 0)
            {
                ErrorMessage = "Please select at least one course to enroll.";
                return Page();
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    loadStudentDetails(connection);

                    int totalNewCreditHours = 0;
                    foreach (var courseID in selectedCourses)
                    {
                        string courseSql = "SELECT CreditHours FROM Courses WHERE CourseID = @CourseID";
                        using (var command = new SqlCommand(courseSql, connection))
                        {
                            command.Parameters.AddWithValue("@CourseID", courseID);
                            totalNewCreditHours += Convert.ToInt32(await command.ExecuteScalarAsync());
                        }
                    }

                    if (TotalCreditHours + totalNewCreditHours > MaxCreditHours)
                    {
                        ErrorMessage = $"Total credit hours ({TotalCreditHours + totalNewCreditHours}) exceed the maximum allowed ({MaxCreditHours}).";
                        return Page();
                    }

                    string semester = GetCurrentSemester();

                    // Check for timetable conflicts
                    List<(string DayOfWeek, TimeSpan StartTime, TimeSpan EndTime)> selectedCourseTimetable = new();
                    foreach (var courseID in selectedCourses)
                    {
                        string courseTimetableSql = @"
                            SELECT DayOfWeek, StartTime, EndTime
                            FROM Courses
                            WHERE CourseID = @CourseID";
                        using (var command = new SqlCommand(courseTimetableSql, connection))
                        {
                            command.Parameters.AddWithValue("@CourseID", courseID);
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    selectedCourseTimetable.Add((reader.GetString(0), reader.GetTimeSpan(1), reader.GetTimeSpan(2)));
                                }
                            }
                        }
                    }

                    for (int i = 0; i < selectedCourseTimetable.Count; i++)
                    {
                        var (day1, start1, end1) = selectedCourseTimetable[i];
                        for (int j = i + 1; j < selectedCourseTimetable.Count; j++)
                        {
                            var (day2, start2, end2) = selectedCourseTimetable[j];
                            if (day1 == day2 && start1 < end2 && end1 > start2)
                            {
                                ErrorMessage = $"Selected courses have a timetable conflict on {day1}.";
                                return Page();
                            }
                        }
                    }

                    // Check seat availability
                    foreach (var courseID in selectedCourses)
                    {
                        string seatsSql = @"
                            SELECT ISNULL(c.TotalSeats, 30) - ISNULL((
                                SELECT COUNT(*) 
                                FROM Enrollments e 
                                WHERE e.CourseID = c.CourseID
                            ), 0) AS AvailableSeats
                            FROM Courses c
                            WHERE c.CourseID = @CourseID";
                        using (var command = new SqlCommand(seatsSql, connection))
                        {
                            command.Parameters.AddWithValue("@CourseID", courseID);
                            int availableSeats = Convert.ToInt32(await command.ExecuteScalarAsync());
                            if (availableSeats <= 0)
                            {
                                ErrorMessage = $"Course {courseID} is now full.";
                                return Page();
                            }
                        }
                    }

                    // Insert into Invoices and Enrollments
                    string invoiceSql = @"
                        INSERT INTO Invoices (StudentID, Semester, TotalAmount, PaidAmount, IssueDate, InstallmentDueDate, Status)
                        VALUES (@StudentID, @Semester, @TotalAmount, @PaidAmount, @IssueDate, @InstallmentDueDate, @Status);
                        SELECT SCOPE_IDENTITY();";
                    decimal totalAmount = 0;
                    using (var invoiceCommand = new SqlCommand(invoiceSql, connection))
                    {
                        invoiceCommand.Parameters.AddWithValue("@StudentID", StudentDetail.StudentID);
                        invoiceCommand.Parameters.AddWithValue("@Semester", semester);
                        invoiceCommand.Parameters.AddWithValue("@TotalAmount", totalAmount);
                        invoiceCommand.Parameters.AddWithValue("@PaidAmount", 0);
                        invoiceCommand.Parameters.AddWithValue("@IssueDate", DateTime.Now);
                        invoiceCommand.Parameters.AddWithValue("@InstallmentDueDate", DateTime.Now.AddMonths(1));
                        invoiceCommand.Parameters.AddWithValue("@Status", "Pending");

                        var invoiceID = Convert.ToInt32(await invoiceCommand.ExecuteScalarAsync());

                        foreach (var courseID in selectedCourses)
                        {
                            string enrollSql = @"
                                INSERT INTO Enrollments (StudentID, CourseID, LastAction, ActionDate, Semester)
                                VALUES (@StudentID, @CourseID, 'Enrolled', @ActionDate, @Semester);
                                SELECT SCOPE_IDENTITY();";
                            int enrollmentID;
                            using (var command = new SqlCommand(enrollSql, connection))
                            {
                                command.Parameters.AddWithValue("@StudentID", StudentDetail.StudentID);
                                command.Parameters.AddWithValue("@CourseID", courseID);
                                command.Parameters.AddWithValue("@ActionDate", DateTime.Now);
                                command.Parameters.AddWithValue("@Semester", semester);
                                enrollmentID = Convert.ToInt32(await command.ExecuteScalarAsync());
                            }

                            string evalStatusSql = @"
                                INSERT INTO EvaluationStatus (EnrollmentID, Status, FilledUpDate)
                                VALUES (@EnrollmentID, @Status, @FilledUpDate);";
                            using (var evalCmd = new SqlCommand(evalStatusSql, connection))
                            {
                                evalCmd.Parameters.AddWithValue("@EnrollmentID", enrollmentID);
                                evalCmd.Parameters.AddWithValue("@Status", "Pending");
                                evalCmd.Parameters.AddWithValue("@FilledUpDate", DBNull.Value);
                                await evalCmd.ExecuteNonQueryAsync();
                            }

                            decimal courseFee = GetCourseFee(courseID, connection);
                            string invoiceDetailSql = @"
                                INSERT INTO InvoiceDetails (InvoiceID, CourseID, CourseFee)
                                VALUES (@InvoiceID, @CourseID, @CourseFee)";
                            using (var invoiceDetailCommand = new SqlCommand(invoiceDetailSql, connection))
                            {
                                invoiceDetailCommand.Parameters.AddWithValue("@InvoiceID", invoiceID);
                                invoiceDetailCommand.Parameters.AddWithValue("@CourseID", courseID);
                                invoiceDetailCommand.Parameters.AddWithValue("@CourseFee", courseFee);
                                await invoiceDetailCommand.ExecuteNonQueryAsync();
                            }
                            totalAmount += courseFee;
                        }

                        string updateInvoiceSql = "UPDATE Invoices SET TotalAmount = @TotalAmount WHERE InvoiceID = @InvoiceID";
                        using (var updateInvoiceCommand = new SqlCommand(updateInvoiceSql, connection))
                        {
                            updateInvoiceCommand.Parameters.AddWithValue("@TotalAmount", totalAmount);
                            updateInvoiceCommand.Parameters.AddWithValue("@InvoiceID", invoiceID);
                            await updateInvoiceCommand.ExecuteNonQueryAsync();
                        }
                    }

                    TempData["SuccessMessage"] = "Courses enrolled successfully!";
                    return RedirectToPage("/Enrollment/Timetable");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to enroll: {ex.Message}";
                Console.WriteLine($"OnPostEnroll Error: {ex}");
            }
            return Page();
        }

        private decimal GetCourseFee(string courseID, SqlConnection connection)
        {
            string query = "SELECT Cost FROM Courses WHERE CourseID = @CourseID";
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@CourseID", courseID);
                object result = command.ExecuteScalar();
                return result != null ? Convert.ToDecimal(result) : 0;
            }
        }

        private void loadStudentDetails(SqlConnection connection)
        {
            var userID = GetUserID();

            if (connection.State != ConnectionState.Open)
                connection.Open();

            var studentSql = @"
                SELECT StudentID, FirstName, LastName, Program, PersonalEmail, PhoneNumber, DateOfBirth
                FROM StudentDetails
                WHERE UserID = @UserID";
            using (var command = new SqlCommand(studentSql, connection))
            {
                command.Parameters.AddWithValue("@UserID", userID);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        bool isProfileIncomplete = reader.IsDBNull(reader.GetOrdinal("PersonalEmail")) ||
                                                  reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ||
                                                  (reader.GetDateTime(reader.GetOrdinal("DateOfBirth")) == new DateTime(1, 1, 1));

                        if (isProfileIncomplete)
                        {
                            StudentDetail = null;
                            ErrorMessage = "Student profile is incomplete.";
                            return;
                        }

                        StudentDetail = new StudentDetail
                        {
                            StudentID = reader.GetString("StudentID"),
                            FirstName = reader.GetString("FirstName"),
                            LastName = reader.GetString("LastName"),
                            Program = reader.GetString("Program")
                        };
                    }
                    else
                    {
                        StudentDetail = null;
                        ErrorMessage = "Student details not found.";
                        return;
                    }
                }
            }
        }

        private void LoadCourses(SqlConnection connection)
        {
            if (connection.State != ConnectionState.Open)
                connection.Open();

            Courses = new List<Course>();
            string studentId = StudentDetail.StudentID;
            string studentProgram = StudentDetail.Program;

            // Load courses the student has NEVER enrolled in
            var coursesSql = @"
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
                        WHERE e.CourseID = c.CourseID
                    ), 0) AS AvailableSeats
                FROM Courses c
                WHERE c.Major = @Major
                AND c.CourseID NOT IN (
                    SELECT e.CourseID 
                    FROM Enrollments e 
                    WHERE e.StudentID = @StudentID
                )";

            using (var command = new SqlCommand(coursesSql, connection))
            {
                command.Parameters.AddWithValue("@Major", studentProgram);
                command.Parameters.AddWithValue("@StudentID", studentId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Courses.Add(new Course
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

        private void CheckEnrollments()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    string semester = GetCurrentSemester();

                    string enrollmentSql = @"
                        SELECT TOP 1 EnrollmentID
                        FROM Enrollments
                        WHERE StudentID = @StudentID AND Semester = @Semester";
                    using (SqlCommand enrollmentCmd = new SqlCommand(enrollmentSql, connection))
                    {
                        enrollmentCmd.Parameters.AddWithValue("@StudentID", StudentDetail.StudentID);
                        enrollmentCmd.Parameters.AddWithValue("@Semester", semester);
                        var result = enrollmentCmd.ExecuteScalar();
                        IsEnrolled = result != null;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to check enrollments: {ex.Message}";
                Console.WriteLine($"CheckEnrollments Error: {ex}");
            }
        }

        public string GetCurrentSemester()
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
            else
            {
                // This shouldn't happen with a full year range, but included for completeness
                return $"JUN{currentYear}";
            }
        }
    }
}