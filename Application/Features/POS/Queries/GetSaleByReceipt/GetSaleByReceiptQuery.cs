using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetSaleByReceipt;

/// <summary>
/// Fiş sorgusu. Mağazalar arası iade desteklendiği için başka mağazanın fişi
/// de okunabilir (kalemler / ödemeler iade için gerekir) ANCAK o durumda
/// müşteri kişisel verileri (ad, telefon) ve fişi kesen kasiyerin kimliği
/// gizlenir. Eskiden herhangi bir mağazanın kasiyeri, fiş numarasını bilerek
/// başka mağazanın müşterisinin adını ve telefonunu görebiliyordu.
/// </summary>
/// <param name="FullAccess">Admin: maskeleme yapılmaz.</param>
public record GetSaleByReceiptQuery(string ReceiptNumber, bool FullAccess = false)
    : IRequest<SaleReceiptDto>;

public record SaleReceiptDto(
    int Id,
    string ReceiptNumber,
    int StoreId,
    string StoreName,
    string CashierId,
    DateTime SaleDate,
    string PaymentType,
    string Status,
    decimal TotalAmount,
    decimal DiscountAmount,
    decimal FinalAmount,
    bool HasNegativeStockItem,
    List<SaleItemDto> Items,
    List<SalePaymentDto> Payments,  // ✅ Yeni
    // Satisin musterisi (varsa): fisle iade/degisimde yeni satis da ayni
    // musteriye yazilsin diye istemciye tasinir.
    int? CustomerId = null,
    string? CustomerName = null,
    string? CustomerPhone = null
);

public record SaleItemDto(
    int VariantId,
    string Barcode,
    string ProductName,
    string? Color,
    string? Size,
    decimal PriceAtSale,
    int Quantity,
    decimal LineTotal,
    int StockAfterSale,
    string? DiscountType,      // ✅ Yeni — "Amount" / "Percentage" / null
    decimal DiscountAmount,    // ✅ Yeni — hesaplanan indirim tutarı
    int RefundedQuantity = 0,          // daha önce iade edilen adet
    decimal RefundableUnitPrice = 0    // belge indirimi düşülmüş birim iade tutarı
);

public record SalePaymentDto(    // ✅ Yeni
    string PaymentType,
    decimal Amount,
    int? InstallmentCount
);

public class GetSaleByReceiptQueryHandler
    : IRequestHandler<GetSaleByReceiptQuery, SaleReceiptDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetSaleByReceiptQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<SaleReceiptDto> Handle(
        GetSaleByReceiptQuery request,
        CancellationToken cancellationToken)
    {
        var sale = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Store)
            .Include(s => s.Items)
            .Include(s => s.Payments)   // ✅ Yeni
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(
                s => s.ReceiptNumber == request.ReceiptNumber || s.OfflineReference == request.ReceiptNumber,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"'{request.ReceiptNumber}' numaralı fiş bulunamadı.");

        var otherStore = !request.FullAccess
                         && (!int.TryParse(_currentUser.StoreId, out var myStoreId) || myStoreId != sale.StoreId);

        var dto = new SaleReceiptDto(
            Id: sale.Id,
            ReceiptNumber: sale.ReceiptNumber,
            StoreId: sale.StoreId,
            StoreName: sale.Store.Name,
            CashierId: sale.CashierId,
            SaleDate: sale.CreatedAt,
            PaymentType: sale.PaymentType.ToString(),
            Status: sale.Status.ToString(),
            TotalAmount: sale.TotalAmount,
            DiscountAmount: sale.DiscountAmount,
            FinalAmount: sale.FinalAmount,
            HasNegativeStockItem: sale.HasNegativeStockItem,
            Items: sale.Items.Select(i => new SaleItemDto(
                i.VariantId,
                i.BarcodeSnapshot,
                i.ProductNameSnapshot,
                i.ColorSnapshot,
                i.SizeSnapshot,
                i.PriceAtSale,
                i.Quantity,
                i.LineTotal,
                i.StockQuantityAfterSale,
                i.DiscountType?.ToString(),   // ✅
                i.DiscountAmount,             // ✅
                i.RefundedQuantity,
                Application.Features.POS.Common.RefundPricing.UnitRefundable(sale, i)
            )).ToList(),
            Payments: sale.Payments.Select(p => new SalePaymentDto(   // ✅
                p.PaymentType.ToString(),
                p.Amount,
                p.InstallmentCount
            )).ToList(),
            CustomerId: sale.CustomerId,
            CustomerName: sale.Customer?.FullName,
            CustomerPhone: sale.Customer?.PhoneNumber
        );

        if (!otherStore) return dto;

        // Başka mağazanın fişi: iade için gereken bilgiler kalır, kişisel veri gizlenir.
        // CustomerId kalır: değişimdeki yeni satış aynı müşteriye yazılabilsin.
        return dto with
        {
            CashierId = string.Empty,
            CustomerName = dto.CustomerName is null ? null : MaskName(dto.CustomerName),
            CustomerPhone = dto.CustomerPhone is null ? null : MaskPhone(dto.CustomerPhone)
        };
    }

    private static string MaskName(string fullName) => string.Join(' ',
        fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Length <= 1 ? p : p[0] + new string('*', p.Length - 1)));

    private static string MaskPhone(string phone) =>
        phone.Length <= 4 ? new string('*', phone.Length) : new string('*', phone.Length - 4) + phone[^4..];
}