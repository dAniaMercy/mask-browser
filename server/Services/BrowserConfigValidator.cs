using System.Text.RegularExpressions;
using MaskBrowser.Server.Models;

namespace MaskBrowser.Server.Services;

public class BrowserConfigValidator
{
    private static readonly string[] ValidResolutions = new[]
    {
        "1920x1080", "1366x768", "1536x864", "1440x900", "1280x720",
        "1600x900", "1024x768", "1280x1024", "2560x1440", "3840x2160"
    };

    private static readonly string[] ValidTimezones = new[]
    {
        "UTC", "America/New_York", "America/Los_Angeles", "Europe/London",
        "Europe/Moscow", "Asia/Tokyo", "Asia/Shanghai", "Australia/Sydney",
        "America/Chicago", "America/Denver", "Europe/Paris", "Europe/Berlin"
    };

    private static readonly string[] ValidLanguages = new[]
    {
        "en-US", "en-GB", "ru-RU", "de-DE", "fr-FR", "es-ES",
        "it-IT", "pt-BR", "ja-JP", "zh-CN", "ko-KR"
    };

    public static ValidationResult Validate(BrowserConfig config)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Валидация UserAgent
        if (string.IsNullOrWhiteSpace(config.UserAgent))
        {
            errors.Add("UserAgent is required");
        }
        else if (config.UserAgent.Length > 500)
        {
            errors.Add("UserAgent is too long (max 500 characters)");
        }
        else if (!IsValidUserAgent(config.UserAgent))
        {
            warnings.Add("UserAgent format may be invalid");
        }

        // Валидация ScreenResolution
        if (string.IsNullOrWhiteSpace(config.ScreenResolution))
        {
            errors.Add("ScreenResolution is required");
        }
        else if (!IsValidResolution(config.ScreenResolution))
        {
            errors.Add($"Invalid screen resolution format. Expected format: WIDTHxHEIGHT (e.g., 1920x1080)");
        }
        else if (!ValidResolutions.Contains(config.ScreenResolution))
        {
            warnings.Add($"ScreenResolution '{config.ScreenResolution}' is not in the recommended list. Recommended: {string.Join(", ", ValidResolutions.Take(5))}");
        }

        // Валидация Timezone
        if (string.IsNullOrWhiteSpace(config.Timezone))
        {
            errors.Add("Timezone is required");
        }
        else if (!ValidTimezones.Contains(config.Timezone))
        {
            warnings.Add($"Timezone '{config.Timezone}' is not in the recommended list. Recommended: {string.Join(", ", ValidTimezones.Take(5))}");
        }

        // Валидация Language
        if (string.IsNullOrWhiteSpace(config.Language))
        {
            errors.Add("Language is required");
        }
        else if (!ValidLanguages.Contains(config.Language))
        {
            warnings.Add($"Language '{config.Language}' is not in the recommended list. Recommended: {string.Join(", ", ValidLanguages.Take(5))}");
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    private static bool IsValidResolution(string resolution)
    {
        // Формат: WIDTHxHEIGHT, где WIDTH и HEIGHT - числа от 100 до 9999
        return Regex.IsMatch(resolution, @"^\d{3,4}x\d{3,4}$");
    }

    private static bool IsValidUserAgent(string userAgent)
    {
        // Базовая проверка: должен содержать хотя бы один слэш и версию
        return userAgent.Contains('/') && 
               (userAgent.Contains("Mozilla") || userAgent.Contains("Chrome") || userAgent.Contains("Safari") || userAgent.Contains("Firefox"));
    }

    public static BrowserConfig GetDefaultConfig()
    {
        return new BrowserConfig
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ScreenResolution = "1920x1080",
            Timezone = "UTC",
            Language = "en-US",
            WebRTC = false,
            Canvas = false,
            WebGL = false
        };
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
