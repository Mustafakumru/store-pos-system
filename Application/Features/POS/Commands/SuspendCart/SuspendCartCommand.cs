using MediatR;

namespace Application.Features.POS.Commands.SuspendCart;

/// <summary>
/// WPOS "Askıya Al" — henüz ödenmemiş sepeti askıya alır.
/// Sepet içeriği (ürünler, müşteri, indirimler) JSON olarak saklanır;
/// yapısını Angular/WPF tarafı belirler, backend opak string olarak tutar.
/// </summary>
public record SuspendCartCommand(
    string CartDataJson,
    string? Note = null
) : IRequest<SuspendCartResult>;

public record SuspendCartResult(int Id, DateTime CreatedAt);