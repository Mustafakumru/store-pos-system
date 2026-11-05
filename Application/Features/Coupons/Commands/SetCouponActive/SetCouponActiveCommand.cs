using Application.Common.Interfaces;
using Application.Features.Coupons.Commands.CreateCoupon;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Coupons.Commands.SetCouponActive;

/// <summary>Kuponu aktif/pasif yapar (geçmiş satışlar etkilenmez).</summary>
public record SetCouponActiveCommand(int Id, bool IsActive) : IRequest<CouponDto>;

public class SetCouponActiveCommandHandler : IRequestHandler<SetCouponActiveCommand, CouponDto>
{
    private readonly IApplicationDbContext _context;

    public SetCouponActiveCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<CouponDto> Handle(SetCouponActiveCommand request, CancellationToken cancellationToken)
    {
        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Kupon bulunamadı.");

        coupon.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        return CouponDto.From(coupon);
    }
}
