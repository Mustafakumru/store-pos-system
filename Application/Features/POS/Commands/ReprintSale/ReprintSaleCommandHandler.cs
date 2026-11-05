using Application.Common.Interfaces;
using Application.Features.POS.Queries.GetSaleByReceipt;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Commands.ReprintSale;

public class ReprintSaleCommandHandler
    : IRequestHandler<ReprintSaleCommand, SaleReceiptDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IApprovalTokenService _approvalTokenService;

    public ReprintSaleCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IApprovalTokenService approvalTokenService)
    {
        _context = context;
        _currentUser = currentUser;
        _approvalTokenService = approvalTokenService;
    }

    public async Task<SaleReceiptDto> Handle(
        ReprintSaleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException(
                "Bu işlem için mağaza yetkisi gereklidir.");

        // ── Yönetici onayı doğrula ─────────────────────────────
        var approverId = _approvalTokenService.ValidateApprovalToken(
            request.ApprovalToken, "reprint");

        if (approverId is null)
            throw new UnauthorizedAccessException(
                "Onay kodu geçersiz veya süresi dolmuş.");

        var storeId = int.Parse(_currentUser.StoreId);

        var sale = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Store)
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(
                s => s.Id == request.SaleId && s.StoreId == storeId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Fiş bulunamadı.");

        // Onay token'i tek kullanimlik - fis bulunduktan sonra tuketilir.
        _approvalTokenService.MarkTokenUsed(request.ApprovalToken);

        return new SaleReceiptDto(
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
                i.DiscountType?.ToString(),
                i.DiscountAmount
            )).ToList(),
            Payments: sale.Payments.Select(p => new SalePaymentDto(
                p.PaymentType.ToString(),
                p.Amount,
                p.InstallmentCount
            )).ToList()
        );
    }
}