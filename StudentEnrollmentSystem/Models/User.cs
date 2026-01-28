using System.ComponentModel.DataAnnotations;

namespace StudentEnrollmentSystem.Models
{
    public class User
    {
        [Key]
        public string UserID { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; } = "Student"; // Default to "Student"
        public DateTime CreatedDate { get; set; } = DateTime.Now; // Matches SQL default GETDATE()

        // Navigation properties
        public StudentDetail? StudentDetail { get; set; } // Must be nullable with '?'
        public ICollection<EnrollmentHistory> EnrollmentHistories { get; set; }
        public ICollection<EnrollmentRequest> EnrollmentRequests { get; set; }
        public ICollection<StudentEnrollment> Enrollments { get; set; }
        public ICollection<StudentEnquiry> Enquiries { get; set; }
        public ICollection<EnquiryResponse> EnquiryResponses { get; set; } // Added

        public User()
        {
            EnrollmentHistories = new List<EnrollmentHistory>();
            EnrollmentRequests = new List<EnrollmentRequest>();
            Enrollments = new List<StudentEnrollment>();
            Enquiries = new List<StudentEnquiry>();
            EnquiryResponses = new List<EnquiryResponse>();
        }
    }
}