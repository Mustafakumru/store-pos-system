using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

/// <summary>
/// POS PIN'lerinin TEK hash/doğrulama noktası (API, seed, PIN sıfırlama).
///
/// Format "v2$..." = ASP.NET Core Identity PasswordHasher (PBKDF2-HMAC-SHA512,
/// rastgele tuz, 100.000 tur). Eskiden tek tur SHA256 kullanılıyordu: 4-6 haneli
/// bir PIN'in en fazla 1.000.000 olasılığı vardır, veritabanı sızarsa tek tur
/// SHA256 ile tüm PIN'ler saniyeler içinde kırılırdı. Yavaş (iteratif) hash ile
/// her deneme pahalıdır.
///
/// Eski formatlar okunmaya devam eder (geçiş dönemi):
///   "HASH"       - sabit tuzlu SHA256 (ilk sürüm)
///   "HASH:TUZ"   - kullanıcıya özel tuzlu SHA256
/// Eski formatla doğrulanan PIN <c>needsRehash=true</c> döner; çağıran (PinSecurity)
/// başarılı girişte yeni formata yükseltir - kullanıcıdan bir şey istemeden.
/// </summary>
public static class PinHasher
{
    private const string V2Prefix = "v2$";
    private const string LegacyPinSalt = "LCW_POS_SALT";

    private static readonly PasswordHasher<AppUser> Hasher = new();
    private static readonly AppUser HashSubject = new();   // varsayılan hasher kullanıcıyı kullanmaz

    /// <summary>Kullanıcı yokken de aynı maliyette doğrulama yapmak için (zamanlama sızıntısı olmasın).</summary>
    private static readonly Lazy<string> DummyHash = new(() => Hash("000000"));

    public static string Hash(string pin) => V2Prefix + Hasher.HashPassword(HashSubject, pin);

    public static bool Verify(string? pin, string? storedHash, out bool needsRehash)
    {
        needsRehash = false;
        if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(storedHash)) return false;

        if (storedHash.StartsWith(V2Prefix, StringComparison.Ordinal))
        {
            var result = Hasher.VerifyHashedPassword(HashSubject, storedHash[V2Prefix.Length..], pin);
            needsRehash = result == PasswordVerificationResult.SuccessRehashNeeded;
            return result != PasswordVerificationResult.Failed;
        }

        // ── Eski SHA256 formatları ──
        var parts = storedHash.Split(':');
        var computed = parts.Length switch
        {
            1 => Sha256Hex(pin + LegacyPinSalt),
            2 => Sha256Hex(pin + parts[1]),
            _ => null
        };
        if (computed is null) return false;

        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(parts[0].ToUpperInvariant()));
        needsRehash = ok;
        return ok;
    }

    /// <summary>Kullanıcı bulunamadığında: gerçek doğrulamayla aynı süre harcanır.</summary>
    public static void VerifyAgainstDummy(string? pin) => Verify(pin ?? string.Empty, DummyHash.Value, out _);

    private static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
