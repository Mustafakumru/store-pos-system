using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Stores.Commands.ToggleStoreStatus;

public record ToggleStoreStatusCommand(int Id) : IRequest<bool>;

public class ToggleStoreStatusCommandHandler
    : IRequestHandler<ToggleStoreStatusCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ToggleStoreStatusCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<bool> Handle(
        ToggleStoreStatusCommand request,
        CancellationToken cancellationToken)
    {
        var store = await _context.Stores
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"ID={request.Id} mağaza bulunamadı.");

        store.IsActive = !store.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return store.IsActive;
    }
}