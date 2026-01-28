using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentEnrollmentSystem.Models
{
    public class ContactInformation
    {
        public int ContactID { get; set; }
        public string Relationship { get; set; }
        public string ContactPerson { get; set; }
        public string HPNo { get; set; }
        public string AddressLine { get; set; }
        [RegularExpression(@"^\d{5}$", ErrorMessage = "Card number must be exactly 5 digits.")]
        public string Postcode { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string UserID { get; set; }

        [ForeignKey("UserID")]
        public StudentDetail? StudentDetails { get; set; }
    }
}