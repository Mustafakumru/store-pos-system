using Application.Common.Interfaces;
using Application.Features.Products.Common;
using Domain.Common;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.ProductCodes;

// ── Admin formu: secilen sezon/yil/kategori/bolum icin olusacak kodun onizlemesi ──

public record PreviewProductCodeQuery(Season Season, int SeasonYear, int CategoryId, Department Department)
    : IRequest<ProductCodePreviewDto>;

public record ProductCodePreviewDto(
    string Code,
    string CategoryCode,
    string SeasonLabel,
    string DepartmentLabel,
    string AgeGroupLabel,
    string Explanation);

public class PreviewProductCodeQueryHandler : IRequestHandler<PreviewProductCodeQuery, ProductCodePreviewDto>
{
    private readonly IApplicationDbContext _context;
    public PreviewProductCodeQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ProductCodePreviewDto> Handle(PreviewProductCodeQuery request, CancellationToken ct)
    {
        ProductCatalog.ValidateClassification(request.Season, request.SeasonYear, request.Department);
        var category = await _context.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct)
            ?? throw new KeyNotFoundException("Kategori bulunamadi.");

        var code = await new ProductCatalog(_context).PreviewProductCodeAsync(
            category, request.Season, request.SeasonYear, request.Department, ct);
        var age = ProductCode.AgeGroupOf(request.Department);
        return new ProductCodePreviewDto(
            code,
            code.Substring(2, 2),
            $"{ProductCode.SeasonLabel(request.Season)} {request.SeasonYear}",
            ProductCode.DepartmentLabel(request.Department),
            ProductCode.AgeGroupLabel(age),
            $"{code[0]} = {ProductCode.SeasonLabel(request.Season)} sezonu, {code[1]} = {request.SeasonYear}, " +
            $"{code.Substring(2, 2)} = {category.Name}, {code.Substring(4, 2)} = model no, " +
            $"{code.Substring(6, 2)} = {ProductCode.AgeGroupLabel(age)}");
    }
}

// ── Kodu coz (admin aramasi, POS yardimcisi) ──

public record DecodeProductCodeQuery(string Code) : IRequest<DecodedProductCodeDto>;

public record DecodedProductCodeDto(
    string Code,
    bool IsValid,
    string? SeasonLabel,
    int? SeasonYear,
    string? CategoryCode,
    string? CategoryName,
    string? AgeGroupLabel,
    int? ProductId,
    string? ProductName,
    string? Error);

public class DecodeProductCodeQueryHandler : IRequestHandler<DecodeProductCodeQuery, DecodedProductCodeDto>
{
    private readonly IApplicationDbContext _context;
    public DecodeProductCodeQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<DecodedProductCodeDto> Handle(DecodeProductCodeQuery request, CancellationToken ct)
    {
        var info = ProductCode.TryParse(request.Code);
        if (info is null)
            return new DecodedProductCodeDto(request.Code ?? "", false, null, null, null, null, null, null, null,
                "Kod 8 karakter olmali: [W/S][yil][2 harf kategori][2 model][Z1/Z4/Z8]");

        var year = ProductCode.ResolveYear(info.YearDigit, DateTime.UtcNow.Year);
        var category = await _context.Categories.AsNoTracking()
            .Where(c => c.Code == info.CategoryCode).Select(c => c.Name).FirstOrDefaultAsync(ct);
        var product = await _context.Products.AsNoTracking()
            .Where(p => p.Code == info.Code).Select(p => new { p.Id, p.Name }).FirstOrDefaultAsync(ct);

        return new DecodedProductCodeDto(info.Code, true,
            $"{ProductCode.SeasonLabel(info.Season)} {year}", year,
            info.CategoryCode, category, ProductCode.AgeGroupLabel(info.AgeGroup),
            product?.Id, product?.Name,
            category is null ? $"'{info.CategoryCode}' kodlu kategori tanimli degil." : null);
    }
}
