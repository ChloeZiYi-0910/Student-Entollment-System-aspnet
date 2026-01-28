using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StudentEnrollmentSystem.Models;
using System.Data;

namespace StudentEnrollmentSystem.Pages.Enquiry
{
    public class ContactUsModel : PageModel
    {
        private readonly string _connectionString;

        public ContactUsModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [BindProperty]
        public StudentEnquiry Enquiry { get; set; }

        [BindProperty]
        public List<IFormFile>? UploadedFiles { get; set; }

        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public List<StudentEnquiry> PastEnquiries { get; set; } = new(); // Added to hold enquiry history

        public async Task OnGetAsync()
        {
            Console.WriteLine("OnGet: Entering ContactUs page");
            if (TempData["SuccessMessage"] != null)
            {
                Message = TempData["SuccessMessage"].ToString();
                Console.WriteLine($"OnGet: Success message set to '{Message}'");
                TempData.Keep("SuccessMessage");
            }

            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                Console.WriteLine("OnGet: UserID not found, redirecting to /Account/Login");
                Response.Redirect("/Account/Login");
                return;
            }

            // Fetch past enquiries for the logged-in student
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var enquirySql = @"
                        SELECT 
                            e.EnquiryID, 
                            e.UserID, 
                            e.Category, 
                            e.Subject, 
                            e.Message, 
                            e.CreatedDate
                        FROM Enquiries e
                        WHERE e.UserID = @UserID
                        ORDER BY e.CreatedDate DESC";

                    using (var command = new SqlCommand(enquirySql, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userID);

                        using (var reader = await command.ExecuteReaderAsync())
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
                                    EnquiryFiles = new List<StudentEnquiryFiles>(),
                                    Responses = new List<EnquiryResponse>()
                                };
                                PastEnquiries.Add(enquiry);
                            }
                        }
                    }

                    // Fetch files for each enquiry
                    foreach (var enquiry in PastEnquiries)
                    {
                        var filesSql = @"
                            SELECT FileID, EnquiryID, FilePath
                            FROM EnquiryFiles
                            WHERE EnquiryID = @EnquiryID";

                        using (var filesCommand = new SqlCommand(filesSql, connection))
                        {
                            filesCommand.Parameters.AddWithValue("@EnquiryID", enquiry.EnquiryID);

                            using (var reader = await filesCommand.ExecuteReaderAsync())
                            {
                                while (reader.Read())
                                {
                                    enquiry.EnquiryFiles.Add(new StudentEnquiryFiles
                                    {
                                        FileID = reader.GetInt32("FileID"),
                                        EnquiryID = reader.GetInt32("EnquiryID"),
                                        FilePath = reader.GetString("FilePath")
                                    });
                                }
                            }
                        }
                        // Fetch responses for each enquiry
                        var responsesSql = @"
                            SELECT 
                                er.ResponseID, 
                                er.EnquiryID, 
                                er.UserID, 
                                er.Comment, 
                                er.ResponseDate,
                                u.FullName
                            FROM EnquiryResponses er
                            LEFT JOIN Users u ON er.UserID = u.UserID
                            WHERE er.EnquiryID = @EnquiryID
                            ORDER BY er.ResponseDate";

                        using (var responsesCommand = new SqlCommand(responsesSql, connection))
                        {
                            responsesCommand.Parameters.AddWithValue("@EnquiryID", enquiry.EnquiryID);

                            using (var reader = await responsesCommand.ExecuteReaderAsync())
                            {
                                while (reader.Read())
                                {
                                    enquiry.Responses.Add(new EnquiryResponse
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
                    }
                }
                Console.WriteLine($"OnGet: Loaded {PastEnquiries.Count} past enquiries for UserID={userID}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnGet: Failed to load past enquiries - {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine("OnPostAsync: Starting form submission");

            var userID = HttpContext.Session.GetString("UserID");
            Console.WriteLine($"OnPostAsync: UserID from session = '{userID}'");
            if (string.IsNullOrEmpty(userID))
            {
                Console.WriteLine("OnPostAsync: UserID not found, redirecting to /Account/Login");
                return RedirectToPage("/Account/Login");
            }

            Enquiry.UserID = userID;
            Console.WriteLine($"OnPostAsync: UserID set to '{Enquiry.UserID}'");

            Console.WriteLine($"OnPostAsync: Form data - Category: '{Enquiry.Category}', Subject: '{Enquiry.Subject}', Message: '{Enquiry.Message}'");

            ModelState.Remove("Enquiry.UserID");
            ModelState.Remove("Enquiry.User");
            ModelState.Remove("Enquiry.EnquiryID");
            ModelState.Remove("Enquiry.CreatedDate");
            ModelState.Remove("Enquiry.EnquiryFiles");
            ModelState.Remove("Enquiry.Responses");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("OnPostAsync: ModelState is invalid");
                foreach (var entry in ModelState)
                {
                    foreach (var error in entry.Value.Errors)
                    {
                        Console.WriteLine($"OnPostAsync: Validation error in {entry.Key}: {error.ErrorMessage}");
                    }
                }
                ErrorMessage = "Please fill in all required fields correctly.";
                await OnGetAsync(); // Reload past enquiries
                return Page();
            }

            Enquiry.CreatedDate = DateTime.UtcNow.AddHours(8);
            Console.WriteLine($"OnPostAsync: Enquiry details - CreatedDate: {Enquiry.CreatedDate}");

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var enquirySql = @"
                        INSERT INTO Enquiries (UserID, Category, Subject, Message, CreatedDate)
                        OUTPUT INSERTED.EnquiryID
                        VALUES (@UserID, @Category, @Subject, @Message, @CreatedDate)";

                    using (var command = new SqlCommand(enquirySql, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", Enquiry.UserID);
                        command.Parameters.AddWithValue("@Category", Enquiry.Category);
                        command.Parameters.AddWithValue("@Subject", Enquiry.Subject);
                        command.Parameters.AddWithValue("@Message", Enquiry.Message);
                        command.Parameters.AddWithValue("@CreatedDate", Enquiry.CreatedDate);

                        Enquiry.EnquiryID = (int)await command.ExecuteScalarAsync();
                        Console.WriteLine($"OnPostAsync: Enquiry saved with EnquiryID = {Enquiry.EnquiryID}");
                    }

                    var enquiryFiles = new List<StudentEnquiryFiles>();
                    if (UploadedFiles != null && UploadedFiles.Any())
                    {
                        Console.WriteLine($"OnPostAsync: Processing {UploadedFiles.Count} files");
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                        var maxFileSize = 5 * 1024 * 1024;

                        foreach (var file in UploadedFiles)
                        {
                            var fileExtension = Path.GetExtension(file.FileName).ToLower();
                            if (!allowedExtensions.Contains(fileExtension))
                            {
                                ModelState.AddModelError("", "Only JPG, PNG, and PDF files are allowed.");
                                Console.WriteLine("OnPostAsync: Invalid file extension");
                                await OnGetAsync(); // Reload past enquiries
                                return Page();
                            }
                            if (file.Length > maxFileSize)
                            {
                                ModelState.AddModelError("", "File size must be less than 5MB.");
                                Console.WriteLine("OnPostAsync: File too large");
                                await OnGetAsync(); // Reload past enquiries
                                return Page();
                            }

                            var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                            // Ensure the uploads directory exists
                            if (!Directory.Exists(uploadFolder))
                            {
                                Directory.CreateDirectory(uploadFolder);
                            }

                            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                            var filePath = Path.Combine(uploadFolder, uniqueFileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }
                            Console.WriteLine($"OnPostAsync: File saved to '{filePath}'");

                            var fileSql = @"
                                INSERT INTO EnquiryFiles (EnquiryID, FilePath)
                                VALUES (@EnquiryID, @FilePath)";

                            using (var fileCommand = new SqlCommand(fileSql, connection))
                            {
                                fileCommand.Parameters.AddWithValue("@EnquiryID", Enquiry.EnquiryID);
                                fileCommand.Parameters.AddWithValue("@FilePath", "/uploads/" + uniqueFileName);
                                await fileCommand.ExecuteNonQueryAsync();
                            }
                        }
                        Console.WriteLine("OnPostAsync: Files saved successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnPostAsync: Failed to save enquiry or files - {ex.Message}");
                ErrorMessage = "Failed to save enquiry or files: " + ex.Message;
                await OnGetAsync(); // Reload past enquiries
                return Page();
            }

            TempData["SuccessMessage"] = "Your enquiry has been sent successfully! The related department will reach out to you soon.";
            Console.WriteLine("OnPostAsync: Redirecting to /Enquiry/ContactUs");
            return RedirectToPage("/Enquiry/ContactUs");
        }
    }
}