namespace LCWaikiki.POS.WpfClient.Core.Models.Pos;

/// <summary>
/// Backend Application.Features.POS.Queries.GetProductForPos.ProductForPosDto
/// ile birebir eşleşir.
/// </summary>
public record ProductForPosDto(
    int ProductId,
    int VariantId,
    string Barcode,
    string Name,
    string? Color,
    string? Size,
    string? ImagePath,
    string CategoryName,
    decimal CurrentPrice,
    int StockQuantity,
    bool IsLowStock,
    bool IsOutOfStock,
    bool IsNegativeStock,
    string? ProductCode = null   // urun (model) kodu: W6ER01Z8
);
// ── Urun kodu ile arama (barkodsuz urun bulma) ─────────────────────
// GET api/pos/products/by-code/{code}: urun -> renkler -> bedenler (fiyat + stok)

public record ProductByCodeDto(
    int ProductId,
    string Code,
    string Name,
    string CategoryName,
    string? ImagePath,
    string? SeasonLabel,
    string? DepartmentLabel,
    string? AgeGroupLabel,
    decimal MinPrice,
    decimal MaxPrice,
    int TotalStock,
    List<ProductColorOptionDto> Colors);

public record ProductColorOptionDto(
    string Color,
    string? ImagePath,
    int TotalStock,
    List<ProductSizeOptionDto> Sizes);

public record ProductSizeOptionDto(
    int VariantId,
    string? Size,
    string Barcode,
    decimal Price,
    int StockQuantity,
    bool IsSellable);

/// <summary>GET api/pos/products/search?q= sonucu.</summary>
public record ProductSearchItemDto(
    int ProductId,
    string Code,
    string Name,
    string CategoryName,
    string? ImagePath,
    int VariantCount);
