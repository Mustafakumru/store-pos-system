using Domain.Common;

namespace Domain.Entities;

public class ProductVariantGroupImage : BaseEntity
{
    public int GroupId { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;

    public ProductVariantGroup Group { get; set; } = null!;
}