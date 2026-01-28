using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace StudentEnrollmentSystem.Models
{
    public class VerifyTAC
    {
        [Key]
        public int VerifyTACID { get; set; }
        public string UserID { get; set; }
        public string Email { get; set; }
        public string TACcode { get; set; }
        public bool IsVerify { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime ExpiryTime { get; set; }

        [ForeignKey("UserID")]
        public User? Users { get; set; }
    }
}
