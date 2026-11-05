namespace Application.Features.POS.Common;

/// <summary>
/// Fiş ekranındaki "4 Taksit +%5" / "8 Taksit +%10" vade farkı. Fark,
/// satır indiriminden SONRA kalan tutar üzerinden hesaplanır ve satırın
/// LineTotal'ına eklenir (WPF CartItem.InstallmentSurchargeAmount ile aynı
/// formül). Böylece Sale.FinalAmount müşteriden gerçekte tahsil edilen
/// tutarı gösterir; iadede de müşteri ödediği tutarı (fark dahil) geri alır.
/// </summary>
public static class InstallmentPricing
{
    public static decimal GetSurchargeRate(int? installmentPlan) => installmentPlan switch
    {
        null or 0 => 0m,
        4 => 0.05m,
        8 => 0.10m,
        _ => throw new InvalidOperationException(
            $"Geçersiz taksit planı: {installmentPlan}. Sadece 4 veya 8 seçilebilir.")
    };

    public static decimal CalculateSurcharge(decimal amountAfterLineDiscount, decimal rate)
        => rate == 0 ? 0 : Math.Round(amountAfterLineDiscount * rate, 2);
}
