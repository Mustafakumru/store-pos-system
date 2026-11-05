using Application.Common.Validation;
using FluentValidation;

namespace Application.Features.POS.Commands.SuspendCart;

/// <summary>
/// Askıya alınan sepet istemcinin JSON'udur. Sınır: en fazla 300 satırlık bir
/// sepet ~60 KB tutar; 200.000 karakter bol bir üst sınırdır. Eskiden sınır
/// yoktu - 2 MB'lık (ya da JSON olmayan) veri kabul edilip veritabanına yazılıyordu.
/// </summary>
public sealed class SuspendCartCommandValidator : AbstractValidator<SuspendCartCommand>
{
    public const int MaxCartJsonLength = 200_000;

    public SuspendCartCommandValidator()
    {
        RuleFor(x => x.CartDataJson)
            .NotEmpty().WithMessage("Sepet içeriği boş olamaz.")
            .MaximumLength(MaxCartJsonLength).WithMessage("Sepet çok büyük; askıya alınamaz.")
            .Must(CommonRules.IsValidJson).WithMessage("Sepet içeriği geçerli değil.");

        RuleFor(x => x.Note).OptionalText("Not", 200);
    }
}
