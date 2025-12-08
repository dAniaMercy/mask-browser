using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaskBrowser.Server.Infrastructure;
using MaskBrowser.Server.Services;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace MaskBrowser.Server.Controllers;

[ApiController]
[Route("api/profile/{profileId}/browser")]
// Авторизация проверяется вручную в методах
public class BrowserProxyController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly DockerService _dockerService;
    private readonly ILogger<BrowserProxyController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly RsaKeyService _rsaKeyService;

    public BrowserProxyController(
        ApplicationDbContext context,
        DockerService dockerService,
        ILogger<BrowserProxyController> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        RsaKeyService rsaKeyService)
    {
        _context = context;
        _dockerService = dockerService;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _rsaKeyService = rsaKeyService;
    }
    
    private int? ValidateTokenAndGetUserId(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return null;
            
        try
        {
            var publicKey = _rsaKeyService.GetPublicKey();
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"],
                IssuerSigningKey = publicKey
            };
            
            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return null;
                
            return userId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate token from query parameter");
            return null;
        }
    }

    /// <summary>
    /// Проксирование HTTP запросов к noVNC (статичные файлы)
    /// Модифицирует HTML noVNC для использования прокси WebSocket
    /// </summary>
    [HttpGet("proxy")]
    public async Task<IActionResult> ProxyHttp([FromRoute] int profileId, [FromQuery] string? path = "")
    {
        try
        {
            // Проверяем авторизацию
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int userId;
            
            // Если пользователь не авторизован через стандартный механизм, проверяем токен из заголовка
            if (string.IsNullOrEmpty(userIdClaim))
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    _logger.LogWarning("Unauthorized request to proxy endpoint for profile {ProfileId}", profileId);
                    return Unauthorized(new { message = "Unauthorized" });
                }
                
                var token = authHeader.Substring(7); // Убираем "Bearer "
                var userIdFromToken = ValidateTokenAndGetUserId(token);
                if (userIdFromToken == null)
                {
                    _logger.LogWarning("Invalid token in proxy request for profile {ProfileId}", profileId);
                    return Unauthorized(new { message = "Invalid token" });
                }
                userId = userIdFromToken.Value;
                _logger.LogInformation("✅ Authenticated via token from header for user {UserId}", userId);
            }
            else
            {
                userId = int.Parse(userIdClaim);
            }
            
            var profile = await _context.BrowserProfiles
                .FirstOrDefaultAsync(p => p.Id == profileId && p.UserId == userId);

            if (profile == null)
            {
                return NotFound(new { message = "Profile not found" });
            }

            if (profile.Status != Models.ProfileStatus.Running)
            {
                return BadRequest(new { message = "Profile is not running" });
            }

            if (string.IsNullOrEmpty(profile.ContainerId) || profile.Port == 0)
            {
                return BadRequest(new { message = "Profile container not available" });
            }

            // Формируем URL для проксирования
            var targetUrl = $"http://{profile.ServerNodeIp}:{profile.Port}";
            if (!string.IsNullOrEmpty(path))
            {
                targetUrl += "/" + path.TrimStart('/');
            }
            else
            {
                targetUrl += "/vnc.html?autoconnect=true&resize=scale";
            }

            _logger.LogInformation("🔄 Proxying HTTP request to {Url} for profile {ProfileId}", targetUrl, profileId);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var response = await httpClient.GetAsync(targetUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("⚠️ Proxy request failed: {StatusCode} for {Url}", response.StatusCode, targetUrl);
                return StatusCode((int)response.StatusCode, new { message = "Proxy request failed" });
            }

            var content = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "text/html";

            // Если это HTML файл (vnc.html), модифицируем его для использования прокси WebSocket
            if (contentType.Contains("text/html") && content.Contains("noVNC"))
            {
                // Заменяем WebSocket URL на наш прокси endpoint
                var apiBaseUrl = $"{Request.Scheme}://{Request.Host}";
                var wsProxyUrl = $"{apiBaseUrl}/api/profile/{profileId}/browser/ws";
                
                // Получаем токен из заголовка Authorization для передачи в WebSocket URL
                var authHeader = Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring(7); // Убираем "Bearer "
                    // Добавляем токен в WebSocket URL через query параметр (для авторизации в WebSocket)
                    wsProxyUrl += $"?token={Uri.EscapeDataString(token)}";
                }
                
                // Заменяем относительные WebSocket пути на наш прокси
                content = content.Replace("'websockify'", $"'{wsProxyUrl}'");
                content = content.Replace("\"websockify\"", $"\"{wsProxyUrl}\"");
                content = content.Replace("path: 'websockify'", $"path: '{wsProxyUrl}'");
                content = content.Replace("path: \"websockify\"", $"path: \"{wsProxyUrl}\"");
                
                // Также заменяем возможные абсолютные пути
                content = content.Replace($"ws://{profile.ServerNodeIp}:{profile.Port}/websockify", wsProxyUrl);
                content = content.Replace($"wss://{profile.ServerNodeIp}:{profile.Port}/websockify", wsProxyUrl.Replace("http://", "wss://").Replace("https://", "wss://"));
                
                // Заменяем все вхождения websockify на наш прокси URL
                content = System.Text.RegularExpressions.Regex.Replace(
                    content, 
                    @"['""]websockify['""]", 
                    $"'{wsProxyUrl}'",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                
                _logger.LogInformation("✅ Modified noVNC HTML to use WebSocket proxy: {WsUrl}", wsProxyUrl);
            }

            return Content(content, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error proxying HTTP request for profile {ProfileId}", profileId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Проксирование WebSocket соединений к websockify
    /// </summary>
    [HttpGet("ws")]
    public async Task ProxyWebSocket([FromRoute] int profileId, [FromQuery] string? token = null)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = 400;
            return;
        }

        try
        {
            // Проверяем авторизацию
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int userId;
            
            // Если пользователь не авторизован через стандартный механизм, проверяем токен из query
            if (string.IsNullOrEmpty(userIdClaim) && !string.IsNullOrEmpty(token))
            {
                var userIdFromToken = ValidateTokenAndGetUserId(token);
                if (userIdFromToken == null)
                {
                    HttpContext.Response.StatusCode = 401;
                    await HttpContext.Response.WriteAsync("Unauthorized: Invalid token");
                    return;
                }
                userId = userIdFromToken.Value;
                _logger.LogInformation("✅ Authenticated via token from query parameter for user {UserId}", userId);
            }
            else if (!string.IsNullOrEmpty(userIdClaim))
            {
                userId = int.Parse(userIdClaim);
            }
            else
            {
                HttpContext.Response.StatusCode = 401;
                await HttpContext.Response.WriteAsync("Unauthorized");
                return;
            }
            
            var profile = await _context.BrowserProfiles
                .FirstOrDefaultAsync(p => p.Id == profileId && p.UserId == userId);

            if (profile == null)
            {
                HttpContext.Response.StatusCode = 404;
                await HttpContext.Response.WriteAsync("Profile not found");
                return;
            }

            if (profile.Status != Models.ProfileStatus.Running)
            {
                HttpContext.Response.StatusCode = 400;
                await HttpContext.Response.WriteAsync("Profile is not running");
                return;
            }

            if (string.IsNullOrEmpty(profile.ContainerId) || profile.Port == 0)
            {
                HttpContext.Response.StatusCode = 400;
                await HttpContext.Response.WriteAsync("Profile container not available");
                return;
            }

            // Формируем WebSocket URL для websockify
            // websockify слушает на порту 6080 и WebSocket endpoint обычно на корневом пути или /websockify
            var wsUrl = $"ws://{profile.ServerNodeIp}:{profile.Port}";
            
            // Пробуем сначала корневой путь, если не сработает - попробуем /websockify
            // websockify обычно слушает WebSocket на корневом пути
            
            _logger.LogInformation("🔄 Proxying WebSocket to {Url} for profile {ProfileId}", wsUrl, profileId);

            // Принимаем WebSocket соединение от клиента
            var clientWs = await HttpContext.WebSockets.AcceptWebSocketAsync();

            // Создаем WebSocket клиент для подключения к websockify
            using var serverWs = new ClientWebSocket();
            await serverWs.ConnectAsync(new Uri(wsUrl), CancellationToken.None);

            _logger.LogInformation("✅ WebSocket proxy established for profile {ProfileId}", profileId);

            // Проксируем данные в обе стороны
            var clientToServer = Task.Run(async () =>
            {
                try
                {
                    var buffer = new byte[4096];
                    while (clientWs.State == WebSocketState.Open && serverWs.State == WebSocketState.Open)
                    {
                        var result = await clientWs.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await serverWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", CancellationToken.None);
                            break;
                        }
                        await serverWs.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in client-to-server proxy for profile {ProfileId}", profileId);
                }
            });

            var serverToClient = Task.Run(async () =>
            {
                try
                {
                    var buffer = new byte[4096];
                    while (serverWs.State == WebSocketState.Open && clientWs.State == WebSocketState.Open)
                    {
                        var result = await serverWs.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await clientWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", CancellationToken.None);
                            break;
                        }
                        await clientWs.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in server-to-client proxy for profile {ProfileId}", profileId);
                }
            });

            await Task.WhenAny(clientToServer, serverToClient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error establishing WebSocket proxy for profile {ProfileId}", profileId);
            if (HttpContext.WebSockets.IsWebSocketRequest && HttpContext.WebSockets.WebSocketRequestedProtocols.Count > 0)
            {
                HttpContext.Response.StatusCode = 500;
            }
        }
    }
}

