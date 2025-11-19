using MaskAdmin.Data;
using MaskAdmin.Models;
using Microsoft.EntityFrameworkCore;

namespace MaskAdmin.Services;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(ApplicationDbContext context, ILogger<PaymentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(List<Payment> Payments, int TotalCount)> GetPaymentsAsync(
        int page, 
        int pageSize, 
        PaymentStatus? status, 
        PaymentProvider? provider, 
        int? userId)
    {
        var query = _context.Payments
            .Include(p => p.User)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (provider.HasValue)
            query = query.Where(p => p.Provider == provider.Value);

        if (userId.HasValue)
            query = query.Where(p => p.UserId == userId.Value);

        var total = await query.CountAsync();
        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (payments, total);
    }

    public async Task<Payment?> GetPaymentByIdAsync(int id)
    {
        return await _context.Payments
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PaymentStats> GetPaymentStatsAsync()
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var completedPayments = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .ToListAsync();

        var stats = new PaymentStats
        {
            TotalRevenue = completedPayments.Sum(p => p.Amount),
            MonthlyRevenue = completedPayments
                .Where(p => p.CompletedAt >= monthStart)
                .Sum(p => p.Amount),
            TodayRevenue = completedPayments
                .Where(p => p.CompletedAt >= todayStart)
                .Sum(p => p.Amount),
            TotalPayments = await _context.Payments.CountAsync(),
            SuccessfulPayments = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Completed),
            FailedPayments = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Failed),
            ByProvider = completedPayments
                .GroupBy(p => p.Provider)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount))
        };

        return stats;
    }

    public Task<byte[]> ExportPaymentsAsync(string format)
    {
        // TODO: Implement export logic
        _logger.LogWarning("ExportPaymentsAsync not implemented");
        return Task.FromResult(Array.Empty<byte>());
    }
}
