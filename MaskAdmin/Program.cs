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
