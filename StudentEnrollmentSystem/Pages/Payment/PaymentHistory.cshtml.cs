// Pages/Payment/PaymentHistory.cshtml.cs
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
    public class PaymentHistoryModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public PaymentHistoryModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public DateTime? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ToDate { get; set; }
        public string StudentID { get; set; }

        public List<PaymentHistoryViewModel> PaymentHistories { get; set; } = new();

        public class PaymentHistoryViewModel
        {
            public DateTime PaymentDate { get; set; }  // Non-nullable
            public decimal Amount { get; set; }  // Non-nullable
            public string PaymentMethod { get; set; }
            public string Reference { get; set; }
            public string Status { get; set; }
            public string ReceiptNumber { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                Console.WriteLine("OnGet: UserID not found, redirecting to /Account/Login");
                Response.Redirect("/Account/Login");
            }

            // Fetch the StudentID from StudentDetails table
            StudentID = await _context.StudentDetails
                .Where(s => s.UserID == userID)
                .Select(s => s.StudentID)
                .FirstOrDefaultAsync();

            // If the StudentID is not found, handle it appropriately
            if (string.IsNullOrEmpty(StudentID))
            {
                Console.WriteLine("Student not found for UserID, redirecting to /Account/Login");
                Response.Redirect("/Account/Login");
            }

            // Build the query with all filters before ordering
            var query = _context.Payments
                .Where(p => p.StudentID == StudentID);

            if (FromDate.HasValue)
            {
                query = query.Where(p => p.PaymentDate >= FromDate.Value);
            }

            if (ToDate.HasValue)
            {
                query = query.Where(p => p.PaymentDate <= ToDate.Value.AddDays(1));
            }

            // Apply ordering after all filters
            var orderedQuery = query.OrderByDescending(p => p.PaymentDate);

            // Use orderedQuery to populate PaymentHistories
            PaymentHistories = await orderedQuery
                .Select(p => new PaymentHistoryViewModel
                {
                    PaymentDate = p.PaymentDate,
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod,
                    Reference = p.ReferenceNumber,
                    Status = p.Status,
                    ReceiptNumber = $"REC-{p.PaymentID:00000}"
                })
                .ToListAsync();

            return Page();
        }
    }
}