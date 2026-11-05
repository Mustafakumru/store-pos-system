namespace Application.Common.Interfaces;

/// <summary>
/// Yönetici onay token'larını doğrular (indirim, belge iptal, reprint vb.)
/// Token üretimi WebAPI/Identity katmanında (JwtService) yapılır;
/// Application katmanı sadece doğrulama sonucunu kullanır.
/// </summary>
public interface IApprovalTokenService
{
    /// <summary>
    /// Token geçerliyse onaylayan kullanıcının Id'sini döner, değilse null.
    /// </summary>
    string? ValidateApprovalToken(string token, string expectedAction);

    /// <summary>
    /// Onaylanan işlem BAŞARIYLA kaydedildikten sonra çağrılır; token bir
    /// daha kabul edilmez (tek kullanımlık onay).
    /// </summary>
    void MarkTokenUsed(string token);
}