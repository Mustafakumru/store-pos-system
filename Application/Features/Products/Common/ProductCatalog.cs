using Application.Common.Interfaces;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Common;

/// <summary>
/// Urun/varyant olusturmanin TEK kural noktasi (komutlar ve geriye donuk
/// doldurma ayni kurallari kullanir):
///  • Urun kodu (W6BI21Z8) sayactan uretilir, benzersizdir, degismez.
///  • Barkod verilmezse EAN-13 olarak sayactan uretilir; verilirse dogrulanir.
///  • Ayni urunde ayni Renk+Beden ikinci kez eklenemez.
///  • Her renk icin bir renk grubu (ProductVariantGroup, "Color") tutulur;
///    yeni varyant otomatik olarak kendi renk grubuna baglanir (gorseller
///    renk bazli paylasilir).
/// </summary>
public class ProductCatalog
{
    private readonly IApplicationDbContext _context;

    // Ayni istek icinde uretilen/ayrilan barkodlar (henuz kaydedilmemis)
    private readonly HashSet<string> _reservedBarcodes = new(StringComparer.Ordinal);
    // (urun nesnesi, RENK) -> grup; yeni (Id'siz) urunler icin de calisir
    private readonly Dictionary<(Product Product, string Color), ProductVariantGroup> _groupCache = new();

    public ProductCatalog(IApplicationDbContext context) => _context = context;

    // ══════════════════════ URUN KODU ══════════════════════

    public static void ValidateClassification(Season season, int seasonYear, Department department)
    {
        if (!Enum.IsDefined(season))
            throw new InvalidOperationException("Gecersiz sezon. W (Kis) veya S (Yaz) secin.");
        if (!Enum.IsDefined(department))
            throw new InvalidOperationException("Gecersiz bolum (Kadin, Erkek, Kiz Cocuk, Erkek Cocuk, Bebek).");
        var thisYear = DateTime.UtcNow.Year;
        if (seasonYear < thisYear - 5 || seasonYear > thisYear + 2)
            throw new InvalidOperationException($"Sezon yili {thisYear - 5}-{thisYear + 2} araliginda olmalidir.");
    }

    /// <summary>Kategorinin 2 harfli kodu; yoksa adindan turetip kaydeder.</summary>
    public async Task<string> EnsureCategoryCodeAsync(Category category, CancellationToken ct)
    {
        if (ProductCode.IsValidCategoryCode(category.Code))
            return category.Code!.ToUpperInvariant();

        var used = await _context.Categories.IgnoreQueryFilters()
            .Where(c => c.Code != null && c.Id != category.Id)
            .Select(c => c.Code!)
            .ToListAsync(ct);
        category.Code = CategoryCodeSuggester.Suggest(category.Name, used);
        await _context.SaveChangesAsync(ct);
        return category.Code;
    }

    /// <summary>Yeni urun kodu uretir (sayaci tuketir). Var olan kodlarla cakismaz.</summary>
    public async Task<string> GenerateProductCodeAsync(
        Category category, Season season, int seasonYear, Department department, CancellationToken ct)
    {
        var categoryCode = await EnsureCategoryCodeAsync(category, ct);
        var age = ProductCode.AgeGroupOf(department);
        var scope = "PC:" + ProductCode.BucketKey(season, seasonYear, categoryCode, age);

        for (var attempt = 0; attempt < ProductCode.MaxModelsPerBucket; attempt++)
        {
            var seq = await _context.GetNextCodeSequenceValueAsync(scope, ct);
            var code = ProductCode.Build(season, seasonYear, categoryCode, age, seq);
            var exists = await _context.Products.IgnoreQueryFilters().AnyAsync(p => p.Code == code, ct);
            if (!exists) return code;
        }
        throw new InvalidOperationException("Urun kodu uretilemedi: kova dolu.");
    }

    /// <summary>Sayaci tuketmeden bir sonraki kodun onizlemesi (admin formu icin).</summary>
    public async Task<string> PreviewProductCodeAsync(
        Category category, Season season, int seasonYear, Department department, CancellationToken ct)
    {
        var categoryCode = ProductCode.IsValidCategoryCode(category.Code)
            ? category.Code!.ToUpperInvariant()
            : CategoryCodeSuggester.Suggest(category.Name,
                await _context.Categories.IgnoreQueryFilters().Where(c => c.Code != null).Select(c => c.Code!).ToListAsync(ct));
        var age = ProductCode.AgeGroupOf(department);
        var scope = "PC:" + ProductCode.BucketKey(season, seasonYear, categoryCode, age);
        var seq = await _context.PeekCodeSequenceValueAsync(scope, ct) + 1;
        while (seq <= ProductCode.MaxModelsPerBucket)
        {
            var code = ProductCode.Build(season, seasonYear, categoryCode, age, seq);
            if (!await _context.Products.IgnoreQueryFilters().AnyAsync(p => p.Code == code, ct))
                return code;
            seq++;
        }
        throw new InvalidOperationException("Bu sezon/kategori/yas kitlesi icin kod kapasitesi doldu.");
    }

    // ══════════════════════ BARKOD ══════════════════════

    public async Task<string> GenerateBarcodeAsync(int categoryId, CancellationToken ct)
    {
        var scope = Ean13.ScopeKey(categoryId);
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var seq = await _context.GetNextCodeSequenceValueAsync(scope, ct);
            var barcode = Ean13.Build(categoryId, seq);
            if (_reservedBarcodes.Contains(barcode)) continue;
            var exists = await _context.ProductVariants.IgnoreQueryFilters().AnyAsync(v => v.Barcode == barcode, ct);
            if (exists) continue;   // elle girilmis eski barkodla cakisma
            _reservedBarcodes.Add(barcode);
            return barcode;
        }
        throw new InvalidOperationException("Barkod uretilemedi.");
    }

    /// <summary>Elle girilen/okutulan barkodu dogrular ve ayirir.</summary>
    public async Task<string> ReserveBarcodeAsync(string barcode, CancellationToken ct)
    {
        var b = barcode.Trim();
        if (b.Length is < 6 or > 20 || !b.All(char.IsLetterOrDigit))
            throw new InvalidOperationException($"'{barcode}' gecerli bir barkod degil (6-20 harf/rakam).");
        if (b.Length == 13 && b.All(char.IsDigit) && !Ean13.IsValid(b))
            throw new InvalidOperationException($"'{b}' EAN-13 kontrol hanesi hatali.");
        if (_reservedBarcodes.Contains(b)
            || await _context.ProductVariants.IgnoreQueryFilters().AnyAsync(v => v.Barcode == b, ct))
            throw new InvalidOperationException($"'{b}' barkodu zaten kullaniliyor.");
        _reservedBarcodes.Add(b);
        return b;
    }

    // ══════════════════════ VARYANT ══════════════════════

    public static string? NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return null;
        var t = color.Trim();
        // "lacivert" -> "Lacivert" (Turkce buyuk harf)
        return char.ToUpper(t[0], new System.Globalization.CultureInfo("tr-TR")) + t[1..];
    }

    public static string? NormalizeSize(string? size)
        => string.IsNullOrWhiteSpace(size) ? null : size.Trim().ToUpperInvariant();

    /// <summary>
    /// Varyant olusturur: tekrar kontrolu, barkod (uret/dogrula), baslangic
    /// fiyati, renk grubu. Kaydetmez - cagiran SaveChanges yapar.
    /// </summary>
    public async Task<ProductVariant> CreateVariantAsync(
        Product product, string? barcode, string? color, string? size,
        string? imagePath, decimal initialPrice, CancellationToken ct)
    {
        if (initialPrice <= 0)
            throw new InvalidOperationException("Varyant fiyati 0'dan buyuk olmalidir.");

        color = NormalizeColor(color);
        size = NormalizeSize(size);

        var duplicate = product.Id != 0 && await _context.ProductVariants.AnyAsync(v =>
            v.ProductId == product.Id && v.Color == color && v.Size == size, ct);
        if (duplicate || product.Variants.Any(v => !v.IsDeleted && v.Color == color && v.Size == size))
            throw new InvalidOperationException(
                $"Bu urunde '{color ?? "-"} / {size ?? "-"}' varyanti zaten var.");

        var finalBarcode = string.IsNullOrWhiteSpace(barcode)
            ? await GenerateBarcodeAsync(product.CategoryId, ct)
            : await ReserveBarcodeAsync(barcode, ct);

        var variant = new ProductVariant
        {
            Product = product,
            Barcode = finalBarcode,
            Color = color,
            Size = size,
            ImagePath = imagePath,
            IsActive = true,
            PriceHistories =
            {
                new PriceHistory { Price = initialPrice, StartDate = DateTime.UtcNow }
            }
        };

        if (color is not null)
            variant.Group = await EnsureColorGroupAsync(product, color, ct);

        product.Variants.Add(variant);
        return variant;
    }

    /// <summary>Urunun bu renk icin grubu (yoksa olusturur).</summary>
    public async Task<ProductVariantGroup> EnsureColorGroupAsync(Product product, string color, CancellationToken ct)
    {
        var key = (product, color.ToUpperInvariant());
        if (_groupCache.TryGetValue(key, out var cached)) return cached;

        ProductVariantGroup? group = null;
        if (product.Id != 0)
            group = await _context.ProductVariantGroups.FirstOrDefaultAsync(g =>
                g.ProductId == product.Id && g.GroupType == "Color" && g.GroupName == color, ct);
        if (group is null)
        {
            group = new ProductVariantGroup { Product = product, GroupName = color, GroupType = "Color" };
            _context.ProductVariantGroups.Add(group);
        }
        _groupCache[key] = group;
        return group;
    }
}

/// <summary>Kategori adindan 2 harfli benzersiz kod onerir (Turkce karakterler sadelestirilir).</summary>
public static class CategoryCodeSuggester
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kadin Giyim"] = "KD", ["Erkek Giyim"] = "ER", ["Cocuk Giyim"] = "CC", ["Aksesuar"] = "AK",
        ["Ic Giyim"] = "IC", ["Spor Giyim"] = "SP", ["Ayakkabi"] = "AY", ["Canta & Valiz"] = "CN",
        ["Pantolon"] = "PN", ["Gomlek"] = "GM", ["Jean"] = "JN", ["Ceket"] = "CK", ["Tisort"] = "TS",
        ["Elbise"] = "EL", ["Etek"] = "ET", ["Mont"] = "MT", ["Kazak"] = "KZ", ["Bluz"] = "BL"
    };

    public static string Fold(string s)
    {
        var map = new Dictionary<char, char>
        {
            ['ç'] = 'c', ['Ç'] = 'C', ['ğ'] = 'g', ['Ğ'] = 'G', ['ı'] = 'i', ['İ'] = 'I',
            ['ö'] = 'o', ['Ö'] = 'O', ['ş'] = 's', ['Ş'] = 'S', ['ü'] = 'u', ['Ü'] = 'U'
        };
        return new string(s.Select(c => map.TryGetValue(c, out var r) ? r : c).ToArray());
    }

    public static string Suggest(string name, IEnumerable<string> usedCodes)
    {
        var used = new HashSet<string>(usedCodes.Select(c => c.ToUpperInvariant()));
        var folded = Fold(name ?? string.Empty).Trim();
        if (Known.TryGetValue(folded, out var known) && !used.Contains(known)) return known;

        var letters = new string(folded.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z').ToArray());
        if (letters.Length == 0) letters = "XX";
        // Ilk harf + sonraki harflerden ilk bos olani
        for (var i = 1; i < letters.Length; i++)
        {
            var c = $"{letters[0]}{letters[i]}";
            if (!used.Contains(c)) return c;
        }
        for (var a = 'A'; a <= 'Z'; a++)
            for (var b = 'A'; b <= 'Z'; b++)
            {
                var c = $"{a}{b}";
                if (!used.Contains(c)) return c;
            }
        throw new InvalidOperationException("Kategori kodu kalmadi.");
    }
}
