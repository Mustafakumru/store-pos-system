using Domain.Enums;

namespace Domain.Common;

/// <summary>
/// Urun (model) kodu kurallari. Kod her zaman 8 karakterdir:
///
///   W 6 BI 21 Z8
///   │ │ │  │  └─ Yas kitlesi: Z1 bebek, Z4 10-14 yas, Z8 yetiskin
///   │ │ │  └──── Model sira no (kategori + sezon + yas kitlesi icinde benzersiz)
///   │ │ └─────── Kategori kodu (2 harf, Categories.Code)
///   │ └───────── Sezon yili (yilin son hanesi: 2026 -> 6)
///   └─────────── Sezon: W kis, S yaz
///
/// Model sira no 2 karakterdir: once 00..99, sonra harfli kombinasyonlar
/// (0A..ZZ) - bir kova (sezon+yil+kategori+yas) icin 1296 model.
/// Renk ve beden kodda YOKTUR: ayni kod altinda varyant (barkod) olarak tutulur.
/// </summary>
public static class ProductCode
{
    public const int Length = 8;
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>Kova basina uretilebilecek en fazla model sayisi.</summary>
    public const int MaxModelsPerBucket = 36 * 36;

    // Sira: once yalniz rakamli 100 kombinasyon (00-99), ardindan harf iceren 1196.
    private static readonly string[] ModelSuffixes = BuildSuffixes();

    private static string[] BuildSuffixes()
    {
        var digitsOnly = new List<string>();
        var withLetters = new List<string>();
        foreach (var a in Alphabet)
            foreach (var b in Alphabet)
            {
                var s = $"{a}{b}";
                if (char.IsDigit(a) && char.IsDigit(b)) digitsOnly.Add(s);
                else withLetters.Add(s);
            }
        return digitsOnly.Concat(withLetters).ToArray();
    }

    public static char SeasonChar(Season season) => season == Season.Winter ? 'W' : 'S';

    public static AgeGroup AgeGroupOf(Department department) => department switch
    {
        Department.Baby => AgeGroup.Baby,
        Department.Girl or Department.Boy => AgeGroup.Kids,
        _ => AgeGroup.Adult
    };

    /// <summary>Sezon/yil/kategori/yas kitlesi kovasinin adi (sayac kapsami): "W6BIZ8".</summary>
    public static string BucketKey(Season season, int seasonYear, string categoryCode, AgeGroup ageGroup)
        => $"{SeasonChar(season)}{seasonYear % 10}{NormalizeCategoryCode(categoryCode)}Z{(int)ageGroup}";

    /// <summary>1 tabanli sira numarasindan 8 karakterlik kodu uretir.</summary>
    public static string Build(Season season, int seasonYear, string categoryCode, AgeGroup ageGroup, int sequence)
    {
        if (seasonYear is < 2000 or > 2099)
            throw new ArgumentOutOfRangeException(nameof(seasonYear), "Sezon yili 2000-2099 araliginda olmalidir.");
        if (sequence < 1 || sequence > MaxModelsPerBucket)
            throw new InvalidOperationException(
                $"Bu sezon/kategori/yas kitlesi icin model kodu kapasitesi ({MaxModelsPerBucket}) doldu.");

        var cat = NormalizeCategoryCode(categoryCode);
        return $"{SeasonChar(season)}{seasonYear % 10}{cat}{ModelSuffixes[sequence - 1]}Z{(int)ageGroup}";
    }

    /// <summary>Kategori kodu: tam 2 buyuk Latin harfi.</summary>
    public static string NormalizeCategoryCode(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (c.Length != 2 || !c.All(ch => ch is >= 'A' and <= 'Z'))
            throw new InvalidOperationException(
                $"Kategori kodu 2 Latin harften olusmalidir (orn. PN, GM). Gecersiz: '{code}'.");
        return c;
    }

    public static bool IsValidCategoryCode(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        return c.Length == 2 && c.All(ch => ch is >= 'A' and <= 'Z');
    }

    /// <summary>Kodu cozer; gecersizse null doner. Buyuk/kucuk harf duyarsiz.</summary>
    public static ProductCodeInfo? TryParse(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var c = code.Trim().ToUpperInvariant();
        if (c.Length != Length) return null;

        Season season;
        if (c[0] == 'W') season = Season.Winter;
        else if (c[0] == 'S') season = Season.Summer;
        else return null;

        if (!char.IsDigit(c[1])) return null;
        var yearDigit = c[1] - '0';

        var cat = c.Substring(2, 2);
        if (!IsValidCategoryCode(cat)) return null;

        var model = c.Substring(4, 2);
        if (!model.All(Alphabet.Contains)) return null;

        if (c[6] != 'Z') return null;
        AgeGroup age;
        switch (c[7])
        {
            case '1': age = AgeGroup.Baby; break;
            case '4': age = AgeGroup.Kids; break;
            case '8': age = AgeGroup.Adult; break;
            default: return null;
        }

        return new ProductCodeInfo(c, season, yearDigit, cat, model, age);
    }

    /// <summary>
    /// Tek haneli yildan tam yil: referans yila (bugun) en yakin, gelecege en fazla 1 yil.
    /// Orn. 2026'da: 6 -> 2026, 7 -> 2027, 5 -> 2025, 9 -> 2019.
    /// </summary>
    public static int ResolveYear(int yearDigit, int referenceYear)
    {
        var decade = referenceYear - referenceYear % 10;
        var candidate = decade + yearDigit;
        if (candidate > referenceYear + 1) candidate -= 10;
        return candidate;
    }

    public static string SeasonLabel(Season season) => season == Season.Winter ? "Kis" : "Yaz";

    public static string AgeGroupLabel(AgeGroup age) => age switch
    {
        AgeGroup.Baby => "Bebek (Z1)",
        AgeGroup.Kids => "Cocuk 10-14 yas (Z4)",
        _ => "Yetiskin (Z8)"
    };

    public static string DepartmentLabel(Department d) => d switch
    {
        Department.Women => "Kadin",
        Department.Men => "Erkek",
        Department.Girl => "Kiz Cocuk",
        Department.Boy => "Erkek Cocuk",
        Department.Baby => "Bebek",
        _ => d.ToString()
    };
}

/// <summary>Cozulmus urun kodu.</summary>
public record ProductCodeInfo(
    string Code,
    Season Season,
    int YearDigit,
    string CategoryCode,
    string ModelSuffix,
    AgeGroup AgeGroup);
