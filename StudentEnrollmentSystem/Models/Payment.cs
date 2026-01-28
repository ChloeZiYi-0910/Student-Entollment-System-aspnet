using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentEnrollmentSystem.Models
{
    public class Payment
    {
        [Key]
        public int PaymentID { get; set; }
        [ForeignKey(nameof(Student))]  // Ensure this maps correctly
        public string StudentID { get; set; }
        public decimal Amount { get; set; } = 0m;  // Non-nullable with default
        public string PaymentMethod { get; set; }
        public string PaymentType { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.Now;  // Non-nullable with default
        public string Status { get; set; }
        public string ReferenceNumber { get; set; }
        public string ProofFilePath { get; set; }

        public virtual StudentDetail Student { get; set; }
    }
}
