using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Kasiyerin kasa oturumu. "Avans" ile açılır (OpeningFloat),
/// "Teslim" ile kapanır (ClosingAmount). Her ikisi de yönetici onayı gerektirir.
/// </summary>
public class CashDrawerSession : BaseEntity
{
    public int StoreId { get; set; }
    public string CashierId { get; set; } = string.Empty;

    // ── Açılış (Avans) ─────────────────────────────────────────
    public decimal OpeningFloat { get; set; }
    public DateTime OpenedAt { get; set; }
    public string? OpeningApprovedByUserId { get; set; }

    // ── Kapanış (Teslim) ───────────────────────────────────────
    public decimal? ClosingAmount { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosingApprovedByUserId { get; set; }

    public CashSessionStatus Status { get; set; } = CashSessionStatus.Open;

    // Navigation
    public Store Store { get; set; } = null!;
}