using MediatR;

namespace Application.Features.Products.Commands.UpdateProductPrice;

public record UpdateProductPriceCommand(
    int VariantId,  // ✅ ProductId → VariantId
    decimal NewPrice
) : IRequest;