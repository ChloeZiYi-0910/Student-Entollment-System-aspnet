using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentEnrollmentSystem.Models
{
    public class InvoiceDetail
    {
        [Key]
        public int InvoiceDetailID { get; set; }

        [ForeignKey(nameof(Invoice))]
        public int InvoiceID { get; set; }

        [ForeignKey(nameof(Course))]
        public string CourseID { get; set; }
        public decimal CourseFee { get; set; }

        public Invoice Invoice { get; set; }
        public Course Course { get; set; }
    }
}
