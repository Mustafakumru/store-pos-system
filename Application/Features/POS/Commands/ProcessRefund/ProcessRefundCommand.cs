using Application.Common.Interfaces;
using Application.Features.POS.Common;
using MediatR;

namespace Application.Features.POS.Commands.ProcessRefund;

/// <param name="PayoutMethod">Müşteriye ödeme: Nakit (varsayılan) / Kart / Hediye kartı.</param>
/// <param name="GiftCardNumber">PayoutMethod = GiftCard ise yüklenecek kart.</param>
public record ProcessRefundCommand(
    string ReceiptNumber,
    List<RefundItem> Items,
    string? Reason,
    RefundMethodDto? PayoutMethod = null,
    string? GiftCardNumber = null,
    string? RefundApprovalToken = null   // nakit iade, fişin nakit payını aşıyorsa ("refund-cash")
) : IRequest<ProcessRefundResult>, Application.Common.Behaviors.IConcurrencySensitiveCommand;

public record RefundItem(
    int VariantId,
    int RefundQuantity,
    int? ReturnReasonId = null
);

public record ProcessRefundResult(
    bool Success,
    decimal RefundAmount,
    int RefundedItemCount,
    string Message,
    int? SaleReturnId = null
);

/// <summary>
/// Fişli iade. İade tarafı ReturnProcessor'da (değişimle ortak): stok iadeyi
/// yapan mağazaya döner, tutar belge indirimi dağıtılarak hesaplanır ve iade
/// defterine (SaleReturn) ödeme yöntemiyle birlikte yazılır.
/// </summary>
public class ProcessRefundCommandHandler
    : IRequestHandler<ProcessRefundCommand, ProcessRefundResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IApprovalTokenService _approvalTokenService;

    public ProcessRefundCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IApprovalTokenService approvalTokenService)
    {
        _context = context;
        _currentUser = currentUser;
        _approvalTokenService = approvalTokenService;
    }

    public async Task<ProcessRefundResult> Handle(
        ProcessRefundCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException("Bu işlem için mağaza yetkisi gereklidir.");
        if (request.Items is null || request.Items.Count == 0)
            throw new InvalidOperationException("İade edilecek ürün seçilmedi.");
        SaleRules.ValidateQuantities(request.Items.Select(i => i.RefundQuantity).ToList(), "iade");

        var storeId = int.Parse(_currentUser.StoreId);
        await CashSessionGuard.EnsureOpenAsync(_context, storeId, _currentUser.UserId, cancellationToken);
        var processor = new ReturnProcessor(_context);

        var result = await processor.ProcessAsync(
            storeId, _currentUser.UserId,
            request.Items.Select(i => new ReturnLineInput(
                request.ReceiptNumber, i.VariantId, i.RefundQuantity, i.ReturnReasonId)).ToList(),
            cancellationToken);

        var saleReturn = result.Return;
        var reason = request.Reason?.Trim();
        saleReturn.Note = string.IsNullOrEmpty(reason) ? null : reason[..Math.Min(500, reason.Length)];

        // Opsiyonel: fişin nakit payını aşan nakit iade için müdür onayı
        string? cashOverrideApproverId = null;
        if (!string.IsNullOrEmpty(request.RefundApprovalToken))
            cashOverrideApproverId = _approvalTokenService.ValidateApprovalToken(
                    request.RefundApprovalToken, ReturnProcessor.RefundCashOverrideAction)
                ?? throw new UnauthorizedAccessException("Nakit iade onay kodu geçersiz veya süresi dolmuş.");

        await processor.AddPayoutAsync(
            saleReturn, ReturnProcessor.ToDomain(request.PayoutMethod),
            saleReturn.TotalAmount, request.GiftCardNumber, result,
            cashOverrideApproved: cashOverrideApproverId is not null,
            cancellationToken);

        if (cashOverrideApproverId is not null)
            saleReturn.Note = $"{saleReturn.Note} [Nakit iade onayı: {cashOverrideApproverId}]".Trim();

        _context.SaleReturns.Add(saleReturn);
        await _context.SaveChangesAsync(cancellationToken);

        if (cashOverrideApproverId is not null)
            _approvalTokenService.MarkTokenUsed(request.RefundApprovalToken!);

        var fully = saleReturn.Lines
            .Where(l => l.SaleItem != null)
            .Select(l => l.SaleItem!.Sale)
            .Distinct()
            .All(s => s.Status == Domain.Enums.SaleStatus.Refunded);

        return new ProcessRefundResult(
            Success: true,
            RefundAmount: saleReturn.TotalAmount,
            RefundedItemCount: request.Items.Count,
            Message: (fully ? "Tam iade başarıyla tamamlandı." : "Kısmi iade başarıyla tamamlandı.")
                     + $" Ödeme: {ReturnProcessor.DescribePayouts(saleReturn)}.",
            SaleReturnId: saleReturn.Id);
    }
}
