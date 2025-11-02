using System.Net.Http.Json;

namespace MaskBrowser.Server.Services;

public class CryptoPaymentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CryptoPaymentService> _logger;

    public CryptoPaymentService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CryptoPaymentService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> VerifyCryptoBotPaymentAsync(string invoiceId)
    {
        var apiKey = _configuration["CryptoPayments:CryptoBot:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return false;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Crypto-Pay-API-Token", apiKey);
            
            var apiUrl = _configuration["CryptoPayments:CryptoBot:ApiUrl"] ?? "https://pay.crypt.bot/api";
            var response = await httpClient.GetAsync($"{apiUrl}/getInvoices?invoice_ids={invoiceId}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CryptoBotResponse>();
                return result?.Result?.Any(i => i.Status == "paid") ?? false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify CryptoBot payment {InvoiceId}", invoiceId);
        }

        return false;
    }

    public async Task<bool> VerifyBybitPaymentAsync(string orderId)
    {
        // Implement Bybit payment verification
        _logger.LogInformation("Verifying Bybit payment: {OrderId}", orderId);
        return await Task.FromResult(false);
    }

    private class CryptoBotResponse
    {
        public bool Ok { get; set; }
        public List<CryptoBotInvoice>? Result { get; set; }
    }

    private class CryptoBotInvoice
    {
        public int InvoiceId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}