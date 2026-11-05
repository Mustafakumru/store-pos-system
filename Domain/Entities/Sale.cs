using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities;

public class Sale : BaseEntity
{
    // Fiş numarası — örn: "RCP-IST001-20250507-0042"
    public string ReceiptNumber { get; set; } = string.Empty;

    public int StoreId { get; set; }

    // JWT'den gelen kasiyer ID
    public string CashierId { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }

    public PaymentType PaymentType { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    // Bu satışta en az bir ürün negatif stoğa düşürdüyse true
    // Yönetim paneli bu flag ile uyarı üretir
    public bool HasNegativeStockItem { get; set; }
    // ── Müşteri bağlantısı (opsiyonel) ────────────────────────────
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    // ── Kupon (opsiyonel) - kupon indirimi DiscountAmount'a dahildir ──
    public int? CouponId { get; set; }
    public Coupon? Coupon { get; set; }
    public decimal CouponDiscountAmount { get; set; }

    // Bu fişten şimdiye kadar iade edilen toplam (iade defteriyle aynı).
    // Her iadede değiştiği için satırın rowversion'ı değişir: aynı fişe
    // eşzamanlı iki iade ödeme sınırlarını eski veriyle hesaplayamaz.
    public decimal RefundedAmount { get; set; }

    // ── Çevrimdışı satış ─────────────────────────────────────────
    // Sunucuya ulaşılamadığında kasada yapılıp sonradan gönderilen satış.
    // Müşterinin fişinde OfflineReference basılıdır; iade/değişimde bu numarayla
    // da bulunur. ReceiptNumber gönderim anında (mağaza sırasıyla) atanır.
    public bool IsOffline { get; set; }
    public string? OfflineReference { get; set; }
    /// <summary>Satışın kasada gerçekten yapıldığı an (UTC). CreatedAt = sunucuya ulaştığı an.</summary>
    public DateTime? OfflineSoldAt { get; set; }

    // Navigation
    public ICollection<SalePayment> Payments { get; set; } = [];
    public Store Store { get; set; } = null!;
    public ICollection<SaleItem> Items { get; set; } = [];
}
