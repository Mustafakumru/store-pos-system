using System.Text.Json;
using FluentValidation;

namespace Application.Common.Validation;

/// <summary>
/// Birden çok komutta kullanılan doğrulama kuralları. Uzunluk sınırları
/// veritabanı kolonlarıyla aynıdır (Infrastructure/Persistence/Configurations):
/// sınırı aşan metin eskiden SQL "truncation" hatasıyla 500 dönüyordu.
/// </summary>
public static class CommonRules
{
    public const decimal MaxPrice = 1_000_000m;

    /// <summary>
    /// Türkiye telefon numarasını tek biçime getirir: sadece rakam, 10 hane
    /// (başındaki 0 veya +90 atılır). "0555 123 45 67", "+90 555 123 4567" ve
    /// "5551234567" aynı müşteriyi bulur. Eskiden numara girildiği gibi
    /// saklanıp birebir aranıyordu: aynı kişi farklı yazımla iki kez kaydolabiliyordu.
    /// </summary>
    public static string NormalizePhone(string? raw)
    {
        var digits = new string((raw ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("90")) digits = digits[2..];
        if (digits.Length == 11 && digits.StartsWith('0')) digits = digits[1..];
        return digits;
    }

    /// <summary>Geçerli TR numarası: 10 hane; sabit hat (2/3/4), mobil (5) veya 8xx.</summary>
    public static bool IsValidPhone(string? raw)
    {
        var n = NormalizePhone(raw);
        return n.Length == 10 && n[0] is '2' or '3' or '4' or '5' or '8';
    }

    /// <summary>Kişi adı: harf (Türkçe dahil), boşluk, nokta, kesme, tire.</summary>
    public static bool IsPersonName(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Trim().All(c => char.IsLetter(c) || c is ' ' or '.' or '\'' or '-');

    public static bool IsValidJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { using var _ = JsonDocument.Parse(value); return true; }
        catch (JsonException) { return false; }
    }

    // ── Kural uzantıları ─────────────────────────────────────────

    public static IRuleBuilderOptions<T, string> RequiredText<T>(
        this IRuleBuilder<T, string> rule, string field, int min, int max)
        => rule
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage($"{field} zorunludur.")
            .Must(v => v is null || v.Trim().Length >= min).WithMessage($"{field} en az {min} karakter olmalıdır.")
            .Must(v => v is null || v.Trim().Length <= max).WithMessage($"{field} en fazla {max} karakter olabilir.");

    public static IRuleBuilderOptions<T, string?> OptionalText<T>(
        this IRuleBuilder<T, string?> rule, string field, int max)
        => rule
            .Must(v => v is null || v.Trim().Length <= max)
            .WithMessage($"{field} en fazla {max} karakter olabilir.");

    public static IRuleBuilderOptions<T, decimal> ValidPrice<T>(this IRuleBuilder<T, decimal> rule, string field = "Fiyat")
        => rule
            .GreaterThan(0).WithMessage($"{field} 0'dan büyük olmalıdır.")
            .LessThanOrEqualTo(MaxPrice).WithMessage($"{field} {MaxPrice:N0} TL'yi aşamaz.");

    public static IRuleBuilderOptions<T, int> ValidId<T>(this IRuleBuilder<T, int> rule, string field)
        => rule.GreaterThan(0).WithMessage($"Geçersiz {field}.");
}
