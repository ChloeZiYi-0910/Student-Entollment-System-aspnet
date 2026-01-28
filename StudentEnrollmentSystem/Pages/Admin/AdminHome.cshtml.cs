using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace StudentEnrollmentSystem.Pages.Admin
{
    public class AdminHomeModel : PageModel
    {
        private readonly string _connectionString;

        public AdminHomeModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public int TotalStudents { get; set; }
        public int TotalCourses { get; set; }
        public int PendingRequests { get; set; }
        public int PendingPayments { get; set; }
        public int PendingEnquiries { get; set; }
        public string ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            if (!IsAdmin())
            {
                Debug.WriteLine("AdminHome: Unauthorized access, redirecting to Login");
                return RedirectToPage("/Account/Login");
            }

            Debug.WriteLine("AdminHome: Loading dashboard data");
            LoadDashboardData();
            return Page();
        }

        private void LoadDashboardData()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    // Total Students
                    var studentsSql = "SELECT COUNT(*) FROM StudentDetails";
                    using (var command = new SqlCommand(studentsSql, connection))
                    {
                        TotalStudents = (int)command.ExecuteScalar();
                    }
                    Debug.WriteLine($"AdminHome: TotalStudents={TotalStudents}");

                    // Total Courses
                    var coursesSql = "SELECT COUNT(*) FROM Courses";
                    using (var command = new SqlCommand(coursesSql, connection))
                    {
                        TotalCourses = (int)command.ExecuteScalar();
                    }
                    Debug.WriteLine($"AdminHome: TotalCourses={TotalCourses}");

                    // Pending Requests
                    var requestsSql = "SELECT COUNT(*) FROM EnrollmentRequests WHERE Status = @Status";
                    using (var command = new SqlCommand(requestsSql, connection))
                    {
                        command.Parameters.AddWithValue("@Status", "Pending");
                        PendingRequests = (int)command.ExecuteScalar();
                    }
                    Debug.WriteLine($"AdminHome: PendingRequests={PendingRequests}");

                    // Pending Payments
                    var paymentsSql = "SELECT COUNT(*) FROM Payments WHERE Status = @Status AND PaymentMethod = 'Bank Transfer'";
                    using (var command = new SqlCommand(paymentsSql, connection))
                    {
                        command.Parameters.AddWithValue("@Status", "Pending");
                        PendingPayments = (int)command.ExecuteScalar();
                    }
                    Debug.WriteLine($"AdminHome: PendingPayments={PendingPayments}");

                    // Pending Enquiries
                    var enquirySql = "SELECT COUNT(*) FROM Enquiries WHERE Status = @Status";
                    using (var command = new SqlCommand(enquirySql, connection))
                    {
                        command.Parameters.AddWithValue("@Status", "Pending");
                        PendingEnquiries = (int)command.ExecuteScalar();
                    }
                    Debug.WriteLine($"AdminHome: PendingEnquiries={PendingEnquiries}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load dashboard data: {ex.Message}";
                Debug.WriteLine($"AdminHome: Error - {ex.Message}");
            }
        }

        private bool IsAdmin()
        {
            var userID = HttpContext.Session.GetString("UserID");
            var role = HttpContext.Session.GetString("Role");
            Debug.WriteLine($"AdminHome: Checking admin status - UserID={userID}, Role={role}");
            return !string.IsNullOrEmpty(userID) && role == "Admin";
        }
    }
}