using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Stores.Commands.UpdateStore;

public record UpdateStoreCommand(
    int Id,
    string Name,
    string? Address,
    string? City,
    string? Phone
) : IRequest;

public class UpdateStoreCommandHandler
    : IRequestHandler<UpdateStoreCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateStoreCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(
        UpdateStoreCommand request,
        CancellationToken cancellationToken)
    {
        var store = await _context.Stores
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"ID={request.Id} mağaza bulunamadı.");

        store.Name = request.Name;
        store.Address = request.Address;
        store.City = request.City;
        store.Phone = request.Phone;

        await _context.SaveChangesAsync(cancellationToken);
    }
}