using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Product> Products { get; set; }
    DbSet<Category> Categories { get; set; }
    DbSet<PriceHistory> PriceHistories { get; set; }
    DbSet<Store> Stores { get; }
    DbSet<ProductVariantGroup> ProductVariantGroups { get; } 
    DbSet<ProductVariantGroupImage> ProductVariantGroupImages { get; } 
    DbSet<WarehouseStock> WarehouseStocks { get; }
    DbSet<StoreInventory> StoreInventories { get; }
    DbSet<StockTransfer> StockTransfers { get; }
    DbSet<StockTransferItem> StockTransferItems { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }
    DbSet<ProductVariant> ProductVariants { get; set; }
    DbSet<ReceiptSequence> ReceiptSequences { get; }
    DbSet<Customer> Customers { get; }
    DbSet<SalePayment> SalePayments { get; }
    DbSet<SaleReturn> SaleReturns { get; }
    DbSet<SaleReturnLine> SaleReturnLines { get; }
    DbSet<SaleReturnPayout> SaleReturnPayouts { get; }
    DbSet<GiftCard> GiftCards { get; }
    DbSet<GiftCardSale> GiftCardSales { get; }
    DbSet<GiftCardTransaction> GiftCardTransactions { get; }
    DbSet<ReturnReason> ReturnReasons { get; }
    DbSet<SuspendedSale> SuspendedSales { get; }
    DbSet<CashDrawerSession> CashDrawerSessions { get; }
    DbSet<Coupon> Coupons { get; }
    DbSet<ZReport> ZReports { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<BackupRecord> BackupRecords { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    DbSet<CodeSequence> CodeSequences { get; }

    /// <summary>
    /// Hata, eszamanli degisiklik cakismasi mi? (rowversion uyusmazligi ya da
    /// ayni stok satirini iki istegin ayni anda olusturmasi) - yeniden denenebilir.
    /// </summary>
    bool IsConcurrencyConflict(Exception exception);

    /// <summary>Izlenen tum varliklari birakir (yeniden deneme oncesi temiz baslangic).</summary>
    void ResetTracking();

    /// <summary>Urun kodu / barkod sayaci: atomik +1.</summary>
    Task<int> GetNextCodeSequenceValueAsync(string scope, CancellationToken cancellationToken);

    /// <summary>Sayacin mevcut degeri (artirmadan).</summary>
    Task<int> PeekCodeSequenceValueAsync(string scope, CancellationToken cancellationToken);

    /// <summary>
    /// Açık transaction başlatır. Fiş numarası sayacı (GetNextReceiptSequenceValueAsync)
    /// bu transaction'a katılır: kayıt başarısız olursa sayaç da geri alınır,
    /// fiş numaralarında boşluk oluşmaz.
    /// </summary>
    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken);

    Task<int> GetNextReceiptSequenceValueAsync(
        int storeId, DateTime date, CancellationToken cancellationToken);
}