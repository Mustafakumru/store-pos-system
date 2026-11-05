namespace Domain.Entities;

/// <summary>
/// İşlem kaydı: kim, hangi mağazada, ne zaman, ne yaptı ve sonuç ne oldu.
///
/// İki tür kayıt:
///  • <see cref="AuditCategory.Request"/>  — sisteme yazan her API isteği (satış, iade, giriş
///    denemesi, yönetici onayı, kullanıcı/ürün işlemleri...). Başarısız denemeler de yazılır.
///  • <see cref="AuditCategory.Change"/>   — ana verilerde (ürün, kupon, mağaza, kullanıcı...)
///    hangi alanın eski → yeni değeri. İsteğe <see cref="CorrelationId"/> ile bağlanır.
///
/// Yalnızca EKLENİR: veritabanındaki tetikleyici UPDATE/DELETE'i reddeder.
/// PIN, şifre, token asla yazılmaz; kart no / telefon / e-posta maskelenir.
/// BaseEntity'den türemez (soft-delete / UpdatedBy anlamsız).
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public AuditCategory Category { get; set; }

    /// <summary>"Pos.ProcessSale", "Product.Updated" gibi makine anahtarı (etiket: AuditActionCatalog).</summary>
    public string Action { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public int? StatusCode { get; set; }

    public string? UserId { get; set; }
    public string? PersonelId { get; set; }
    public string? UserName { get; set; }
    public string? Roles { get; set; }
    public int? StoreId { get; set; }

    public string? EntityType { get; set; }
    public string? EntityId { get; set; }

    /// <summary>Kısa sonuç: "Fiş 001202611050012", hata mesajı vb.</summary>
    public string? Summary { get; set; }

    /// <summary>Maskelenmiş istek gövdesi ya da alan değişiklikleri (JSON).</summary>
    public string? Details { get; set; }

    public string? HttpMethod { get; set; }
    public string? Path { get; set; }
    public string? IpAddress { get; set; }
    public int? DurationMs { get; set; }
    public string? CorrelationId { get; set; }
}

public enum AuditCategory
{
    Request = 1,
    Change = 2
}
