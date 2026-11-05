using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Common;

/// <summary>
/// Kasaya para giren/çıkan her işlem (satış, iade, değişim) için kasiyerin o
/// mağazada AÇIK kasa oturumu (avans) olmalıdır. Eskiden kontrol yoktu: kasa
/// açılmadan satış/iade yapılabiliyor, oturumsuz nakit hiçbir kasa sayımına
/// girmiyordu (kasa farkı ölçülemiyordu).
/// </summary>
public static class CashSessionGuard
{
    public const string NoSessionMessage =
        "Kasa açılmadan satış/iade yapılamaz. Ana menüden 'Avans' ile kasayı açın.";

    public static async Task EnsureOpenAsync(
        IApplicationDbContext context, int storeId, string cashierId, CancellationToken ct)
    {
        var open = await context.CashDrawerSessions.AnyAsync(
            s => s.StoreId == storeId && s.CashierId == cashierId && s.Status == CashSessionStatus.Open, ct);

        if (!open)
            throw new CashSessionRequiredException(NoSessionMessage);
    }
}

/// <summary>Açık kasa oturumu gerektiren işlem oturumsuz denendiğinde.</summary>
public class CashSessionRequiredException : InvalidOperationException
{
    public CashSessionRequiredException(string message) : base(message) { }
}
