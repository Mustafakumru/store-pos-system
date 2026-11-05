using Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviors;

/// <summary>
/// Para/stok degistiren komutlarin isaretleyicisi. Bu komutlar eszamanli
/// cakismada (ayni hediye karti, stok satiri, satis satiri iki kasada ayni
/// anda degisti) bastan, guncel veriyle yeniden calistirilir.
/// </summary>
public interface IConcurrencySensitiveCommand { }

/// <summary>Tum denemeler cakismaya takildi: kasiyer tekrar denemeli.</summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("Islem baska bir kasadaki islemle ayni anda gerceklesti. Lutfen tekrar deneyin.") { }
}

/// <summary>
/// Iyimser eszamanlilik (rowversion) cakismasinda komutu yeniden calistirir.
/// Ornek: ayni 100 TL'lik hediye karti iki kasada ayni anda harcanirsa ikinci
/// islem ilk denemede cakisir; yeniden denemede guncel bakiyeyi (0) gorur ve
/// "yetersiz bakiye" ile reddedilir. Stok gibi engel olmayan durumlarda ise
/// yeniden deneme dogru degerle basarili olur.
/// </summary>
public class ConcurrencyRetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Yogun anda (ayni urun bircok kasada ayni anda) her turda yalniz bir islem
    // kazanir: 4 deneme 15 eszamanli satista yetmiyordu (test: HardeningTests).
    private const int MaxAttempts = 8;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ConcurrencyRetryBehavior<TRequest, TResponse>> _logger;

    public ConcurrencyRetryBehavior(IApplicationDbContext context,
        ILogger<ConcurrencyRetryBehavior<TRequest, TResponse>> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IConcurrencySensitiveCommand)
            return await next();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await next();
            }
            catch (Exception ex) when (_context.IsConcurrencyConflict(ex))
            {
                _context.ResetTracking();
                if (attempt >= MaxAttempts)
                {
                    _logger.LogWarning(ex, "{Command}: {Attempts} denemede de eszamanlilik cakismasi", typeof(TRequest).Name, attempt);
                    throw new ConcurrencyConflictException();
                }
                _logger.LogInformation("{Command}: eszamanlilik cakismasi, yeniden deneniyor ({Attempt})", typeof(TRequest).Name, attempt);
                // Ayni anda yeniden deneyen istekler tekrar carpismasin
                // (artan, rastgele bekleme; en kotu durumda toplam ~2 sn)
                await Task.Delay(Random.Shared.Next(20, 80) * Math.Min(attempt, 5), cancellationToken);
            }
        }
    }
}
