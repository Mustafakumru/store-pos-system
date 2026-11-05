using LCWaikiki.POS.WpfClient.Core.Http;

namespace LCWaikiki.POS.WpfClient.Core.Services;

/// <summary>
/// Kasa oturumu (Avans / Teslim). Backend satis/iade/degisimi ACIK oturum
/// olmadan reddeder; Ana Menu buradan durumu okur.
/// </summary>
public class CashSessionService(ApiClient api)
{
    protected readonly ApiClient _api = api;

    public const string OpenApprovalAction = "cash-session-open";
    public const string CloseApprovalAction = "cash-session-close";

    public Task<CurrentCashSessionDto> GetCurrentAsync(CancellationToken ct = default)
        => _api.GetAsync<CurrentCashSessionDto>("api/pos/cash-session/current", ct);

    /// <summary>Avans: kasayi acilis tutariyla acar (mudur onayi gerekir).</summary>
    public Task<CashSessionDto> OpenAsync(decimal openingFloat, string approvalToken, CancellationToken ct = default)
        => _api.PostAsync<CashSessionDto>("api/pos/cash-session/open",
            new { openingFloat, approvalToken }, ct);

    /// <summary>Teslim: sayilan tutarla kasayi kapatir (kor sayim, mudur onayi gerekir).</summary>
    public Task<CashSessionDto> CloseAsync(decimal closingAmount, string approvalToken, CancellationToken ct = default)
        => _api.PostAsync<CashSessionDto>("api/pos/cash-session/close",
            new { closingAmount, approvalToken }, ct);
}

public record CurrentCashSessionDto(bool IsOpen, int? SessionId, decimal? OpeningFloat, DateTime? OpenedAt);

public record CashSessionDto(
    int Id, int StoreId, string CashierId, decimal OpeningFloat, DateTime OpenedAt,
    decimal? ClosingAmount, DateTime? ClosedAt, string Status);
