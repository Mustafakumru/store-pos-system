using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetCurrentCashSession;

/// <summary>
/// Kasiyerin bu mağazadaki açık kasa oturumu (yoksa IsOpen=false).
/// POS ekranı satış/iadeye geçmeden önce kasanın açık olduğunu buradan anlar.
/// Beklenen kasa tutarı BİLEREK dönülmez: teslim kör sayım yapılır, fark
/// mağaza panelinde müdüre görünür.
/// </summary>
public record GetCurrentCashSessionQuery : IRequest<CurrentCashSessionDto>;

public record CurrentCashSessionDto(
    bool IsOpen,
    int? SessionId,
    decimal? OpeningFloat,
    DateTime? OpenedAt
);

public class GetCurrentCashSessionQueryHandler
    : IRequestHandler<GetCurrentCashSessionQuery, CurrentCashSessionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetCurrentCashSessionQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CurrentCashSessionDto> Handle(GetCurrentCashSessionQuery request, CancellationToken ct)
    {
        if (!int.TryParse(_currentUser.StoreId, out var storeId))
            throw new UnauthorizedAccessException("Bu işlem için mağaza yetkisi gereklidir.");

        var session = await _context.CashDrawerSessions
            .AsNoTracking()
            .Where(s => s.StoreId == storeId
                     && s.CashierId == _currentUser.UserId
                     && s.Status == CashSessionStatus.Open)
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync(ct);

        return session is null
            ? new CurrentCashSessionDto(false, null, null, null)
            : new CurrentCashSessionDto(true, session.Id, session.OpeningFloat, session.OpenedAt);
    }
}
