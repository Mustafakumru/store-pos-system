using Domain.Common;

namespace Domain.Entities;

public class Store : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    // Mağaza kodu — barkod gibi unique, örn: "LCW-IST-001"
    public string Code { get; set; } = string.Empty;

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    // AppUser FK — nullable, mağaza müdürü sonradan atanabilir
    public string? ManagerId { get; set; }

    // Navigation
    public ICollection<StoreInventory> Inventories { get; set; } = [];
    public ICollection<Sale> Sales { get; set; } = [];
}