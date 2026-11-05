using MediatR;

namespace Application.Features.POS.Commands.ResumeCart;

/// <summary>
/// WPOS "Askıdan Çağır" — askıdaki sepeti geri çağırır.
/// Sepet, çağrıldıktan sonra listeden kaldırılır (soft delete).
/// </summary>
public record ResumeCartCommand(int Id) : IRequest<ResumeCartResult>;

public record ResumeCartResult(string CartDataJson);