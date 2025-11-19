using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace MaskAdmin.Services;

public class ExportService : IExportService
{
    private readonly ILogger<ExportService> _logger;

    public ExportService(ILogger<ExportService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> ExportToCsvAsync<T>(List<T> data, string[] headers)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8);
            using var csvWriter = new CsvWriter(streamWriter, new CsvConfiguration(CultureInfo.InvariantCulture));

            await csvWriter.WriteRecordsAsync(data);
            await streamWriter.FlushAsync();

            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to CSV");
            throw;
        }
    }

    public async Task<byte[]> ExportToExcelAsync<T>(List<T> data, string sheetName, string[] headers)
    {
        try
        {
            // TODO: Implement Excel export using ClosedXML
            _logger.LogWarning("ExportToExcelAsync not fully implemented");
            
            // Fallback to CSV for now
            return await ExportToCsvAsync(data, headers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to Excel");
            throw;
        }
    }
}
