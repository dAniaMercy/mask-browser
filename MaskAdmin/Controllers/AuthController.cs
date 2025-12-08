using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MaskAdmin.Services;
using MaskAdmin.Data;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace MaskAdmin.Controllers;

// Helper class for login query (without IsBanned column)
public class LoginUserData
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
}

[AllowAnonymous]
public class AuthController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        
        // Check if admin user exists, create if not
        try
        {
            // Use SQL to avoid loading IsBanned column
            var adminId = await _context.Database.SqlQueryRaw<int>(
                "SELECT \"Id\" FROM \"Users\" WHERE \"Username\" = 'admin' OR \"Email\" = 'admin@maskbrowser.com' LIMIT 1"
            ).FirstOrDefaultAsync();
            
            var existingAdmin = adminId > 0 ? await _context.Users.FindAsync(adminId) : null;
            if (existingAdmin == null)
            {
                _logger.LogWarning("Admin user not found, creating default admin user");
                
                // Get next available ID using SQL
                var maxIdResult = await _context.Database.SqlQueryRaw<int>(
                    "SELECT COALESCE(MAX(\"Id\"), 0) FROM \"Users\""
                ).FirstOrDefaultAsync();
                var newId = maxIdResult + 1;
                
                // Create admin user using direct SQL to avoid IsBanned column
                var passwordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
                var createdAt = DateTime.UtcNow;
                
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        @"INSERT INTO ""Users"" (""Id"", ""Username"", ""Email"", ""PasswordHash"", ""Balance"", ""IsActive"", ""IsAdmin"", ""TwoFactorEnabled"", ""TwoFactorSecret"", ""CreatedAt"")
                          VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9})",
                        newId, "admin", "admin@maskbrowser.com", passwordHash, 0, true, true, false, DBNull.Value, createdAt);
                    
                    _logger.LogInformation("Default admin user created successfully via SQL with ID: {Id}", newId);
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
                {
                    _logger.LogWarning(dbEx, "Failed to create admin user, may already exist. Error: {Error}", dbEx.InnerException?.Message ?? dbEx.Message);
                    // Continue anyway, user can be created via /create-admin endpoint
                }
            }
            else
            {
                // Ensure admin user has correct permissions using SQL
                if (!existingAdmin.IsAdmin || !existingAdmin.IsActive)
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE \"Users\" SET \"IsActive\" = true, \"IsAdmin\" = true WHERE \"Id\" = {0}",
                        existingAdmin.Id);
                    _logger.LogInformation("Updated existing admin user permissions via SQL");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking/creating admin user");
            // Continue anyway, user can be created via /create-admin endpoint
        }
        
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        try
        {
            _logger.LogInformation("Login attempt for username: {Username}", username);

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewData["Error"] = "Username and password are required";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Check database connection
            try
            {
                await _context.Database.CanConnectAsync();
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Database connection failed during login");
                ViewData["Error"] = "Database connection error. Please contact administrator.";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Use SQL to load user data without IsBanned column
            LoginUserData? userData = null;
            try
            {
                userData = await _context.Database.SqlQueryRaw<LoginUserData>(
                    "SELECT \"Id\", \"Username\", \"Email\", \"PasswordHash\", \"IsActive\", \"IsAdmin\" FROM \"Users\" WHERE \"Username\" = {0} OR \"Email\" = {1} LIMIT 1",
                    username, username
                ).FirstOrDefaultAsync();
            }
            catch (Exception sqlEx)
            {
                _logger.LogError(sqlEx, "SQL error loading user: {Username}. Error: {Error}", username, sqlEx.Message);
                ViewData["Error"] = "Database error. Please contact administrator.";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            if (userData == null)
            {
                _logger.LogWarning("User not found: {Username}. Checking if admin user exists...", username);
                
                // Try to create admin user if it doesn't exist
                try
                {
                    var adminId = await _context.Database.SqlQueryRaw<int>(
                        "SELECT \"Id\" FROM \"Users\" WHERE \"Username\" = 'admin' OR \"Email\" = 'admin@maskbrowser.com' LIMIT 1"
                    ).FirstOrDefaultAsync();
                    
                    if (adminId == 0)
                    {
                        _logger.LogWarning("Admin user does not exist. Please create it via /create-admin endpoint or SQL.");
                    }
                    else
                    {
                        _logger.LogWarning("Admin user exists with ID: {Id}, but query returned null. Possible SQL mapping issue.", adminId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking admin user existence");
                }
                
                ViewData["Error"] = "Invalid username or password";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }
            
            _logger.LogInformation("User found: {Username}, ID: {Id}, IsActive: {IsActive}, IsAdmin: {IsAdmin}, PasswordHash length: {HashLength}", 
                userData.Username, userData.Id, userData.IsActive, userData.IsAdmin, userData.PasswordHash?.Length ?? 0);

            // Verify password
            bool passwordValid = false;
            try
            {
                if (string.IsNullOrEmpty(userData.PasswordHash))
                {
                    _logger.LogError("PasswordHash is null or empty for user: {Username}", username);
                    ViewData["Error"] = "Password verification error. Please contact administrator.";
                    ViewData["ReturnUrl"] = returnUrl;
                    return View();
                }
                
                _logger.LogInformation("Verifying password for user: {Username}, Hash prefix: {HashPrefix}", 
                    username, userData.PasswordHash.Substring(0, Math.Min(20, userData.PasswordHash.Length)));
                
                passwordValid = BCrypt.Net.BCrypt.Verify(password, userData.PasswordHash);
                
                _logger.LogInformation("Password verification result for user: {Username}: {Result}", username, passwordValid);
            }
            catch (Exception bcryptEx)
            {
                _logger.LogError(bcryptEx, "BCrypt verification error for user: {Username}. Hash: {HashPrefix}, Error: {Error}", 
                    username, 
                    userData.PasswordHash?.Substring(0, Math.Min(20, userData.PasswordHash.Length)) ?? "null",
                    bcryptEx.Message);
                ViewData["Error"] = "Password verification error. Please contact administrator.";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            if (!passwordValid)
            {
                _logger.LogWarning("Invalid password for user: {Username}. Provided password length: {PwdLength}", 
                    username, password?.Length ?? 0);
                ViewData["Error"] = "Invalid username or password";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            if (!userData.IsActive)
            {
                _logger.LogWarning("Login attempt for inactive user: {Username}", username);
                ViewData["Error"] = "Account is disabled";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Update last login using SQL
            try
            {
                var lastLoginIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE \"Users\" SET \"LastLoginAt\" = {0}, \"LastLoginIp\" = {1} WHERE \"Id\" = {2}",
                    DateTime.UtcNow, lastLoginIp ?? (object)DBNull.Value, userData.Id);
            }
            catch (Exception saveEx)
            {
                _logger.LogWarning(saveEx, "Failed to update last login for user: {Username}", username);
                // Continue anyway, this is not critical
            }

            // Create JWT token
            string token;
            try
            {
                // Create a temporary user object for token generation
                var tempUser = new Models.User
                {
                    Id = userData.Id,
                    Username = userData.Username,
                    Email = userData.Email,
                    IsAdmin = userData.IsAdmin
                };
                token = GenerateJwtToken(tempUser);
            }
            catch (Exception tokenEx)
            {
                _logger.LogError(tokenEx, "Failed to generate JWT token for user: {Username}", username);
                ViewData["Error"] = "Authentication token error. Please contact administrator.";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Set cookie with token
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Set to true in production with HTTPS
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddHours(8)
            };

            Response.Cookies.Append("auth_token", token, cookieOptions);

            _logger.LogInformation("User {Username} logged in successfully", userData.Username);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for username: {Username}", username);
            ViewData["Error"] = $"An error occurred during login: {ex.Message}";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        Response.Cookies.Delete("auth_token");
        await HttpContext.SignOutAsync();
        return RedirectToAction("Login");
    }

    private string GenerateJwtToken(Models.User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("Role", user.IsAdmin ? "Admin" : "User")
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
