// Pages/Payment/FakeGateway.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StudentEnrollmentSystem.Pages.Payment
{
    public class FakeGatewayModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public FakeGatewayModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public decimal TotalAmount { get; set; }

        [BindProperty]
        public string ReferenceNumber { get; set; }

        [BindProperty]
        public string CardNumber { get; set; }

        [BindProperty]
        public string CardHolderName { get; set; }

        [BindProperty]
        public string ExpiryDate { get; set; }

        [BindProperty]
        public string CVV { get; set; }

        public string PaymentStatus { get; set; }
        public string ResponseMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(decimal totalAmount, string referenceNumber)
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                Console.WriteLine("OnGet: UserID not found, redirecting to /Account/Login");
                return RedirectToPage("/Account/Login");
            }

            TotalAmount = totalAmount;
            ReferenceNumber = referenceNumber;

            // Retrieve payment details
            var paymentDetails = await _context.PaymentDetails
                .FirstOrDefaultAsync(pd => pd.UserID == userID);

            if (paymentDetails != null)
            {
                CardNumber = paymentDetails.CardAccNo;
                CardHolderName = paymentDetails.CardHolderName;
                ExpiryDate = paymentDetails.ExpiryDate;
                // CVV is not stored for security reasons, so leave it blank
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                Console.WriteLine("OnPost: UserID not found, redirecting to /Account/Login");
                return RedirectToPage("/Account/Login");
            }

            var studentId = await _context.StudentDetails
                .Where(s => s.UserID == userID)
                .Select(s => s.StudentID)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(studentId))
            {
                Console.WriteLine("Student not found for UserID, redirecting to /Account/Login");
                return RedirectToPage("/Account/Login");
            }

            // Validation
            if (!Regex.IsMatch(CardNumber?.Replace(" ", "") ?? "", @"^\d{16}$"))
            {
                ModelState.AddModelError("CardNumber", "Card number must be 16 digits.");
                return Page();
            }

            if (!Regex.IsMatch(ExpiryDate ?? "", @"^(0[1-9]|1[0-2])\/?([0-9]{2})$"))
            {
                ModelState.AddModelError("ExpiryDate", "Invalid expiry date (MM/YY format).");
                return Page();
            }

            if (!Regex.IsMatch(CVV ?? "", @"^\d{3}$"))
            {
                ModelState.AddModelError("CVV", "CVV must be 3 digits.");
                return Page();
            }

            // Additional expiry date validation (ensure it's not in the past)
            var expiryMatch = Regex.Match(ExpiryDate, @"^(0[1-9]|1[0-2])\/?([0-9]{2})$");
            if (expiryMatch.Success)
            {
                int month = int.Parse(expiryMatch.Groups[1].Value);
                int year = int.Parse("20" + expiryMatch.Groups[2].Value); // Assuming 20XX
                var expiryDateTime = new DateTime(year, month, DateTime.DaysInMonth(year, month));
                if (expiryDateTime < DateTime.Now)
                {
                    ModelState.AddModelError("ExpiryDate", "Card has expired.");
                    return Page();
                }
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Simulate payment processing - Accept any valid 16-digit card number
            PaymentStatus = "Completed";
            ResponseMessage = "Payment processed successfully";

            try
            {
                var selectedPaymentMethod = HttpContext.Session.GetString("SelectedPaymentCard");

                if (string.IsNullOrEmpty(selectedPaymentMethod))
                {
                    TempData["ErrorMessage"] = "Invalid or missing payment method.";
                }

                var payment = new Models.Payment
                {
                    StudentID = studentId,
                    Amount = TotalAmount,
                    PaymentMethod = selectedPaymentMethod, //payment method based on selected card in Payment page
                    PaymentType = "Card",
                    PaymentDate = DateTime.Now,
                    Status = "Completed",
                    ReferenceNumber = ReferenceNumber
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                var invoiceRef = ReferenceNumber.Replace("INV-", "");
                if (int.TryParse(invoiceRef, out int invoiceId))
                {
                    var invoice = await _context.Invoices.FindAsync(invoiceId);
                    if (invoice != null)
                    {
                        invoice.PaidAmount += TotalAmount;
                        invoice.Status = invoice.PaidAmount >= invoice.TotalAmount
                            ? "Paid"
                            : "Partially Paid";
                        await _context.SaveChangesAsync();
                    }
                }

                return RedirectToPage("/Payment/PaymentHistory");
            }
            catch (Exception ex)
            {
                ResponseMessage = $"Payment failed: {ex.Message}";
                PaymentStatus = "Declined";
                return Page();
            }
        }
    }
}