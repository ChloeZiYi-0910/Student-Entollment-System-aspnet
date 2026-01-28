using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Models;

namespace StudentEnrollmentSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<StudentEnrollment> Enrollments { get; set; }
        public DbSet<StudentDetail> StudentDetails { get; set; }
        public DbSet<ContactInformation> ContactInformations { get; set; }
        public DbSet<PaymentDetail> PaymentDetails { get; set; }
        public DbSet<PaymentDocumentHistory> PaymentDocumentHistories { get; set; } // New DbSet
        public DbSet<StudentEnquiry> Enquiries { get; set; }
        public DbSet<StudentEnquiryFiles> EnquiryFiles { get; set; }
        public DbSet<EnrollmentHistory> EnrollmentHistories { get; set; }
        public DbSet<EnrollmentRequest> EnrollmentRequests { get; set; }
        public DbSet<EnquiryResponse> EnquiryResponses { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceDetail> InvoiceDetails { get; set; }
        public DbSet<EvaluationStatus> EvaluationStatuses { get; set; }
        public DbSet<EvaluationResponse> EvaluationResponses { get; set; }
        public DbSet<VerifyTAC> VerifyTAC { get; set; } // Added for TAC verification


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Table Mappings
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<Course>().ToTable("Courses");
            modelBuilder.Entity<StudentEnrollment>().ToTable("Enrollments");
            modelBuilder.Entity<StudentDetail>().ToTable("StudentDetails");
            modelBuilder.Entity<ContactInformation>().ToTable("ContactInformation");
            modelBuilder.Entity<PaymentDetail>().ToTable("PaymentDetails");
            modelBuilder.Entity<StudentEnquiry>().ToTable("Enquiries");
            modelBuilder.Entity<StudentEnquiryFiles>().ToTable("EnquiryFiles");
            modelBuilder.Entity<EnrollmentHistory>().ToTable("EnrollmentHistory");
            modelBuilder.Entity<EnrollmentRequest>().ToTable("EnrollmentRequests");
            modelBuilder.Entity<EnquiryResponse>().ToTable("EnquiryResponses");
            modelBuilder.Entity<Payment>().ToTable("Payments");
            modelBuilder.Entity<Invoice>().ToTable("Invoices");
            modelBuilder.Entity<InvoiceDetail>().ToTable("InvoiceDetails");
            modelBuilder.Entity<EvaluationResponse>().ToTable("EvaluationResponse");
            modelBuilder.Entity<EvaluationStatus>().ToTable("EvaluationStatus");
            modelBuilder.Entity<VerifyTAC>().ToTable("VerifyTAC");

            // Primary Keys
            modelBuilder.Entity<User>().HasKey(u => u.UserID);
            modelBuilder.Entity<Course>().HasKey(c => c.CourseID);
            modelBuilder.Entity<StudentEnrollment>().HasKey(se => se.EnrollmentID);
            modelBuilder.Entity<StudentDetail>().HasKey(sd => sd.StudentID);
            modelBuilder.Entity<ContactInformation>().HasKey(ci => ci.ContactID);
            modelBuilder.Entity<PaymentDetail>().HasKey(bd => bd.PaymentDetailsID);
            modelBuilder.Entity<StudentEnquiry>().HasKey(se => se.EnquiryID);
            modelBuilder.Entity<StudentEnquiryFiles>().HasKey(sef => sef.FileID);
            modelBuilder.Entity<EnrollmentHistory>().HasKey(eh => eh.HistoryID);
            modelBuilder.Entity<EnrollmentRequest>().HasKey(er => er.RequestID);
            modelBuilder.Entity<EnquiryResponse>().HasKey(er => er.ResponseID);
            modelBuilder.Entity<Payment>().HasKey(p => p.PaymentID);
            modelBuilder.Entity<Invoice>().HasKey(i => i.InvoiceID);
            modelBuilder.Entity<InvoiceDetail>().HasKey(id => id.InvoiceDetailID);
            modelBuilder.Entity<EvaluationResponse>().HasKey(er => er.EvaluationResponseID);
            modelBuilder.Entity<EvaluationStatus>().HasKey(es => es.EvaluationStatusID);
            modelBuilder.Entity<VerifyTAC>().HasKey(es => es.VerifyTACID);

            modelBuilder.Entity<EvaluationStatus>()
                .HasOne(es => es.Enrollment)
                .WithMany() // or .WithOne() depending on your design
                .HasForeignKey(es => es.EnrollmentID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EvaluationStatus>()
                .HasOne(es => es.EvaluationResponse)
                .WithOne(er => er.EvaluationStatus)
                .HasForeignKey<EvaluationResponse>(er => er.EvaluationStatusID)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationships
            modelBuilder.Entity<StudentEnquiry>()
                .HasOne(se => se.User)
                .WithMany(u => u.Enquiries)
                .HasForeignKey(se => se.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentEnquiryFiles>()
                .HasOne(ef => ef.Enquiry)
                .WithMany(e => e.EnquiryFiles)
                .HasForeignKey(ef => ef.EnquiryID)
                .OnDelete(DeleteBehavior.Cascade);

            // EnquiryResponse -> User relationship
            modelBuilder.Entity<EnquiryResponse>()
                .HasOne(e => e.User)
                .WithMany(u => u.EnquiryResponses)
                .HasForeignKey(e => e.UserID)
                .OnDelete(DeleteBehavior.Restrict); // Prevent accidental deletions

            // EnquiryResponse -> Enquiry relationship
            modelBuilder.Entity<EnquiryResponse>()
                .HasOne(e => e.Enquiry)
                .WithMany(e => e.Responses)
                .HasForeignKey(e => e.EnquiryID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentEnrollment>()
                .HasOne(se => se.Student)
                .WithMany()
                .HasForeignKey(se => se.StudentID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentEnrollment>()
                .HasOne(se => se.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(se => se.CourseID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EnrollmentHistory>()
                .HasOne(eh => eh.Student)
                .WithMany()
                .HasForeignKey(eh => eh.StudentID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EnrollmentHistory>()
                .HasOne(eh => eh.Course)
                .WithMany(c => c.EnrollmentHistories)
                .HasForeignKey(eh => eh.CourseID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EnrollmentRequest>()
            .HasOne(e => e.StudentDetail)
            .WithMany(s => s.EnrollmentRequests)
            .HasForeignKey(e => e.StudentID)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EnrollmentRequest>()
                .HasOne(e => e.Course)
                .WithMany(c => c.EnrollmentRequests)
                .HasForeignKey(e => e.CourseID)
                .OnDelete(DeleteBehavior.Restrict);

            // StudentDetail to StudentEnrollment relationship (one-to-many)
            modelBuilder.Entity<StudentDetail>()
                .HasMany(s => s.Enrollments)  // A StudentDetail can have many Enrollments
                .WithOne(se => se.Student)  // Each Enrollment has one StudentDetail
                .HasForeignKey(se => se.StudentID)  // The foreign key in StudentEnrollment is StudentID
                .OnDelete(DeleteBehavior.Cascade);  // Cascade delete if needed

            modelBuilder.Entity<StudentDetail>()
                .HasOne(sd => sd.User)
                .WithOne(u => u.StudentDetail)
                .HasForeignKey<StudentDetail>(sd => sd.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContactInformation>()
                .HasOne(ci => ci.StudentDetails)
                .WithOne(sd => sd.ContactInformation)
                .HasForeignKey<ContactInformation>(ci => ci.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentDetail>()
                .HasOne(bd => bd.StudentDetails)
                .WithOne(sd => sd.PaymentDetail)
                .HasForeignKey<PaymentDetail>(bd => bd.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Student)  // Invoice has one Student
                .WithMany(s => s.Invoices)  // Student has many Invoices
                .HasForeignKey(i => i.StudentID)  // Invoice's foreign key is StudentID
                .OnDelete(DeleteBehavior.Cascade);  // Optionally configure cascading delete behavior

            modelBuilder.Entity<InvoiceDetail>()
                .HasOne(id => id.Invoice)
                .WithMany(i => i.Details)
                .HasForeignKey(id => id.InvoiceID)
                .OnDelete(DeleteBehavior.Cascade); // Matches ON DELETE CASCADE

            modelBuilder.Entity<InvoiceDetail>()
                .HasOne(id => id.Course)
                .WithMany()
                .HasForeignKey(id => id.CourseID)
                .OnDelete(DeleteBehavior.Restrict); // No CASCADE in DB

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Student)
                .WithMany(s => s.Payments)
                .HasForeignKey(p => p.StudentID)
                .OnDelete(DeleteBehavior.Cascade); 

            // Additional Configurations
            modelBuilder.Entity<StudentDetail>()
                .Property(sd => sd.CGPA)
                .HasColumnType("decimal(3,2)");

            modelBuilder.Entity<Course>()
                .Property(c => c.Major)
                .HasDefaultValue("General");

            modelBuilder.Entity<EnrollmentHistory>()
                .Property(eh => eh.ActionDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<EnrollmentRequest>()
                .Property(er => er.RequestDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<EnrollmentRequest>()
                .Property(er => er.Status)
                .HasDefaultValue("Pending");

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasDefaultValue("Student");

            modelBuilder.Entity<Course>()
                .Property(c => c.Cost)
                .HasColumnType("decimal(18,2)"); 


            modelBuilder.Entity<Invoice>()
                .Property(i => i.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Invoice>()
                .Property(i => i.PaidAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<InvoiceDetail>()
                .Property(id => id.CourseFee)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Invoice>()
                .Property(i => i.Status)
                .HasDefaultValue("Pending");

            modelBuilder.Entity<Payment>()
                .Property(p => p.Status)
                .HasDefaultValue("Pending");

            modelBuilder.Entity<PaymentDocumentHistory>().ToTable("PaymentDocumentHistory");
            modelBuilder.Entity<PaymentDocumentHistory>().HasKey(pdh => pdh.HistoryID);
            modelBuilder.Entity<PaymentDocumentHistory>()
                .HasOne(pdh => pdh.PaymentDetail)
                .WithMany(pd => pd.PaymentDocumentHistories)
                .HasForeignKey(pdh => pdh.PaymentDetailsID)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}