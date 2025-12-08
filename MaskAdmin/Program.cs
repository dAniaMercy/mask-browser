using MaskAdmin.Data;
using MaskAdmin.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;
using System.Text;
using Microsoft.AspNetCore.Http;

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

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

// Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("Role", "Admin", "SuperAdmin"));
    options.AddPolicy("SuperAdminOnly", policy => policy.RequireClaim("Role", "SuperAdmin"));
    options.AddPolicy("ModeratorOrAbove", policy => policy.RequireClaim("Role", "Moderator", "Admin", "SuperAdmin"));
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

// HTTP Client with Polly
builder.Services.AddHttpClient("MaskBrowserAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MaskBrowserAPI:BaseUrl"] ?? "http://localhost:5050");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // Only use HSTS if HTTPS is configured
    // app.UseHsts();
}

// Only redirect to HTTPS if HTTPS is configured
// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Prometheus metrics
app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// SignalR Hub
app.MapHub<NotificationHub>("/notificationHub");

// Prometheus metrics endpoint
app.MapMetrics();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Database migration on startup (optional, remove in production)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        // Check if there are pending migrations
        var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();
        if (pendingMigrations.Any())
        {
            Log.Warning("There are {Count} pending migrations: {Migrations}. Please create and apply migrations manually.",
                pendingMigrations.Count, string.Join(", ", pendingMigrations));
            // In production, don't auto-migrate if there are pending changes
            if (app.Environment.IsDevelopment())
            {
                Log.Information("Running in Development mode, attempting to apply migrations...");
                dbContext.Database.Migrate();
                Log.Information("Database migration completed successfully");
            }
            else
            {
                Log.Warning("Skipping auto-migration in Production. Please apply migrations manually.");
            }
        }
        else
        {
            // Only migrate if database is up to date
            dbContext.Database.Migrate();
            Log.Information("Database migration completed successfully");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while migrating the database. Application will continue to start.");
        // Don't throw - allow application to start even if migration fails
    }
}

Log.Information("MaskAdmin starting up...");
Log.Information("Listening on: {Urls}", app.Urls);

// Log environment info
Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
Log.Information("ASPNETCORE_URLS: {Urls}", Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));

app.Run();
