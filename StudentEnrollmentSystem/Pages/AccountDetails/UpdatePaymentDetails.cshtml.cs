// Pages/AccountDetails/UpdatePaymentDetails.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace StudentEnrollmentSystem.Pages.AccountDetails
{
    public class UpdatePaymentDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public UpdatePaymentDetailsModel(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public PaymentDetail PaymentDetails { get; set; }

        [BindProperty]
        public IFormFile? CardDocumentFile { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string Errormessage { get; set; }
        public string PaymentFlow { get; set; }
        public async Task<IActionResult> OnGetAsync()
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                return RedirectToPage("/Account/Login");
            }

            var user = await _context.Users
                .Where(u => u.UserID == userID)
                .FirstOrDefaultAsync();

            PaymentDetails = await _context.PaymentDetails
                .Include(pd => pd.PaymentDocumentHistories)
                .Where(pd => pd.UserID == userID)
                .SingleOrDefaultAsync() ?? new PaymentDetail { UserID = userID };

            if (string.IsNullOrEmpty(PaymentDetails.CardHolderName) && user != null && !string.IsNullOrEmpty(user.FullName))
            {
                PaymentDetails.CardHolderName = user.FullName;
            }

            // Sort PaymentDocumentHistories in descending order by UploadDate
            PaymentDetails.PaymentDocumentHistories = PaymentDetails.PaymentDocumentHistories
                .OrderByDescending(d => d.UploadDate)
                .ToList();

            // Get the PaymentFlow from session
            PaymentFlow = HttpContext.Session.GetString("PaymentFlow");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                return RedirectToPage("/Account/Login");
            }

            PaymentDetails.UserID = userID;

            // Make CardDocumentFile optional by removing it from ModelState validation
            ModelState.Remove("PaymentDetails.CardDocumentFile");

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                Errormessage = "Please check the form for errors: " + string.Join(", ", errors);
                return Page();
            }

            try
            {
                var existingDetails = await _context.PaymentDetails
                    .Include(pd => pd.PaymentDocumentHistories)
                    .FirstOrDefaultAsync(p => p.UserID == userID);

                string uploadedFileName = null;

                // Handle optional file upload
                if (CardDocumentFile != null && CardDocumentFile.Length > 0)
                {
                    string fileExtension = Path.GetExtension(CardDocumentFile.FileName).ToLower();
                    if (fileExtension != ".jpg" && fileExtension != ".jpeg" && fileExtension != ".png" && fileExtension != ".gif")
                    {
                        Errormessage = "Only JPG, PNG, and GIF files are allowed for document uploads.";
                        return Page();
                    }

                    if (CardDocumentFile.Length > 800 * 1024)
                    {
                        Errormessage = "File size must be less than 800KB.";
                        return Page();
                    }

                    uploadedFileName = Guid.NewGuid().ToString() + fileExtension;
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", uploadedFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await CardDocumentFile.CopyToAsync(stream);
                    }
                }

                if (existingDetails != null)
                {
                    // Update existing record
                    existingDetails.BankName = PaymentDetails.BankName;
                    existingDetails.CardAccNo = PaymentDetails.CardAccNo;
                    existingDetails.CardHolderName = PaymentDetails.CardHolderName;
                    existingDetails.ExpiryDate = PaymentDetails.ExpiryDate;

                    if (uploadedFileName != null)
                    {
                        // Update CardDOC with the latest upload
                        existingDetails.CardDOC = uploadedFileName;
                        // Log to history
                        existingDetails.PaymentDocumentHistories.Add(new PaymentDocumentHistory
                        {
                            FilePath = uploadedFileName,
                            UploadDate = DateTime.Now
                        });
                    }

                    _context.Update(existingDetails);
                }
                else
                {
                    // Insert new record
                    PaymentDetails.UserID = userID;
                    if (uploadedFileName != null)
                    {
                        PaymentDetails.CardDOC = uploadedFileName;
                        PaymentDetails.PaymentDocumentHistories.Add(new PaymentDocumentHistory
                        {
                            FilePath = uploadedFileName,
                            UploadDate = DateTime.Now
                        });
                    }

                    _context.PaymentDetails.Add(PaymentDetails);
                }

                await _context.SaveChangesAsync();

                // Success message
                SuccessMessage = "Your payment details have been saved successfully.";

                // Check if the session variable "PaymentFlow" is set
                var paymentFlow = HttpContext.Session.GetString("PaymentFlow");
                if (!string.IsNullOrEmpty(paymentFlow))
                {
                    // If the session variable exists, clear it and redirect to the Payment page
                    HttpContext.Session.Remove("PaymentFlow");
                    return RedirectToPage("/Payment/Payment");
                }
                else
                {
                    // If the session variable does not exist, just return to the current page
                    return RedirectToPage();
                }
            }
            catch (Exception ex)
            {
                Errormessage = "An error occurred while saving your changes. Details: " + ex.Message;
                return Page();
            }
        }
    }
}