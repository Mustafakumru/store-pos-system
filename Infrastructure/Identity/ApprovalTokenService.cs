using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using Application.Common.Interfaces;

namespace Infrastructure.Identity;

/// <summary>
/// Yönetici onay token'larını doğrular ve TEK KULLANIMLIK olmasını sağlar.
///
/// Eskiden "kullanıldı mı?" kontrolü ile "kullanıldı" işareti ayrı adımlardı:
/// aynı token'la aynı anda gelen 5 istek kontrolü birlikte geçiyor, kasiyer
/// için 5 ayrı kasa oturumu açılabiliyordu. Artık doğrulama token'ı ATOMİK
/// olarak sahiplenir (TryAdd): aynı anda gelen diğer istekler reddedilir.
///
/// Sahiplenen istek başarısız olursa (işlem kaydedilmeden biterse) token
/// istek sonunda (Dispose) serbest bırakılır, kasiyer aynı onayla tekrar
/// deneyebilir. Başarılı kayıttan sonra MarkTokenUsed ile kalıcı tüketilir.
/// </summary>
public sealed class ApprovalTokenService : IApprovalTokenService, IDisposable
{
    private readonly JwtService _jwtService;
    private readonly ApprovalTokenUsageStore _usageStore;
    private readonly ICurrentUserService _currentUser;

    // Bu istekte sahiplenilmiş ama henüz tüketilmemiş jti'ler. Aynı istek
    // içinde yeniden doğrulama (ConcurrencyRetryBehavior yeniden denemesi)
    // kendi sahiplendiği token'ı tekrar kullanabilir.
    private readonly HashSet<string> _claimedByThisRequest = new();

    public ApprovalTokenService(
        JwtService jwtService, ApprovalTokenUsageStore usageStore, ICurrentUserService currentUser)
    {
        _jwtService = jwtService;
        _usageStore = usageStore;
        _currentUser = currentUser;
    }

    public string? ValidateApprovalToken(string token, string expectedAction)
    {
        if (string.IsNullOrEmpty(token)) return null;

        var principal = _jwtService.ValidateApprovalToken(token, expectedAction);
        if (principal is null) return null;

        // Onay, onu isteyen kasiyere aittir: başka kullanıcının işleminde geçersiz.
        var requesterId = principal.FindFirst("requesterId")?.Value;
        if (string.IsNullOrEmpty(requesterId) || requesterId != _currentUser.UserId) return null;

        var jwt = TryRead(token);
        if (jwt is null || string.IsNullOrEmpty(jwt.Id)) return null;

        if (!_claimedByThisRequest.Contains(jwt.Id))
        {
            // Atomik sahiplenme: kullanılmış ya da şu an başka istekte olan token reddedilir
            if (!_usageStore.TryClaim(jwt.Id, jwt.ValidTo)) return null;
            _claimedByThisRequest.Add(jwt.Id);
        }

        return principal.FindFirst("approverId")?.Value;
    }

    public void MarkTokenUsed(string token)
    {
        var jwt = TryRead(token);
        if (jwt is null || string.IsNullOrEmpty(jwt.Id)) return;

        _usageStore.MarkUsed(jwt.Id, jwt.ValidTo);
        _claimedByThisRequest.Remove(jwt.Id);
    }

    /// <summary>İstek sonu: tüketilmeden kalan sahiplenmeleri serbest bırak.</summary>
    public void Dispose()
    {
        foreach (var jti in _claimedByThisRequest)
            _usageStore.Release(jti);
        _claimedByThisRequest.Clear();
    }

    private static JwtSecurityToken? TryRead(string token)
    {
        try { return new JwtSecurityTokenHandler().ReadJwtToken(token); }
        catch { return null; }
    }
}

/// <summary>
/// Onay token'larının durumunu (sahiplenildi / kullanıldı) süreleri dolana kadar
/// tutar (singleton, bellek içi). NOT: Tek API instance'ı varsayar; birden fazla
/// instance veya yeniden başlatma sonrası koruma için kalıcı (DB/Redis) depo gerekir.
/// Tek kullanımlık işlemlerde veritabanı kısıtları (örn. tek açık kasa oturumu)
/// ikinci savunma hattıdır.
/// </summary>
public class ApprovalTokenUsageStore
{
    private sealed record Entry(DateTime ExpiresAtUtc, bool Used);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    /// <summary>Token daha önce kullanılmamış ve şu an başka istekte değilse sahiplenir.</summary>
    public bool TryClaim(string jti, DateTime expiresAtUtc)
    {
        Cleanup();
        return _entries.TryAdd(jti, new Entry(expiresAtUtc, Used: false));
    }

    public void MarkUsed(string jti, DateTime expiresAtUtc)
        => _entries[jti] = new Entry(expiresAtUtc, Used: true);

    /// <summary>Sahiplenilmiş ama kullanılmamış token'ı serbest bırakır.</summary>
    public void Release(string jti)
    {
        if (_entries.TryGetValue(jti, out var e) && !e.Used)
            _entries.TryRemove(new KeyValuePair<string, Entry>(jti, e));
    }

    private void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in _entries)
            if (entry.Value.ExpiresAtUtc < now)
                _entries.TryRemove(entry);
    }
}
