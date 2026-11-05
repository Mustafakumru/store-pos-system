using Application.Common.Interfaces;
using Application.Features.POS.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Commands.ProcessSale;

public class ProcessSaleCommandHandler
    : IRequestHandler<ProcessSaleCommand, ProcessSaleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IApprovalTokenService _approvalTokenService;

    public ProcessSaleCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IApprovalTokenService approvalTokenService)
    {
        _context = context;
        _currentUser = currentUser;
        _approvalTokenService = approvalTokenService;
    }

    public async Task<ProcessSaleResult> Handle(
        ProcessSaleCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException(
                "Bu işlem için mağaza yetkisi gereklidir.");

        // ── Ödeme kalemlerinin ön kontrolü ───────────────────────
        if (request.Payments is null || request.Payments.Count == 0)
            throw new InvalidOperationException("En az bir ödeme kalemi girilmelidir.");

        // Sadece Nakit/Kart/Hediye kartı (Mixed ve ExchangeCredit sunucu tipleridir)
        SaleRules.ValidatePaymentLines(request.Payments);

        if (request.Items is null || request.Items.Count == 0)
            throw new InvalidOperationException("Sepette ürün yok.");
        if (request.Items.Any(i => i.Quantity <= 0))
            throw new InvalidOperationException("Satış miktarı 0'dan büyük olmalıdır.");
        SaleRules.ValidateQuantities(request.Items.Select(i => i.Quantity).ToList(), "satış");

        var storeId = int.Parse(_currentUser.StoreId);
        await CashSessionGuard.EnsureOpenAsync(_context, storeId, _currentUser.UserId, cancellationToken);
        await SaleRules.EnsureCustomerExistsAsync(_context, request.CustomerId, cancellationToken);
        var variantIds = request.Items.Select(i => i.VariantId).Distinct().ToList();
        var now = DateTime.UtcNow;

        if (request.DiscountAmount < 0)
            throw new InvalidOperationException("İndirim tutarı negatif olamaz.");

        var surchargeRate = InstallmentPricing.GetSurchargeRate(request.InstallmentPlan);

        // ── İndirim onayı: satır indirimi VEYA ara toplam indirimi ──
        var approverId = SaleRules.RequireDiscountApproval(
            _approvalTokenService, request.ApprovalToken,
            hasLineDiscount: request.Items.Any(i => i.DiscountValue is > 0),
            hasDocumentDiscount: request.DiscountAmount > 0);

        // ── Varyantları çek ──────────────────────────────────
        var variants = await _context.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.PriceHistories)
            .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        if (variants.Count != variantIds.Count)
            throw new InvalidOperationException(
                "Sepette silinmiş veya bulunamayan ürün var.");

        // ── Mağaza stoklarını çek ────────────────────────────
        var inventories = await _context.StoreInventories
            .Where(si => si.StoreId == storeId
                      && variantIds.Contains(si.VariantId))
            .ToDictionaryAsync(si => si.VariantId, cancellationToken);

        var saleItems = new List<SaleItem>();
        var negativeWarnings = new List<NegativeStockWarning>();
        decimal totalAmount = 0;

        foreach (var itemRequest in request.Items)
        {
            var variant = variants.First(v => v.Id == itemRequest.VariantId);

            var price = variant.PriceHistories
                .Where(ph => ph.StartDate <= now
                          && (ph.EndDate == null || ph.EndDate > now))
                .OrderByDescending(ph => ph.StartDate)   // çakışan kayıtta en güncel fiyat
                .Select(ph => ph.Price)
                .FirstOrDefault();

            if (price == 0)
                throw new InvalidOperationException(
                    $"'{variant.Product.Name}' için aktif fiyat tanımlanmamış.");

            if (!inventories.TryGetValue(variant.Id, out var inventory))
            {
                inventory = new StoreInventory
                {
                    StoreId = storeId,
                    VariantId = variant.Id,
                    Quantity = 0
                };
                _context.StoreInventories.Add(inventory);
                inventories[variant.Id] = inventory;
            }

            inventory.Quantity -= itemRequest.Quantity;

            if (inventory.Quantity < 0)
                negativeWarnings.Add(new NegativeStockWarning(
                    variant.Product.Name,
                    variant.Barcode,
                    variant.Color,
                    variant.Size,
                    inventory.Quantity));

            var originalLineTotal = price * itemRequest.Quantity;

            decimal lineDiscountAmount = 0;

            if (itemRequest.DiscountValue.HasValue
                && itemRequest.DiscountValue.Value > 0)
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
            totalAmount += lineTotal;

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

        if (request.DiscountAmount > totalAmount)
            throw new InvalidOperationException(
                $"İndirim ({request.DiscountAmount:F2}) satış tutarını ({totalAmount:F2}) aşamaz.");

        var amountAfterDocumentDiscount = totalAmount - request.DiscountAmount;

        // ── Kupon: indirim HER ZAMAN burada hesaplanır ─────────
        Coupon? coupon = null;
        decimal couponDiscount = 0;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var code = Coupon.NormalizeCode(request.CouponCode);
            coupon = await _context.Coupons
                .FirstOrDefaultAsync(c => c.Code == code, cancellationToken)
                ?? throw new InvalidOperationException($"'{code}' kodlu kupon bulunamadı.");

            var invalidReason = coupon.GetInvalidReason(now, amountAfterDocumentDiscount);
            if (invalidReason is not null)
                throw new InvalidOperationException(invalidReason);

            couponDiscount = coupon.CalculateDiscount(amountAfterDocumentDiscount);
            coupon.UsedCount++;
        }

        var finalAmount = amountAfterDocumentDiscount - couponDiscount;

        // ── Ödeme toplamı doğrulaması ────────────────────────
        // ESKI KURAL: paymentsTotal tam finalAmount'a esit olmali idi
        // (Math.Abs(paymentsTotal - finalAmount) > 0.01m) - asiri katiydi,
        // musteri fazla nakit verirse ("para ustu" senaryosu) hata firlatiyordu.
        // YENI KURAL: paymentsTotal SADECE eksik OLAMAZ - fazlasi normal POS
        // davranisidir (para ustu musteriye elden verilir, client tarafinda
        // zaten hesaplanip Para Ustu ekraninda gosteriliyor - backend'in
        // ayrica bir "change" alani dondurmesine gerek yok).
        var paymentsTotal = request.Payments.Sum(p => p.Amount);

        if (paymentsTotal < finalAmount - 0.01m)
            throw new InvalidOperationException(
                $"Ödemeler toplamı ({paymentsTotal:F2}) satış tutarından " +
                $"({finalAmount:F2}) az olamaz.");

        // Kaydedilecek ödemeler = uygulanan tutarlar (para üstü nakitten düşülür)
        var appliedPayments = PaymentSettlement.ApplyChange(request.Payments, finalAmount, out _);

        // ── Sale.PaymentType belirle (tek tipse o, değilse Mixed) ─
        var distinctTypes = appliedPayments
            .Select(p => p.PaymentType)
            .Distinct()
            .ToList();

        var resolvedPaymentType = distinctTypes.Count == 1
            ? (PaymentType)distinctTypes[0]
            : PaymentType.Mixed;

        var sale = new Sale
        {
            // Fiş numarası kayıttan hemen önce, aynı transaction'da atanır (aşağıda)
            StoreId = storeId,
            CustomerId = request.CustomerId,
            CashierId = _currentUser.UserId,
            TotalAmount = totalAmount,
            DiscountAmount = request.DiscountAmount + couponDiscount,
            CouponId = coupon?.Id,
            CouponDiscountAmount = couponDiscount,
            FinalAmount = finalAmount,
            PaymentType = resolvedPaymentType,
            Status = SaleStatus.Completed,
            HasNegativeStockItem = negativeWarnings.Count > 0,
            Items = saleItems,
            Payments = appliedPayments.Select(p => new SalePayment
            {
                PaymentType = (PaymentType)p.PaymentType,
                Amount = p.Amount,
                InstallmentCount = p.InstallmentCount
            }).ToList()
        };

        _context.Sales.Add(sale);

        // ── Hediye kartı ödemelerini işle ────────────────────
        var giftCardPayments = request.Payments
            .Where(p => p.PaymentType == PaymentTypeDto.GiftCard)
            .ToList();

        foreach (var payment in giftCardPayments)
        {
            var card = await _context.GiftCards
                .FirstOrDefaultAsync(
                    g => g.CardNumber == payment.CardNumber, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"'{payment.CardNumber}' numaralı hediye kartı bulunamadı.");

            if (!card.IsActive)
                throw new InvalidOperationException(
                    $"'{payment.CardNumber}' numaralı hediye kartı pasif durumda.");

            if (card.ExpiryDate < now)
                throw new InvalidOperationException(
                    $"'{payment.CardNumber}' numaralı hediye kartının süresi dolmuş.");

            if (card.Balance < payment.Amount)
                throw new InvalidOperationException(
                    $"'{payment.CardNumber}' numaralı hediye kartında yeterli bakiye yok " +
                    $"(Bakiye: {card.Balance:F2}, İstenen: {payment.Amount:F2}).");

            card.Balance -= payment.Amount;

            _context.GiftCardTransactions.Add(new GiftCardTransaction
            {
                GiftCard = card,
                Sale = sale,
                Type = GiftCardTransactionType.Redeem,
                Amount = payment.Amount,
                BalanceAfter = card.Balance
            });
        }

        // ── Fiş numarası + kayıt TEK transaction'da ──────────
        // Eskiden numara en başta (doğrulamalardan önce, transaction dışında)
        // alınıyordu: eksik ödeme, geçersiz kupon, hediye kartı bakiyesi gibi
        // her başarısız satış bir numara "yakıyordu" (0044 → 0047). Artık
        // numara sadece tüm doğrulamalar geçtikten sonra alınır ve kayıt
        // başarısız olursa sayaç artışı da geri alınır.
        await using (var transaction = await _context.BeginTransactionAsync(cancellationToken))
        {
            sale.ReceiptNumber = await GenerateReceiptNumberAsync(storeId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        // Onay token'i tek kullanimlik: satis kaydedildikten sonra tuketilir
        // (kayit basarisiz olursa kasiyer ayni onayla tekrar deneyebilir).
        if (approverId is not null)
            _approvalTokenService.MarkTokenUsed(request.ApprovalToken!);

        return new ProcessSaleResult(
            Success: true,
            ReceiptNumber: sale.ReceiptNumber,
            FinalAmount: finalAmount,
            SaleDate: DateTime.UtcNow,
            NegativeStockWarnings: negativeWarnings);
    }

    private async Task<string> GenerateReceiptNumberAsync(
        int storeId, CancellationToken cancellationToken)
    {
        // Yerel iş günü (UTC günü değil): 00:00–03:00 arası fişler doğru güne yazılsın
        var today = Application.Common.BusinessClock.Today;

        var sequenceValue = await _context.GetNextReceiptSequenceValueAsync(
            storeId, today, cancellationToken);

        // Tamamen sayisal fis numarasi - barkod olarak basilip okutulabilmesi
        // icin harf/tire icermez. Format: {MagazaId:000}{yyyyMMdd}{Sira:0000}
        // orn. StoreId=1, 07.05.2025, 42. fis -> "001202505070042" (15 hane)
        return $"{storeId:000}{today:yyyyMMdd}{sequenceValue:D4}";
    }
}