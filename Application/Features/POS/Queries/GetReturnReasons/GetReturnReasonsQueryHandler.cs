using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetReturnReasons;

public class GetReturnReasonsQueryHandler
    : IRequestHandler<GetReturnReasonsQuery, List<ReturnReasonDto>>
{
    private readonly IApplicationDbContext _context;

    public GetReturnReasonsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<ReturnReasonDto>> Handle(
        GetReturnReasonsQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.ReturnReasons
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new ReturnReasonDto(r.Id, r.Name))
            .ToListAsync(cancellationToken);
    }
}