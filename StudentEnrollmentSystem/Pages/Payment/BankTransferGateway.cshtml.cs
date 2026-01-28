using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace StudentEnrollmentSystem.Pages.Payment
{
    public class BankTransferGatewayModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string _connectionString;

        public BankTransferGatewayModel(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [BindProperty]
        public DateTime TransactionDate { get; set; }

        [BindProperty]
        public string Currency { get; set; }

        [BindProperty]
        public decimal TotalAmount { get; set; }

        [BindProperty]
        public string ReferenceNumber { get; set; }

        [BindProperty]
        public string Remarks { get; set; }

        [BindProperty]
        public IFormFile ProofFile { get; set; }

        [BindProperty]
        public string ContactNo { get; set; }

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public IActionResult OnGet(decimal totalAmount, string referenceNumber)
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                return RedirectToPage("/Account/Login");
            }

            TransactionDate = DateTime.Now;
            Currency = "RM";
            TotalAmount = totalAmount;
            ReferenceNumber = referenceNumber;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                ErrorMessage = "User not logged in.";
                return RedirectToPage("/Account/Login");
            }

            var studentId = await GetStudentIdAsync(userID);
            if (string.IsNullOrEmpty(studentId))
            {
                ErrorMessage = "Student not found.";
                return Page();
            }

            // Validate Transaction Date
            if (TransactionDate > DateTime.Now)
            {
                ErrorMessage = "Transaction date cannot be in the future.";
                return Page();
            }

            // Validate Currency
            if (Currency != "RM")
            {
                ErrorMessage = "Only RM currency is supported.";
                return Page();
            }

            // Validate Amount
            if (TotalAmount <= 0)
            {
                ErrorMessage = "Amount must be greater than 0.";
                return Page();
            }

            // Validate Contact No
            if (string.IsNullOrEmpty(ContactNo) || !System.Text.RegularExpressions.Regex.IsMatch(ContactNo, @"^\d{10,15}$"))
            {
                ErrorMessage = "Please enter a valid contact number (10-15 digits).";
                return Page();
            }

            // Validate file upload
            if (ProofFile == null || ProofFile.Length == 0)
            {
                ErrorMessage = "Please upload a proof of payment file.";
                return Page();
            }

            string fileExtension = Path.GetExtension(ProofFile.FileName).ToLower();
            if (fileExtension != ".jpg" && fileExtension != ".jpeg" && fileExtension != ".bmp" && fileExtension != ".gif" && fileExtension != ".pdf")
            {
                ErrorMessage = "Only JPG, JPEG, BMP, GIF, and PDF files are allowed.";
                return Page();
            }

            if (ProofFile.Length > 1024 * 1024) // 1MB = 1024 * 1024 bytes
            {
                ErrorMessage = "File size must be less than 1MB.";
                return Page();
            }

            // Save the file
            string uploadedFileName = Guid.NewGuid().ToString() + fileExtension;
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", uploadedFileName);

            // Ensure the uploads directory exists
            var uploadsDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await ProofFile.CopyToAsync(stream);
            }

            // Process the payment
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var invoiceRef = ReferenceNumber.Replace("INV-", "");
                    if (!int.TryParse(invoiceRef, out int invoiceId))
                    {
                        ErrorMessage = "Invalid invoice reference.";
                        return Page();
                    }

                    var dbInvoice = await _context.Invoices.FindAsync(invoiceId);
                    if (dbInvoice == null)
                    {
                        ErrorMessage = "Invoice not found.";
                        return Page();
                    }

                    // Validate that the payment doesn't exceed the remaining balance
                    if (dbInvoice.PaidAmount + TotalAmount > dbInvoice.TotalAmount)
                    {
                        ErrorMessage = "Payment amount cannot exceed the remaining balance.";
                        return Page();
                    }

                    // Do NOT update the invoice PaidAmount and Status immediately for bank transfer
                    // The invoice status should remain "Pending" until the payment is verified
                    // We’ll update the invoice status when the payment is marked as "Completed"

                    // Save the payment
                    var payment = new Models.Payment
                    {
                        StudentID = studentId,
                        Amount = TotalAmount,
                        PaymentMethod = "Bank Transfer",
                        PaymentType = "Manual",
                        PaymentDate = TransactionDate.Date,
                        Status = "Pending",
                        ReferenceNumber = ReferenceNumber,
                        ProofFilePath = uploadedFileName
                    };

                    try
                    {
                        _context.Payments.Add(payment);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        ErrorMessage = $"Failed to save payment: {ex.Message}";
                        if (ex.InnerException != null)
                        {
                            ErrorMessage += $" Inner Exception: {ex.InnerException.Message}";
                        }
                        return Page();
                    }

                    // Build the response message with truncation
                    string remarksPart = Remarks ?? "N/A";
                    if (remarksPart.Length > 200)
                    {
                        remarksPart = remarksPart.Substring(0, 200);
                    }
                    string responseMessage = $"Currency: {Currency}, Remarks: {remarksPart}, Contact No: {ContactNo}";
                    responseMessage = responseMessage.Substring(0, Math.Min(255, responseMessage.Length));


                    SuccessMessage = "Payment submitted successfully. It will be verified soon.";
                    return RedirectToPage("/Payment/PaymentHistory");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Payment failed: {ex.Message}";
                if (ex.InnerException != null)
                {
                    ErrorMessage += $" Inner Exception: {ex.InnerException.Message}";
                }
                return Page();
            }
        }

        private async Task<string> GetStudentIdAsync(string userId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = "SELECT StudentID FROM StudentDetails WHERE UserID = @UserID";
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@UserID", userId);
                    var result = await command.ExecuteScalarAsync();
                    return result?.ToString();
                }
            }
        }
    }
}