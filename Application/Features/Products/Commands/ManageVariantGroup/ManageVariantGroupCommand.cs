using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.ManageVariantGroup;

// ── Grup Oluştur ─────────────────────────────────────────────
public record CreateVariantGroupCommand(
    int ProductId,
    string GroupName,
    string GroupType = "Color"
) : IRequest<int>;

public class CreateVariantGroupCommandHandler
    : IRequestHandler<CreateVariantGroupCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateVariantGroupCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(
        CreateVariantGroupCommand request,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException("Ürün bulunamadı.");

        var group = new ProductVariantGroup
        {
            ProductId = request.ProductId,
            GroupName = request.GroupName,
            GroupType = request.GroupType
        };

        _context.ProductVariantGroups.Add(group);
        await _context.SaveChangesAsync(cancellationToken);
        return group.Id;
    }
}

// ── Gruba Varyant Ata ────────────────────────────────────────
public record AssignVariantToGroupCommand(
    int VariantId,
    int GroupId
) : IRequest;

public class AssignVariantToGroupCommandHandler
    : IRequestHandler<AssignVariantToGroupCommand>
{
    private readonly IApplicationDbContext _context;

    public AssignVariantToGroupCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(
        AssignVariantToGroupCommand request,
        CancellationToken cancellationToken)
    {
        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == request.VariantId, cancellationToken)
            ?? throw new KeyNotFoundException("Varyant bulunamadı.");

        variant.GroupId = request.GroupId;
        await _context.SaveChangesAsync(cancellationToken);
    }
}

// ── Gruba Görsel Ekle ────────────────────────────────────────
public record AddImageToGroupCommand(
    int GroupId,
    string ImagePath,
    int DisplayOrder = 0
) : IRequest<int>;

public class AddImageToGroupCommandHandler
    : IRequestHandler<AddImageToGroupCommand, int>
{
    private readonly IApplicationDbContext _context;

    public AddImageToGroupCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(
        AddImageToGroupCommand request,
        CancellationToken cancellationToken)
    {
        var group = await _context.ProductVariantGroups
            .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken)
            ?? throw new KeyNotFoundException("Grup bulunamadı.");

        var image = new ProductVariantGroupImage
        {
            GroupId = request.GroupId,
            ImagePath = request.ImagePath,
            DisplayOrder = request.DisplayOrder
        };

        _context.ProductVariantGroupImages.Add(image);
        await _context.SaveChangesAsync(cancellationToken);
        return image.Id;
    }
}

// ── Grup Görseli Sil ─────────────────────────────────────────
public record DeleteGroupImageCommand(int ImageId) : IRequest;

public class DeleteGroupImageCommandHandler
    : IRequestHandler<DeleteGroupImageCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteGroupImageCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(
        DeleteGroupImageCommand request,
        CancellationToken cancellationToken)
    {
        var image = await _context.ProductVariantGroupImages
            .FirstOrDefaultAsync(i => i.Id == request.ImageId, cancellationToken)
            ?? throw new KeyNotFoundException("Görsel bulunamadı.");

        image.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
    }
}