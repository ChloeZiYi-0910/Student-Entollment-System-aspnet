namespace StudentEnrollmentSystem.Models
{
    public class StudentEnquiryFiles
    {
        public int FileID { get; set; }
        public int EnquiryID { get; set; }
        public string FilePath { get; set; }

        public StudentEnquiry Enquiry { get; set; }
    }
}