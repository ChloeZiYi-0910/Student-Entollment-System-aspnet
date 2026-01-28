using System.ComponentModel.DataAnnotations.Schema;

namespace StudentEnrollmentSystem.Models
{
    public class EnquiryResponse
    {
        public int ResponseID { get; set; }
        [ForeignKey(nameof(Enquiry))] // Explicitly declare Enquiry foreign key
        public int EnquiryID { get; set; }
        [ForeignKey(nameof(User))] // Explicitly declare User foreign key
        public string UserID { get; set; } 
        public string Comment { get; set; }
        public DateTime ResponseDate { get; set; }

        // Navigation properties
        public StudentEnquiry Enquiry { get; set; }
        public User User { get; set; }
    }
}