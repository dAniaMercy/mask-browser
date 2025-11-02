using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MaskBrowser.Server.Infrastructure;
using MaskBrowser.Server.Services;
using MaskBrowser.Server.BackgroundJobs;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var configuration = builder.Configuration;

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("PostgreSQL")));

// Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
});

// RabbitMQ
builder.Services.AddSingleton<RabbitMQService>();

// Kafka
builder.Services.AddSingleton<KafkaService>();

// Docker SDK
builder.Services.AddSingleton<DockerService>();

// HTTP Client for Cybernetics API
builder.Services.AddHttpClient<CyberneticsApiService>(client =>
{
    var apiUrl = configuration["Cybernetics:ApiUrl"] ?? "https://api.cybernetics.com";
    client.BaseAddress = new Uri(apiUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// HTTP Client Factory for services
builder.Services.AddHttpClient();

// RSA Key Service (must be singleton and initialized early)
builder.Services.AddSingleton<RsaKeyService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger<RsaKeyService>();
    return new RsaKeyService(config, logger);
});

// TOTP Service
builder.Services.AddSingleton<TotpService>();

// Business Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<LoadBalancerService>();
builder.Services.AddScoped<CryptoPaymentService>();
builder.Services.AddSingleton<IMetricsService, MetricsService>();
builder.Services.AddScoped<CyberneticsApiService>();

// Background Jobs
builder.Services.AddHostedService<ContainerMonitorJob>();
builder.Services.AddHostedService<SessionCleanupJob>();

// JWT Authentication with RSA256
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = null // Will be set in middleware
        };
        
        // Set the public key dynamically
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var rsaKeyService = context.HttpContext.RequestServices.GetRequiredService<RsaKeyService>();
                context.Options.TokenValidationParameters.IssuerSigningKey = rsaKeyService.GetPublicKey();
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(configuration.GetConnectionString("PostgreSQL")!)
    .AddRedis(configuration.GetConnectionString("Redis")!);

var app = builder.Build();

// Prometheus metrics
app.UseHttpMetrics();
app.MapMetrics();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Simple health endpoint
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();