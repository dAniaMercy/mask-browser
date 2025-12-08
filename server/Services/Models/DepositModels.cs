namespace MaskBrowser.Server.Services;

public record CreateDepositRequestDto(int PaymentMethodId, decimal? Amount = null);

public record DepositRequestDto(
    int DepositId,
    string PaymentCode,
    DateTime ExpiresAt,
    string Instructions,
    string? QrCodeUrl,
    string? RedirectUrl,
    decimal? ExpectedAmount,
    string Currency);

public record DepositStatusDto(
    int DepositId,
    string Status,
    decimal? ActualAmount,
    DateTime? CompletedAt,
    int SecondsRemaining);

public record DepositHistoryDto(
    int Id,
    string PaymentCode,
    string MethodName,
    string MethodIcon,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public record PaymentMethodDto(
    int Id,
    string Name,
    string DisplayName,
    string? Description,
    string? IconUrl,
    string? QrCodeUrl,
    string? RedirectUrl,
    decimal MinAmount,
    decimal MaxAmount,
    string Currency,
    decimal FeePercent,
    decimal FeeFixed);

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
