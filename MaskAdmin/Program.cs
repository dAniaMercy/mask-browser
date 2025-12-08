using MaskAdmin.Data;
using MaskAdmin.Models;
using MaskAdmin.Services;
using MaskAdmin.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;
using System.Text;
using Microsoft.AspNetCore.Http;
using BCrypt.Net;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/maskadmin-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllersWithViews();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

// Redis Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "MaskAdmin_";
});

// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Name = "MaskAdmin.Auth";
    });

// Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User", "Admin"));
});

// SignalR for real-time updates
builder.Services.AddSignalR();

// Application Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IServerService, ServerService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Background Services
builder.Services.AddHostedService<RateLimitCleanupService>();

// HTTP Client with Polly
builder.Services.AddHttpClient("MaskBrowserAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MaskBrowserAPI:BaseUrl"] ?? "http://localhost:5050");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Session with enhanced security
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Use Secure cookies in production
    options.Cookie.SameSite = SameSiteMode.Strict; // CSRF protection
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // Use HSTS in production
    app.UseHsts();
}

// Redirect to HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

// Rate Limiting (before authentication)
app.UseMiddleware<RateLimitingMiddleware>();

// Prometheus metrics
app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

// SignalR Hub
app.MapHub<NotificationHub>("/notificationHub");

// Prometheus metrics endpoint
app.MapMetrics();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Admin user check endpoint (for debugging)
app.MapGet("/check-admin", async (ApplicationDbContext db) =>
{
    try
    {
        // Use Select to avoid IsBanned column issue
        var adminExists = await db.Users
            .Where(u => u.Username == "admin" || u.Email == "admin@maskbrowser.com")
            .Select(u => u.Id)
            .AnyAsync();
        var userCount = await db.Users.CountAsync();
        return Results.Ok(new { 
            adminExists, 
            userCount,
            canConnect = await db.Database.CanConnectAsync()
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error: {ex.Message}");
    }
});

// Test password endpoint (for debugging)
app.MapPost("/test-password", async (ApplicationDbContext db, ILogger<Program> logger, string username, string password) =>
{
    try
    {
        var userData = await db.Database.SqlQueryRaw<LoginUserData>(
            "SELECT \"Id\", \"Username\", \"Email\", \"PasswordHash\", \"IsActive\", \"IsAdmin\" FROM \"Users\" WHERE \"Username\" = {0} OR \"Email\" = {1} LIMIT 1",
            username, username
        ).FirstOrDefaultAsync();
        
        if (userData == null)
        {
            return Results.Ok(new { found = false, message = "User not found" });
        }
        
        var passwordValid = BCrypt.Net.BCrypt.Verify(password, userData.PasswordHash);
        
        return Results.Ok(new { 
            found = true,
            userId = userData.Id,
            username = userData.Username,
            email = userData.Email,
            isActive = userData.IsActive,
            isAdmin = userData.IsAdmin,
            passwordValid = passwordValid,
            hashLength = userData.PasswordHash?.Length ?? 0,
            hashPrefix = userData.PasswordHash?.Substring(0, Math.Min(30, userData.PasswordHash.Length)) ?? "null"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error testing password");
        return Results.Problem($"Error: {ex.Message}");
    }
});

// Reset admin password endpoint
app.MapPost("/reset-admin-password", async (ApplicationDbContext db, ILogger<Program> logger) =>
{
    try
    {
        var adminId = await db.Database.SqlQueryRaw<int>(
            "SELECT \"Id\" FROM \"Users\" WHERE \"Username\" = 'admin' OR \"Email\" = 'admin@maskbrowser.com' LIMIT 1"
        ).FirstOrDefaultAsync();
        
        if (adminId == 0)
        {
            return Results.Problem("Admin user not found. Use /create-admin to create it.");
        }
        
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Users\" SET \"PasswordHash\" = {0}, \"IsActive\" = true, \"IsAdmin\" = true WHERE \"Id\" = {1}",
            passwordHash, adminId);
        
        logger.LogInformation("Admin password reset, ID: {Id}", adminId);
        
        return Results.Ok(new { 
            message = "Admin password reset to 'Admin123!'",
            id = adminId,
            username = "admin"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error resetting admin password");
        return Results.Problem($"Error: {ex.Message}");
    }
});

// Create admin user endpoint
app.MapPost("/create-admin", async (ApplicationDbContext db, ILogger<Program> logger) =>
{
    try
    {
        // Check if admin already exists (using SQL to avoid IsBanned column issue)
        var adminIdResult = await db.Database.SqlQueryRaw<int>(
            "SELECT \"Id\" FROM \"Users\" WHERE \"Username\" = 'admin' OR \"Email\" = 'admin@maskbrowser.com' LIMIT 1"
        ).FirstOrDefaultAsync();
        
        var adminId = adminIdResult;
        
        if (adminId > 0)
        {
            // Update existing admin using direct SQL to avoid IsBanned
            var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Users\" SET \"IsActive\" = true, \"IsAdmin\" = true, \"PasswordHash\" = {0} WHERE \"Id\" = {1}",
                adminPasswordHash, adminId);
            
            logger.LogInformation("Admin user updated via SQL, ID: {Id}", adminId);
            
            return Results.Ok(new { 
                message = "Admin user already exists, password reset to default",
                created = false,
                updated = true,
                id = adminId,
                username = "admin",
                email = "admin@maskbrowser.com",
                password = "Admin123!"
            });
        }

        // Get next available ID using SQL
        var maxIdResult = await db.Database.SqlQueryRaw<int>(
            "SELECT COALESCE(MAX(\"Id\"), 0) FROM \"Users\""
        ).FirstOrDefaultAsync();
        var newId = maxIdResult + 1;
        
        // Create admin user using direct SQL to avoid IsBanned column
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
        var createdAt = DateTime.UtcNow;
        
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                @"INSERT INTO ""Users"" (""Id"", ""Username"", ""Email"", ""PasswordHash"", ""Balance"", ""IsActive"", ""IsAdmin"", ""TwoFactorEnabled"", ""TwoFactorSecret"", ""CreatedAt"")
                  VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9})",
                newId, "admin", "admin@maskbrowser.com", passwordHash, 0, true, true, false, DBNull.Value, createdAt);
            
            logger.LogInformation("Admin user created successfully via SQL with ID: {Id}", newId);

            return Results.Ok(new { 
                message = "Admin user created successfully",
                created = true,
                id = newId,
                username = "admin",
                email = "admin@maskbrowser.com",
                password = "Admin123!"
            });
        }
        catch (DbUpdateException dbEx)
        {
            logger.LogError(dbEx, "Database error creating admin user. Inner exception: {InnerException}", dbEx.InnerException?.Message);
            
            // Try to get more details
            var errorDetails = dbEx.InnerException?.Message ?? dbEx.Message;
            
            // If it's a unique constraint violation, try to find and update existing user
            if (errorDetails.Contains("unique") || errorDetails.Contains("duplicate"))
            {
                var existingUserId = await db.Database.SqlQueryRaw<int>(
                    "SELECT \"Id\" FROM \"Users\" WHERE \"Username\" = 'admin' OR \"Email\" = 'admin@maskbrowser.com' LIMIT 1"
                ).FirstOrDefaultAsync();
                    
                if (existingUserId > 0)
                {
                    var updatePasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
                    await db.Database.ExecuteSqlRawAsync(
                        "UPDATE \"Users\" SET \"IsActive\" = true, \"IsAdmin\" = true, \"PasswordHash\" = {0} WHERE \"Id\" = {1}",
                        updatePasswordHash, existingUserId);
                    
                    return Results.Ok(new { 
                        message = "Admin user found and updated",
                        created = false,
                        updated = true,
                        id = existingUserId,
                        username = "admin",
                        email = "admin@maskbrowser.com",
                        password = "Admin123!"
                    });
                }
            }
            
            return Results.Problem($"Database error: {errorDetails}");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error creating admin user. Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
            ex.GetType().Name, ex.Message, ex.StackTrace);
        return Results.Problem($"Error: {ex.Message}. Inner: {ex.InnerException?.Message}");
    }
});

// Database migration on startup (optional, remove in production)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        // Suppress pending model changes warning in production
        var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();
        if (pendingMigrations.Any())
        {
            Log.Warning("There are {Count} pending migrations: {Migrations}. Skipping auto-migration. Please create and apply migrations manually.",
                pendingMigrations.Count, string.Join(", ", pendingMigrations));
            // Don't try to migrate if there are pending changes
        }
        else
        {
            // Only migrate if database is up to date
            try
            {
                dbContext.Database.Migrate();
                Log.Information("Database migration completed successfully");
            }
            catch (Exception migrateEx)
            {
                Log.Warning(migrateEx, "Migration failed, but continuing startup");
            }
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "An error occurred while checking migrations. Application will continue to start.");
        // Don't throw - allow application to start even if migration check fails
    }
}

// Ensure we're listening on the correct URL
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://+:80";
if (!app.Urls.Any())
{
    app.Urls.Add(urls);
    Log.Information("Explicitly set listening URL to: {Url}", urls);
}

Log.Information("MaskAdmin starting up...");
Log.Information("Listening on: {Urls}", string.Join(", ", app.Urls));

// Log environment info
Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
Log.Information("ASPNETCORE_URLS: {Urls}", Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));

app.Run();

// Helper class for password test (must be after all top-level statements)
public class LoginUserData
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
}
