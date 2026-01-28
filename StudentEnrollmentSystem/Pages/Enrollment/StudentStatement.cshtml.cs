using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentEnrollmentSystem.Pages.Enrollment
{
    public class StudentStatementModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public StudentStatementModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public string StudentName { get; set; }
        public string IntakeSession { get; set; }
        public string Program { get; set; }

        public decimal Deposit { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal CurrentMonthDue { get; set; }
        public decimal OverdueAmount { get; set; }
        public decimal Balance { get; set; }

        public List<Transaction> Transactions { get; set; }
        public string ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ToDate { get; set; }

        public class Transaction
        {
            public DateTime Date { get; set; }
            public string Process { get; set; }
            public string Particulars { get; set; }
            public string DocumentNo { get; set; }
            public string Session { get; set; }
            public decimal AmountDue { get; set; }
            public decimal AmountPaid { get; set; }
            public decimal TotalDuePaid { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var userID = HttpContext.Session.GetString("UserID");
                if (string.IsNullOrEmpty(userID))
                {
                    ErrorMessage = "Please log in to view your statement.";
                    Console.WriteLine("OnGet: UserID not found, redirecting to /Account/Login");
                    return RedirectToPage("/Account/Login");
                }

                // Fetch StudentID
                var studentId = await _context.StudentDetails
                    .Where(s => s.UserID == userID)
                    .Select(s => s.StudentID)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(studentId))
                {
                    ErrorMessage = "Student profile not found.";
                    Console.WriteLine($"OnGet: StudentID not found for UserID {userID}");
                    return RedirectToPage("/Account/Login");
                }

                // Fetch student details with enrollments
                var student = await _context.StudentDetails
                    .Where(s => s.StudentID == studentId)
                    .Select(s => new
                    {
                        s.StudentID,
                        FirstName = s.FirstName ?? "",
                        LastName = s.LastName ?? "",
                        Program = s.Program ?? "Not Specified",
                        s.EnrollmentDate,
                        Enrollments = s.Enrollments.Select(e => new
                        {
                            e.EnrollmentID,
                            e.ActionDate,
                            Semester = e.Semester ?? "",
                            Course = e.Course != null ? new
                            {
                                CourseID = e.Course.CourseID ?? "",
                                CourseName = e.Course.CourseName ?? "N/A",
                                Major = e.Course.Major ?? "",
                                e.Course.Cost,
                                e.Course.TotalSeats
                            } : null
                        }).ToList()
                    })
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (student == null)
                {
                    ErrorMessage = "Student data not found.";
                    Console.WriteLine($"OnGet: Student not found for StudentID {studentId}");
                    return NotFound("Student not found.");
                }

                // Populate student details
                StudentName = $"{student.LastName} {student.FirstName}".Trim();
                if (string.IsNullOrEmpty(StudentName))
                    StudentName = "Unknown";
                Program = student.Program;

                // Get IntakeSession
                var firstEnrollment = student.Enrollments
                    .Where(e => e.ActionDate.HasValue)
                    .OrderBy(e => e.ActionDate)
                    .FirstOrDefault();

                DateTime intakeDate = firstEnrollment != null && firstEnrollment.ActionDate.HasValue
                    ? firstEnrollment.ActionDate.Value
                    : student.EnrollmentDate;

                IntakeSession = intakeDate.Month >= 1 && intakeDate.Month <= 5
                    ? $"JAN{intakeDate.Year}"
                    : $"JUN{intakeDate.Year}";

                // Fetch deposits
                Deposit = await _context.Payments
                    .Where(p => p.StudentID == studentId && (p.PaymentType ?? "") == "Deposit" && (p.Status ?? "") == "Completed")
                    .SumAsync(p => p.Amount);

                // Fetch invoices and payments
                var today = DateTime.Today;
                var isTodayOnly = FromDate.HasValue && ToDate.HasValue && FromDate.Value.Date == today && ToDate.Value.Date == today;

                var invoicesQuery = _context.Invoices
                    .Where(i => i.StudentID == studentId)
                    .Select(i => new
                    {
                        i.InvoiceID,
                        i.StudentID,
                        Semester = i.Semester ?? "",
                        i.TotalAmount,
                        i.PaidAmount,
                        i.IssueDate,
                        i.InstallmentDueDate,
                        Status = i.Status ?? ""
                    })
                    .AsNoTracking();

                var paymentsQuery = _context.Payments
                    .Where(p => p.StudentID == studentId && (p.Status ?? "") == "Completed")
                    .Select(p => new
                    {
                        p.PaymentID,
                        p.StudentID,
                        p.Amount,
                        p.PaymentDate,
                        PaymentMethod = p.PaymentMethod ?? "",
                        PaymentType = p.PaymentType ?? "",
                        ReferenceNumber = p.ReferenceNumber ?? "",
                        Status = p.Status ?? ""
                    })
                    .AsNoTracking();

                if (isTodayOnly)
                {
                    invoicesQuery = invoicesQuery.Where(i => i.IssueDate.Date == today);
                    paymentsQuery = paymentsQuery.Where(p => p.PaymentDate.Date == today);
                }
                else
                {
                    if (FromDate.HasValue)
                    {
                        invoicesQuery = invoicesQuery.Where(i => i.IssueDate >= FromDate.Value);
                        paymentsQuery = paymentsQuery.Where(p => p.PaymentDate >= FromDate.Value);
                    }

                    if (ToDate.HasValue)
                    {
                        invoicesQuery = invoicesQuery.Where(i => i.IssueDate <= ToDate.Value);
                        paymentsQuery = paymentsQuery.Where(p => p.PaymentDate <= ToDate.Value);
                    }
                }

                var invoices = await invoicesQuery
                    .OrderBy(i => i.IssueDate)
                    .ToListAsync();

                var payments = await paymentsQuery
                    .OrderBy(p => p.PaymentDate)
                    .ToListAsync();

                // Map transactions
                Transactions = new List<Transaction>();
                Transactions.AddRange(invoices.Select(i => new Transaction
                {
                    Date = i.IssueDate,
                    Process = "Invoice",
                    Particulars = $"Invoice for {(i.Semester.Length > 0 ? i.Semester : "N/A")}",
                    DocumentNo = $"INV-{i.InvoiceID}",
                    Session = i.Semester,
                    AmountDue = i.TotalAmount,
                    AmountPaid = 0m,
                    TotalDuePaid = i.TotalAmount - i.PaidAmount
                }));

                Transactions.AddRange(payments.Select(p => new Transaction
                {
                    Date = p.PaymentDate,
                    Process = "Payment",
                    Particulars = $"Payment {(p.PaymentMethod.Length > 0 ? $"({p.PaymentMethod})" : "(Unknown)")}",
                    DocumentNo = p.ReferenceNumber.Length > 0 ? p.ReferenceNumber : $"PAY-{p.PaymentID}",
                    Session = "",
                    AmountDue = 0m,
                    AmountPaid = p.Amount,
                    TotalDuePaid = -p.Amount
                }));

                Transactions = Transactions.OrderBy(t => t.Date).ToList();

                // Financial summary
                TotalAmount = invoices.Sum(i => i.TotalAmount);
                var allInvoices = await _context.Invoices
                    .Where(i => i.StudentID == studentId)
                    .Select(i => new
                    {
                        i.InvoiceID,
                        i.StudentID,
                        Semester = i.Semester ?? "",
                        i.TotalAmount,
                        i.PaidAmount,
                        i.IssueDate,
                        i.InstallmentDueDate,
                        Status = i.Status ?? ""
                    })
                    .AsNoTracking()
                    .ToListAsync();

                CurrentMonthDue = allInvoices
                    .Where(i => i.InstallmentDueDate.Month == today.Month && i.InstallmentDueDate.Year == today.Year && i.Status != "Paid")
                    .Sum(i => i.TotalAmount - i.PaidAmount);

                OverdueAmount = allInvoices
                    .Where(i => i.InstallmentDueDate < today && i.Status != "Paid")
                    .Sum(i => i.TotalAmount - i.PaidAmount);

                Balance = allInvoices.Sum(i => i.TotalAmount - i.PaidAmount);
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred while loading your statement. Please try again later.";
                Console.WriteLine($"OnGet: Exception - {ex.Message}, StackTrace: {ex.StackTrace}");
            }

            return Page();
        }
    }
}