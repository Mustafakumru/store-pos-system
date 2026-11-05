using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using WebAPI.Controllers;

namespace WebAPI.Common;

public enum PinCheckResult { Success, Invalid, LockedOut }

/// <summary>
/// POS PIN doğrulaması - kasa girişi, yönetici onayı ve PIN değiştirmede ORTAK.
///
/// • Kilit: 5 hatalı PIN → hesap 15 dk kilitlenir (Identity lockout sayaçları;
///   panel şifresiyle aynı). 4 haneli PIN'de 10.000 olasılık var - kilitsiz
///   deneme-yanılmayla dakikalar içinde bulunabiliyordu.
/// • Tek tip hata mesajı: "kullanıcı yok" ile "PIN hatalı" ayırt edilmez,
///   böylece geçerli personel numaraları tahmin edilemez.
/// </summary>
public static class PinSecurity
{
    public const string InvalidMessage = "Personel numarası veya PIN hatalı.";

    public static async Task<PinCheckResult> VerifyAsync(
        UserManager<AppUser> userManager, AppUser? user, string? pin)
    {
        if (user is null || user.PinHash is null)
        {
            // Zamanlama farkı ile kullanıcı varlığı sızmasın diye aynı maliyette hash hesapla
            PinHasher.VerifyAgainstDummy(pin);
            return PinCheckResult.Invalid;
        }

        if (!await userManager.GetLockoutEnabledAsync(user))
            await userManager.SetLockoutEnabledAsync(user, true);

        if (await userManager.IsLockedOutAsync(user))
            return PinCheckResult.LockedOut;

        if (!PinHasher.Verify(pin, user.PinHash, out var needsRehash))
        {
            await userManager.AccessFailedAsync(user);
            return await userManager.IsLockedOutAsync(user)
                ? PinCheckResult.LockedOut
                : PinCheckResult.Invalid;
        }

        if (await userManager.GetAccessFailedCountAsync(user) > 0)
            await userManager.ResetAccessFailedCountAsync(user);

        // Eski (SHA256) formatta saklanan PIN: doğru girildiği anda PBKDF2'ye yükselt
        if (needsRehash)
        {
            user.PinHash = PinHasher.Hash(pin!);
            await userManager.UpdateAsync(user);
        }

        return PinCheckResult.Success;
    }

    public static string LockedMessage(AppUser? user)
    {
        var minutes = user?.LockoutEnd is { } end
            ? Math.Max(1, (int)Math.Ceiling((end - DateTimeOffset.UtcNow).TotalMinutes))
            : 15;
        return $"Çok fazla hatalı deneme. Hesap {minutes} dakika kilitlendi; " +
               "acil durumda mağaza müdürünüz PIN'inizi sıfırlayabilir.";
    }

    /// <summary>Yeni PIN kuralı: 4-6 rakam, tahmin edilebilir dizi olamaz.</summary>
    public static string? ValidateNewPin(string? pin)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length is < 4 or > 6 || !pin.All(char.IsAsciiDigit))
            return "Yeni PIN 4-6 haneli ve sadece rakamlardan oluşmalıdır.";

        if (pin.Distinct().Count() == 1)
            return "PIN tek bir rakamın tekrarı olamaz (örn. 1111).";

        bool Sequential(int step) => Enumerable.Range(1, pin.Length - 1)
            .All(i => pin[i] - pin[i - 1] == step);
        if (Sequential(1) || Sequential(-1))
            return "PIN ardışık rakamlardan oluşamaz (örn. 1234, 4321).";

        return null;
    }
}
