using Application.Common.Interfaces;
using Application.Features.POS.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Commands.CreateZReport;

public class CreateZReportCommandHandler
    : IRequestHandler<CreateZReportCommand, ZReportDto>
{
    public const string ZReportAction = "z-report";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IApprovalTokenService _approvalTokenService;

    public CreateZReportCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser,
        IApprovalTokenService approvalTokenService)
    {
        _context = context;
        _currentUser = currentUser;
        _approvalTokenService = approvalTokenService;
    }

    public async Task<ZReportDto> Handle(
        CreateZReportCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.StoreId))
            throw new UnauthorizedAccessException(
                "Bu işlem için mağaza yetkisi gereklidir.");

        var storeId = int.Parse(_currentUser.StoreId);

        // Z gün sonu kapanışıdır (geri alınamaz): kasiyer ancak müdür onayıyla alabilir
        string? approverId = null;
        if (!request.IsManager)
        {
            if (string.IsNullOrEmpty(request.ApprovalToken))
                throw new UnauthorizedAccessException("Z raporu için mağaza müdürü onayı gereklidir.");
            approverId = _approvalTokenService.ValidateApprovalToken(request.ApprovalToken, ZReportAction)
                ?? throw new UnauthorizedAccessException("Onay kodu geçersiz veya süresi dolmuş.");
        }

        // Açık kasa varken Z alınamaz: teslim edilmemiş nakit dönem dışında kalır
        var openSessions = await _context.CashDrawerSessions
            .CountAsync(s => s.StoreId == storeId && s.Status == Domain.Enums.CashSessionStatus.Open, cancellationToken);
        if (openSessions > 0)
            throw new InvalidOperationException(
                $"Mağazada {openSessions} açık kasa var. Z raporundan önce tüm kasalar 'Teslim' ile kapatılmalıdır.");

        var lastZReport = await _context.ZReports
            .Where(z => z.StoreId == storeId)
            .OrderByDescending(z => z.ReportNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var fromDate = lastZReport?.ToDate ?? DateTime.MinValue;
        var toDate = DateTime.UtcNow;
        var nextReportNumber = (lastZReport?.ReportNumber ?? 0) + 1;

        var data = await SalesReportCalculator.CalculateAsync(
            _context, storeId, fromDate, toDate, cancellationToken);

        var zReport = new ZReport
        {
            StoreId = storeId,
            ReportNumber = nextReportNumber,
            FromDate = fromDate,
            ToDate = toDate,
            TotalSalesAmount = data.TotalSalesAmount,
            TotalRefundsAmount = data.TotalRefundsAmount,
            NetAmount = data.NetAmount,
            CashTotal = data.CashTotal,
            CardTotal = data.CardTotal,
            GiftCardTotal = data.GiftCardTotal,
            CashRefundTotal = data.CashRefundTotal,
            CardRefundTotal = data.CardRefundTotal,
            GiftCardSoldTotal = data.GiftCardSoldTotal,
            GiftCardSoldCash = data.GiftCardSoldCash,
            GiftCardSoldCard = data.GiftCardSoldCard,
            TransactionCount = data.TransactionCount,
            RefundCount = data.RefundCount,
            CashierId = _currentUser.UserId
        };

        _context.ZReports.Add(zReport);
        await _context.SaveChangesAsync(cancellationToken);

        if (approverId is not null)
            _approvalTokenService.MarkTokenUsed(request.ApprovalToken!);

        return new ZReportDto(
            zReport.Id, zReport.StoreId, zReport.ReportNumber,
            zReport.FromDate, zReport.ToDate,
            zReport.TotalSalesAmount, zReport.TotalRefundsAmount, zReport.NetAmount,
            zReport.CashTotal, zReport.CardTotal, zReport.GiftCardTotal,
            zReport.TransactionCount, zReport.RefundCount,
            zReport.CashRefundTotal, zReport.CardRefundTotal,
            zReport.GiftCardSoldTotal, zReport.GiftCardSoldCash, zReport.GiftCardSoldCard);
    }
}