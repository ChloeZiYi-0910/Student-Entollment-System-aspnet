using Microsoft.AspNetCore.Identity; // For PasswordHasher
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StudentEnrollmentSystem.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace StudentEnrollmentSystem.Pages.Admin
{
    public class ManageAdminsModel : PageModel
    {
        private readonly string _connectionString;
        private readonly PasswordHasher<User> _passwordHasher; // Instantiate directly

        public ManageAdminsModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _passwordHasher = new PasswordHasher<User>(); // Same as ForgotPasswordModel
        }

        public List<User> Admins { get; set; } = new List<User>();

        [BindProperty]
        public User NewAdmin { get; set; } = new User();

        public string Message { get; set; }

        public IActionResult OnGet()
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            LoadAdmins();
            return Page();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (!IsAdmin()) return RedirectToPage("/Account/Login");

            Console.WriteLine("OnPostAddAsync started.");

            // Remove StudentDetail from validation since it's not in the form
            ModelState.Remove("NewAdmin.StudentDetail");

            // Manual validation for form fields only
            if (string.IsNullOrWhiteSpace(NewAdmin.UserID))
            {
                ModelState.AddModelError("NewAdmin.UserID", "User ID is required.");
            }
            if (string.IsNullOrWhiteSpace(NewAdmin.FullName))
            {
                ModelState.AddModelError("NewAdmin.FullName", "Full Name is required.");
            }
            if (string.IsNullOrWhiteSpace(NewAdmin.Password) || NewAdmin.Password.Length < 6)
            {
                ModelState.AddModelError("NewAdmin.Password", "Password must be at least 6 characters long.");
            }

            if (!ModelState.IsValid)
            {
                Console.WriteLine("Validation failed: " + string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                LoadAdmins();
                return Page();
            }

            Console.WriteLine($"Adding admin: UserID={NewAdmin.UserID}, FullName={NewAdmin.FullName}");

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    Console.WriteLine("Database connection opened.");

                    // Check for duplicate UserID
                    var checkSql = "SELECT COUNT(*) FROM Users WHERE UserID = @UserID";
                    using (var command = new SqlCommand(checkSql, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", NewAdmin.UserID);
                        var count = (int)await command.ExecuteScalarAsync();
                        Console.WriteLine($"Duplicate check: {count} users found with UserID {NewAdmin.UserID}");
                        if (count > 0)
                        {
                            ModelState.AddModelError("NewAdmin.UserID", "User ID already exists.");
                            LoadAdmins();
                            return Page();
                        }
                    }

                    // Hash the password using PasswordHasher (same as ForgotPasswordModel)
                    string hashedPassword = _passwordHasher.HashPassword(NewAdmin, NewAdmin.Password);
                    Console.WriteLine($"Password hashed: {hashedPassword}");

                    // Insert new admin
                    var insertSql = @"
                        INSERT INTO Users (UserID, Password, FullName, Role, CreatedDate)
                        VALUES (@UserID, @Password, @FullName, 'Admin', @CreatedDate)";
                    using (var command = new SqlCommand(insertSql, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", NewAdmin.UserID);
                        command.Parameters.AddWithValue("@Password", hashedPassword);
                        command.Parameters.AddWithValue("@FullName", NewAdmin.FullName);
                        command.Parameters.AddWithValue("@CreatedDate", DateTime.Now);

                        Console.WriteLine($"Executing SQL: {insertSql}");
                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        Console.WriteLine($"Rows affected: {rowsAffected}");
                        if (rowsAffected == 0)
                        {
                            throw new Exception("Insert failed: No rows affected.");
                        }
                    }
                }
                TempData["Message"] = "Admin added successfully.";
                TempData["MessageType"] = "success";
                Console.WriteLine("Admin added successfully, redirecting.");
                return RedirectToPage("/Admin/ManageAdmins");
            }
            catch (SqlException sqlEx)
            {
                TempData["Message"] = $"Database error: {sqlEx.Message} (Error Number: {sqlEx.Number})";
                TempData["MessageType"] = "danger";
                Console.WriteLine($"SQL Exception: {sqlEx.Message}, StackTrace: {sqlEx.StackTrace}");
            }
            catch (Exception ex)
            {
                TempData["Message"] = $"Failed to add admin: {ex.Message}";
                TempData["MessageType"] = "danger";
                Console.WriteLine($"General Exception: {ex.Message}, StackTrace: {ex.StackTrace}");
            }

            LoadAdmins();
            Console.WriteLine("Returning page with error message: " + TempData["Message"]);
            return Page();
        }

        private void LoadAdmins()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var sql = @"
                        SELECT UserID, FullName, CreatedDate
                        FROM Users
                        WHERE Role = 'Admin'
                        ORDER BY CreatedDate DESC";
                    using (var command = new SqlCommand(sql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            Admins = new List<User>();
                            while (reader.Read())
                            {
                                Admins.Add(new User
                                {
                                    UserID = reader.GetString("UserID"),
                                    FullName = reader.GetString("FullName"),
                                    CreatedDate = reader.GetDateTime("CreatedDate"),
                                    Role = "Admin"
                                });
                            }
                        }
                    }
                }
                Console.WriteLine($"Loaded {Admins.Count} admins.");
            }
            catch (Exception ex)
            {
                Message = $"Failed to load admins: {ex.Message}";
                Console.WriteLine($"LoadAdmins Exception: {ex.Message}");
            }
        }

        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetString("UserID");
            Console.WriteLine($"Checking admin status: UserID={userId}, Role={role}");
            return !string.IsNullOrEmpty(userId) && role == "Admin";
        }
    }
}