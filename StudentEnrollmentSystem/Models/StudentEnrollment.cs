using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentEnrollmentSystem.Models
{
    public class StudentEnrollment
    {
        [Key]
        public int EnrollmentID { get; set; }

        [ForeignKey(nameof(StudentDetail))]
        public string StudentID { get; set; }

        [ForeignKey(nameof(Course))]
        public string CourseID { get; set; }
        public string Semester {  get; set; }
        public string LastAction { get; set; }
        public DateTime? ActionDate { get; set; }

        public StudentDetail Student { get; set; }
        public Course Course { get; set; }
    }
}