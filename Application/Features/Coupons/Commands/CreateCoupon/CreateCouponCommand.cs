using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Coupons.Commands.CreateCoupon;

public record CreateCouponCommand(
    string Code,
    string? Description,
    DiscountType DiscountType,
    decimal Value,
    decimal? MinPurchaseAmount,
    decimal? MaxDiscountAmount,
    DateTime? StartDate,
    DateTime? EndDate,
    int? UsageLimit
) : IRequest<CouponDto>;

public record CouponDto(
    int Id,
    string Code,
    string? Description,
    DiscountType DiscountType,
    decimal Value,
    decimal? MinPurchaseAmount,
    decimal? MaxDiscountAmount,
    DateTime StartDate,
    DateTime? EndDate,
    int? UsageLimit,
    int UsedCount,
    bool IsActive
)
{
    public static CouponDto From(Coupon c) => new(
        c.Id, c.Code, c.Description, c.DiscountType, c.Value,
        c.MinPurchaseAmount, c.MaxDiscountAmount, c.StartDate, c.EndDate,
        c.UsageLimit, c.UsedCount, c.IsActive);
}

public class CreateCouponCommandHandler : IRequestHandler<CreateCouponCommand, CouponDto>
{
    private readonly IApplicationDbContext _context;

    public CreateCouponCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<CouponDto> Handle(CreateCouponCommand request, CancellationToken cancellationToken)
    {
        // Biçim/tutar kuralları CreateCouponCommandValidator'da (pipeline). Burada
        // sadece veritabanı gerektiren kontrol (kod benzersizliği) yapılır.
        var code = Coupon.NormalizeCode(request.Code);
        var startDate = request.StartDate ?? DateTime.UtcNow;

        // Soft-delete edilmiş kuponlar da unique index'e takılır - filtreyi yok say.
        if (await _context.Coupons.IgnoreQueryFilters().AnyAsync(c => c.Code == code, cancellationToken))
            throw new InvalidOperationException($"'{code}' kodlu kupon zaten var.");

        var coupon = new Coupon
        {
            Code = code,
            Description = request.Description?.Trim(),
            DiscountType = request.DiscountType,
            Value = request.Value,
            MinPurchaseAmount = request.MinPurchaseAmount,
            MaxDiscountAmount = request.MaxDiscountAmount,
            StartDate = startDate,
            EndDate = request.EndDate,
            UsageLimit = request.UsageLimit,
            IsActive = true
        };

        _context.Coupons.Add(coupon);
        await _context.SaveChangesAsync(cancellationToken);

        return CouponDto.From(coupon);
    }
}

/// <summary>Kupon kuralları (eskiden handler içinde elle yazılıydı).</summary>
public sealed class CreateCouponCommandValidator : FluentValidation.AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator()
    {
        RuleFor(x => x.Code)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Kupon kodu zorunludur.")
            .Must(c => string.IsNullOrWhiteSpace(c) || Coupon.NormalizeCode(c).Length is >= 3 and <= 30)
                .WithMessage("Kupon kodu 3-30 karakter olmalıdır.")
            .Must(c => string.IsNullOrWhiteSpace(c)
                       || System.Text.RegularExpressions.Regex.IsMatch(Coupon.NormalizeCode(c), "^[A-Z0-9_-]+$"))
                .WithMessage("Kupon kodu sadece harf (A-Z), rakam, '-' ve '_' içerebilir.");

        RuleFor(x => x.Description)
            .Must(d => d is null || d.Trim().Length <= 200).WithMessage("Açıklama en fazla 200 karakter olabilir.");

        RuleFor(x => x.DiscountType).IsInEnum().WithMessage("Geçersiz indirim tipi.");

        RuleFor(x => x.Value)
            .GreaterThan(0).WithMessage("İndirim değeri sıfırdan büyük olmalıdır.")
            .LessThanOrEqualTo(Application.Common.Validation.CommonRules.MaxPrice)
                .WithMessage("İndirim tutarı çok büyük.");
        RuleFor(x => x.Value)
            .LessThanOrEqualTo(100).When(x => x.DiscountType == DiscountType.Percentage)
            .WithMessage("Yüzde indirim 100'ü aşamaz.");

        RuleFor(x => x.MinPurchaseAmount)
            .GreaterThanOrEqualTo(0).When(x => x.MinPurchaseAmount.HasValue)
            .WithMessage("Alt limit negatif olamaz.");
        RuleFor(x => x.MaxDiscountAmount)
            .GreaterThan(0).When(x => x.MaxDiscountAmount.HasValue)
            .WithMessage("İndirim tavanı sıfırdan büyük olmalıdır.");
        RuleFor(x => x.UsageLimit)
            .GreaterThan(0).When(x => x.UsageLimit.HasValue)
            .WithMessage("Kullanım limiti sıfırdan büyük olmalıdır.");

        RuleFor(x => x.EndDate)
            .Must((cmd, end) => end is null || end > (cmd.StartDate ?? DateTime.UtcNow))
            .WithMessage("Bitiş tarihi başlangıçtan sonra olmalıdır.");
    }
}
