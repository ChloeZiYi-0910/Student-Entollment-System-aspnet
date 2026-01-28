// Utilities/SemesterHelper.cs
using System;
namespace StudentEnrollmentSystem.Utilities
{
    public static class SemesterHelper
    {
        public static string GetCurrentSemester()
        {
            var now = DateTime.Now;
            return now.Month <= 5 ? $"JAN{now.Year}" : $"JUN{now.Year}";
        }

        public static string GetNextSemester()
        {
            var now = DateTime.Now;
            return now.Month <= 5 ? $"JUN{now.Year}" : $"JAN{now.Year + 1}";
        }

        public static (DateTime startDate, DateTime endDate) GetSemesterDates(string semesterCode)
        {
            if (semesterCode.Length < 7 || (!semesterCode.StartsWith("JAN") && !semesterCode.StartsWith("JUN")))
                throw new ArgumentException("Invalid semester code");
            var year = int.Parse(semesterCode.Substring(3));
            var isJanSemester = semesterCode.StartsWith("JAN");
            return isJanSemester
                ? (new DateTime(year, 1, 1), new DateTime(year, 5, 31))
                : (new DateTime(year, 6, 1), new DateTime(year, 11, 30));
        }

        public static bool IsFirstSemester(DateTime userCreatedDate)
        {
            // Get current semester end date
            string currentSemester = GetCurrentSemester();
            var (_, currentSemesterEndDate) = GetSemesterDates(currentSemester);

            // If user was created in the current semester, it's their first semester
            string previousSemester;
            if (currentSemester.StartsWith("JAN"))
            {
                // If current is JAN, previous was JUN of last year
                int year = int.Parse(currentSemester.Substring(3)) - 1;
                previousSemester = $"JUN{year}";
            }
            else
            {
                // If current is JUN, previous was JAN of same year
                int year = int.Parse(currentSemester.Substring(3));
                previousSemester = $"JAN{year}";
            }

            var (previousSemesterStartDate, _) = GetSemesterDates(previousSemester);

            // User is in first semester if they were created after the start of the previous semester
            return userCreatedDate >= previousSemesterStartDate;
        }
    }
}