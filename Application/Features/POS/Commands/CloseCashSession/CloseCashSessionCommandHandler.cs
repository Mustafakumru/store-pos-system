using Application.Common.Interfaces;
using Application.Features.POS.Commands.OpenCashSession;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Commands.CloseCashSession;

public class CloseCashSessionCommandHandler
    : IRequestHandler<CloseCashSessionCommand, CashSessionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IApprovalTokenService _approvalTokenService;

    public CloseCashSessionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IApprovalTokenService approvalTokenService)
    {
        _context = context;
        _currentUser = currentUser;
        _approvalTokenService = approvalTokenService;
    }

    public async Task<CashSessionDto> Handle(
        CloseCashSessionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException(
                "Bu işlem için mağaza yetkisi gereklidir.");

        if (request.ClosingAmount < 0)
            throw new InvalidOperationException("Teslim tutarı negatif olamaz.");

        var approverId = _approvalTokenService.ValidateApprovalToken(
            request.ApprovalToken, "cash-session-close");

        if (approverId is null)
            throw new UnauthorizedAccessException(
                "Onay kodu geçersiz veya süresi dolmuş.");

        var storeId = int.Parse(_currentUser.StoreId);

        var session = await _context.CashDrawerSessions
            .FirstOrDefaultAsync(s => s.StoreId == storeId
                                   && s.CashierId == _currentUser.UserId
                                   && s.Status == CashSessionStatus.Open,
                                  cancellationToken)
            ?? throw new InvalidOperationException(
                "Açık bir kasa oturumunuz bulunamadı.");

        session.ClosingAmount = request.ClosingAmount;
        session.ClosedAt = DateTime.UtcNow;
        session.ClosingApprovedByUserId = approverId;
        session.Status = CashSessionStatus.Closed;

        await _context.SaveChangesAsync(cancellationToken);

        // Onay token'i tek kullanimlik - islem kaydedildikten sonra tuketilir.
        _approvalTokenService.MarkTokenUsed(request.ApprovalToken);

        return new CashSessionDto(
            session.Id, session.StoreId, session.CashierId,
            session.OpeningFloat, session.OpenedAt,
            session.ClosingAmount, session.ClosedAt,
            session.Status.ToString());
    }
}