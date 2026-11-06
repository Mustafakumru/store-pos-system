using Application.Common.Auditing;
using Domain.Common;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;

namespace Infrastructure.Persistence.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditableEntityInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // Sync çağrı — async versiyona yönlendir
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureFieldChanges(eventData.Context);
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    // Async çağrı — asıl iş burada
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureFieldChanges(eventData.Context);
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null) return;

        // JWT token'dan kullanıcı kimliğini oku
        var userId = _httpContextAccessor.HttpContext?
            .User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system"; // Arka plan işlemleri için fallback

        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    // CreatedAt ve CreatedBy asla değiştirilmez
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    break;
            }
        }
    }

    // ── Ana veri değişiklik kaydı (işlem kaydı, kategori: Change) ───────────
    // Ürün fiyatı, kupon, mağaza, kullanıcı (aktif/pasif, PIN sıfırlama) gibi
    // değişikliklerde HANGİ ALANIN eski → yeni değeri aynı SaveChanges içinde
    // AuditLogs'a yazılır (değişiklikle birlikte kaydedilir ya da hiç yazılmaz).
    // Satış/stok hareketleri burada izlenmez: onlar kendi belgelerinde ve istek
    // kaydında (AuditMiddleware) zaten var.

    /// <summary>İzlenen tür → kayda girmeyecek (gürültü) alanlar.</summary>
    private static readonly Dictionary<string, HashSet<string>> TrackedTypes = new()
    {
        ["Product"] = new(),
        ["ProductVariant"] = new(),
        ["ProductVariantGroup"] = new(),
        ["Category"] = new(),
        ["Coupon"] = new() { "UsedCount", "RowVersion" },
        ["Store"] = new(),
        ["ReturnReason"] = new(),
        ["StoreSalesTarget"] = new(),   // hedef değişikliği: eski -> yeni tutar
        ["Customer"] = new(),
        ["StoreInventory"] = new() { "Quantity", "RowVersion" },   // sadece min. stok gibi ayarlar
        ["AppUser"] = new() { "AccessFailedCount", "SecurityStamp", "ConcurrencyStamp" }
    };

    private static readonly HashSet<string> AlwaysIgnored = new()
    {
        "UpdatedAt", "UpdatedBy", "CreatedAt", "CreatedBy", "RowVersion"
    };

    /// <summary>Değeri yazılmayan, sadece "değişti" bilgisi tutulan alanlar.</summary>
    private static readonly HashSet<string> ValueHidden = new()
    {
        "PinHash", "PasswordHash", "FullName"
    };

    private void CaptureFieldChanges(DbContext? context)
    {
        if (context is null) return;
        var http = _httpContextAccessor.HttpContext;
        var user = http?.User;

        var logs = new List<AuditLog>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted)) continue;
            var typeName = entry.Metadata.ClrType.Name;
            if (!TrackedTypes.TryGetValue(typeName, out var ignored)) continue;

            var changes = new Dictionary<string, object?>();
            var softDeleted = false;
            foreach (var prop in entry.Properties)
            {
                var name = prop.Metadata.Name;
                if (AlwaysIgnored.Contains(name) || ignored.Contains(name)) continue;
                if (entry.State == EntityState.Modified && !prop.IsModified) continue;
                var oldValue = prop.OriginalValue;
                var newValue = entry.State == EntityState.Deleted ? null : prop.CurrentValue;
                if (entry.State == EntityState.Modified && Equals(oldValue, newValue)) continue;
                if (name == "IsDeleted" && newValue is true) softDeleted = true;

                changes[name] = ValueHidden.Contains(name)
                    ? "(değişti)"
                    : new Dictionary<string, string?>
                    {
                        ["eski"] = AuditSanitizer.MaskValue(name, oldValue),
                        ["yeni"] = AuditSanitizer.MaskValue(name, newValue)
                    };
            }
            if (changes.Count == 0) continue;

            var deleted = entry.State == EntityState.Deleted || softDeleted;
            var key = entry.Metadata.FindPrimaryKey()?.Properties
                .Select(p => entry.Property(p.Name).CurrentValue?.ToString())
                .FirstOrDefault();

            logs.Add(new AuditLog
            {
                OccurredAt = DateTime.UtcNow,
                Category = AuditCategory.Change,
                Action = $"{typeName}.{(deleted ? "Deleted" : "Updated")}",
                Succeeded = true,
                UserId = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system",
                PersonelId = user?.FindFirstValue("personelId"),
                UserName = user?.FindFirstValue(ClaimTypes.Name),
                StoreId = int.TryParse(user?.FindFirstValue("storeId"), out var sid) ? sid : null,
                EntityType = typeName,
                EntityId = key,
                Summary = AuditSanitizer.Truncate(string.Join(", ", changes.Keys), 500),
                Details = AuditSanitizer.Serialize(changes),
                HttpMethod = http?.Request.Method,
                Path = http?.Request.Path.Value,
                CorrelationId = http?.TraceIdentifier
            });
        }

        if (logs.Count > 0)
            context.Set<AuditLog>().AddRange(logs);
    }
}
