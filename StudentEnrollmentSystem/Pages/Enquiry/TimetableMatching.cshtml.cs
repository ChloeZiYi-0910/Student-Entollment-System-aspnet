using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;
using StudentEnrollmentSystem.Utilities;

namespace StudentEnrollmentSystem.Pages.Enquiry
{
    public class TimetableMatchingModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public TimetableMatchingModel(ApplicationDbContext context)
        {
            _context = context;
        }
        public List<Course> EnrolledCourses { get; set; } = new List<Course>();
        public List<Course> AvailableCourses { get; set; } = new(); // Courses not enrolled
        public List<Course> MatchedCourses { get; set; } = new(); // Store selected courses for display
        public string ClashMessage { get; set; } = "";
        public string[] DaysOfWeek { get; set; } = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        [BindProperty]
        [Required(ErrorMessage = "Please select a course.")]
        public string SelectedCourseID { get; set; }

        public IActionResult OnGet()
        {
            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                return RedirectToPage("/Account/Login");
            }

            var studentInfo = GetStudentInfo(userID);
            if (studentInfo == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var studentID = studentInfo.StudentID;
            var studentProgram = studentInfo.Program;

            // Fetch enrolled courses for the student
            EnrolledCourses = GetEnrolledCourses(studentID);

            // Get matched courses from session (if any)
            var matchedCoursesJson = HttpContext.Session.GetString("MatchedCourses");
            if (!string.IsNullOrEmpty(matchedCoursesJson))
            {
                try
                {
                    var matchedCourseIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(matchedCoursesJson);
                    if (matchedCourseIds != null && matchedCourseIds.Any())
                    {
                        MatchedCourses = _context.Courses
                            .Where(c => matchedCourseIds.Contains(c.CourseID))
                            .Select(c => new Course
                            {
                                CourseID = c.CourseID,
                                CourseName = c.CourseName,
                                DayOfWeek = c.DayOfWeek,
                                StartTime = c.StartTime,
                                EndTime = c.EndTime,
                                CreditHours = c.CreditHours,
                                Venue = c.Venue,
                                Lecturer = c.Lecturer,
                                Section = c.Section
                            })
                            .ToList();
                    }
                }
                catch
                {
                    // Handle deserialization error if needed
                    HttpContext.Session.Remove("MatchedCourses");
                }
            }

            // Fetch available courses based on the student's program
            AvailableCourses = GetAvailableCourses(studentID, studentProgram);
            return Page();
        }

        public IActionResult OnPostAddCourse()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            if (string.IsNullOrEmpty(SelectedCourseID))
            {
                return RedirectToPage();
            }

            var userID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(userID))
            {
                return RedirectToPage("/Account/Login");
            }

            var studentInfo = GetStudentInfo(userID);
            if (studentInfo == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var studentID = studentInfo.StudentID;

            // Get course details
            var courseToAdd = _context.Courses
                .Where(c => c.CourseID == SelectedCourseID)
                .Select(c => new Course
                {
                    CourseID = c.CourseID,
                    CourseName = c.CourseName,
                    DayOfWeek = c.DayOfWeek,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    CreditHours = c.CreditHours,
                    Venue = c.Venue,
                    Lecturer = c.Lecturer,
                    Section = c.Section
                })
                .FirstOrDefault();

            if (courseToAdd == null)
            {
                return RedirectToPage();
            }

            // Get enrolled courses
            EnrolledCourses = GetEnrolledCourses(studentID);

            // Get matched courses from session
            var matchedCourseIds = new List<string>();
            var matchedCoursesJson = HttpContext.Session.GetString("MatchedCourses");
            if (!string.IsNullOrEmpty(matchedCoursesJson))
            {
                try
                {
                    matchedCourseIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(matchedCoursesJson) ?? new List<string>();
                }
                catch
                {
                    matchedCourseIds = new List<string>();
                }
            }

            // Check if course is already in matched list
            if (matchedCourseIds.Contains(SelectedCourseID))
            {
                ClashMessage = $"Course {courseToAdd.CourseID} is already in your preview list.";
                return RedirectToPage();
            }

            // Add all matched courses to check for timetable clashes
            var allCoursesToCheck = new List<Course>(EnrolledCourses);
            if (matchedCourseIds.Any())
            {
                var tempMatchedCourses = _context.Courses
                    .Where(c => matchedCourseIds.Contains(c.CourseID))
                    .Select(c => new Course
                    {
                        CourseID = c.CourseID,
                        CourseName = c.CourseName,
                        DayOfWeek = c.DayOfWeek,
                        StartTime = c.StartTime,
                        EndTime = c.EndTime,
                        CreditHours = c.CreditHours,
                        Venue = c.Venue,
                        Lecturer = c.Lecturer,
                        Section = c.Section
                    })
                    .ToList();
                allCoursesToCheck.AddRange(tempMatchedCourses);
            }

            // Check for time clash
            if (IsCourseClashing(courseToAdd, allCoursesToCheck))
            {
                ClashMessage = $"Course {courseToAdd.CourseID} has a time clash with your current timetable.";
                HttpContext.Session.SetString("ClashMessage", ClashMessage);
                return RedirectToPage();
            }

            // Add course to matched list
            matchedCourseIds.Add(SelectedCourseID);
            HttpContext.Session.SetString("MatchedCourses", System.Text.Json.JsonSerializer.Serialize(matchedCourseIds));

            return RedirectToPage();
        }

        public IActionResult OnPostClearMatched()
        {
            HttpContext.Session.Remove("MatchedCourses");
            return RedirectToPage();
        }

        // Helper function to fetch student info based on UserID
        private dynamic GetStudentInfo(string userID)
        {
            return _context.StudentDetails
                .Where(s => s.UserID == userID)
                .Select(s => new { s.StudentID, s.Program })
                .FirstOrDefault();
        }

        private List<Course> GetEnrolledCourses(string studentID)
        {
            var courses = _context.Enrollments
                .Where(e => e.StudentID == studentID && e.Semester == SemesterHelper.GetCurrentSemester())
                .Join(_context.Courses,
                    e => e.CourseID,
                    c => c.CourseID,
                    (e, c) => new Course
                    {
                        CourseID = c.CourseID,
                        CourseName = c.CourseName,
                        DayOfWeek = c.DayOfWeek,
                        StartTime = c.StartTime,
                        EndTime = c.EndTime,
                        CreditHours = c.CreditHours,
                        Venue = c.Venue,
                        Lecturer = c.Lecturer,
                        Section = c.Section
                    })
                .AsNoTracking()
                .ToList();

            return courses;
        }

        // Helper function to fetch available courses for a student based on program
        private List<Course> GetAvailableCourses(string studentID, string studentProgram)
        {
            var enrolledCourseIDs = EnrolledCourses.Select(c => c.CourseID).ToList();

            return _context.Courses
                .Where(c => !enrolledCourseIDs.Contains(c.CourseID)) // Exclude already enrolled courses
                .Where(c => c.Major == studentProgram) // Ensure the course's Major matches the student's Program
                .Select(c => new Course
                {
                    CourseID = c.CourseID,
                    CourseName = c.CourseName,
                    DayOfWeek = c.DayOfWeek,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    CreditHours = c.CreditHours,
                    Venue = c.Venue,
                    Lecturer = c.Lecturer,
                    Section = c.Section
                })
                .AsNoTracking()
                .ToList();
        }

        // Helper function to check if a course time clashes with any enrolled courses
        private bool IsCourseClashing(Course courseToCheck, List<Course> coursesToCompare = null)
        {
            var courses = coursesToCompare ?? EnrolledCourses;

            return courses.Any(e =>
                e.DayOfWeek == courseToCheck.DayOfWeek &&
                ((e.StartTime < courseToCheck.EndTime && e.EndTime > courseToCheck.StartTime) ||
                 (courseToCheck.StartTime < e.EndTime && courseToCheck.EndTime > e.StartTime))
            );
        }
    }
}