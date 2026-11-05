using MediatR;

namespace Application.Features.Products.Commands.DeleteProduct;

public record DeleteProductCommand(int ProductId) : IRequest<Unit>;