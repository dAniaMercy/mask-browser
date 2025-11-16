using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MaskBrowser.Desktop.Models;
using System.IO;

namespace MaskBrowser.Desktop.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl = "http://109.172.101.73:5050/api";
        private string? _token;
        private readonly string _logPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "api.log");

        public ApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(_apiUrl);
        }

        // Accept optional twoFactorCode
        public async Task<AuthResult> LoginAsync(string email, string password, string? twoFactorCode = null)
        {
            try
            {
                var payload = new { email, password, twoFactorCode };

                var content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var requestUri = new Uri(_httpClient.BaseAddress, "auth/login");

                // Log request
                Log($"POST {requestUri} -> payload: {JsonConvert.SerializeObject(payload)}");

                var response = await _httpClient.PostAsync(requestUri, content);
                var json = await response.Content.ReadAsStringAsync();

                // Log response
                Log($"RESPONSE {requestUri} -> {(int)response.StatusCode} {response.ReasonPhrase} - {json}");

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<AuthResponse>(json);
                    _token = result?.Token;
                    if (!string.IsNullOrEmpty(_token))
                    {
                        _httpClient.DefaultRequestHeaders.Authorization = 
                            new AuthenticationHeaderValue("Bearer", _token);
                    }

                    return new AuthResult { Success = true, Token = _token };
                }

                // parse error body for message and 2FA requirement
                try
                {
                    var obj = JObject.Parse(json);
                    var message = obj["message"]?.ToString() ?? obj["error"]?.ToString() ?? json;
                    var requires2FA = obj["requires2FA"]?.Value<bool?>() ?? obj["requires_2fa"]?.Value<bool?>() ?? false;

                    return new AuthResult { Success = false, Message = $"{(int)response.StatusCode} {response.ReasonPhrase}: {message}", RequiresTwoFactor = requires2FA };
                }
                catch
                {
                    return new AuthResult { Success = false, Message = $"{(int)response.StatusCode} {response.ReasonPhrase} - {requestUri}: {json}" };
                }
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION LoginAsync: {ex}");
                return new AuthResult { Success = false, Message = ex.Message };
            }
        }

        private void Log(string text)
        {
            try
            {
                File.AppendAllText(_logPath, $"[{DateTime.UtcNow:O}] {text}\n");
            }
            catch
            {
                // ignore logging failures
            }
        }

        public void Logout()
        {
            _token = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        public async Task<List<BrowserProfile>> GetProfilesAsync()
        {
            var response = await _httpClient.GetAsync("/profile");
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<BrowserProfile>>(json) ?? new List<BrowserProfile>();
        }

        public async Task<BrowserProfile?> CreateProfileAsync(string name, BrowserConfig config)
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(new { name, config }),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync("/profile", content);
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<BrowserProfile>(json);
        }

        public async Task StartProfileAsync(int profileId)
        {
            await _httpClient.PostAsync($"/profile/{profileId}/start", null);
        }

        public async Task StopProfileAsync(int profileId)
        {
            await _httpClient.PostAsync($"/profile/{profileId}/stop", null);
        }

        public async Task DeleteProfileAsync(int profileId)
        {
            await _httpClient.DeleteAsync($"/profile/{profileId}");
        }
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public bool RequiresTwoFactor { get; set; }
        public string? Token { get; set; }
    }

    public class AuthResponse
    {
        [JsonProperty("token")]
        public string? Token { get; set; }
    }

    public class ErrorResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }
    }
}

