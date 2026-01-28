using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentEnrollmentSystem.Models
{
    public class EnrollmentHistory
    {
        [Key]
        public int HistoryID { get; set; }
        [ForeignKey(nameof(Student))]
        public string StudentID { get; set; }

        [ForeignKey(nameof(Course))]
        public string CourseID { get; set; }
        public string Action { get; set; }
        public DateTime ActionDate { get; set; }
        public string Reason { get; set; } // Added to match prior logic (optional, add to DB if needed)

        public Course Course { get; set; }
        public StudentDetail Student { get; set; }
    }
}