using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Linq;

namespace StudentEnrollmentSystem.Pages.Payment
{
    public class PaymentModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public PaymentModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public PaymentInputModel PaymentInput { get; set; }

        public List<InvoiceDisplayModel> Invoices { get; set; }
        public string ErrorMessage { get; set; }

        public class PaymentInputModel
        {
            [Required]
            [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
            public decimal Amount { get; set; }

            [Required]
            public string PaymentMethod { get; set; }
        }

        public class InvoiceDisplayModel
        {
            public int InvoiceID { get; set; }
            public string StudentID { get; set; }
            public string StudentName { get; set; }
            public string Semester { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal PaidAmount { get; set; }
            public decimal BalanceDue => TotalAmount - PaidAmount;
            public string Status { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int? invoiceId = null)
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                Console.WriteLine("OnGet: UserID not found, redirecting to /Account/Login");
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

            await LoadInvoicesAsync(studentId);

            PaymentInput = new PaymentInputModel
            {
                Amount = 0m,
                PaymentMethod = string.Empty
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int invoiceId)
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

            // Check if payment details exist for Credit/Debit card payment
            if (PaymentInput.PaymentMethod == "Credit Card" || PaymentInput.PaymentMethod == "Debit Card")
            {
                HttpContext.Session.SetString("SelectedPaymentCard", PaymentInput.PaymentMethod);
                var paymentDetails = await _context.PaymentDetails
                    .FirstOrDefaultAsync(pd => pd.UserID == userID);

                if (paymentDetails == null || string.IsNullOrEmpty(paymentDetails.CardAccNo))
                {
                    TempData["ErrorMessage"] = "Please complete your payment details before payment.";
                    HttpContext.Session.SetString("PaymentFlow", "Payment");
                    return RedirectToPage("/AccountDetails/UpdatePaymentDetails");
                }
            }

            // Load invoices to validate the selected invoice
            await LoadInvoicesAsync(studentId);

            var invoice = Invoices.FirstOrDefault(i => i.InvoiceID == invoiceId);
            if (invoice == null)
            {
                ErrorMessage = "Invalid invoice or no unpaid invoices found.";
                return Page();
            }

            var balanceDue = invoice.TotalAmount - invoice.PaidAmount;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (PaymentInput.Amount <= 0 || PaymentInput.Amount > balanceDue)
            {
                ErrorMessage = "Payment amount must be positive and not exceed balance due";
                return Page();
            }

            try
            {
                if (PaymentInput.PaymentMethod == "Credit Card" || PaymentInput.PaymentMethod == "Debit Card")
                {

                    return RedirectToPage("/Payment/FakeGateway", new
                    {
                        totalAmount = PaymentInput.Amount,
                        referenceNumber = $"INV-{invoiceId}"
                    });
                }
                else if (PaymentInput.PaymentMethod == "Bank Transfer")
                {
                    return RedirectToPage("/Payment/BankTransferGateway", new
                    {
                        totalAmount = PaymentInput.Amount,
                        referenceNumber = $"INV-{invoiceId}"
                    });
                }
                else
                {
                    ErrorMessage = "Invalid payment method.";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Payment failed: {ex.Message}";
                Console.WriteLine($"Payment error: {ex}");
                return Page();
            }
        }

        private async Task LoadInvoicesAsync(string studentId)
        {
            var invoiceData = await _context.Invoices
                .Include(i => i.Student)
                .Where(i => i.StudentID == studentId && (i.TotalAmount - i.PaidAmount) > 0)
                .OrderByDescending(i => i.IssueDate)
                .Select(i => new
                {
                    i.InvoiceID,
                    i.StudentID,
                    StudentName = i.Student != null ? i.Student.LastName + " " + i.Student.FirstName : "Unknown",
                    i.Semester,
                    i.TotalAmount,
                    i.PaidAmount,
                    i.Status
                })
                .ToListAsync();

            Invoices = new List<InvoiceDisplayModel>();

            foreach (var invoice in invoiceData)
            {
                // Check the status of payments associated with this invoice
                var hasPendingPayments = await _context.Payments
                    .AnyAsync(p => p.ReferenceNumber == $"INV-{invoice.InvoiceID}" && p.Status == "Pending");

                // If there are pending payments, the invoice status should be "Pending"
                string effectiveStatus = hasPendingPayments ? "Pending" : invoice.Status;

                Invoices.Add(new InvoiceDisplayModel
                {
                    InvoiceID = invoice.InvoiceID,
                    StudentID = invoice.StudentID,
                    StudentName = invoice.StudentName,
                    Semester = invoice.Semester,
                    TotalAmount = invoice.TotalAmount,
                    PaidAmount = invoice.PaidAmount,
                    Status = effectiveStatus
                });
            }
        }
    }
}