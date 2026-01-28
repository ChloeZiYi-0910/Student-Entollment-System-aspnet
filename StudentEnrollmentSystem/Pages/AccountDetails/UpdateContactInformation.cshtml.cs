using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentEnrollmentSystem.Data;
using StudentEnrollmentSystem.Models;

namespace StudentEnrollmentSystem.Pages.AccountDetails
{
    public class UpdateProfileModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public UpdateProfileModel(ApplicationDbContext context)
        {
            _context = context;
        }
        [BindProperty]
        public ContactInformation ContactInformations { get; set; }
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var UserID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(UserID))
            {
                return RedirectToPage("/Account/Login");
            }

            // Load ContactInformation data
            ContactInformations = await _context.ContactInformations
                .Where(sd => sd.UserID == UserID)
                .SingleOrDefaultAsync() ?? new ContactInformation();


            return Page();
        }
        public async Task<IActionResult> OnPostAsync()
        {
            var UserID = HttpContext.Session.GetString("UserID");
            if (string.IsNullOrEmpty(UserID))
            {
                return RedirectToPage("/Account/Login");
            }
            ModelState.Remove("ContactInformations.UserID");
            if (!ModelState.IsValid)
            {
                foreach (var modelError in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine(modelError.ErrorMessage);
                }
                ErrorMessage = "Please check the form for errors.";
                return Page();
            }

            try
            {
                // Check if contact information exists
                var contactInformationExist = await _context.ContactInformations.AnyAsync(s => s.UserID == UserID);
                ContactInformations.UserID = UserID;
                if (contactInformationExist)
                {
                    string updateSql = @"
                            UPDATE ContactInformation 
                            SET AddressLine = @AddressLine, 
                                Postcode = @Postcode, 
                                City = @City, 
                                State = @State, 
                                Country = @Country,
                                Relationship = @Relationship, 
                                ContactPerson = @ContactPerson, 
                                HPNo = @HPNo
                             
                            WHERE UserID = @UserID";
                    int ContactInformationRowsUpdated = await _context.Database.ExecuteSqlRawAsync(updateSql,
                        new SqlParameter("@AddressLine", ContactInformations.AddressLine),
                        new SqlParameter("@Postcode", ContactInformations.Postcode),
                        new SqlParameter("@City", ContactInformations.City),
                        new SqlParameter("@State", ContactInformations.State),
                        new SqlParameter("@Country", ContactInformations.Country),
                        new SqlParameter("@Relationship", ContactInformations.Relationship),
                        new SqlParameter("@ContactPerson", ContactInformations.ContactPerson),
                        new SqlParameter("@HPNo", ContactInformations.HPNo),
                        new SqlParameter("@UserID", UserID));
                }
                else
                {
                    // Insert new ContactInformation record
                    string insertContactInformationSql = @"
                        INSERT INTO ContactInformation (AddressLine, Postcode, City, State, Country, Relationship, ContactPerson, HPNo, UserID) 
                        VALUES (@AddressLine, @Postcode, @City, @State, @Country, @Relationship, @ContactPerson, @HPNo, @UserID)";

                    int ContactInformationRowsInserted = await _context.Database.ExecuteSqlRawAsync(insertContactInformationSql,
                        new SqlParameter("@AddressLine", ContactInformations.AddressLine),
                        new SqlParameter("@Postcode", ContactInformations.Postcode),
                        new SqlParameter("@City", ContactInformations.City),
                        new SqlParameter("@State", ContactInformations.State),
                        new SqlParameter("@Country", ContactInformations.Country),
                        new SqlParameter("@Relationship", ContactInformations.Relationship),
                        new SqlParameter("@ContactPerson", ContactInformations.ContactPerson),
                        new SqlParameter("@HPNo", ContactInformations.HPNo),
                        new SqlParameter("@UserID", UserID));
                }

                SuccessMessage = "Your changes have been saved successfully.";
            }
            catch (SqlException ex)
            {
                // Log the specific SQL error
                ErrorMessage = "Database error occurred. Please try again." + ex.Message;
            }
            catch (Exception ex)
            {
                // Log the general error
                ErrorMessage = "An error occurred while saving your changes.";
            }

            // Reload the data
            await OnGetAsync();

            return Page();
        }

    }
}
