using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Linq;
using MaskBrowser.Server.Infrastructure;
using MaskBrowser.Server.Services;
using MaskBrowser.Server.BackgroundJobs;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var configuration = builder.Configuration;

// Увеличиваем timeout для Kestrel (для долгих операций запуска профилей и WebSocket)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
    // Включаем WebSocket поддержку
    options.AllowSynchronousIO = false;
});

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
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MaskBrowser API",
        Version = "v1",
        Description = "MaskBrowser API Documentation",
        Contact = new OpenApiContact
        {
            Name = "MaskBrowser Support"
        }
    });

    // Добавляем JWT авторизацию в Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

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
    client.Timeout = TimeSpan.FromMinutes(5); // Увеличиваем timeout для запуска профилей
});

// Business Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<LoadBalancerService>();
builder.Services.AddScoped<CryptoPaymentService>();
builder.Services.AddScoped<IDepositService, DepositService>();
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

// CORS - Указываем конкретные origins для работы с credentials
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                // Production domains
                "https://maskbrowser.ru",
                "https://www.maskbrowser.ru",
                "https://admin.maskbrowser.ru",
                // Development
                "http://localhost:5052",
                "http://localhost:3000",
                "http://localhost:5100",
                // IP access (for development/testing)
                "http://109.172.101.73:5052",
                "https://109.172.101.73"
            )
            .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
            .WithHeaders("Authorization", "Content-Type", "X-Requested-With", "Accept", "Origin")
            .AllowCredentials(); // ВАЖНО: для работы с credentials: 'include'
    });
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Rate limit для API endpoints
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });

    // Более строгий лимит для auth endpoints
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 5;
    });

    // Лимит для создания профилей
    options.AddFixedWindowLimiter("profile-creation", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(5);
        opt.PermitLimit = 5;
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
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
app.UseCors("AllowFrontend");

// Rate Limiting
app.UseRateLimiter();

// Разрешаем неавторизованные запросы для некоторых endpoints (они проверяют авторизацию вручную)
app.UseAuthentication();
app.UseAuthorization();

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
// Swagger доступен всегда для удобства разработки
    app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "MaskBrowser API v1");
    options.RoutePrefix = "swagger"; // Доступ по /swagger
    options.DisplayRequestDuration();
    options.EnableTryItOutByDefault();
});

// Authentication и Authorization уже применены выше
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Логирование при запуске
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 MASK BROWSER API started");
logger.LogInformation("📍 CORS enabled for all origins");
logger.LogInformation("🔐 JWT RSA256 authentication enabled");

app.Run();