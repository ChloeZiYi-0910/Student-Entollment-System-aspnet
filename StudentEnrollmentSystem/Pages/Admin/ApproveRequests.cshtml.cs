using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using StudentEnrollmentSystem.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace StudentEnrollmentSystem.Pages.Enrollment
{
    public class ApproveRequestsModel : PageModel
    {
        private readonly string _connectionString;
        private readonly ApplicationDbContext _context;
        public ApproveRequestsModel(IConfiguration configuration, ApplicationDbContext context)
        {
            _context = context;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public List<EnrollmentRequest> PendingRequests { get; set; } = new();
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public string CurrentSemester { get; set; }

        public IActionResult OnGet()
        {
            if (!IsAdmin())
            {
                return RedirectToPage("/Home");
            }

            CurrentSemester = GetCurrentSemester();

            try
            {
                LoadPendingRequests();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load requests: {ex.Message}";
                Console.WriteLine($"OnGet Error: {ex}");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(int requestId)
        {
            if (!IsAdmin())
            {
                return RedirectToPage("/Home");
            }

            CurrentSemester = GetCurrentSemester();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Load request details
                    var requestSql = @"
                        SELECT StudentID, CourseID, Action
                        FROM EnrollmentRequests
                        WHERE RequestID = @RequestID AND Status = 'Pending'";
                    string studentId, courseId, action;
                    using (var command = new SqlCommand(requestSql, connection))
                    {
                        command.Parameters.AddWithValue("@RequestID", requestId);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (!reader.Read())
                            {
                                ErrorMessage = "Request not found or already processed.";
                                LoadPendingRequests();
                                return Page();
                            }
                            studentId = reader.GetString("StudentID");
                            courseId = reader.GetString("CourseID");
                            action = reader.GetString("Action");
                        }
                    }

                    // Load course name for messaging
                    var courseSql = "SELECT CourseName FROM Courses WHERE CourseID = @CourseID";
                    string courseName;
                    using (var command = new SqlCommand(courseSql, connection))
                    {
                        command.Parameters.AddWithValue("@CourseID", courseId);
                        courseName = (string)await command.ExecuteScalarAsync();
                    }

                    if (action == "Add")
                    {
                        await ProcessAddRequest(connection, studentId, courseId, courseName, requestId);
                    }
                    else if (action == "Drop")
                    {
                        await ProcessDropRequest(connection, studentId, courseId, courseName);
                    }

                    // Update request status
                    var updateSql = @"
                        UPDATE EnrollmentRequests
                        SET Status = 'Approved', ProcessedDate = @ProcessedDate, ProcessedBy = @ProcessedBy
                        WHERE RequestID = @RequestID";
                    using (var command = new SqlCommand(updateSql, connection))
                    {
                        command.Parameters.AddWithValue("@RequestID", requestId);
                        command.Parameters.AddWithValue("@ProcessedDate", DateTime.Now);
                        command.Parameters.AddWithValue("@ProcessedBy", HttpContext.Session.GetString("UserID"));
                        await command.ExecuteNonQueryAsync();
                    }

                    SuccessMessage = $"{action} request for {courseName} ({courseId}) approved for {CurrentSemester}.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to approve request: {ex.Message}";
                Console.WriteLine($"OnPostApprove Error: {ex}");
            }

            LoadPendingRequests();
            return Page();
        }

        public async Task<IActionResult> OnPostRejectAsync(int requestId)
        {
            if (!IsAdmin())
            {
                return RedirectToPage("/Home");
            }

            CurrentSemester = GetCurrentSemester();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Load request details
                    var requestSql = @"
                        SELECT CourseID, Action
                        FROM EnrollmentRequests
                        WHERE RequestID = @RequestID AND Status = 'Pending'";
                    string courseId, action;
                    using (var command = new SqlCommand(requestSql, connection))
                    {
                        command.Parameters.AddWithValue("@RequestID", requestId);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (!reader.Read())
                            {
                                ErrorMessage = "Request not found or already processed.";
                                LoadPendingRequests();
                                return Page();
                            }
                            courseId = reader.GetString("CourseID");
                            action = reader.GetString("Action");
                        }
                    }

                    // Load course name for messaging
                    var courseSql = "SELECT CourseName FROM Courses WHERE CourseID = @CourseID";
                    string courseName;
                    using (var command = new SqlCommand(courseSql, connection))
                    {
                        command.Parameters.AddWithValue("@CourseID", courseId);
                        courseName = (string)await command.ExecuteScalarAsync();
                    }

                    // Update request status to Rejected
                    var updateSql = @"
                        UPDATE EnrollmentRequests
                        SET Status = 'Rejected', ProcessedDate = @ProcessedDate, ProcessedBy = @ProcessedBy
                        WHERE RequestID = @RequestID";
                    using (var command = new SqlCommand(updateSql, connection))
                    {
                        command.Parameters.AddWithValue("@RequestID", requestId);
                        command.Parameters.AddWithValue("@ProcessedDate", DateTime.Now);
                        command.Parameters.AddWithValue("@ProcessedBy", HttpContext.Session.GetString("UserID"));
                        await command.ExecuteNonQueryAsync();
                    }

                    SuccessMessage = $"{action} request for {courseName} ({courseId}) rejected.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to reject request: {ex.Message}";
                Console.WriteLine($"OnPostReject Error: {ex}");
            }

            LoadPendingRequests();
            return Page();
        }

        private async Task ProcessAddRequest(SqlConnection connection, string studentId, string courseId, string courseName, int requestId)
        {
            // Check if already enrolled in current semester
            var checkSql = @"
                SELECT COUNT(*) 
                FROM Enrollments 
                WHERE StudentID = @StudentID AND CourseID = @CourseID AND Semester = @Semester";
            using (var command = new SqlCommand(checkSql, connection))
            {
                command.Parameters.AddWithValue("@StudentID", studentId);
                command.Parameters.AddWithValue("@CourseID", courseId);
                command.Parameters.AddWithValue("@Semester", CurrentSemester);
                var count = (int)await command.ExecuteScalarAsync();
                if (count > 0)
                {
                    throw new Exception($"Student is already enrolled in {courseName} for {CurrentSemester}.");
                }
            }

            // Check available seats for current semester
            var seatsSql = @"
                SELECT 
                    ISNULL(TotalSeats, 30) AS TotalSeats,
                    ISNULL(TotalSeats, 30) - ISNULL((
                        SELECT COUNT(*) 
                        FROM Enrollments e 
                        WHERE e.CourseID = c.CourseID AND e.Semester = @Semester
                    ), 0) AS AvailableSeats
                FROM Courses c
                WHERE CourseID = @CourseID";
            int availableSeats;
            using (var command = new SqlCommand(seatsSql, connection))
            {
                command.Parameters.AddWithValue("@CourseID", courseId);
                command.Parameters.AddWithValue("@Semester", CurrentSemester);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!reader.Read() || reader.GetInt32("AvailableSeats") <= 0)
                    {
                        throw new Exception($"No available seats for {courseName} in {CurrentSemester}.");
                    }
                    availableSeats = reader.GetInt32("AvailableSeats");
                }
            }

            // Check credit hours for current semester
            var creditSql = @"
                SELECT SUM(c.CreditHours) AS TotalCredits
                FROM Enrollments e
                INNER JOIN Courses c ON e.CourseID = c.CourseID
                WHERE e.StudentID = @StudentID AND e.Semester = @Semester";
            int totalCredits;
            using (var command = new SqlCommand(creditSql, connection))
            {
                command.Parameters.AddWithValue("@StudentID", studentId);
                command.Parameters.AddWithValue("@Semester", CurrentSemester);
                var result = await command.ExecuteScalarAsync();
                totalCredits = result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }

            var courseCreditSql = "SELECT CreditHours FROM Courses WHERE CourseID = @CourseID";
            int courseCredits;
            using (var command = new SqlCommand(courseCreditSql, connection))
            {
                command.Parameters.AddWithValue("@CourseID", courseId);
                courseCredits = (int)await command.ExecuteScalarAsync();
            }

            if (totalCredits + courseCredits > 18)
            {
                throw new Exception($"Adding {courseName} exceeds the 18-credit limit for {CurrentSemester}.");
            }

            // Check timetable conflict for current semester
            if (await HasScheduleConflict(connection, studentId, courseId))
            {
                throw new Exception($"Schedule conflict detected for {courseName} with existing courses in {CurrentSemester}.");
            }

            int insertedId = 0;
            // Add to Enrollments for current semester
            var insertSql = @"
                INSERT INTO Enrollments (StudentID, CourseID, Semester, LastAction, ActionDate)
                OUTPUT INSERTED.EnrollmentID
                VALUES (@StudentID, @CourseID, @Semester, 'Enrolled', @ActionDate)";
            using (var command = new SqlCommand(insertSql, connection))
            {
                command.Parameters.AddWithValue("@StudentID", studentId);
                command.Parameters.AddWithValue("@CourseID", courseId);
                command.Parameters.AddWithValue("@Semester", CurrentSemester);
                command.Parameters.AddWithValue("@ActionDate", DateTime.Now);

                insertedId = (int)await command.ExecuteScalarAsync(); // Get EnrollmentID
            }

            //Get Invoice from student
            var invoice = _context.Invoices.FirstOrDefault(
                i => i.StudentID == studentId && i.Semester == CurrentSemester);

            if (invoice == null)
            {
                throw new Exception($"No invoice founded for student {studentId}");
            }

            //Retrieve the cost of relevant course
            decimal courseCost = _context.Courses
                .Where(c => c.CourseID == courseId)
                .Select(c => c.Cost)
                .FirstOrDefault();
            if (courseCost <= 0)
            {
                throw new Exception($"No cost founded for {courseId}");
            }

            // Insert into Invoice Details Table
            var invoiceDetailSql = @"
                INSERT INTO InvoiceDetails (InvoiceID, CourseID, CourseFee)
                VALUES (@InvoiceID, @CourseID, @CourseFee)";
            using (var command = new SqlCommand(invoiceDetailSql, connection))
            {
                command.Parameters.AddWithValue("@InvoiceID", invoice.InvoiceID);
                command.Parameters.AddWithValue("@CourseID", courseId);
                command.Parameters.AddWithValue("@CourseFee", courseCost);
                await command.ExecuteNonQueryAsync();
            }

            //Update Student Invoice
            var updateInvoiceSql = @"
                UPDATE Invoices 
                SET TotalAmount = (SELECT SUM(CourseFee) FROM InvoiceDetails WHERE InvoiceID = @InvoiceID)
                WHERE InvoiceID = @InvoiceID";
            using (var command = new SqlCommand(updateInvoiceSql, connection))
            {
                command.Parameters.AddWithValue("@InvoiceID", invoice.InvoiceID);
                await command.ExecuteNonQueryAsync();
            }

            // Insert EvaluationStatus (after getting EnrollmentID)
            var insertEvaluationStatusSql = @"
                INSERT INTO EvaluationStatus (EnrollmentID, Status, FilledUpDate)
                VALUES (@EnrollmentID, @Status, @FilledUpDate)";
            using (var command = new SqlCommand(insertEvaluationStatusSql, connection))
            {
                command.Parameters.AddWithValue("@EnrollmentID", insertedId);
                command.Parameters.AddWithValue("@Status", "Pending");
                command.Parameters.AddWithValue("@FilledUpDate", DBNull.Value);
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task ProcessDropRequest(SqlConnection connection, string studentId, string courseId, string courseName)
        {
            // Check if enrolled in current semester
            var checkSql = @"
                SELECT COUNT(*) 
                FROM Enrollments 
                WHERE StudentID = @StudentID AND CourseID = @CourseID AND Semester = @Semester";
            using (var command = new SqlCommand(checkSql, connection))
            {
                command.Parameters.AddWithValue("@StudentID", studentId);
                command.Parameters.AddWithValue("@CourseID", courseId);
                command.Parameters.AddWithValue("@Semester", CurrentSemester);
                var count = (int)await command.ExecuteScalarAsync();
                if (count == 0)
                {
                    throw new Exception($"Student is not enrolled in {courseName} for {CurrentSemester}.");
                }
            }

            //Get Invoice of Student in Current Semester
            var invoice = _context.Invoices
                .FirstOrDefault(i => i.StudentID == studentId && i.Semester == CurrentSemester);
            if (invoice == null)
            {
                throw new Exception($"No invoice founded for student {studentId}");
            }

            // Delete from InvoiceDetails
            var deleteInvoiceDetailSql = @"
                DELETE FROM InvoiceDetails 
                WHERE InvoiceID = @InvoiceID AND CourseID = @CourseID";
            using (var command = new SqlCommand(deleteInvoiceDetailSql, connection))
            {
                command.Parameters.AddWithValue("@InvoiceID", invoice.InvoiceID);
                command.Parameters.AddWithValue("@CourseID", courseId);
                await command.ExecuteNonQueryAsync();
            }

            var updateInvoiceSql = @"
                UPDATE Invoices 
                SET TotalAmount = (SELECT SUM(CourseFee) FROM InvoiceDetails WHERE InvoiceID = @InvoiceID)
                WHERE InvoiceID = @InvoiceID";
            using (var command = new SqlCommand(updateInvoiceSql, connection))
            {
                command.Parameters.AddWithValue("@InvoiceID", invoice.InvoiceID);
                await command.ExecuteNonQueryAsync();
            }

            var dropEvaluationAndEnrollmentSql = @"
                DELETE FROM EvaluationStatus WHERE EnrollmentID = 
                (SELECT EnrollmentID FROM Enrollments WHERE StudentID = @StudentID AND CourseID = @CourseID);
                DELETE FROM Enrollments WHERE StudentID = @StudentID AND CourseID = @CourseID";
            using (var command = new SqlCommand(dropEvaluationAndEnrollmentSql, connection))
            {
                command.Parameters.AddWithValue("@StudentID", studentId);
                command.Parameters.AddWithValue("@CourseID", courseId);
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task<bool> HasScheduleConflict(SqlConnection connection, string studentId, string newCourseId)
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
                using (var reader = await command.ExecuteReaderAsync())
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
                using (var reader = await command.ExecuteReaderAsync())
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

        private void LoadPendingRequests()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var sql = @"
                    SELECT 
                        r.RequestID, 
                        r.StudentID, 
                        r.CourseID, 
                        r.Action, 
                        r.Reason, 
                        r.RequestDate,
                        c.CourseName,
                        s.FirstName + ' ' + s.LastName AS StudentName
                    FROM EnrollmentRequests r
                    INNER JOIN Courses c ON r.CourseID = c.CourseID
                    INNER JOIN StudentDetails s ON r.StudentID = s.StudentID
                    WHERE r.Status = 'Pending'
                    ORDER BY r.RequestDate";
                using (var command = new SqlCommand(sql, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        PendingRequests = new List<EnrollmentRequest>();
                        while (reader.Read())
                        {
                            PendingRequests.Add(new EnrollmentRequest
                            {
                                RequestID = reader.GetInt32("RequestID"),
                                StudentID = reader.GetString("StudentID"),
                                CourseID = reader.GetString("CourseID"),
                                Action = reader.GetString("Action"),
                                Reason = reader.GetString("Reason"),
                                RequestDate = reader.GetDateTime("RequestDate"),
                                CourseName = reader.GetString("CourseName"),
                                StudentName = reader.GetString("StudentName")
                            });
                        }
                    }
                }
            }
        }

        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("Role");
            return role == "Admin"; // Adjust based on your auth logic
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

    public class EnrollmentRequest
    {
        public int RequestID { get; set; }
        public string StudentID { get; set; }
        public string CourseID { get; set; }
        public string Action { get; set; }
        public string Reason { get; set; }
        public DateTime RequestDate { get; set; }
        public string CourseName { get; set; }
        public string StudentName { get; set; }
    }
}