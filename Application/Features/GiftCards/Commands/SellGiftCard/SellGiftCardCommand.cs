using Application.Common.Interfaces;
using Application.Features.POS.Commands.ProcessSale;
using Application.Features.POS.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.GiftCards.Commands.SellGiftCard;

/// <summary>
/// Hediye kartı SATIŞI: müşteri ödeme yapar, karta bakiye yüklenir.
/// Tek işlemde birden çok kart yüklenebilir (Çoklu H.Kart Yükleme).
///
/// Kurallar:
///  • Açık kasa oturumu şart (nakit kasa sayımına girer)
///  • Ödeme sadece Nakit / Kart (hediye kartıyla hediye kartı alınamaz)
///  • İndirim / kupon yok; kart başına 1 – MaxPerCard TL
///  • Kayıt EMANET'tir: satış/ciro toplamlarına girmez (bkz. GiftCardSale)
/// </summary>
public record SellGiftCardCommand(
    List<GiftCardSaleLineRequest> Lines,
    List<PaymentRequest> Payments
) : IRequest<SellGiftCardResult>, Application.Common.Behaviors.IConcurrencySensitiveCommand;

public record GiftCardSaleLineRequest(string CardNumber, decimal Amount);

public record SellGiftCardResult(
    int Id,
    string DocumentNumber,
    decimal TotalAmount,
    decimal ChangeAmount,
    DateTime SoldAt,
    List<SoldCardDto> Cards
);

public record SoldCardDto(string CardNumber, decimal LoadedAmount, decimal NewBalance, DateTime ExpiryDate);

public class SellGiftCardCommandHandler : IRequestHandler<SellGiftCardCommand, SellGiftCardResult>
{
    public const decimal MaxPerCard = 10_000m;
    public const int MaxCardsPerSale = 20;

    // Mevcut yükleme kuralıyla aynı: "01.01.2100" = pratikte süresiz
    private static readonly DateTime DefaultExpiryDate = new(2100, 1, 1);

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SellGiftCardCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<SellGiftCardResult> Handle(SellGiftCardCommand request, CancellationToken ct)
    {
        if (!int.TryParse(_currentUser.StoreId, out var storeId))
            throw new UnauthorizedAccessException("Bu işlem için mağaza yetkisi gereklidir.");

        await CashSessionGuard.EnsureOpenAsync(_context, storeId, _currentUser.UserId, ct);

        // ── Kalemler ─────────────────────────────────────────
        var lines = (request.Lines ?? new())
            .Select(l => l with { CardNumber = (l.CardNumber ?? string.Empty).Trim() })
            .ToList();

        if (lines.Count == 0)
            throw new InvalidOperationException("En az bir hediye kartı girilmelidir.");
        if (lines.Count > MaxCardsPerSale)
            throw new InvalidOperationException($"Tek işlemde en fazla {MaxCardsPerSale} kart yüklenebilir.");
        if (lines.Any(l => l.CardNumber.Length is < 4 or > 50))
            throw new InvalidOperationException("Geçersiz hediye kartı numarası.");
        if (lines.GroupBy(l => l.CardNumber).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Aynı kart işlemde birden fazla kez yer alamaz.");
        if (lines.Any(l => l.Amount < 1 || l.Amount > MaxPerCard || decimal.Round(l.Amount, 2) != l.Amount))
            throw new InvalidOperationException($"Kart başına yükleme 1 – {MaxPerCard:N0} TL arasında olmalıdır.");

        var total = lines.Sum(l => l.Amount);

        // ── Ödemeler: sadece Nakit / Kart ────────────────────
        var payments = request.Payments ?? new();
        SaleRules.ValidatePaymentLines(payments);
        if (payments.Any(p => p.PaymentType == PaymentTypeDto.GiftCard))
            throw new InvalidOperationException("Hediye kartı, hediye kartıyla satın alınamaz.");
        if (payments.Sum(p => p.Amount) < total - 0.01m)
            throw new InvalidOperationException(
                $"Ödemeler toplamı ({payments.Sum(p => p.Amount):F2}) kart tutarından ({total:F2}) az olamaz.");

        var applied = PaymentSettlement.ApplyChange(payments, total, out var change);

        // ── Kartlar: yoksa oluştur, varsa aktif/süresi geçerli olmalı ──
        var numbers = lines.Select(l => l.CardNumber).ToList();
        var cards = await _context.GiftCards
            .Where(g => numbers.Contains(g.CardNumber))
            .ToDictionaryAsync(g => g.CardNumber, ct);

        var now = DateTime.UtcNow;
        var sale = new GiftCardSale
        {
            StoreId = storeId,
            CashierId = _currentUser.UserId,
            TotalAmount = total,
            Payments = applied.Select(p => new GiftCardSalePayment
            {
                PaymentType = (PaymentType)p.PaymentType,
                Amount = p.Amount,
                InstallmentCount = p.InstallmentCount
            }).ToList()
        };

        foreach (var line in lines)
        {
            if (!cards.TryGetValue(line.CardNumber, out var card))
            {
                card = new GiftCard
                {
                    CardNumber = line.CardNumber,
                    Balance = 0,
                    ExpiryDate = DefaultExpiryDate,
                    IsActive = true
                };
                _context.GiftCards.Add(card);
                cards[line.CardNumber] = card;
            }
            else if (!card.IsActive)
                throw new InvalidOperationException($"'{card.CardNumber}' numaralı hediye kartı pasif durumda.");
            else if (card.ExpiryDate < now)
                throw new InvalidOperationException($"'{card.CardNumber}' numaralı hediye kartının süresi dolmuş.");

            card.Balance += line.Amount;
            _context.GiftCardTransactions.Add(new GiftCardTransaction
            {
                GiftCard = card,
                Type = GiftCardTransactionType.Load,
                Amount = line.Amount,
                BalanceAfter = card.Balance
            });

            sale.Lines.Add(new GiftCardSaleLine
            {
                GiftCard = card,
                CardNumber = card.CardNumber,
                Amount = line.Amount
            });
        }

        _context.GiftCardSales.Add(sale);
        await _context.SaveChangesAsync(ct);

        // Belge no: mağaza + kayıt no (fiş sırasından ayrı - mali fiş numaralarında boşluk oluşmasın)
        sale.DocumentNumber = $"HK{storeId:000}-{sale.Id:D6}";
        await _context.SaveChangesAsync(ct);

        return new SellGiftCardResult(
            sale.Id, sale.DocumentNumber, total, change, sale.CreatedAt == default ? now : sale.CreatedAt,
            sale.Lines.Select(l => new SoldCardDto(
                l.CardNumber, l.Amount, l.GiftCard.Balance, l.GiftCard.ExpiryDate)).ToList());
    }
}
