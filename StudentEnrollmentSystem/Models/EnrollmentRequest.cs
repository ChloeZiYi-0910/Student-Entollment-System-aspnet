using StudentEnrollmentSystem.Models;
using System.ComponentModel.DataAnnotations.Schema;

public class EnrollmentRequest
{
    public int RequestID { get; set; }
    [ForeignKey(nameof(StudentDetail))]
    public string StudentID { get; set; }
    [ForeignKey(nameof(Course))]
    public string CourseID { get; set; }
    public string Action { get; set; } // "Add" or "Drop"
    public string Reason { get; set; }
    public DateTime? RequestDate { get; set; }
    public string Status { get; set; } // "Pending", "Approved", "Rejected"

    public StudentDetail StudentDetail { get; set; }

    [ForeignKey("CourseID")]
    public Course Course { get; set; }
}