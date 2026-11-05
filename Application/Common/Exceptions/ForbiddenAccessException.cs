namespace Application.Common.Exceptions;

/// <summary>
/// Kullanıcı kimliği geçerli ama bu kaynak üzerinde yetkisi yok → HTTP 403.
/// (UnauthorizedAccessException 401'e çevrilir; admin paneli 401'de oturumu
/// kapattığı için "başka mağazanın sevkiyatı" gibi durumlarda bu kullanılır.)
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message) { }
}
