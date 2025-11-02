using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using MaskBrowser.Desktop.Models;

namespace MaskBrowser.Desktop.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl = "http://localhost:5050/api";
        private string? _token;

        public ApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(_apiUrl);
        }

        public async Task<AuthResult> LoginAsync(string email, string password)
        {
            try
            {
                var content = new StringContent(
                    JsonConvert.SerializeObject(new { email, password }),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync("/auth/login", content);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<AuthResponse>(json);
                    _token = result?.Token;
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Bearer", _token);
                    return new AuthResult { Success = true };
                }
                else
                {
                    var error = JsonConvert.DeserializeObject<ErrorResponse>(json);
                    return new AuthResult { Success = false, Message = error?.Message ?? "Ошибка входа" };
                }
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, Message = ex.Message };
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

