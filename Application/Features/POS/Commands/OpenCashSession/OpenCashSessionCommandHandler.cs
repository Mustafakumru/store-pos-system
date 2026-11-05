using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Commands.OpenCashSession;

public class OpenCashSessionCommandHandler
    : IRequestHandler<OpenCashSessionCommand, CashSessionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IApprovalTokenService _approvalTokenService;

    public OpenCashSessionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IApprovalTokenService approvalTokenService)
    {
        _context = context;
        _currentUser = currentUser;
        _approvalTokenService = approvalTokenService;
    }

    public async Task<CashSessionDto> Handle(
        OpenCashSessionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException(
                "Bu işlem için mağaza yetkisi gereklidir.");

        if (request.OpeningFloat < 0)
            throw new InvalidOperationException("Avans tutarı negatif olamaz.");

        var approverId = _approvalTokenService.ValidateApprovalToken(
            request.ApprovalToken, "cash-session-open");

        if (approverId is null)
            throw new UnauthorizedAccessException(
                "Onay kodu geçersiz veya süresi dolmuş.");

        var storeId = int.Parse(_currentUser.StoreId);

        var hasOpenSession = await _context.CashDrawerSessions
            .AnyAsync(s => s.StoreId == storeId
                        && s.CashierId == _currentUser.UserId
                        && s.Status == CashSessionStatus.Open,
                      cancellationToken);

        if (hasOpenSession)
            throw new InvalidOperationException(
                "Zaten açık bir kasa oturumunuz var. Önce mevcut oturumu " +
                "teslim ile kapatmanız gerekir.");

        var session = new CashDrawerSession
        {
            StoreId = storeId,
            CashierId = _currentUser.UserId,
            OpeningFloat = request.OpeningFloat,
            OpenedAt = DateTime.UtcNow,
            OpeningApprovedByUserId = approverId,
            Status = CashSessionStatus.Open
        };

        _context.CashDrawerSessions.Add(session);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Eşzamanlı ikinci açılış: veritabanı tek-açık-oturum kısıtı yakaladı
            _context.ResetTracking();
            var alreadyOpen = await _context.CashDrawerSessions.AsNoTracking().AnyAsync(
                s => s.StoreId == storeId && s.CashierId == _currentUser.UserId && s.Status == CashSessionStatus.Open,
                cancellationToken);
            if (!alreadyOpen) throw;

            throw new InvalidOperationException(
                "Zaten açık bir kasa oturumunuz var. Önce mevcut oturumu teslim ile kapatmanız gerekir.");
        }

        // Onay token'i tek kullanimlik - islem kaydedildikten sonra tuketilir.
        _approvalTokenService.MarkTokenUsed(request.ApprovalToken);

        return new CashSessionDto(
            session.Id, session.StoreId, session.CashierId,
            session.OpeningFloat, session.OpenedAt,
            session.ClosingAmount, session.ClosedAt,
            session.Status.ToString());
    }
}