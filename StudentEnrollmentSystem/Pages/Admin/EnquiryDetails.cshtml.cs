using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StudentEnrollmentSystem.Models;
using System.Data;

namespace StudentEnrollmentSystem.Pages.Admin
{
    public class EnquiryDetailsModel : PageModel
    {
        private readonly string _connectionString;

        public EnquiryDetailsModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public StudentEnquiry? Enquiry { get; set; }
        public string? StudentFullName { get; set; }
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }

        [BindProperty]
        public string? Comment { get; set; }

        public IActionResult OnGet(int enquiryID)
        {
            Console.WriteLine($"OnGet: enquiryID={enquiryID}");
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    var enquirySql = @"
                        SELECT e.EnquiryID, e.UserID, e.Category, e.Subject, e.Message, e.CreatedDate
                        FROM Enquiries e
                        WHERE e.EnquiryID = @EnquiryID";

                    using (var command = new SqlCommand(enquirySql, connection))
                    {
                        command.Parameters.AddWithValue("@EnquiryID", enquiryID);

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Enquiry = new StudentEnquiry
                                {
                                    EnquiryID = reader.GetInt32("EnquiryID"),
                                    UserID = reader.GetString("UserID"),
                                    Category = reader.GetString("Category"),
                                    Subject = reader.GetString("Subject"),
                                    Message = reader.GetString("Message"),
                                    CreatedDate = reader.GetDateTime("CreatedDate"),
                                    EnquiryFiles = new List<StudentEnquiryFiles>(),
                                    Responses = new List<EnquiryResponse>()
                                };
                            }
                            else
                            {
                                return NotFound();
                            }
                        }
                    }

                    var filesSql = @"
                        SELECT FileID, EnquiryID, FilePath
                        FROM EnquiryFiles
                        WHERE EnquiryID = @EnquiryID";

                    using (var filesCommand = new SqlCommand(filesSql, connection))
                    {
                        filesCommand.Parameters.AddWithValue("@EnquiryID", enquiryID);

                        using (var reader = filesCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Enquiry.EnquiryFiles.Add(new StudentEnquiryFiles
                                {
                                    FileID = reader.GetInt32("FileID"),
                                    EnquiryID = reader.GetInt32("EnquiryID"),
                                    FilePath = reader.GetString("FilePath")
                                });
                            }
                        }
                    }

                    var responsesSql = @"
                        SELECT er.ResponseID, er.EnquiryID, er.UserID, er.Comment, er.ResponseDate, u.FullName
                        FROM EnquiryResponses er
                        LEFT JOIN Users u ON er.UserID = u.UserID
                        WHERE er.EnquiryID = @EnquiryID";

                    using (var responsesCommand = new SqlCommand(responsesSql, connection))
                    {
                        responsesCommand.Parameters.AddWithValue("@EnquiryID", enquiryID);

                        using (var reader = responsesCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Enquiry.Responses.Add(new EnquiryResponse
                                {
                                    ResponseID = reader.GetInt32("ResponseID"),
                                    EnquiryID = reader.GetInt32("EnquiryID"),
                                    UserID = reader.GetString("UserID"),
                                    Comment = reader.GetString("Comment"),
                                    ResponseDate = reader.GetDateTime("ResponseDate"),
                                    User = new User { FullName = reader.IsDBNull("FullName") ? "Unknown" : reader.GetString("FullName") }
                                });
                            }
                        }
                    }

                    var userSql = @"
                        SELECT FullName
                        FROM Users
                        WHERE UserID = @UserID";

                    using (var userCommand = new SqlCommand(userSql, connection))
                    {
                        userCommand.Parameters.AddWithValue("@UserID", Enquiry.UserID);

                        var fullName = userCommand.ExecuteScalar();
                        StudentFullName = fullName != null ? fullName.ToString() : "Unknown";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnGet: Failed to load enquiry - {ex.Message}");
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostRespondAsync(int enquiryID)
        {
            Console.WriteLine($"OnPostRespond: enquiryID={enquiryID}, Comment={Comment}");
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            if (string.IsNullOrWhiteSpace(Comment))
            {
                ErrorMessage = "Please enter a reply.";
                await LoadEnquiryData(enquiryID);
                return Page();
            }

            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID)) return RedirectToPage("/Account/Login");

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var checkSql = "SELECT COUNT(*) FROM Enquiries WHERE EnquiryID = @EnquiryID";
                    using (var checkCommand = new SqlCommand(checkSql, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@EnquiryID", enquiryID);
                        int count = (int)await checkCommand.ExecuteScalarAsync();
                        if (count == 0) return NotFound();
                    }

                    var responseSql = @"
                        INSERT INTO EnquiryResponses (EnquiryID, UserID, Comment, ResponseDate)
                        VALUES (@EnquiryID, @UserID, @Comment, @ResponseDate)";

                    // SQL to update the Status of the Enquiry
                    var updateStatusSql = @"
                        UPDATE Enquiries 
                        SET Status = 'Responded' 
                        WHERE EnquiryID = @EnquiryID";

                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // First, insert the response
                            using (var command = connection.CreateCommand())
                            {
                                command.Transaction = (SqlTransaction)transaction;
                                command.CommandText = responseSql;
                                command.Parameters.Add(new SqlParameter("@EnquiryID", enquiryID));
                                command.Parameters.Add(new SqlParameter("@UserID", userID));
                                command.Parameters.Add(new SqlParameter("@Comment", Comment));
                                command.Parameters.Add(new SqlParameter("@ResponseDate", DateTime.UtcNow.AddHours(8)));

                                await command.ExecuteNonQueryAsync();
                                Console.WriteLine($"OnPostRespond: Response saved for EnquiryID={enquiryID}");
                            }

                            // Then, update the status of the enquiry
                            using (var command = connection.CreateCommand())
                            {
                                command.Transaction = (SqlTransaction)transaction;
                                command.CommandText = updateStatusSql;
                                command.Parameters.Add(new SqlParameter("@EnquiryID", enquiryID));

                                await command.ExecuteNonQueryAsync();
                                Console.WriteLine($"OnPostRespond: Status updated for EnquiryID={enquiryID} to 'Responded'");
                            }

                            // Commit the transaction if both operations succeed
                            await transaction.CommitAsync();
                        }
                        catch (Exception ex)
                        {
                            // In case of an error, roll back the transaction
                            await transaction.RollbackAsync();
                            Console.WriteLine($"Error in OnPostRespond: {ex.Message}");
                        }
                    }
                }

                Message = "Reply submitted successfully!";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to submit reply: {ex.Message}";
                Console.WriteLine($"OnPostRespond: Error - {ex.Message}");
                await LoadEnquiryData(enquiryID);
                return Page();
            }

            return RedirectToPage(new { enquiryID });
        }

        private async Task LoadEnquiryData(int enquiryID)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var enquirySql = @"
                        SELECT e.EnquiryID, e.UserID, e.Category, e.Subject, e.Message, e.CreatedDate
                        FROM Enquiries e
                        WHERE e.EnquiryID = @EnquiryID";

                    using (var command = new SqlCommand(enquirySql, connection))
                    {
                        command.Parameters.AddWithValue("@EnquiryID", enquiryID);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (reader.Read())
                            {
                                Enquiry = new StudentEnquiry
                                {
                                    EnquiryID = reader.GetInt32("EnquiryID"),
                                    UserID = reader.GetString("UserID"),
                                    Category = reader.GetString("Category"),
                                    Subject = reader.GetString("Subject"),
                                    Message = reader.GetString("Message"),
                                    CreatedDate = reader.GetDateTime("CreatedDate"),
                                    EnquiryFiles = new List<StudentEnquiryFiles>(),
                                    Responses = new List<EnquiryResponse>()
                                };
                            }
                            else
                            {
                                Enquiry = null;
                                StudentFullName = "Unknown";
                                return;
                            }
                        }
                    }

                    var filesSql = @"
                        SELECT FileID, EnquiryID, FilePath
                        FROM EnquiryFiles
                        WHERE EnquiryID = @EnquiryID";

                    using (var filesCommand = new SqlCommand(filesSql, connection))
                    {
                        filesCommand.Parameters.AddWithValue("@EnquiryID", enquiryID);

                        using (var reader = await filesCommand.ExecuteReaderAsync())
                        {
                            while (reader.Read())
                            {
                                Enquiry.EnquiryFiles.Add(new StudentEnquiryFiles
                                {
                                    FileID = reader.GetInt32("FileID"),
                                    EnquiryID = reader.GetInt32("EnquiryID"),
                                    FilePath = reader.GetString("FilePath")
                                });
                            }
                        }
                    }

                    var responsesSql = @"
                        SELECT er.ResponseID, er.EnquiryID, er.UserID, er.Comment, er.ResponseDate, u.FullName
                        FROM EnquiryResponses er
                        LEFT JOIN Users u ON er.UserID = u.UserID
                        WHERE er.EnquiryID = @EnquiryID";

                    using (var responsesCommand = new SqlCommand(responsesSql, connection))
                    {
                        responsesCommand.Parameters.AddWithValue("@EnquiryID", enquiryID);

                        using (var reader = await responsesCommand.ExecuteReaderAsync())
                        {
                            while (reader.Read())
                            {
                                Enquiry.Responses.Add(new EnquiryResponse
                                {
                                    ResponseID = reader.GetInt32("ResponseID"),
                                    EnquiryID = reader.GetInt32("EnquiryID"),
                                    UserID = reader.GetString("UserID"),
                                    Comment = reader.GetString("Comment"),
                                    ResponseDate = reader.GetDateTime("ResponseDate"),
                                    User = new User { FullName = reader.IsDBNull("FullName") ? "Unknown" : reader.GetString("FullName") }
                                });
                            }
                        }
                    }

                    var userSql = @"
                        SELECT FullName
                        FROM Users
                        WHERE UserID = @UserID";

                    using (var userCommand = new SqlCommand(userSql, connection))
                    {
                        userCommand.Parameters.AddWithValue("@UserID", Enquiry.UserID);

                        var fullName = await userCommand.ExecuteScalarAsync();
                        StudentFullName = fullName != null ? fullName.ToString() : "Unknown";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadEnquiryData: Error - {ex.Message}");
                Enquiry = null;
                StudentFullName = "Unknown";
            }
        }

        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetString("UserID");
            return role == "Admin" && !string.IsNullOrEmpty(userId);
        }
    }
}