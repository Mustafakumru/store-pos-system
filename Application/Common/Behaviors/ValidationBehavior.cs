using FluentValidation;
using MediatR;

namespace Application.Common.Behaviors;

/// <summary>
/// Her MediatR isteğini handler'a ulaşmadan önce o istek için kayıtlı
/// FluentValidation kurallarıyla doğrular. Hata varsa ValidationException
/// fırlatılır; WebAPI bunu alan bazlı hata listesiyle 400'e çevirir.
///
/// Eskiden FluentValidation DI'a kayıtlıydı ama ne bir validator ne de bu
/// pipeline vardı: doğrulama her handler'da elle (ve tutarsız) yapılıyordu.
/// Boş adlı mağaza, "abc" telefonlu müşteri, 2 MB'lık askı sepeti kabul
/// ediliyor; DB sınırını aşan metinler 500 hatası veriyordu.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
