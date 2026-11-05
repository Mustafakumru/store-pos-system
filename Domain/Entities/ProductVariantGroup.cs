using Domain.Common;

namespace Domain.Entities;

public class ProductVariantGroup : BaseEntity
{
    public int ProductId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string GroupType { get; set; } = "Color";

    public Product Product { get; set; } = null!;
    public ICollection<ProductVariant> Variants { get; set; } = [];
    public ICollection<ProductVariantGroupImage> Images { get; set; } = [];
}