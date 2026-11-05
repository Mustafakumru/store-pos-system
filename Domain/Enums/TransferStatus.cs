using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Enums;

public enum TransferStatus
{
    Draft = 0,  // Oluşturuldu, henüz onaylanmadı
    Shipped = 1,  // Depodan gönderildi (WarehouseStock.ReservedQty artar)
    Received = 2,  // Mağaza teslim aldı (StoreInventory artar)
    Cancelled = 3   // İptal edildi
}

public enum PaymentType
{
    Cash = 0,
    Card = 1,
    Mixed = 2 , // Hem nakit hem kart
    GiftCard = 3,  // ✅ eklendi
    ExchangeCredit = 4  // Değişimde iade edilen ürünün kredisi (para girişi değil)
}

public enum SaleStatus
{
    Completed = 0,
    Refunded = 1,  // Tam iade
    Partial = 2   // Kısmi iade
}