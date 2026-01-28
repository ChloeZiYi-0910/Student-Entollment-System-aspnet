using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StudentEnrollmentSystem.Models;
using System.Data;

namespace StudentEnrollmentSystem.Pages.Admin
{
    public class ViewEnquiriesModel : PageModel
    {
        private readonly string _connectionString;

        public ViewEnquiriesModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public List<StudentEnquiry> Enquiries { get; set; } = new();

        public IActionResult OnGet()
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    var sql = @"
                        SELECT e.EnquiryID, e.UserID, e.Category, e.Subject, e.Message, e.CreatedDate,
                               COUNT(er.ResponseID) AS ResponseCount
                        FROM Enquiries e
                        LEFT JOIN EnquiryResponses er ON e.EnquiryID = er.EnquiryID
                        GROUP BY e.EnquiryID, e.UserID, e.Category, e.Subject, e.Message, e.CreatedDate
                        ORDER BY e.CreatedDate DESC";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var enquiry = new StudentEnquiry
                                {
                                    EnquiryID = reader.GetInt32("EnquiryID"),
                                    UserID = reader.GetString("UserID"),
                                    Category = reader.GetString("Category"),
                                    Subject = reader.GetString("Subject"),
                                    Message = reader.GetString("Message"),
                                    CreatedDate = reader.GetDateTime("CreatedDate"),
                                    Responses = new List<EnquiryResponse>()
                                };

                                int responseCount = reader.GetInt32("ResponseCount");
                                if (responseCount > 0)
                                {
                                    enquiry.Responses.Add(new EnquiryResponse());
                                }

                                Enquiries.Add(enquiry);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnGet: Failed to load enquiries - {ex.Message}");
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