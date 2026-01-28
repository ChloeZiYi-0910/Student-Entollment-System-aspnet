// Model\PaymentDocumentHistory.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentEnrollmentSystem.Models
{
    public class PaymentDocumentHistory
    {
        [Key]
        public int HistoryID { get; set; }
        public int PaymentDetailsID { get; set; }
        public string? FilePath { get; set; }
        public DateTime UploadDate { get; set; }

        [ForeignKey("PaymentDetailsID")]
        public PaymentDetail PaymentDetail { get; set; }
    }
}