using Application.Common.Validation;
using Application.Features.Stores.Commands.UpdateStore;
using FluentValidation;

namespace Application.Features.Stores.Commands.CreateStore;

/// <summary>Mağaza kodu: harf, rakam ve tire (örn. LCW-IST-001). DB: 30 karakter.</summary>
public sealed class CreateStoreCommandValidator : AbstractValidator<CreateStoreCommand>
{
    public CreateStoreCommandValidator()
    {
        RuleFor(x => x.Name).RequiredText("Mağaza adı", 2, 150);
        RuleFor(x => x.Code)
            .RequiredText("Mağaza kodu", 2, 30)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("Mağaza kodu sadece harf, rakam ve tire içerebilir (örn. LCW-IST-001).");
        StoreRules.Apply(this, x => x.City, x => x.Phone, x => x.Address);
    }
}

public sealed class UpdateStoreCommandValidator : AbstractValidator<UpdateStoreCommand>
{
    public UpdateStoreCommandValidator()
    {
        RuleFor(x => x.Id).ValidId("mağaza");
        RuleFor(x => x.Name).RequiredText("Mağaza adı", 2, 150);
        StoreRules.Apply(this, x => x.City, x => x.Phone, x => x.Address);
    }
}

internal static class StoreRules
{
    public static void Apply<T>(AbstractValidator<T> v,
        System.Linq.Expressions.Expression<Func<T, string?>> city,
        System.Linq.Expressions.Expression<Func<T, string?>> phone,
        System.Linq.Expressions.Expression<Func<T, string?>> address)
    {
        v.RuleFor(city).OptionalText("Şehir", 100);
        v.RuleFor(address).OptionalText("Adres", 500);
        v.RuleFor(phone)
            .Must(p => string.IsNullOrWhiteSpace(p) || CommonRules.IsValidPhone(p))
            .WithMessage("Mağaza telefonu geçerli bir numara olmalıdır (örn. 0212 123 45 67).");
    }
}
