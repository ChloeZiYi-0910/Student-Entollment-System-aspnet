using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace StudentEnrollmentSystem.Models
{
    public class Course
    {
        [Key]
        public string CourseID { get; set; }  // Primary key
        public string CourseName { get; set; }
        public string DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Major { get; set; }
        public int CreditHours { get; set; }
        public string Venue { get; set; }
        public string Lecturer { get; set; }
        public string Section { get; set; }
        public int EnrolledCount { get; set; } // For display purposes, may be computed

        public ICollection<EnrollmentHistory> EnrollmentHistories { get; set; } // Relationship with EnrollmentHistory
        public ICollection<EnrollmentRequest> EnrollmentRequests { get; set; } // Relationship with EnrollmentRequest
        public ICollection<StudentEnrollment> Enrollments { get; set; } // Relationship with StudentEnrollment
        public List<AddDropHistory> AddDropHistories { get; set; } // Relationship with AddDropHistory
        public int TotalSeats { get; set; } // Total capacity of the course
        public int AvailableSeats { get; set; } // Changed to writable property to reflect available seats
        public decimal Cost { get; set; } // Changed from Fee to Cost

        // Constructor to initialize collections
        public Course()
        {
            Enrollments = new List<StudentEnrollment>();
            EnrollmentHistories = new List<EnrollmentHistory>();
            EnrollmentRequests = new List<EnrollmentRequest>();
            AddDropHistories = new List<AddDropHistory>();
        }
    }
}
