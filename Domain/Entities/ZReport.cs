using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// WPOS "Z Raporu" — gün sonu kapanışında alınan, kapanan dönemin
/// özetini kalıcı olarak saklayan rapor. Mağaza bazında artan
/// ReportNumber ile numaralanır (Mali Rapor'da Z no aralığı seçimi
/// için kullanılır).
/// </summary>
public class ZReport : BaseEntity
{
    public int StoreId { get; set; }
    public int ReportNumber { get; set; } // Mağaza bazında artan Z no
    public DateTime FromDate { get; set; } // Önceki Z raporundan bu yana
    public DateTime ToDate { get; set; } // Rapor anı

    // ── Özet Tutarlar ──────────────────────────────────────────
    public decimal TotalSalesAmount { get; set; }
    public decimal TotalRefundsAmount { get; set; }
    public decimal NetAmount { get; set; }

    // ── Ödeme Tipi Kırılımı ────────────────────────────────────
    public decimal CashTotal { get; set; }
    public decimal CardTotal { get; set; }
    public decimal GiftCardTotal { get; set; }

    // ── İade ödeme kırılımı (iade defterinden) ─────────────────
    public decimal CashRefundTotal { get; set; }   // kasadan çıkan nakit iade
    public decimal CardRefundTotal { get; set; }   // karta yapılan iade

    // ── Hediye kartı satışı (EMANET - ciroya dahil değil) ───────
    public decimal GiftCardSoldTotal { get; set; }
    public decimal GiftCardSoldCash { get; set; }  // kasaya giren nakit
    public decimal GiftCardSoldCard { get; set; }

    public int TransactionCount { get; set; }
    public int RefundCount { get; set; }
    public string CashierId { get; set; } = string.Empty; // Raporu alan kasiyer

    // Navigation
    public Store Store { get; set; } = null!;
}