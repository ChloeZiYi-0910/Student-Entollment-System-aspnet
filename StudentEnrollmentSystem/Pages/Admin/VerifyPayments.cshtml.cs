using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentEnrollmentSystem.Pages.Admin
{
    public class VerifyPaymentsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public VerifyPaymentsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<StudentEnrollmentSystem.Models.Payment> PendingPayments { get; set; }
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userID = HttpContext.Session.GetString("UserID");
            var role = HttpContext.Session.GetString("Role");
            if (string.IsNullOrEmpty(userID) || role != "Admin")
            {
                return RedirectToPage("/Account/Login");
            }

            try
            {
                PendingPayments = await _context.Payments
                    .Where(p => p.Status == "Pending" && p.PaymentMethod == "Bank Transfer")
                    .ToListAsync();

                Console.WriteLine($"OnGet: Retrieved {PendingPayments.Count} pending payments");
                foreach (var payment in PendingPayments)
                {
                    Console.WriteLine($"PaymentID: {payment.PaymentID}, StudentID: {payment.StudentID}, ReferenceNumber: {payment.ReferenceNumber}, Amount: {payment.Amount}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading pending payments: {ex.Message}";
                Console.WriteLine($"OnGet: Exception - {ex.Message}, StackTrace: {ex.StackTrace}");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostVerifyAsync(int paymentId)
        {
            var userID = HttpContext.Session.GetString("UserID");
            var role = HttpContext.Session.GetString("Role");
            if (string.IsNullOrEmpty(userID) || role != "Admin")
            {
                return RedirectToPage("/Account/Login");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.PaymentID == paymentId);
                if (payment == null)
                {
                    ErrorMessage = "Payment not found.";
                    Console.WriteLine($"OnPostVerify: PaymentID {paymentId} not found");
                    return RedirectToPage();
                }
                if (payment.Status != "Pending")
                {
                    ErrorMessage = "Payment is already processed.";
                    Console.WriteLine($"OnPostVerify: PaymentID {paymentId} already processed, Status: {payment.Status}");
                    return RedirectToPage();
                }

                if (string.IsNullOrEmpty(payment.ReferenceNumber) || !payment.ReferenceNumber.StartsWith("INV-"))
                {
                    ErrorMessage = $"Invalid ReferenceNumber format: {payment.ReferenceNumber}";
                    Console.WriteLine($"OnPostVerify: Invalid ReferenceNumber for PaymentID {paymentId}: {payment.ReferenceNumber}");
                    return RedirectToPage();
                }

                var invoiceRef = payment.ReferenceNumber.Replace("INV-", "");
                if (!int.TryParse(invoiceRef, out int invoiceId))
                {
                    ErrorMessage = $"Failed to parse InvoiceID from ReferenceNumber: {payment.ReferenceNumber}";
                    Console.WriteLine($"OnPostVerify: Failed to parse InvoiceID for PaymentID {paymentId}: {payment.ReferenceNumber}");
                    return RedirectToPage();
                }

                var dbInvoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);
                if (dbInvoice == null)
                {
                    ErrorMessage = $"Invoice not found for InvoiceID: {invoiceId}";
                    Console.WriteLine($"OnPostVerify: Invoice not found for InvoiceID {invoiceId}, PaymentID {paymentId}");
                    return RedirectToPage();
                }

                Console.WriteLine($"OnPostVerify: Before Update - PaymentID: {paymentId}, InvoiceID: {invoiceId}, Invoice Status: {dbInvoice.Status}, PaidAmount: {dbInvoice.PaidAmount}, TotalAmount: {dbInvoice.TotalAmount}, Payment Amount: {payment.Amount}");

                payment.Status = "Completed";
                dbInvoice.PaidAmount += payment.Amount;
                dbInvoice.Status = dbInvoice.PaidAmount >= dbInvoice.TotalAmount ? "Paid" : "Partially Paid";

                Console.WriteLine($"OnPostVerify: Intended Update - InvoiceID: {invoiceId}, Status: {dbInvoice.Status}, PaidAmount: {dbInvoice.PaidAmount}, TotalAmount: {dbInvoice.TotalAmount}");

                int rowsAffected = await _context.SaveChangesAsync();
                Console.WriteLine($"OnPostVerify: SaveChanges affected {rowsAffected} rows");

                var updatedInvoice = await _context.Invoices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);
                Console.WriteLine($"OnPostVerify: Post-Save Check - InvoiceID: {invoiceId}, Status: {updatedInvoice.Status}, PaidAmount: {updatedInvoice.PaidAmount}, TotalAmount: {updatedInvoice.TotalAmount}");

                if (updatedInvoice.Status == "Pending")
                {
                    throw new Exception($"Invoice status failed to update for InvoiceID {invoiceId}. Expected 'Paid' or 'Partially Paid', got 'Pending'.");
                }

                await transaction.CommitAsync();
                SuccessMessage = "Payment verified successfully.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ErrorMessage = $"Failed to verify payment: {ex.Message}";
                if (ex.InnerException != null)
                {
                    ErrorMessage += $" Inner Exception: {ex.InnerException.Message}";
                }
                Console.WriteLine($"OnPostVerify: Exception - {ex.Message}, StackTrace: {ex.StackTrace}");
                return RedirectToPage();
            }

            return RedirectToPage();
        }
    }
}