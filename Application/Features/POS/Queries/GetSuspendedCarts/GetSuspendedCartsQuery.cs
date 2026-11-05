using MediatR;

namespace Application.Features.POS.Queries.GetSuspendedCarts;

/// <summary>
/// WPOS "Askıdan Çağır" — mağazadaki askıdaki sepetlerin listesi.
/// Herhangi bir kasiyer, mağazadaki tüm askıdaki sepetleri görebilir.
/// </summary>
public record GetSuspendedCartsQuery : IRequest<List<SuspendedCartDto>>;

public record SuspendedCartDto(
    int Id,
    string CashierId,
    string? Note,
    DateTime CreatedAt
);