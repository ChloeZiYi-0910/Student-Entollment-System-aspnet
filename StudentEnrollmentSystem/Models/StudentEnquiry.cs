using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentEnrollmentSystem.Models
{
    public class StudentEnquiry
    {
        [Key]
        public int EnquiryID { get; set; }

        [Required]
        public string UserID { get; set; } // Matches Enquiries(UserID)

        [Required]
        public string Category { get; set; }

        [Required]
        public string Subject { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime CreatedDate { get; set; }

        [ForeignKey("UserID")]
        public User? User { get; set; }

        public List<StudentEnquiryFiles> EnquiryFiles { get; set; } = new List<StudentEnquiryFiles>();
        public List<EnquiryResponse> Responses { get; set; } = new List<EnquiryResponse>();
    }
}