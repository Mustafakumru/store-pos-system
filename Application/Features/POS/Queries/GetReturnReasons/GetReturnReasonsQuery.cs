using MediatR;

namespace Application.Features.POS.Queries.GetReturnReasons;

/// <summary>
/// WPOS "İade Nedeni" listesi — kasiyer ekranında dropdown/grid olarak gösterilir.
/// </summary>
public record GetReturnReasonsQuery : IRequest<List<ReturnReasonDto>>;

public record ReturnReasonDto(int Id, string Name);