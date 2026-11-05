namespace Domain.Enums;

/// <summary>
/// Hediye kartı hareket türü.
/// Load: Bakiye yükleme, Redeem: Ödemede kullanma, Refund: İade ile bakiye geri yükleme.
/// </summary>
public enum GiftCardTransactionType
{
    Load = 0,
    Redeem = 1,
    Refund = 2,
    GiftCard = 3
}