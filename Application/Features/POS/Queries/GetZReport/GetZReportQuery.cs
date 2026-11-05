using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.POS.Queries.GetZReport;

public record GetZReportQuery(
    int StoreId,
    DateTime? Date = null
) : IRequest<ZReportDto>;

public record ZReportDto(
    string StoreName,
    string StoreCode,
    DateTime ReportDate,
    DateTime GeneratedAt,
    int TotalTransactions,
    int TotalItemsSold,
    decimal GrossRevenue,
    decimal TotalDiscount,
    decimal NetRevenue,
    decimal CashTotal,
    decimal CardTotal,
    decimal MixedTotal,
    int RefundCount,
    decimal RefundTotal,
    int NegativeSaleCount,
    decimal FinalNet,
    List<HourlySalesDto> HourlySales
);

public record HourlySalesDto(
    int Hour,
    int SaleCount,
    decimal Revenue
);

public class GetZReportQueryHandler
    : IRequestHandler<GetZReportQuery, ZReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetZReportQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<ZReportDto> Handle(
        GetZReportQuery request,
        CancellationToken cancellationToken)
    {
        // Yerel iş günü → UTC aralık (eskiden UTC günü kullanılıyordu)
        var targetDate = request.Date?.Date ?? Application.Common.BusinessClock.Today;
        var (dayFromUtc, dayToUtc) = Application.Common.BusinessClock.DayRangeUtc(targetDate);

        var store = await _context.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StoreId, cancellationToken)
            ?? throw new KeyNotFoundException("Mağaza bulunamadı.");

        var sales = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.StoreId == request.StoreId
                     && s.CreatedAt >= dayFromUtc
                     && s.CreatedAt < dayToUtc)
            .ToListAsync(cancellationToken);

        // Satış: o gün kesilen TÜM fişler (sonradan iade edilenler dahil - iade
        // kendi gününde defterden düşülür). Eskiden iade edilmiş fiş satıştan
        // hiç sayılmıyor, kısmi iadeli fişin TAMAMI iade sayılıyordu.
        var completed = sales;

        var report = await Application.Features.POS.Common.SalesReportCalculator.CalculateAsync(
            _context, request.StoreId, dayFromUtc, dayToUtc, cancellationToken);

        var hourlySales = completed
            .GroupBy(s => Application.Common.BusinessClock.ToLocal(s.CreatedAt).Hour)   // yerel saat
            .Select(g => new HourlySalesDto(
                Hour: g.Key,
                SaleCount: g.Count(),
                Revenue: g.Sum(s => s.FinalAmount)
            ))
            .OrderBy(h => h.Hour)
            .ToList();

        var netRevenue = completed.Sum(s => s.FinalAmount);
        var refundTotal = report.TotalRefundsAmount;

        return new ZReportDto(
            StoreName: store.Name,
            StoreCode: store.Code,
            ReportDate: targetDate,
            GeneratedAt: DateTime.UtcNow,
            TotalTransactions: completed.Count,
            TotalItemsSold: completed.Sum(s => s.Items.Sum(i => i.Quantity)),
            GrossRevenue: completed.Sum(s => s.TotalAmount),
            TotalDiscount: completed.Sum(s => s.DiscountAmount),
            NetRevenue: netRevenue,
            // Ödeme kayıtlarından (karma ödemeler doğru dağılır)
            CashTotal: report.CashTotal,
            CardTotal: report.CardTotal,
            MixedTotal: completed
                .Where(s => s.PaymentType == Domain.Enums.PaymentType.Mixed)
                .Sum(s => s.FinalAmount),
            RefundCount: report.RefundCount,
            RefundTotal: refundTotal,
            NegativeSaleCount: completed.Count(s => s.HasNegativeStockItem),
            FinalNet: netRevenue - refundTotal,
            HourlySales: hourlySales
        );
    }
}