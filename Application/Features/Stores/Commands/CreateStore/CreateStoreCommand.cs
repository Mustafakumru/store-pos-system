using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Stores.Commands.CreateStore;

public record CreateStoreCommand(
    string Name,
    string Code,
    string? Address,
    string? City,
    string? Phone
) : IRequest<CreateStoreResult>;

public record CreateStoreResult(int Id, string Name, string Code);

public class CreateStoreCommandHandler
    : IRequestHandler<CreateStoreCommand, CreateStoreResult>
{
    private readonly IApplicationDbContext _context;

    public CreateStoreCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<CreateStoreResult> Handle(
        CreateStoreCommand request,
        CancellationToken cancellationToken)
    {
        // Aynı kod ile mağaza var mı?
        var exists = await _context.Stores
            .AnyAsync(s => s.Code == request.Code, cancellationToken);

        if (exists)
            throw new InvalidOperationException(
                $"'{request.Code}' kodlu mağaza zaten mevcut.");

        var store = new Store
        {
            Name = request.Name,
            Code = request.Code.ToUpper().Trim(),
            Address = request.Address,
            City = request.City,
            Phone = request.Phone,
            IsActive = true
        };

        _context.Stores.Add(store);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateStoreResult(store.Id, store.Name, store.Code);
    }
}