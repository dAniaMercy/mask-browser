using MaskAdmin.Data;
using MaskAdmin.Models;
using Microsoft.EntityFrameworkCore;

namespace MaskAdmin.Services;

public class SettingsService : ISettingsService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(ApplicationDbContext context, ILogger<SettingsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> GetSettingsByCategoryAsync(string category)
    {
        var settings = await _context.SystemSettings
            .Where(s => s.Category == category)
            .ToListAsync();

        return settings.ToDictionary(s => s.Key, s => s.Value ?? string.Empty);
    }

    public async Task<bool> UpdateSettingAsync(string key, string value)
    {
        try
        {
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting == null)
            {
                setting = new SystemSettings
                {
                    Key = key,
                    Value = value,
                    Category = "General"
                };
                _context.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = value;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating setting {Key}", key);
            return false;
        }
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key);

        return setting?.Value;
    }
}
