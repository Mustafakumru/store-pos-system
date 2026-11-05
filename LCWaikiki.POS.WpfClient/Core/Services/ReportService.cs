using LCWaikiki.POS.WpfClient.Core.Http;

namespace LCWaikiki.POS.WpfClient.Core.Services;

/// <summary>X (ara) ve Z (gun sonu) raporlari.</summary>
public class ReportService(ApiClient api)
{
    protected readonly ApiClient _api = api;

    public const string ZApprovalAction = "z-report";

    /// <summary>Son Z'den bu yana ara rapor - sayac sifirlanmaz.</summary>
    public Task<XReportDto> GetXReportAsync(CancellationToken ct = default)
        => _api.GetAsync<XReportDto>("api/pos/reports/x-report", ct);

    /// <summary>Mali rapor: Z no araligi VEYA tarih araligindaki kayitli Z'lerin toplami.</summary>
    public Task<FinancialReportDto> GetFinancialReportAsync(
        int? fromZ, int? toZ, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (fromZ is int fz && toZ is int tz) { q.Add($"fromZNumber={fz}"); q.Add($"toZNumber={tz}"); }
        else if (fromDate is DateTime fd && toDate is DateTime td)
        {
            q.Add($"fromDate={Uri.EscapeDataString(fd.ToUniversalTime().ToString("o"))}");
            q.Add($"toDate={Uri.EscapeDataString(td.ToUniversalTime().ToString("o"))}");
        }
        return _api.GetAsync<FinancialReportDto>("api/pos/reports/financial?" + string.Join("&", q), ct);
    }

    /// <summary>Kasa raporu (beklenen kasa) - SADECE mudur (kor sayim).</summary>
    public Task<CashSessionReportDto> GetCashReportAsync(CancellationToken ct = default)
        => _api.GetAsync<CashSessionReportDto>("api/pos/cash-session/report", ct);

    /// <summary>Gun sonu kapanisi - kalici kaydedilir, yeni Z no atanir (geri alinamaz).</summary>
    public Task<ZReportDto> CreateZReportAsync(string? approvalToken, CancellationToken ct = default)
        => _api.PostAsync<ZReportDto>("api/pos/reports/z-report", new { approvalToken }, ct);
}

public record XReportDto(
    DateTime FromDate, DateTime ToDate,
    decimal TotalSalesAmount, decimal TotalRefundsAmount, decimal NetAmount,
    decimal CashTotal, decimal CardTotal, decimal GiftCardTotal,
    int TransactionCount, int RefundCount, int? LastZReportNumber,
    decimal CashRefundTotal = 0, decimal CardRefundTotal = 0, decimal NetCash = 0,
    decimal GiftCardSoldTotal = 0, decimal GiftCardSoldCash = 0, decimal GiftCardSoldCard = 0,
    bool CashDetailsHidden = false);

public record FinancialReportDto(
    int FromZNumber, int ToZNumber, DateTime FromDate, DateTime ToDate,
    decimal TotalSalesAmount, decimal TotalRefundsAmount, decimal NetAmount,
    decimal CashTotal, decimal CardTotal, decimal GiftCardTotal,
    int TransactionCount, int RefundCount, int ZReportCount, DateTime ReportGeneratedAt,
    decimal CashRefundTotal = 0, decimal CardRefundTotal = 0,
    decimal GiftCardSoldTotal = 0, decimal GiftCardSoldCash = 0, decimal GiftCardSoldCard = 0);

public record CashSessionReportDto(
    int SessionId, decimal OpeningFloat, decimal CashSalesTotal, decimal ExpectedCashAmount,
    int TransactionCount, DateTime OpenedAt, DateTime ReportGeneratedAt, decimal CashRefundTotal = 0,
    decimal GiftCardSoldCash = 0);

public record ZReportDto(
    int Id, int StoreId, int ReportNumber, DateTime FromDate, DateTime ToDate,
    decimal TotalSalesAmount, decimal TotalRefundsAmount, decimal NetAmount,
    decimal CashTotal, decimal CardTotal, decimal GiftCardTotal,
    int TransactionCount, int RefundCount,
    decimal CashRefundTotal = 0, decimal CardRefundTotal = 0,
    decimal GiftCardSoldTotal = 0, decimal GiftCardSoldCash = 0, decimal GiftCardSoldCard = 0);
