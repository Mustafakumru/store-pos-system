using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Domain.Common;

namespace Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = null!;

    /// <summary>Urun kodundaki 2 harfli kategori parcasi (W6[BI]21Z8). Benzersiz.</summary>
    public string? Code { get; set; }
    public string? Description { get; set; }

    // Navigation
    public ICollection<Product> Products { get; set; } = new List<Product>();
}