using Application.Features.POS.Queries.GetSaleByReceipt;
using MediatR;

namespace Application.Features.POS.Commands.ReprintSale;

/// <summary>
/// WPOS "Son Belge Tekrarı" — yönetici onayı ile fişi tekrar yazdırır.
/// Gerçek yazıcı entegrasyonu WPF POS terminalinde yapılacak;
/// bu endpoint sadece fiş verisini döner (reprint için).
/// </summary>
public record ReprintSaleCommand(
    int SaleId,
    string ApprovalToken
) : IRequest<SaleReceiptDto>;