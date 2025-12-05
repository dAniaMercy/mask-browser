using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Linq;
using MaskBrowser.Server.Infrastructure;
using MaskBrowser.Server.Services;
using MaskBrowser.Server.BackgroundJobs;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var configuration = builder.Configuration;

// Add services
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Возвращаем детальные ошибки валидации
        options.InvalidModelStateResponseFactory = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Model validation failed: {Errors}", 
                string.Join(", ", context.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            
            return new BadRequestObjectResult(new
            {
                message = "Validation failed",
                errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                    )
            });
        };
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

// RSA Key Service
var rsaKeyService = new RsaKeyService(configuration, 
    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<RsaKeyService>());
builder.Services.AddSingleton(rsaKeyService);

// TOTP Service
builder.Services.AddSingleton<TotpService>();

// JWT Authentication with RSA256
var publicKey = rsaKeyService.GetPublicKey();
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
            IssuerSigningKey = publicKey
        };
    });

builder.Services.AddAuthorization();

// CORS - ВАЖНО: разрешаем все источники для разработки
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

// Применяем миграции БД автоматически при старте
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var migrationLogger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
        migrationLogger.LogInformation("✅ Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        migrationLogger.LogError(ex, "❌ An error occurred while applying database migrations");
        throw;
    }
}

// ВАЖНО: CORS должен быть ДО UseAuthentication и UseAuthorization
app.UseCors("AllowAll");

// Логирование входящих запросов
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Incoming request: {Method} {Path} from {RemoteIp}", 
        context.Request.Method, 
        context.Request.Path,
        context.Connection.RemoteIpAddress);
    await next();
});

// Prometheus endpoint
app.UseHttpMetrics();
app.MapMetrics();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Логирование при запуске
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 MASK BROWSER API started");
logger.LogInformation("📍 CORS enabled for all origins");
logger.LogInformation("🔐 JWT RSA256 authentication enabled");

app.Run();