using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Coupons.Queries.ValidateCoupon;

/// <summary>
/// Kasiyer ödeme ekranında kupon kodunu girdiğinde çağrılır. Kupon
/// geçerliyse verilen tutara uygulanacak indirimi döner. Bu sadece ÖN
/// İZLEMEdir - satışta indirim ProcessSaleCommandHandler'da aynı kurallarla
/// yeniden hesaplanır ve kullanım sayısı orada artırılır.
/// </summary>
public record ValidateCouponQuery(string Code, decimal Amount) : IRequest<CouponValidationResult>;

public record CouponValidationResult(
    string Code,
    string? Description,
    DiscountType DiscountType,
    decimal Value,
    decimal DiscountAmount
);

public class ValidateCouponQueryHandler : IRequestHandler<ValidateCouponQuery, CouponValidationResult>
{
    private readonly IApplicationDbContext _context;

    public ValidateCouponQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<CouponValidationResult> Handle(
        ValidateCouponQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("Kupon kodu girin.");

        var code = Coupon.NormalizeCode(request.Code);

        var coupon = await _context.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == code, cancellationToken)
            ?? throw new KeyNotFoundException($"'{code}' kodlu kupon bulunamadı.");

        var reason = coupon.GetInvalidReason(DateTime.UtcNow, request.Amount);
        if (reason is not null)
            throw new InvalidOperationException(reason);

        return new CouponValidationResult(
            coupon.Code,
            coupon.Description,
            coupon.DiscountType,
            coupon.Value,
            coupon.CalculateDiscount(request.Amount));
    }
}
