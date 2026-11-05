namespace Domain.Common;

/// <summary>
/// Varyant barkodu (EAN-13): 8684 (GS1 Turkiye on eki) + 2 hane kategori +
/// 6 hane sira no + 1 hane kontrol. Sunucu tarafinda sayactan uretilir,
/// cakisma olmaz (eskiden admin panelinde rastgele uretiliyordu).
/// </summary>
public static class Ean13
{
    public const string CompanyPrefix = "8684";
    public const int MaxSequence = 999_999;

    public static string Build(int categoryId, int sequence)
    {
        if (sequence < 1 || sequence > MaxSequence)
            throw new InvalidOperationException("Bu kategori icin barkod kapasitesi doldu.");
        var body = $"{CompanyPrefix}{categoryId % 100:00}{sequence:000000}";
        return body + CheckDigit(body);
    }

    /// <summary>Barkod kapsami (sayac anahtari): kategori bazli.</summary>
    public static string ScopeKey(int categoryId) => $"BC{CompanyPrefix}{categoryId % 100:00}";

    public static int CheckDigit(string twelveDigits)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
            sum += (twelveDigits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (10 - sum % 10) % 10;
    }

    public static bool IsValid(string? barcode)
        => barcode is { Length: 13 } && barcode.All(char.IsDigit)
           && CheckDigit(barcode[..12]) == barcode[12] - '0';
}
