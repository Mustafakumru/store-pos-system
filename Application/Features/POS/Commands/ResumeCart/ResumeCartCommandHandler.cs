using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Commands.ResumeCart;

public class ResumeCartCommandHandler
    : IRequestHandler<ResumeCartCommand, ResumeCartResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ResumeCartCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ResumeCartResult> Handle(
        ResumeCartCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException(
                "Bu işlem için mağaza yetkisi gereklidir.");

        var storeId = int.Parse(_currentUser.StoreId);

        var suspended = await _context.SuspendedSales
            .FirstOrDefaultAsync(
                s => s.Id == request.Id && s.StoreId == storeId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Askıdaki sepet bulunamadı.");

        var cartDataJson = suspended.CartDataJson;

        // Geri çağrılan sepet listeden kaldırılır
        suspended.IsDeleted = true;

        await _context.SaveChangesAsync(cancellationToken);

        return new ResumeCartResult(cartDataJson);
    }
}