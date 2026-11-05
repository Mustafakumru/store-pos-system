using Application.Common.Interfaces;
using Application.Features.Coupons.Commands.CreateCoupon;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Coupons.Queries.GetCoupons;

public record GetCouponsQuery : IRequest<List<CouponDto>>;

public class GetCouponsQueryHandler : IRequestHandler<GetCouponsQuery, List<CouponDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCouponsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<CouponDto>> Handle(GetCouponsQuery request, CancellationToken cancellationToken)
    {
        var coupons = await _context.Coupons
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return coupons.Select(CouponDto.From).ToList();
    }
}
