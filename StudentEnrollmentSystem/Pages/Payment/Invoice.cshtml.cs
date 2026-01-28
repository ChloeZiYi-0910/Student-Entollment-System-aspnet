using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentEnrollmentSystem.Pages.Payment
{
    public class InvoiceModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public InvoiceModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<InvoiceDisplayModel> Invoices { get; set; }
        public string ErrorMessage { get; set; }
        public string StudentID { get; set; }

        public class InvoiceDisplayModel
        {
            public int InvoiceID { get; set; }
            public string Semester { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal PaidAmount { get; set; }
            public decimal BalanceDue => TotalAmount - PaidAmount;
            public string Status { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                Console.WriteLine("OnGet: UserID not found, redirecting to /Account/Login");
                return RedirectToPage("/Account/Login");
            }

            // Fetch the StudentID from StudentDetails table
            StudentID = await _context.StudentDetails
                .Where(s => s.UserID == userID)
                .Select(s => s.StudentID)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(StudentID))
            {
                Console.WriteLine($"OnGet: StudentID not found for UserID {userID}, redirecting to /Account/Login");
                return RedirectToPage("/Account/Login");
            }

            try
            {
                Invoices = await _context.Invoices
                    .AsNoTracking() // Avoid EF caching issues
                    .Where(i => i.StudentID == StudentID)
                    .OrderByDescending(i => i.IssueDate)
                    .Select(i => new InvoiceDisplayModel
                    {
                        InvoiceID = i.InvoiceID,
                        Semester = i.Semester,
                        TotalAmount = i.TotalAmount,
                        PaidAmount = i.PaidAmount,
                        Status = i.Status
                    })
                    .ToListAsync();

                // Log retrieved invoices for debugging
                Console.WriteLine($"OnGet: Retrieved {Invoices.Count} invoices for StudentID {StudentID}");
                foreach (var invoice in Invoices)
                {
                    Console.WriteLine($"InvoiceID: {invoice.InvoiceID}, Semester: {invoice.Semester}, Status: {invoice.Status}, PaidAmount: {invoice.PaidAmount}, TotalAmount: {invoice.TotalAmount}, BalanceDue: {invoice.BalanceDue}");
                }

                if (!Invoices.Any())
                {
                    ErrorMessage = "No invoices found for your account.";
                }

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading invoices: {ex.Message}";
                Console.WriteLine($"OnGet: Exception - {ex.Message}, StackTrace: {ex.StackTrace}");
                return Page();
            }
        }
    }
}