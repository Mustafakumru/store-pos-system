using CommunityToolkit.Mvvm.ComponentModel;

namespace LCWaikiki.POS.WpfClient.Core.Models.Pos;

public partial class CartItem : ObservableObject
{
    public int VariantId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Size { get; set; }
    public string? ImagePath { get; set; }
    public string? FullImageUrl { get; set; }
    public decimal UnitPrice { get; set; }

    // ── Degisim/Iade destegi ────────────────────────────────────
    // Bu satir bir iade kalemi mi? True ise LineTotal NEGATIF hesaplanir,
    // indirim/taksit farki uygulanmaz ve taksit rozeti (Installment4/8)
    // hesabina katilmaz - sadece yeni satis satirlari taksit hesabina girer.
    public bool IsReturnItem { get; set; }

    // Iade Fisle/Numarayla bulunduysa dolu (hangi fisten geldigi), Manuel/
    // Kayitsiz iadede null.
    public string? SourceReceiptNumber { get; set; }

    [ObservableProperty] private int? _returnReasonId;
    [ObservableProperty] private string? _returnReasonName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    [NotifyPropertyChangedFor(nameof(OriginalLineTotal))]
    [NotifyPropertyChangedFor(nameof(DiscountAmount))]
    [NotifyPropertyChangedFor(nameof(InstallmentSurchargeAmount))]
    [NotifyPropertyChangedFor(nameof(LineTotal8))]
    [NotifyPropertyChangedFor(nameof(LineTotal4))]
    private int _quantity = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    [NotifyPropertyChangedFor(nameof(DiscountAmount))]
    [NotifyPropertyChangedFor(nameof(DiscountType))]
    [NotifyPropertyChangedFor(nameof(InstallmentSurchargeAmount))]
    [NotifyPropertyChangedFor(nameof(LineTotal8))]
    [NotifyPropertyChangedFor(nameof(LineTotal4))]
    private int? _discountTypeValue; // int olarak sakliyoruz (0=Amount, 1=Percentage)

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    [NotifyPropertyChangedFor(nameof(DiscountAmount))]
    [NotifyPropertyChangedFor(nameof(HasDiscount))]
    [NotifyPropertyChangedFor(nameof(InstallmentSurchargeAmount))]
    [NotifyPropertyChangedFor(nameof(LineTotal8))]
    [NotifyPropertyChangedFor(nameof(LineTotal4))]
    private decimal? _discountValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    [NotifyPropertyChangedFor(nameof(LineTotal8))]
    [NotifyPropertyChangedFor(nameof(LineTotal4))]
    [NotifyPropertyChangedFor(nameof(InstallmentSurchargeAmount))]
    private bool _isVoided;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    [NotifyPropertyChangedFor(nameof(InstallmentSurchargeAmount))]
    [NotifyPropertyChangedFor(nameof(LineTotal8))]
    [NotifyPropertyChangedFor(nameof(LineTotal4))]
    [NotifyPropertyChangedFor(nameof(HasInstallment))]
    private int _installmentCount = 0;

    public string? ApprovedByPersonelId { get; set; }

    // DiscountType property'si - int uzerinden calisir, enum cakismasi olmaz
    // Uretilen DiscountTypeValue property'si uzerinden calisir - tum bagimli
    // bildirimler (DiscountAmount, vade farki, LineTotal...) oradan tetiklenir.
    public DiscountType? DiscountType
    {
        get => DiscountTypeValue.HasValue ? (DiscountType?)DiscountTypeValue.Value : null;
        set => DiscountTypeValue = value.HasValue ? (int?)value.Value : null;
    }

    public decimal OriginalLineTotal => UnitPrice * Quantity;

    public decimal DiscountAmount
    {
        get
        {
            if (IsVoided || IsReturnItem) return 0;
            if (DiscountValue is null || DiscountValue <= 0) return 0;
            // 1 = Percentage, 0 = Amount
            var isPercentage = DiscountTypeValue == 1;
            var amount = isPercentage
                ? Math.Round(OriginalLineTotal * (DiscountValue.Value / 100m), 2)
                : DiscountValue.Value;
            return Math.Min(amount, OriginalLineTotal);
        }
    }

    public decimal InstallmentSurchargeAmount
    {
        get
        {
            if (IsVoided || IsReturnItem || InstallmentCount == 0) return 0;
            var baseAmount = OriginalLineTotal - DiscountAmount;
            return InstallmentCount == 4
                ? Math.Round(baseAmount * 0.05m, 2)
                : InstallmentCount == 8
                    ? Math.Round(baseAmount * 0.10m, 2)
                    : 0;
        }
    }

    public decimal LineTotal
    {
        get
        {
            if (IsVoided) return 0;
            // Iade satirinda indirim/taksit farki uygulanmaz - fiyat kilitli
            // (Fisle/Numarayla) ya da kasiyerin girdigi (Manuel) tutardir,
            // negatif olarak toplam iceri dahil edilir.
            if (IsReturnItem) return -OriginalLineTotal;
            return OriginalLineTotal - DiscountAmount + InstallmentSurchargeAmount;
        }
    }

    public bool HasDiscount => DiscountValue is > 0;

    // XAML'da BoolToVisibilityConverter ile kullanmak icin (InstallmentCount int, dogrudan baglanamaz)
    public bool HasInstallment => InstallmentCount > 0;

    public decimal LineTotal8 => LineTotal > 0 ? Math.Round(LineTotal / 8, 2) : 0;
    public decimal LineTotal4 => LineTotal > 0 ? Math.Round(LineTotal / 4, 2) : 0;
}