using Application.Common.Interfaces;
using Application.Features.POS.Common;
using Application.Features.POS.Commands.ProcessSale;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Commands.ProcessExchange;

/// <summary>
/// Değişim (iade + yeni satış) TEK atomik transaction'da işlenir.
///
/// MUHASEBE (raporların doğru çıkması için):
///  • İade tarafı iade defterine (SaleReturn) yazılır - iadeyi YAPAN mağaza,
///    gerçek iade anı ve kalemleri ile (ReturnProcessor, düz iadeyle ortak).
///  • Yeni satış TAM tutarıyla kaydedilir (FinalAmount = yeni ürünler − belge
///    indirimi). Kullanılan iade kredisi satışın bir ÖDEME kalemi olur
///    (PaymentType.ExchangeCredit) ve defterde "ExchangeCredit" ile kapanır.
///    Eskiden kredi satış tutarından düşülüp AYRICA iade sayılıyordu: 100 TL
///    iade + 150 TL alım raporda −50 TL görünüyordu (doğrusu +50 TL).
///  • Müşteriye kalan fark (iade > yeni satış) aynı işlemde seçilen yöntemle
///    ödenir (OwedRefundMethod: Nakit/Kart/Hediye kartı) - kasa sayımı bunu görür.
///
/// İADE KAYNAKLARI: fişli / müşteri geçmişinden (ReceiptNumber dolu, fiyat
/// satıştan) ve fişsiz (ReceiptNumber null, ManualUnitPrice zorunlu).
/// </summary>
public record ProcessExchangeCommand(
    List<ExchangeReturnItemRequest> ReturnItems,
    List<SaleItemRequest> NewItems,
    List<PaymentRequest> SettlementPayments,
    int? CustomerId = null,
    decimal DiscountAmount = 0,       // belge seviyesi indirim - yeni satışa uygulanır (onay gerekir)
    string? ApprovalToken = null,     // satır veya belge indirimi varsa zorunlu ("discount")
    int? InstallmentPlan = null,      // 4 / 8 = vade farkı yeni satış satırlarına eklenir
    RefundMethodDto? OwedRefundMethod = null,   // müşteriye kalan fark: varsayılan Nakit
    string? OwedGiftCardNumber = null,
    string? ReturnApprovalToken = null,         // fişsiz iade varsa zorunlu ("return-no-receipt")
    string? RefundApprovalToken = null          // fişin nakit payını aşan nakit iade için ("refund-cash")
) : IRequest<ProcessExchangeResult>, Application.Common.Behaviors.IConcurrencySensitiveCommand;

public record ExchangeReturnItemRequest(
    string? ReceiptNumber,      // null = kayıtsız/izsiz iade
    int VariantId,
    int RefundQuantity,
    int? ReturnReasonId = null,
    decimal? ManualUnitPrice = null  // sadece ReceiptNumber null iken kullanılır/zorunlu
);

public record ProcessExchangeResult(
    bool Success,
    string? NewReceiptNumber,
    decimal ReturnTotal,
    decimal NewTotal,
    decimal NetAmount,
    decimal AmountCollectedFromCustomer,
    decimal AmountOwedToCustomer,
    string Message,
    decimal ExchangeCreditApplied = 0,
    decimal NewSaleFinalAmount = 0,
    string? OwedRefundMethod = null
);

public class ProcessExchangeCommandHandler
    : IRequestHandler<ProcessExchangeCommand, ProcessExchangeResult>
{
    /// <summary>Fişsiz iade için yönetici onay işlemi adı.</summary>
    public const string ReturnNoReceiptAction = "return-no-receipt";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IApprovalTokenService _approvalTokenService;

    public ProcessExchangeCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IApprovalTokenService approvalTokenService)
    {
        _context = context;
        _currentUser = currentUser;
        _approvalTokenService = approvalTokenService;
    }

    public async Task<ProcessExchangeResult> Handle(
        ProcessExchangeCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException("Bu işlem için mağaza yetkisi gereklidir.");

        var storeId = int.Parse(_currentUser.StoreId);
        await CashSessionGuard.EnsureOpenAsync(_context, storeId, _currentUser.UserId, cancellationToken);
        await SaleRules.EnsureCustomerExistsAsync(_context, request.CustomerId, cancellationToken);
        var returnItems = request.ReturnItems ?? new();
        var newItems = request.NewItems ?? new();

        if (returnItems.Count == 0 && newItems.Count == 0)
            throw new InvalidOperationException("En az bir iade veya yeni ürün kalemi girilmelidir.");
        if (newItems.Count > 0)
            SaleRules.ValidateQuantities(newItems.Select(i => i.Quantity).ToList(), "satış");
        if (returnItems.Count > 0)
            SaleRules.ValidateQuantities(returnItems.Select(i => i.RefundQuantity).ToList(), "iade");

        // ── Ödeme kalemi ve indirim ön kontrolleri (ProcessSale ile aynı) ──
        var settlementPayments = request.SettlementPayments ?? new List<PaymentRequest>();
        SaleRules.ValidatePaymentLines(settlementPayments);

        if (request.DiscountAmount < 0)
            throw new InvalidOperationException("İndirim tutarı negatif olamaz.");
        if (request.DiscountAmount > 0 && newItems.Count == 0)
            throw new InvalidOperationException("Yeni ürün olmadan belge indirimi uygulanamaz.");

        var surchargeRate = InstallmentPricing.GetSurchargeRate(request.InstallmentPlan);

        var approverId = SaleRules.RequireDiscountApproval(
            _approvalTokenService, request.ApprovalToken,
            hasLineDiscount: newItems.Any(i => i.DiscountValue is > 0),
            hasDocumentDiscount: request.DiscountAmount > 0);

        // Fişsiz (kayıtsız) iade yönetici onayı ister: satış kaydı olmadan
        // kasadan para çıkışıdır (eskiden kasiyer tek başına yapabiliyordu).
        string? returnApproverId = null;
        if (returnItems.Any(r => r.ReceiptNumber == null))
        {
            if (string.IsNullOrEmpty(request.ReturnApprovalToken))
                throw new UnauthorizedAccessException("Fişsiz iade için yönetici onayı gereklidir.");
            returnApproverId = _approvalTokenService.ValidateApprovalToken(
                                   request.ReturnApprovalToken, ReturnNoReceiptAction)
                               ?? throw new UnauthorizedAccessException("Fişsiz iade onay kodu geçersiz veya süresi dolmuş.");
        }

        // ══════════════════════ 1) İADE TARAFI ══════════════════════
        var processor = new ReturnProcessor(_context);
        SaleReturn? saleReturn = null;
        ReturnProcessor.Result? returnResult = null;
        decimal returnTotal = 0;

        if (returnItems.Count > 0)
        {
            var result = await processor.ProcessAsync(
                storeId, _currentUser.UserId,
                returnItems.Select(r => new ReturnLineInput(
                    r.ReceiptNumber, r.VariantId, r.RefundQuantity, r.ReturnReasonId,
                    r.ReceiptNumber == null ? r.ManualUnitPrice : null)).ToList(),
                cancellationToken);

            returnResult = result;
            saleReturn = result.Return;
            if (returnApproverId is not null)
                saleReturn.Note = $"Fişsiz iade - onaylayan yönetici: {returnApproverId}";
            returnTotal = saleReturn.TotalAmount;
            _context.SaleReturns.Add(saleReturn);
        }

        // ══════════════════════ 2) YENİ SATIŞ TARAFI ══════════════════════
        string? newReceiptNumber = null;
        decimal newTotal = 0, documentDiscount = 0, newFinal = 0;
        decimal creditApplied = 0, amountCollected = 0;
        Sale? newSale = null;

        if (newItems.Count > 0)
        {
            var variantIds = newItems.Select(i => i.VariantId).ToList();
            var now = DateTime.UtcNow;

            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.PriceHistories)
                .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted)
                .ToListAsync(cancellationToken);

            if (variants.Count != variantIds.Distinct().Count())
                throw new InvalidOperationException("Yeni ürünler içinde silinmiş veya bulunamayan ürün var.");

            var inventories = await _context.StoreInventories
                .Where(si => si.StoreId == storeId && variantIds.Contains(si.VariantId))
                .ToDictionaryAsync(si => si.VariantId, cancellationToken);

            var saleItems = new List<SaleItem>();

            foreach (var itemRequest in newItems)
            {
                if (itemRequest.Quantity <= 0)
                    throw new InvalidOperationException("Satış miktarı 0'dan büyük olmalıdır.");

                var variant = variants.First(v => v.Id == itemRequest.VariantId);

                var price = variant.PriceHistories
                    .Where(ph => ph.StartDate <= now && (ph.EndDate == null || ph.EndDate > now))
                    .OrderByDescending(ph => ph.StartDate)
                    .Select(ph => ph.Price)
                    .FirstOrDefault();

                if (price == 0)
                    throw new InvalidOperationException($"'{variant.Product.Name}' için aktif fiyat tanımlanmamış.");

                // Bu işlemde iade edilen aynı ürün stoğa eklenmiş olabilir (Local)
                if (!inventories.TryGetValue(variant.Id, out var inventory))
                {
                    inventory = _context.StoreInventories.Local
                        .FirstOrDefault(si => si.StoreId == storeId && si.VariantId == variant.Id);
                    if (inventory is null)
                    {
                        inventory = new StoreInventory { StoreId = storeId, VariantId = variant.Id, Quantity = 0 };
                        _context.StoreInventories.Add(inventory);
                    }
                    inventories[variant.Id] = inventory;
                }

                inventory.Quantity -= itemRequest.Quantity;

                var originalLineTotal = price * itemRequest.Quantity;
                decimal lineDiscountAmount = 0;
                if (itemRequest.DiscountValue is > 0)
                {
                    lineDiscountAmount = itemRequest.DiscountType == DiscountType.Percentage
                        ? Math.Round(originalLineTotal * (itemRequest.DiscountValue.Value / 100m), 2)
                        : itemRequest.DiscountValue.Value;
                    if (lineDiscountAmount > originalLineTotal)
                        lineDiscountAmount = originalLineTotal;
                }

                var lineAfterDiscount = originalLineTotal - lineDiscountAmount;
                var lineTotal = lineAfterDiscount
                    + InstallmentPricing.CalculateSurcharge(lineAfterDiscount, surchargeRate);
                newTotal += lineTotal;

                saleItems.Add(new SaleItem
                {
                    VariantId = variant.Id,
                    BarcodeSnapshot = variant.Barcode,
                    ProductNameSnapshot = variant.Product.Name,
                    ColorSnapshot = variant.Color,
                    SizeSnapshot = variant.Size,
                    PriceAtSale = price,
                    Quantity = itemRequest.Quantity,
                    LineTotal = lineTotal,
                    StockQuantityAfterSale = inventory.Quantity,
                    DiscountType = itemRequest.DiscountType,
                    DiscountValue = itemRequest.DiscountValue,
                    DiscountAmount = lineDiscountAmount,
                    ApprovedByUserId = lineDiscountAmount > 0 ? approverId : null
                });
            }

            if (request.DiscountAmount > newTotal)
                throw new InvalidOperationException(
                    $"İndirim ({request.DiscountAmount:F2}) yeni satış tutarını ({newTotal:F2}) aşamaz.");

            documentDiscount = request.DiscountAmount;
            newFinal = newTotal - documentDiscount;

            // İade kredisi yeni satışın tutarını aşamaz; fazlası müşteriye ödenir
            creditApplied = Math.Min(returnTotal, newFinal);
            amountCollected = newFinal - creditApplied;

            // Müşteriden alınması gereken tutar eksik olamaz, fazlası para üstü
            var paymentsTotal = settlementPayments.Sum(p => p.Amount);
            if (paymentsTotal < amountCollected - 0.01m)
                throw new InvalidOperationException(
                    $"Ödemeler toplamı ({paymentsTotal:F2}) tahsil edilmesi gereken " +
                    $"tutardan ({amountCollected:F2}) az olamaz.");

            var appliedPayments = PaymentSettlement.ApplyChange(settlementPayments, amountCollected, out _);

            var payments = appliedPayments.Select(p => new SalePayment
            {
                PaymentType = (PaymentType)p.PaymentType,
                Amount = p.Amount,
                InstallmentCount = p.InstallmentCount
            }).ToList();

            if (creditApplied > 0)
                payments.Add(new SalePayment { PaymentType = PaymentType.ExchangeCredit, Amount = creditApplied });

            var types = payments.Select(p => p.PaymentType).Distinct().ToList();

            newSale = new Sale
            {
                // Fiş numarası kayıttan hemen önce, aynı transaction'da atanır
                StoreId = storeId,
                CustomerId = request.CustomerId,
                CashierId = _currentUser.UserId,
                TotalAmount = newTotal,
                DiscountAmount = documentDiscount,     // SADECE belge indirimi
                FinalAmount = newFinal,                // tam satış tutarı
                PaymentType = types.Count == 1 ? types[0] : PaymentType.Mixed,
                Status = SaleStatus.Completed,
                HasNegativeStockItem = saleItems.Any(i => i.StockQuantityAfterSale < 0),
                Items = saleItems,
                Payments = payments
            };
            _context.Sales.Add(newSale);

            // ── Hediye kartı ödemeleri (ProcessSale ile aynı) ──
            foreach (var payment in appliedPayments.Where(p => p.PaymentType == PaymentTypeDto.GiftCard))
                await SaleRules.RedeemGiftCardAsync(_context, payment, newSale, now, cancellationToken);
        }
        else if (settlementPayments.Count > 0)
        {
            throw new InvalidOperationException("Yeni ürün olmadan ödeme alınamaz.");
        }

        // ══════════════════════ 3) İADE ÖDEMESİ (defter) ══════════════════════
        var owed = Math.Max(0, returnTotal - creditApplied);
        var owedMethod = ReturnProcessor.ToDomain(request.OwedRefundMethod);

        // Opsiyonel: fişin nakit payını aşan nakit iade için müdür onayı
        string? cashOverrideApproverId = null;
        if (!string.IsNullOrEmpty(request.RefundApprovalToken))
            cashOverrideApproverId = _approvalTokenService.ValidateApprovalToken(
                    request.RefundApprovalToken, ReturnProcessor.RefundCashOverrideAction)
                ?? throw new UnauthorizedAccessException("Nakit iade onay kodu geçersiz veya süresi dolmuş.");

        if (saleReturn is not null)
        {
            saleReturn.ExchangeSale = newSale;
            if (creditApplied > 0)
                saleReturn.Payouts.Add(new SaleReturnPayout
                {
                    Method = RefundMethod.ExchangeCredit,
                    Amount = creditApplied
                });

            await processor.AddPayoutAsync(
                saleReturn, owedMethod, owed, request.OwedGiftCardNumber,
                returnResult!, cashOverrideApproverId is not null, cancellationToken);

            if (cashOverrideApproverId is not null)
                saleReturn.Note = $"{saleReturn.Note} [Nakit iade onayı: {cashOverrideApproverId}]".Trim();
        }

        // ══════ TEK transaction: fiş numarası + tüm kayıtlar birlikte ya da hiç ══════
        // Fiş numarası doğrulamalardan SONRA alınır; kayıt başarısız olursa
        // sayaç artışı da geri alınır (numara boşluğu oluşmaz).
        await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
        {
            if (newSale is not null)
            {
                newReceiptNumber = await GenerateReceiptNumberAsync(storeId, cancellationToken);
                newSale.ReceiptNumber = newReceiptNumber;
            }
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (cashOverrideApproverId is not null)
            _approvalTokenService.MarkTokenUsed(request.RefundApprovalToken!);

        if (approverId is not null)
            _approvalTokenService.MarkTokenUsed(request.ApprovalToken!);
        if (returnApproverId is not null)
            _approvalTokenService.MarkTokenUsed(request.ReturnApprovalToken!);

        return new ProcessExchangeResult(
            Success: true,
            NewReceiptNumber: newReceiptNumber,
            ReturnTotal: returnTotal,
            NewTotal: newTotal,
            NetAmount: newFinal - returnTotal,
            AmountCollectedFromCustomer: amountCollected,
            AmountOwedToCustomer: owed,
            Message: owed > 0
                ? $"İşlem tamamlandı. Müşteriye {owed:F2} TL iade edildi ({ReturnProcessor.DescribePayouts(saleReturn!)})."
                : "Değişim başarıyla tamamlandı.",
            ExchangeCreditApplied: creditApplied,
            NewSaleFinalAmount: newFinal,
            OwedRefundMethod: owed > 0 ? owedMethod.ToString() : null);
    }

    private async Task<string> GenerateReceiptNumberAsync(int storeId, CancellationToken cancellationToken)
    {
        var today = Application.Common.BusinessClock.Today;   // yerel iş günü
        var sequenceValue = await _context.GetNextReceiptSequenceValueAsync(storeId, today, cancellationToken);
        return $"{storeId:000}{today:yyyyMMdd}{sequenceValue:D4}";
    }
}
