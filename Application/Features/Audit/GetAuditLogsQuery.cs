using Application.Common.Auditing;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Audit;

/// <summary>
/// İşlem kaydı listesi (en yeni üstte, sayfalı).
/// Yetki: Admin tüm mağazalar; mağaza müdürü SADECE kendi mağazası
/// (StoreId parametresi müdür için yok sayılır).
/// </summary>
public record GetAuditLogsQuery(
    DateTime? From,
    DateTime? To,
    int? StoreId,
    string? User,          // personel no, ad ya da e-posta (içerir)
    string? Action,        // "Pos.ProcessRefund" gibi anahtar
    string? EntityId,      // fiş no, ürün id...
    bool? Succeeded,
    AuditCategory? Category,
    int Page = 1,
    int PageSize = 50
) : IRequest<AuditLogPage>;

public record AuditLogDto(
    long Id, DateTime OccurredAt, string Category, string Action, string ActionLabel,
    bool Succeeded, int? StatusCode, string? PersonelId, string? UserName, string? Roles,
    int? StoreId, string? StoreName, string? EntityType, string? EntityId, string? Summary,
    string? Details, string? HttpMethod, string? Path, string? IpAddress, int? DurationMs, string? CorrelationId);

public record AuditLogPage(List<AuditLogDto> Items, int TotalCount, int Page, int PageSize);

public record AuditActionOption(string Key, string Label);

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, AuditLogPage>
{
    public const int MaxPageSize = 200;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAuditLogsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AuditLogPage> Handle(GetAuditLogsQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, MaxPageSize);
        var query = _db.AuditLogs.AsNoTracking();

        // Mağaza müdürünün token'ında storeId var: sadece kendi mağazası
        if (int.TryParse(_currentUser.StoreId, out var ownStore))
            query = query.Where(a => a.StoreId == ownStore);
        else if (q.StoreId is not null)
            query = query.Where(a => a.StoreId == q.StoreId);

        if (q.From is not null) query = query.Where(a => a.OccurredAt >= q.From.Value.ToUniversalTime());
        if (q.To is not null) query = query.Where(a => a.OccurredAt < q.To.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(q.Action)) query = query.Where(a => a.Action == q.Action);
        if (!string.IsNullOrWhiteSpace(q.EntityId)) query = query.Where(a => a.EntityId == q.EntityId.Trim());
        if (q.Succeeded is not null) query = query.Where(a => a.Succeeded == q.Succeeded);
        if (q.Category is not null) query = query.Where(a => a.Category == q.Category);
        if (!string.IsNullOrWhiteSpace(q.User))
        {
            var u = q.User.Trim();
            query = query.Where(a => a.PersonelId == u || a.UserName!.Contains(u));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);

        var storeIds = rows.Where(r => r.StoreId != null).Select(r => r.StoreId!.Value).Distinct().ToList();
        var stores = await _db.Stores.AsNoTracking().IgnoreQueryFilters()
            .Where(s => storeIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var items = rows.Select(a => new AuditLogDto(
            a.Id, a.OccurredAt, a.Category == AuditCategory.Change ? "Değişiklik" : "İşlem",
            a.Action, AuditActionCatalog.Label(a.Action), a.Succeeded, a.StatusCode, a.PersonelId, a.UserName,
            a.Roles, a.StoreId, a.StoreId is int sid && stores.TryGetValue(sid, out var n) ? n : null,
            a.EntityType, a.EntityId, a.Summary, a.Details, a.HttpMethod, a.Path, a.IpAddress,
            a.DurationMs, a.CorrelationId)).ToList();

        return new AuditLogPage(items, total, page, size);
    }
}
