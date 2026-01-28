using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentEnrollmentSystem.Models
{
    public class StudentDetail
    {
        [Key]
        public string StudentID { get; set; }
        public string UserID { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string InstitutionalEmail { get; set; }
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string? PersonalEmail { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Program { get; set; } // Major equivalent
        public DateTime EnrollmentDate { get; set; }
        [RegularExpression(@"^01[0-9]{8,9}$", ErrorMessage = "Phone number must start with '01' and contain 8 or 9 digits.")]
        public string? PhoneNumber { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string? Status { get; set; }
        [Range(0.0, 4.0, ErrorMessage = "CGPA must be between 0.00 and 4.00.")]
        public decimal? CGPA { get; set; }

        // Navigation Properties
        [ForeignKey("UserID")]
        public User? User { get; set; }
        public List<StudentEnrollment>? Enrollments { get; set; }
        public List<StudentEnquiry>? Enquiries { get; set; }
        public List<EnrollmentHistory>? EnrollmentHistories { get; set; }
        public List<EnrollmentRequest>? EnrollmentRequests { get; set; }
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public List<Invoice>? Invoices { get; set; }
        public ContactInformation? ContactInformation { get; set; }
        public PaymentDetail? PaymentDetail { get; set; }
        // Computed properties
        [NotMapped]
        public int TotalCourses { get; set; }

        [NotMapped]
        public int TotalCreditHours { get; set; }
    }
}
