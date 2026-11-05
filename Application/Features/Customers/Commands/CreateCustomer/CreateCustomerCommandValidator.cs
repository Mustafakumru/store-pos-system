using Application.Common.Validation;
using FluentValidation;

namespace Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .Must(CommonRules.IsValidPhone)
            .WithMessage("Telefon numarası geçerli değil. 10 haneli numara girin (örn. 0555 123 45 67).");

        RuleFor(x => x.FullName)
            .RequiredText("Ad soyad", 2, 150)
            .Must(CommonRules.IsPersonName).WithMessage("Ad soyad sadece harf içerebilir.");

        RuleFor(x => x.Email)
            .MaximumLength(150).WithMessage("E-posta en fazla 150 karakter olabilir.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
