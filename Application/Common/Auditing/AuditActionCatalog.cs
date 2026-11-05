namespace Application.Common.Auditing;

/// <summary>İşlem kaydı anahtarlarının ekranda görünen Türkçe adları.</summary>
public static class AuditActionCatalog
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        // Giriş / kullanıcı
        ["Auth.Login"] = "Panel girişi",
        ["Auth.CreateUser"] = "Kullanıcı oluşturma",
        ["Auth.ToggleUserStatus"] = "Kullanıcı aktif/pasif",
        ["Auth.UpdateUser"] = "Kullanıcı güncelleme",
        ["PosAuth.Login"] = "Kasa girişi",
        ["PosAuth.ChangePin"] = "PIN değiştirme",
        ["PosAuth.ResetPin"] = "PIN sıfırlama",
        ["ManagerApproval.RequestApproval"] = "Yönetici onayı",

        // Kasa
        ["Pos.ProcessSale"] = "Satış",
        ["Pos.ProcessExchange"] = "Değişim",
        ["Pos.ProcessRefund"] = "İade",
        ["CashSessions.Open"] = "Kasa açılışı",
        ["CashSessions.Close"] = "Kasa kapanışı",
        ["Reports.CreateZReport"] = "Z raporu",
        ["Reports.Reprint"] = "Fiş yeniden basım",
        ["GiftCards.Sell"] = "Hediye kartı satışı",
        ["GiftCards.Load"] = "Hediye kartı yükleme (ödemesiz)",
        ["SuspendedCarts.Suspend"] = "Belge askıya alma",
        ["SuspendedCarts.Resume"] = "Askıdaki belgeyi açma",
        ["Customers.Create"] = "Müşteri kaydı",

        // Ürün / stok
        ["Products.Create"] = "Ürün oluşturma",
        ["Products.Update"] = "Ürün güncelleme",
        ["Products.Delete"] = "Ürün silme",
        ["Products.AddVariant"] = "Varyant ekleme",
        ["Products.GenerateVariants"] = "Varyant üretme",
        ["Products.UpdateVariant"] = "Varyant güncelleme",
        ["Products.UpdateVariantPrice"] = "Fiyat değişikliği",
        ["Products.DeleteVariant"] = "Varyant silme",
        ["Products.CreateVariantGroup"] = "Renk grubu oluşturma",
        ["Products.AssignVariantToGroup"] = "Varyant grubu atama",
        ["Products.AddImageToGroup"] = "Ürün görseli ekleme",
        ["Products.DeleteGroupImage"] = "Ürün görseli silme",
        ["Products.UploadImage"] = "Görsel yükleme",
        ["Categories.UpdateCode"] = "Kategori kodu değiştirme",
        ["Warehouse.Receive"] = "Depo mal kabul",
        ["StockTransfers.Create"] = "Sevk talebi",
        ["StockTransfers.Ship"] = "Sevk gönderimi",
        ["StockTransfers.Cancel"] = "Sevk iptali",
        ["StockTransfers.Receive"] = "Sevk teslim alma",
        ["Stores.Create"] = "Mağaza oluşturma",
        ["Stores.Update"] = "Mağaza güncelleme",
        ["Stores.ToggleStatus"] = "Mağaza aktif/pasif",
        ["Stores.UpdateMinStock"] = "Minimum stok değiştirme",
        ["Coupons.Create"] = "Kupon oluşturma",
        ["Coupons.SetActive"] = "Kupon aktif/pasif",

        // Sistem
        ["Backups.Run"] = "Elle yedek alma",
        ["System.Backup"] = "Otomatik yedek"
    };

    private static readonly Dictionary<string, string> EntityLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Product"] = "Ürün", ["ProductVariant"] = "Varyant", ["ProductVariantGroup"] = "Renk grubu",
        ["Category"] = "Kategori", ["Coupon"] = "Kupon", ["Store"] = "Mağaza", ["ReturnReason"] = "İade nedeni",
        ["Customer"] = "Müşteri", ["AppUser"] = "Kullanıcı", ["StoreInventory"] = "Mağaza stoğu"
    };

    public static string Label(string action)
    {
        if (Labels.TryGetValue(action, out var label)) return label;
        // Alan değişikliği: "Product.Updated" -> "Ürün değişikliği"
        var dot = action.IndexOf('.');
        if (dot > 0 && EntityLabels.TryGetValue(action[..dot], out var entity))
            return action.EndsWith(".Deleted") ? $"{entity} silindi" : $"{entity} değişikliği";
        return action;
    }

    public static IReadOnlyDictionary<string, string> All => Labels;
}
