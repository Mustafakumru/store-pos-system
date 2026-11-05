using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Common;

/// <summary>İade edilecek tek kalem (fişli veya fişsiz).</summary>
public record ReturnLineInput(
    string? ReceiptNumber,        // null = fişsiz/kayıtsız iade
    int VariantId,
    int Quantity,
    int? ReturnReasonId = null,
    decimal? ManualUnitPrice = null   // sadece fişsiz iadede zorunlu
);

/// <summary>Müşteriye ödeme yöntemi (istemcinin seçebildikleri).</summary>
public enum RefundMethodDto { Cash = 0, Card = 1, GiftCard = 2 }

/// <summary>
/// İade ve değişimin ORTAK iade tarafı: kalemleri doğrular, stoğu iadeyi yapan
/// mağazaya geri yükler, satış kalemlerinin iade sayaçlarını günceller ve iade
/// defteri kaydını (SaleReturn + satırlar) hazırlar. SaveChanges çağırmaz -
/// çağıran handler tek transaction'da kaydeder.
/// </summary>
public class ReturnProcessor
{
    private readonly IApplicationDbContext _context;

    public ReturnProcessor(IApplicationDbContext context) => _context = context;

    /// <summary>Nakit iadede ödeme yöntemi sınırını aşmak için yönetici onay işlemi.</summary>
    public const string RefundCashOverrideAction = "refund-cash";

    /// <param name="CardRefundCap">Karta iade edilebilecek en fazla tutar (fişlerde kartla ödenen − daha önce karta iade edilen).</param>
    /// <param name="CashRefundCap">Nakit iade edilebilecek en fazla tutar (fişlerde nakit ödenen − daha önce nakit iade edilen + onaylı fişsiz iade).</param>
    public record Result(SaleReturn Return, decimal TrackedTotal, decimal CardRefundCap, decimal CashRefundCap = 0);

    public async Task<Result> ProcessAsync(
        int storeId, string cashierId, IReadOnlyCollection<ReturnLineInput> inputs,
        CancellationToken ct)
    {
        if (inputs.Any(i => i.Quantity <= 0))
            throw new InvalidOperationException("İade miktarı 0'dan büyük olmalıdır.");
        if (inputs.Any(i => i.ReturnReasonId is null))
            throw new InvalidOperationException("Her iade kalemi için iade nedeni seçilmelidir.");

        await ValidateReasonsAsync(inputs, ct);

        var saleReturn = new SaleReturn { StoreId = storeId, CashierId = cashierId };
        decimal trackedTotal = 0, cardCap = 0, cashCap = 0;

        // ── 1) Fişli iadeler ─────────────────────────────────────
        foreach (var group in inputs.Where(i => i.ReceiptNumber != null).GroupBy(i => i.ReceiptNumber!))
        {
            var sale = await _context.Sales
                .Include(s => s.Items)
                .Include(s => s.Payments)
                .FirstOrDefaultAsync(s => s.ReceiptNumber == group.Key, ct)
                ?? throw new KeyNotFoundException($"'{group.Key}' numaralı fiş bulunamadı.");

            if (sale.Status == SaleStatus.Refunded)
                throw new InvalidOperationException($"'{group.Key}' numaralı fiş zaten tam iade edilmiş.");

            decimal saleReturnTotal = 0;

            foreach (var input in group)
            {
                // Aynı üründen fişte birden fazla satır olabilir: iade, iade
                // edilebilir adedi olan satırlara sırayla dağıtılır.
                var lines = sale.Items
                    .Where(i => i.VariantId == input.VariantId)
                    .OrderBy(i => i.Id)
                    .ToList();
                if (lines.Count == 0)
                    throw new InvalidOperationException(
                        $"'{group.Key}' fişi içinde bu ürün (VariantId={input.VariantId}) bulunamadı.");

                var available = lines.Sum(i => i.Quantity - i.RefundedQuantity);
                if (input.Quantity > available)
                    throw new InvalidOperationException(
                        $"'{lines[0].ProductNameSnapshot}' için iade miktarı kalan miktarı ({available}) aşamaz.");

                var toAllocate = input.Quantity;
                foreach (var item in lines)
                {
                    if (toAllocate == 0) break;
                    var qty = Math.Min(toAllocate, item.Quantity - item.RefundedQuantity);
                    if (qty <= 0) continue;

                    var amount = RefundPricing.AmountFor(sale, item, qty);

                    item.RefundedQuantity += qty;
                    item.RefundedAmount += amount;
                    if (input.ReturnReasonId.HasValue)
                        item.ReturnReasonId = input.ReturnReasonId;

                    saleReturn.Lines.Add(new SaleReturnLine
                    {
                        SaleItem = item,
                        OriginalSaleId = sale.Id,
                        OriginalReceiptNumber = sale.ReceiptNumber,
                        VariantId = item.VariantId,
                        BarcodeSnapshot = item.BarcodeSnapshot,
                        ProductNameSnapshot = item.ProductNameSnapshot,
                        Quantity = qty,
                        Amount = amount,
                        ReturnReasonId = input.ReturnReasonId
                    });

                    saleReturnTotal += amount;
                    toAllocate -= qty;
                }

                await RestockAsync(storeId, input.VariantId, input.Quantity, ct);
            }

            sale.Status = sale.Items.All(i => i.RefundedQuantity >= i.Quantity)
                ? SaleStatus.Refunded
                : SaleStatus.Partial;

            trackedTotal += saleReturnTotal;

            // Satır sürümünü (rowversion) değiştirir: aynı fişe eşzamanlı iki
            // iade, ödeme sınırlarını aynı eski veriyle hesaplayamaz - ikincisi
            // çakışır ve ConcurrencyRetryBehavior güncel veriyle yeniden dener.
            sale.RefundedAmount += saleReturnTotal;

            // ── İade, ödendiği yönteme: fiş bazında kalan nakit / kart payı ──
            // Eskiden karta iade sınırı daha önce bu fişten karta yapılmış
            // iadeleri düşmüyordu (100 TL kartla ödenen fişe 200 TL karta iade
            // yapılabiliyordu); nakit iadede ise hiç sınır yoktu (kartla veya
            // hediye kartıyla alınan ürün nakde çevrilebiliyordu).
            var (priorCash, priorCard) = await PriorPayoutsAsync(sale.Id, ct);

            var cardPaid = sale.Payments.Where(p => p.PaymentType == PaymentType.Card).Sum(p => p.Amount);
            var cashPaid = sale.Payments.Where(p => p.PaymentType == PaymentType.Cash).Sum(p => p.Amount);

            cardCap += Math.Min(Math.Max(0, cardPaid - priorCard), saleReturnTotal);
            cashCap += Math.Min(Math.Max(0, cashPaid - priorCash), saleReturnTotal);
        }

        // ── 2) Fişsiz (kayıtsız) iadeler ─────────────────────────
        var untracked = inputs.Where(i => i.ReceiptNumber == null).ToList();
        if (untracked.Count > 0)
        {
            var ids = untracked.Select(i => i.VariantId).Distinct().ToList();
            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.PriceHistories)
                .Where(v => ids.Contains(v.Id) && !v.IsDeleted)
                .ToDictionaryAsync(v => v.Id, ct);
            var nowUtc = DateTime.UtcNow;

            var missing = ids.Except(variants.Keys).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"Kayıtsız iadede geçersiz ürünler: {string.Join(", ", missing)}");

            foreach (var input in untracked)
            {
                if (!input.ManualUnitPrice.HasValue || input.ManualUnitPrice.Value <= 0)
                    throw new InvalidOperationException("Kayıtsız iadede birim fiyat zorunludur.");

                var v = variants[input.VariantId];

                // Fişsiz iadede fiyat güncel satış fiyatını aşamaz (kasiyer fiyatı
                // düşürebilir - müşteri indirimli aldığını söyleyebilir - ama
                // ürünün değerinden fazla para iade edilemez).
                var currentPrice = v.PriceHistories
                    .Where(ph => ph.StartDate <= nowUtc && (ph.EndDate == null || ph.EndDate > nowUtc))
                    .OrderByDescending(ph => ph.StartDate)
                    .Select(ph => ph.Price)
                    .FirstOrDefault();
                if (currentPrice > 0 && input.ManualUnitPrice.Value > currentPrice)
                    throw new InvalidOperationException(
                        $"'{v.Product.Name}' için fişsiz iade fiyatı güncel satış fiyatını ({currentPrice:F2} TL) aşamaz.");

                saleReturn.Lines.Add(new SaleReturnLine
                {
                    VariantId = v.Id,
                    BarcodeSnapshot = v.Barcode,
                    ProductNameSnapshot = v.Product.Name,
                    Quantity = input.Quantity,
                    Amount = Math.Round(input.ManualUnitPrice.Value * input.Quantity, 2),
                    ReturnReasonId = input.ReturnReasonId
                });

                await RestockAsync(storeId, input.VariantId, input.Quantity, ct);
            }
        }

        saleReturn.TotalAmount = saleReturn.Lines.Sum(l => l.Amount);

        // Fişsiz iade (yönetici onaylı - bkz. ProcessExchange) nakit ödenebilir
        cashCap += saleReturn.TotalAmount - trackedTotal;

        return new Result(saleReturn, trackedTotal, cardCap, cashCap);
    }

    /// <summary>
    /// Bu fişin kalemlerini içeren ÖNCEKİ iadelerde nakit ve karta ödenen tutar.
    /// Bir iade birden çok fişe dağılabildiği için iade ödemesinin tamamı bu
    /// fişe sayılır (ihtiyatlı: sınırı olduğundan büyük değil, küçük hesaplar).
    /// </summary>
    private async Task<(decimal Cash, decimal Card)> PriorPayoutsAsync(int saleId, CancellationToken ct)
    {
        var returnIds = _context.SaleReturnLines
            .Where(l => l.OriginalSaleId == saleId)
            .Select(l => l.SaleReturnId)
            .Distinct();

        var totals = await _context.SaleReturnPayouts
            .Where(p => returnIds.Contains(p.SaleReturnId)
                     && (p.Method == RefundMethod.Cash || p.Method == RefundMethod.Card))
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(ct);

        return (totals.Where(t => t.Method == RefundMethod.Cash).Sum(t => t.Amount),
                totals.Where(t => t.Method == RefundMethod.Card).Sum(t => t.Amount));
    }

    /// <summary>
    /// İade tutarını müşteriye ödeme satırlarına dağıtır - "iade, ödendiği yönteme":
    ///  • Hediye kartı seçildiyse tamamı karta yüklenir (para mağazada kalır).
    ///  • Nakit / Kart seçildiyse önce seçilen yönteme o yöntemin KALAN payı
    ///    kadar, artanı diğerine (nakit ↔ kart) yazılır. Karışık ödenmiş
    ///    (nakit + kart) fiş böylece tek seçimle iade edilebilir; kasiyer
    ///    sonuç mesajındaki dağılıma göre nakit verir / POS'tan kart iadesi yapar.
    ///  • Hâlâ artan varsa (fiş hediye kartı / değişim kredisiyle ödenmiş)
    ///    nakde çevrilemez: hediye kartına iade seçilmeli ya da mağaza müdürü
    ///    "refund-cash" onayı vermelidir (<paramref name="cashOverrideApproved"/>).
    /// </summary>
    public async Task AddPayoutAsync(
        SaleReturn saleReturn, RefundMethod method, decimal amount,
        string? giftCardNumber, Result caps, bool cashOverrideApproved, CancellationToken ct)
    {
        amount = Math.Round(amount, 2);
        if (amount <= 0) return;

        if (method is RefundMethod.Cash or RefundMethod.Card)
        {
            var remainingCaps = new Dictionary<RefundMethod, decimal>
            {
                [RefundMethod.Cash] = cashOverrideApproved ? decimal.MaxValue : caps.CashRefundCap,
                [RefundMethod.Card] = caps.CardRefundCap
            };
            var order = method == RefundMethod.Card
                ? new[] { RefundMethod.Card, RefundMethod.Cash }
                : new[] { RefundMethod.Cash, RefundMethod.Card };

            var remaining = amount;
            foreach (var m in order)
            {
                var part = Math.Round(Math.Min(remaining, Math.Max(0, remainingCaps[m])), 2);
                if (part <= 0) continue;
                saleReturn.Payouts.Add(new SaleReturnPayout { Method = m, Amount = part });
                remaining -= part;
            }

            if (remaining > 0.009m)
                throw new InvalidOperationException(
                    $"{remaining:F2} TL nakit veya karta iade edilemez: bu tutar fişte hediye kartı / " +
                    "değişim kredisi ile ödenmiş. 'Hediye Kartına' iade seçin ya da nakit iade için " +
                    "mağaza müdürü onayı alın.");
            return;
        }

        switch (method)
        {
            case RefundMethod.GiftCard:
                if (string.IsNullOrWhiteSpace(giftCardNumber))
                    throw new InvalidOperationException("Hediye kartına iade için kart numarası gereklidir.");

                var card = await _context.GiftCards
                    .FirstOrDefaultAsync(g => g.CardNumber == giftCardNumber.Trim(), ct)
                    ?? throw new InvalidOperationException($"'{giftCardNumber}' numaralı hediye kartı bulunamadı.");
                if (!card.IsActive)
                    throw new InvalidOperationException($"'{giftCardNumber}' numaralı hediye kartı pasif durumda.");

                card.Balance += amount;
                _context.GiftCardTransactions.Add(new GiftCardTransaction
                {
                    GiftCard = card,
                    Type = GiftCardTransactionType.Refund,
                    Amount = amount,
                    BalanceAfter = card.Balance
                });
                giftCardNumber = card.CardNumber;
                break;
        }

        saleReturn.Payouts.Add(new SaleReturnPayout
        {
            Method = method,
            Amount = amount,
            GiftCardNumber = method == RefundMethod.GiftCard ? giftCardNumber : null
        });
    }

    /// <summary>Kasiyere gösterilecek ödeme dağılımı, örn. "Nakit 100,00 TL + Kart 150,00 TL".</summary>
    public static string DescribePayouts(SaleReturn saleReturn)
    {
        var parts = saleReturn.Payouts
            .Where(p => p.Method != RefundMethod.ExchangeCredit)
            .GroupBy(p => p.Method)
            .Select(g => $"{Label(g.Key)} {g.Sum(p => p.Amount):N2} TL");
        return string.Join(" + ", parts);

        static string Label(RefundMethod m) => m switch
        {
            RefundMethod.Card => "Kart",
            RefundMethod.GiftCard => "Hediye kartı",
            _ => "Nakit"
        };
    }

    public static RefundMethod ToDomain(RefundMethodDto? dto) => dto switch
    {
        RefundMethodDto.Card => RefundMethod.Card,
        RefundMethodDto.GiftCard => RefundMethod.GiftCard,
        _ => RefundMethod.Cash
    };

    private async Task RestockAsync(int storeId, int variantId, int quantity, CancellationToken ct)
    {
        // Aynı işlemde aynı ürün birden çok kez iade edilebilir: önce
        // change tracker'a bak (henüz kaydedilmemiş yeni satır olabilir)
        var inventory = _context.StoreInventories.Local
                            .FirstOrDefault(si => si.StoreId == storeId && si.VariantId == variantId)
                        ?? await _context.StoreInventories
                            .FirstOrDefaultAsync(si => si.StoreId == storeId && si.VariantId == variantId, ct);

        if (inventory != null)
            inventory.Quantity += quantity;
        else
            _context.StoreInventories.Add(new StoreInventory
            {
                StoreId = storeId,
                VariantId = variantId,
                Quantity = quantity
            });
    }

    private async Task ValidateReasonsAsync(IEnumerable<ReturnLineInput> inputs, CancellationToken ct)
    {
        var ids = inputs.Where(i => i.ReturnReasonId.HasValue)
                        .Select(i => i.ReturnReasonId!.Value).Distinct().ToList();
        if (ids.Count == 0) return;

        var valid = await _context.ReturnReasons
            .Where(r => ids.Contains(r.Id) && r.IsActive)
            .Select(r => r.Id)
            .ToListAsync(ct);
        var invalid = ids.Except(valid).ToList();
        if (invalid.Count > 0)
            throw new InvalidOperationException($"Geçersiz iade nedeni Id'leri: {string.Join(", ", invalid)}");
    }
}
