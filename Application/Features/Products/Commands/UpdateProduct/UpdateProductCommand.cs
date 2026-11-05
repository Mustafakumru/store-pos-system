using MediatR;

namespace Application.Features.Products.Commands.UpdateProduct;

public record UpdateProductCommand(
    int Id,
    string Name,
    string? Description,
    int CategoryId,
    string? ImagePath,
    bool IsActive
) : IRequest;