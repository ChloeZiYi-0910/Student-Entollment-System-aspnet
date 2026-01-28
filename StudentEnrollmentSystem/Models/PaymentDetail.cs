using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace StudentEnrollmentSystem.Models
{
    public class PaymentDetail
    {
        public int PaymentDetailsID { get; set; }

        [Required(ErrorMessage = "Bank name is required.")]
        public string BankName { get; set; }

        [Required(ErrorMessage = "Card number is required.")]
        [RegularExpression(@"^\d{16}$", ErrorMessage = "Card number must be exactly 16 digits.")]
        public string CardAccNo { get; set; }

        [Required(ErrorMessage = "Card holder name is required.")]
        public string CardHolderName { get; set; }

        public string? CardDOC { get; set; } // Keep this for the current document

        [Required(ErrorMessage = "Expiry date is required.")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/(\d{2})$", ErrorMessage = "Expiry date must be in MM/YY format.")]
        public string ExpiryDate { get; set; }

        [NotMapped]
        public IFormFile? CardDocumentFile { get; set; } // Optional file upload

        public string UserID { get; set; }

        [ForeignKey("UserID")]
        public StudentDetail? StudentDetails { get; set; }

        public virtual ICollection<PaymentDocumentHistory> PaymentDocumentHistories { get; set; } = new List<PaymentDocumentHistory>();
    }
}