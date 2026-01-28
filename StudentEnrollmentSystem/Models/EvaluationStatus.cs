using System.ComponentModel.DataAnnotations;

namespace StudentEnrollmentSystem.Models
{
    public class EvaluationStatus
    {
        [Key]
        public int EvaluationStatusID { get; set; }

        public int EnrollmentID { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime? FilledUpDate { get; set; }

        // Navigation
        public StudentEnrollment Enrollment { get; set; }
        public EvaluationResponse EvaluationResponse { get; set; }
    }
}
