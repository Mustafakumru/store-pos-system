using MediatR;

namespace Application.Features.Products.Commands.UpdateVariant;

public record UpdateVariantCommand(
    int Id,
    string? Color,
    string? Size,
    string? ImagePath,
    bool IsActive
) : IRequest;