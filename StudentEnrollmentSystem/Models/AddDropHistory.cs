using System.ComponentModel.DataAnnotations;

namespace StudentEnrollmentSystem.Models
{
    public class AddDropHistory
    {
        [Key]
        public int HistoryID { get; set; }
        public string StudentID { get; set; }
        public string CourseID { get; set; }
        public string Action { get; set; }
        public DateTime ActionDate { get; set; }
        public string Reason { get; set; } // Add this if you’ve updated the table

        public StudentDetail Student { get; set; }
        public Course Course { get; set; }
    }
}