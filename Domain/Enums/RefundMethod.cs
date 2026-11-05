namespace Domain.Enums;

/// <summary>İade tutarının müşteriye nasıl ödendiği.</summary>
public enum RefundMethod
{
    Cash = 0,            // kasadan nakit (kasa sayımında düşülür)
    Card = 1,            // karta iade (POS iadesi)
    GiftCard = 2,        // hediye kartına yükleme
    ExchangeCredit = 3   // değişimde yeni satışta kullanıldı - para çıkışı yok
}
