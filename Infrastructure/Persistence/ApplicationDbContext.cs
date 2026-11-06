using Application.Common.Interfaces;
using Domain.Entities;
using Infrastructure.Identity;
using Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence;

public class ApplicationDbContext
    : IdentityDbContext<AppUser, IdentityRole, string>,
      IApplicationDbContext
{
    private readonly AuditableEntityInterceptor _auditableEntityInterceptor;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        AuditableEntityInterceptor auditableEntityInterceptor)
        : base(options)
    {
        _auditableEntityInterceptor = auditableEntityInterceptor;
    }

    // ── Katalog ──────────────────────────────────────────────
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<ProductVariant> ProductVariants { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<PriceHistory> PriceHistories { get; set; } = null!;

    // ── Mağaza & Stok ────────────────────────────────────────
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<WarehouseStock> WarehouseStocks => Set<WarehouseStock>();
    public DbSet<StoreInventory> StoreInventories => Set<StoreInventory>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();
    public DbSet<SuspendedSale> SuspendedSales => Set<SuspendedSale>();
    public DbSet<CashDrawerSession> CashDrawerSessions => Set<CashDrawerSession>();
    // ── Satış ────────────────────────────────────────────────
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<ZReport> ZReports => Set<ZReport>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<ReturnReason> ReturnReasons => Set<ReturnReason>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnLine> SaleReturnLines => Set<SaleReturnLine>();
    public DbSet<SaleReturnPayout> SaleReturnPayouts => Set<SaleReturnPayout>();
    public DbSet<GiftCard> GiftCards => Set<GiftCard>();
    public DbSet<GiftCardSale> GiftCardSales => Set<GiftCardSale>();
    public DbSet<GiftCardTransaction> GiftCardTransactions => Set<GiftCardTransaction>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ReceiptSequence> ReceiptSequences => Set<ReceiptSequence>();
    public DbSet<CodeSequence> CodeSequences => Set<CodeSequence>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<StoreSalesTarget> StoreSalesTargets => Set<StoreSalesTarget>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();
    public DbSet<ProductVariantGroup> ProductVariantGroups => Set<ProductVariantGroup>();
    public DbSet<ProductVariantGroupImage> ProductVariantGroupImages => Set<ProductVariantGroupImage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);

        // POS girişi PersonelId ile yapılır: numara benzersiz OLMALI
        // (eskiden nvarchar(max) ve indekssizdi - aynı numara birden çok
        // kullanıcıya verilebiliyordu).
        builder.Entity<AppUser>(u =>
        {
            u.Property(x => x.PersonelId).HasMaxLength(20);
            u.HasIndex(x => x.PersonelId)
             .IsUnique()
             .HasFilter("[PersonelId] IS NOT NULL");
        });

        // Global soft delete filtreleri
        builder.Entity<Product>()
               .HasQueryFilter(p => !p.IsDeleted);
        builder.Entity<ProductVariant>()
               .HasQueryFilter(v => !v.IsDeleted);
        builder.Entity<Category>()
               .HasQueryFilter(c => !c.IsDeleted);
        builder.Entity<PriceHistory>()
               .HasQueryFilter(ph => !ph.IsDeleted);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditableEntityInterceptor);
    }

    public async Task<int> GetNextReceiptSequenceValueAsync(
        int storeId, DateTime date, CancellationToken cancellationToken)
    {
        var connection = Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        // ── Atomik upsert (SQL Server): MERGE + HOLDLOCK + OUTPUT ──
        // Kayit yoksa 1 ile olusturur, varsa +1 artirir; ayni statement
        // icinde guncel degeri OUTPUT ile doner. HOLDLOCK: ayni gunun ilk
        // fisini iki istek ayni anda olusturmaya calisirsa ikincisi bekler
        // (unique index ihlali yerine).
        //
        // Acik transaction varsa (satis/degisim handler'lari) ona katilir:
        // satis kaydedilemezse sayac artisi da geri alinir - fis numarasi
        // "yanmaz" (eskiden basarisiz her satis bir numara tuketiyordu).
        await using var command = connection.CreateCommand();
        if (Database.CurrentTransaction is { } tx)
            command.Transaction = tx.GetDbTransaction();
        command.CommandText = @"
        MERGE INTO ReceiptSequences WITH (HOLDLOCK) AS target
        USING (SELECT @storeId AS StoreId, @date AS SequenceDate) AS source
            ON target.StoreId = source.StoreId
           AND target.SequenceDate = source.SequenceDate
        WHEN MATCHED THEN
            UPDATE SET CurrentValue = target.CurrentValue + 1,
                       UpdatedAt = @now
        WHEN NOT MATCHED THEN
            INSERT (StoreId, SequenceDate, CurrentValue, CreatedAt, IsDeleted)
            VALUES (@storeId, @date, 1, @now, 0)
        OUTPUT INSERTED.CurrentValue;";

        command.Parameters.Add(new SqlParameter("@storeId", storeId));
        command.Parameters.Add(new SqlParameter("@date", date.Date));
        command.Parameters.Add(new SqlParameter("@now", DateTime.UtcNow));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Urun kodu / barkod sayaci: scope icin bir sonraki degeri atomik dondurur
    /// (yoksa 1 ile olusturur). Unique (Scope) index ile birlikte yaris durumuna karsi guvenli.
    /// </summary>
    public async Task<int> GetNextCodeSequenceValueAsync(string scope, CancellationToken cancellationToken)
    {
        var connection = Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        if (Database.CurrentTransaction is { } tx)
            command.Transaction = tx.GetDbTransaction();
        command.CommandText = @"
        MERGE INTO CodeSequences WITH (HOLDLOCK) AS target
        USING (SELECT @scope AS Scope) AS source
            ON target.Scope = source.Scope
        WHEN MATCHED THEN
            UPDATE SET CurrentValue = target.CurrentValue + 1, UpdatedAt = @now
        WHEN NOT MATCHED THEN
            INSERT (Scope, CurrentValue, CreatedAt, IsDeleted)
            VALUES (@scope, 1, @now, 0)
        OUTPUT INSERTED.CurrentValue;";
        command.Parameters.Add(new SqlParameter("@scope", scope));
        command.Parameters.Add(new SqlParameter("@now", DateTime.UtcNow));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    /// <summary>Sayacin mevcut degeri (artirmadan) - kod onizlemesi icin.</summary>
    public async Task<int> PeekCodeSequenceValueAsync(string scope, CancellationToken cancellationToken)
        => await CodeSequences.AsNoTracking()
            .Where(s => s.Scope == scope)
            .Select(s => (int?)s.CurrentValue)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
        => Database.BeginTransactionAsync(cancellationToken);

    public bool IsConcurrencyConflict(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException) return true;
        // Ayni (magaza, varyant) stok satirini iki istek ayni anda olusturdu:
        // yeniden denemede satir var olur, guncellenir.
        return exception is DbUpdateException { InnerException: SqlException sql }
               && sql.Number is 2601 or 2627
               && sql.Message.Contains("IX_StoreInventories", StringComparison.OrdinalIgnoreCase);
    }

    public void ResetTracking() => ChangeTracker.Clear();
}
