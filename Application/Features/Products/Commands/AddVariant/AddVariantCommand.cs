using MediatR;

namespace Application.Features.Products.Commands.AddVariant;

public record AddVariantCommand(
    int ProductId,
    string? Barcode,          // bos = sunucu EAN-13 uretir
    string? Color,
    string? Size,
    string? ImagePath,
    decimal InitialPrice
) : IRequest<int>;
