using System.ComponentModel.DataAnnotations;

namespace StudentEnrollmentSystem.Models
{
    public class EvaluationResponse
    {
        [Key]
        public int EvaluationResponseID { get; set; }

        public int EvaluationStatusID { get; set; }
        [Required(ErrorMessage = "Please fill up this field.")]
        public int? Q1 { get; set; }
        public string? Q2 { get; set; } 
        [Required(ErrorMessage = "Please fill up this field.")]
        public int? Q3 { get; set; }
        public string? Q4 { get; set; } 

        // Navigation
        public EvaluationStatus? EvaluationStatus { get; set; }
    }
}
