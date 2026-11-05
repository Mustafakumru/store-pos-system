using Application.Features.POS.Commands.ProcessSale;

namespace Application.Features.POS.Common;

/// <summary>
/// Ödeme kalemlerini kaydedilecek (UYGULANAN) tutarlara indirger.
///
/// Kasiyer müşterinin VERDİĞİ tutarı girer (örn. 80 TL'lik satışa 100 TL nakit).
/// Eskiden SalePayment'a 100 yazılıyordu: X/Z raporlarında nakit toplamı ve
/// kasa oturumundaki "beklenen nakit" para üstü kadar şişiyordu.
/// Burada fazlalık (para üstü) NAKİT kalemlerinden düşülür; kayıtlı ödemelerin
/// toplamı satış tutarına eşit olur. Para üstü sadece nakitten verilebilir -
/// kart/hediye kartı ile fazla ödeme reddedilir.
/// </summary>
public static class PaymentSettlement
{
    public static List<PaymentRequest> ApplyChange(
        IReadOnlyList<PaymentRequest> payments, decimal amountDue, out decimal change)
    {
        var total = payments.Sum(p => p.Amount);
        change = Math.Max(0, Math.Round(total - amountDue, 2));

        var result = payments.ToList();
        if (change < 0.01m)
        {
            change = 0;
            return result;
        }

        var cashTotal = payments.Where(p => p.PaymentType == PaymentTypeDto.Cash).Sum(p => p.Amount);
        if (cashTotal < change - 0.009m)
            throw new InvalidOperationException(
                $"Fazla ödeme ({change:F2}) sadece nakitten para üstü olarak verilebilir; " +
                "kart veya hediye kartı ile kalan tutardan fazlası alınamaz.");

        // Para üstünü sondaki nakit kalemlerinden başlayarak düş
        var remaining = change;
        for (var i = result.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var p = result[i];
            if (p.PaymentType != PaymentTypeDto.Cash) continue;

            var deduct = Math.Min(p.Amount, remaining);
            result[i] = p with { Amount = p.Amount - deduct };
            remaining -= deduct;
        }

        // Tamamen para üstüne giden nakit kalemleri kaydedilmez
        return result.Where(p => p.Amount > 0).ToList();
    }
}
