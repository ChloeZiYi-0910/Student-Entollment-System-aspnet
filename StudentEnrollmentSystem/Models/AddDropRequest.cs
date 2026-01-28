using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentEnrollmentSystem.Models
{
    public class AddDropRequest
    {
        [Key]
        public int RequestID { get; set; }

        [Required]
        public string StudentID { get; set; }

        [Required]
        public string CourseID { get; set; }

        [Required]
        [StringLength(10)]
        public string Action { get; set; } // "Add" or "Drop"

        [Required]
        [StringLength(20)]
        public string Status { get; set; } // "Pending", "Approved", "Rejected"

        public DateTime RequestDate { get; set; }

        public DateTime? DecisionDate { get; set; }

        public string AdminID { get; set; }

        [ForeignKey("StudentID")]
        public StudentDetail Student { get; set; }

        [ForeignKey("CourseID")]
        public Course Course { get; set; }

        [ForeignKey("AdminID")]
        public User Admin { get; set; }
    }
}