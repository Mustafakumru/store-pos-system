namespace Domain.Enums;

/// <summary>
/// Satır indirimi türü — WPOS'taki "Tutar İndirimi" / "Yüzde İndirimi".
/// </summary>
public enum DiscountType
{
    Amount = 0, // Sabit tutar (örn. 50 TL indirim)
    Percentage = 1  // Yüzde (örn. %25 indirim)
}