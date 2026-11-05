using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public bool IsActive { get; set; } = true;  // Hesap askıya alma
    // Kasiyer veya mağaza müdürü ise dolu olur
    // Admin için null
    public int? StoreId { get; set; }

    // ── POS Terminal ─────────────────────────────────────────
    // 6 haneli benzersiz personel kimliği — sistem üretir
    public string? PersonelId { get; set; }

    // PIN hash — default "1234", kasiyer ilk girişte değiştirir
    public string? PinHash { get; set; }

    // İlk girişte PIN değiştirme zorunluluğu
    public bool IsFirstLogin { get; set; } = true;
}