using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// POS müşteri kaydı. Telefon numarası ile hızlı arama yapılır
/// (fiş ekranı müşteri seç / iade ekranı müşteri sorgulama).
/// </summary>
public class Customer : BaseEntity
{
    public string PhoneNumber { get; set; } = null!; // Unique, arama anahtarı
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public bool SmsConsent { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}