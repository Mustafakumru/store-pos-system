using Application.Common.Interfaces;
using Application.Features.POS.Commands.ProcessSale;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Common;

/// <summary>Satış ve değişimin ortak doğrulama kuralları.</summary>
public static class SaleRules
{
    // ── Makullik sinirlari ──────────────────────────────────────────
    // Yanlis okutma / klavyede fazladan sifir (orn. 100000 adet, 34,9 milyon TL'lik
    // fis) gibi hatalari ve kotu niyetli istekleri sunucu tarafinda keser.
    // Gercek bir magaza fisinde bu sinirlara yaklasilmaz.
    public const int MaxQuantityPerLine = 999;
    public const int MaxLinesPerDocument = 300;
    public const decimal MaxPaymentAmount = 1_000_000m;

    /// <summary>Satir sayisi ve satir basina adet sinirlari (satis, degisim, iade).</summary>
    public static void ValidateQuantities(IReadOnlyCollection<int> quantities, string operation)
    {
        if (quantities.Count > MaxLinesPerDocument)
            throw new InvalidOperationException(
                $"Bir {operation} belgesinde en fazla {MaxLinesPerDocument} satir olabilir.");
        if (quantities.Any(q => q <= 0))
            throw new InvalidOperationException($"{char.ToUpper(operation[0])}{operation[1..]} miktari 0'dan buyuk olmalidir.");
        if (quantities.Any(q => q > MaxQuantityPerLine))
            throw new InvalidOperationException(
                $"Bir satirda en fazla {MaxQuantityPerLine} adet olabilir. Adedi kontrol edin.");
    }

    /// <summary>
    /// İstemci sadece Nakit / Kart / Hediye kartı gönderebilir. "Mixed" ve
    /// "ExchangeCredit" sunucunun ürettiği tiplerdir - istemciden gelirse
    /// (örn. sahte iade kredisiyle ödeme) reddedilir.
    /// </summary>
    public static void ValidatePaymentLines(IReadOnlyCollection<PaymentRequest> payments)
    {
        if (payments.Any(p => p.PaymentType is not (PaymentTypeDto.Cash or PaymentTypeDto.Card or PaymentTypeDto.GiftCard)))
            throw new InvalidOperationException(
                "Ödeme kalemleri sadece Nakit, Kart veya Hediye Kartı olabilir.");

        if (payments.Any(p => p.Amount <= 0))
            throw new InvalidOperationException("Ödeme tutarları sıfırdan büyük olmalıdır.");

        if (payments.Any(p => p.Amount > MaxPaymentAmount) || payments.Sum(p => p.Amount) > MaxPaymentAmount)
            throw new InvalidOperationException(
                $"Ödeme tutarı {MaxPaymentAmount:N0} TL'yi aşamaz. Tutarı kontrol edin.");

        if (payments.Count > 20)
            throw new InvalidOperationException("Bir belgede en fazla 20 ödeme kalemi olabilir.");

        if (payments.Any(p => p.PaymentType == PaymentTypeDto.GiftCard && string.IsNullOrWhiteSpace(p.CardNumber)))
            throw new InvalidOperationException("Hediye kartı ödemesi için kart numarası gereklidir.");
    }

    /// <summary>
    /// Satır indirimi VEYA belge (ara toplam) indirimi varsa yönetici onayı
    /// zorunludur. Eskiden sadece satır indirimi kontrol ediliyordu: kasiyer
    /// onaysız %100 ara toplam indirimiyle ürünü 0 TL'ye satabiliyordu.
    /// Kupon indirimi buraya dahil değildir (kupon kendi kurallarıyla doğrulanır).
    /// </summary>
    /// <returns>Onaylayan yöneticinin Id'si; indirim yoksa null.</returns>
    public static string? RequireDiscountApproval(
        IApprovalTokenService approvals, string? token,
        bool hasLineDiscount, bool hasDocumentDiscount)
    {
        if (!hasLineDiscount && !hasDocumentDiscount) return null;

        if (string.IsNullOrEmpty(token))
            throw new UnauthorizedAccessException("İndirim uygulamak için yönetici onayı gereklidir.");

        return approvals.ValidateApprovalToken(token, "discount")
               ?? throw new UnauthorizedAccessException("Onay kodu geçersiz veya süresi dolmuş.");
    }

    /// <summary>
    /// Satışa bağlanacak müşteri gerçekten var mı? Eskiden kontrol edilmiyordu:
    /// geçersiz CustomerId kayıt anında yabancı anahtar hatasıyla 500 dönüyordu.
    /// </summary>
    public static async Task EnsureCustomerExistsAsync(
        IApplicationDbContext context, int? customerId, CancellationToken ct)
    {
        if (customerId is null) return;
        if (!await context.Customers.AnyAsync(c => c.Id == customerId.Value, ct))
            throw new InvalidOperationException($"Müşteri bulunamadı (Id={customerId}). Müşteriyi yeniden seçin.");
    }

    public static async Task RedeemGiftCardAsync(
        IApplicationDbContext context, PaymentRequest payment, Sale sale,
        DateTime nowUtc, CancellationToken ct)
    {
        var card = await context.GiftCards
            .FirstOrDefaultAsync(g => g.CardNumber == payment.CardNumber, ct)
            ?? throw new InvalidOperationException($"'{payment.CardNumber}' numaralı hediye kartı bulunamadı.");

        if (!card.IsActive)
            throw new InvalidOperationException($"'{payment.CardNumber}' numaralı hediye kartı pasif durumda.");
        if (card.ExpiryDate < nowUtc)
            throw new InvalidOperationException($"'{payment.CardNumber}' numaralı hediye kartının süresi dolmuş.");
        if (card.Balance < payment.Amount)
            throw new InvalidOperationException(
                $"'{payment.CardNumber}' numaralı hediye kartında yeterli bakiye yok " +
                $"(Bakiye: {card.Balance:F2}, İstenen: {payment.Amount:F2}).");

        card.Balance -= payment.Amount;
        context.GiftCardTransactions.Add(new GiftCardTransaction
        {
            GiftCard = card,
            Sale = sale,
            Type = GiftCardTransactionType.Redeem,
            Amount = payment.Amount,
            BalanceAfter = card.Balance
        });
    }
}
