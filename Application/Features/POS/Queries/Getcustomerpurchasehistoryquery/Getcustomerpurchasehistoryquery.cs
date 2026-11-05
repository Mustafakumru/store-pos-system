using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetCustomerPurchaseHistory;

/// <summary>
/// Musterinin GECMISTE, BELIRLI BIR URUNU alip almadigini kontrol eder.
/// Degisim akisinda kasiyer bir urunu okuttugunda, bu musterinin o urunu
/// hangi fisle/ne zaman/ne kadara aldigini doner.
///
/// BILEREK KISITLI: Barcode zorunludur. Musterinin TUM alisveris gecmisini
/// tek seferde cekebilen bir sorgu YOK - kasiyerin musteri profili gezmesini
/// (operasyonel/mahremiyet riski) engellemek icin. Her sorgu tek bir urunle
/// sinirlidir.
/// </summary>
public record GetCustomerPurchaseHistoryQuery(
    int CustomerId,
    string Barcode
) : IRequest<List<CustomerPurchaseDto>>;

public record CustomerPurchaseDto(
    string ReceiptNumber,
    DateTime SaleDate,
    int StoreId,
    string StoreName,
    int VariantId,
    string Barcode,
    string ProductName,
    string? Color,
    string? Size,
    int Quantity,
    decimal PriceAtSale,
    decimal LineTotal,
    int RefundedQuantity,
    decimal RefundableUnitPrice = 0   // belge indirimi düşülmüş birim iade tutarı
);

public class GetCustomerPurchaseHistoryQueryHandler
    : IRequestHandler<GetCustomerPurchaseHistoryQuery, List<CustomerPurchaseDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCustomerPurchaseHistoryQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<CustomerPurchaseDto>> Handle(
        GetCustomerPurchaseHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Barcode))
            throw new InvalidOperationException(
                "Barkod zorunludur. Musteri gecmisi barkodsuz sorgulanamaz.");

        // Tamamen iade edilmis (Refunded) satislarin degisecek bir seyi
        // kalmadigi icin cikariyoruz; Partial (kismi iade) satislar hala
        // gosterilir - RefundedQuantity ile kalan miktar hesaplanabilir.
        var sales = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Store)
            .Include(s => s.Items)
            .Where(s => s.CustomerId == request.CustomerId
                     && s.Status != Domain.Enums.SaleStatus.Refunded
                     && s.Items.Any(i => i.BarcodeSnapshot == request.Barcode))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = sales
            .SelectMany(s => s.Items
                .Where(i => i.BarcodeSnapshot == request.Barcode)
                .Select(i => new CustomerPurchaseDto(
                    s.ReceiptNumber,
                    s.CreatedAt,
                    s.StoreId,
                    s.Store.Name,
                    i.VariantId,
                    i.BarcodeSnapshot,
                    i.ProductNameSnapshot,
                    i.ColorSnapshot,
                    i.SizeSnapshot,
                    i.Quantity,
                    i.PriceAtSale,
                    i.LineTotal,
                    i.RefundedQuantity,
                    Application.Features.POS.Common.RefundPricing.UnitRefundable(s, i)
                )))
            .OrderByDescending(p => p.SaleDate)
            .ToList();

        return result;
    }
}