using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.GetVariantGroups;

public record GetVariantGroupsQuery(int ProductId)
    : IRequest<List<VariantGroupDto>>;

public record VariantGroupDto(
    int Id,
    string GroupName,
    string GroupType,
    List<GroupImageDto> Images,
    List<GroupVariantDto> Variants
);

public record GroupImageDto(
    int Id,
    string ImagePath,
    int DisplayOrder
);

public record GroupVariantDto(
    int VariantId,
    string Barcode,
    string? Size,
    bool IsActive
);

public class GetVariantGroupsQueryHandler
    : IRequestHandler<GetVariantGroupsQuery, List<VariantGroupDto>>
{
    private readonly IApplicationDbContext _context;

    public GetVariantGroupsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<VariantGroupDto>> Handle(
        GetVariantGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var groups = await _context.ProductVariantGroups
            .AsNoTracking()
            .Include(g => g.Images)
            .Include(g => g.Variants)
            .Where(g => g.ProductId == request.ProductId)
            .OrderBy(g => g.GroupName)
            .ToListAsync(cancellationToken);

        return groups.Select(g => new VariantGroupDto(
            Id: g.Id,
            GroupName: g.GroupName,
            GroupType: g.GroupType,
            Images: g.Images
                .Where(i => !i.IsDeleted)
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new GroupImageDto(
                    i.Id, i.ImagePath, i.DisplayOrder))
                .ToList(),
            Variants: g.Variants
                .Where(v => !v.IsDeleted)
                .Select(v => new GroupVariantDto(
                    v.Id, v.Barcode, v.Size, v.IsActive))
                .ToList()
        )).ToList();
    }
}