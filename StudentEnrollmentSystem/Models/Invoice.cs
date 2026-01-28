using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentEnrollmentSystem.Models
{
    public class Invoice
    {
        [Key]
        public int InvoiceID { get; set; }

        [Required]
        [StringLength(10)]
        //[ForeignKey(nameof(Student))]
        public string StudentID { get; set; }

        [Required]
        [StringLength(20)]
        public string Semester { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal PaidAmount { get; set; }

        [Required]
        public DateTime IssueDate { get; set; }

        [Required]
        public DateTime InstallmentDueDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } // 'Pending', 'Partial', 'Paid', 'Overdue'

        // Navigation property (Ensuring proper FK mapping)
        public StudentDetail Student { get; set; }

        // Invoice details collection
        public List<InvoiceDetail> Details { get; set; } = new List<InvoiceDetail>();

        // Computed property for remaining balance
        [Display(Name = "Balance Due")]
        [NotMapped] // Ensures this is not treated as a database column
        public decimal BalanceDue => TotalAmount - PaidAmount;
    }
}
