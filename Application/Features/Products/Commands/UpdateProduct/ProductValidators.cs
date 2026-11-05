using Application.Common.Validation;
using Application.Features.Products.Commands.AddVariant;
using Application.Features.Products.Commands.ManageVariantGroup;
using Application.Features.Products.Commands.UpdateProductPrice;
using Application.Features.Products.Commands.UpdateVariant;
using FluentValidation;

namespace Application.Features.Products.Commands.UpdateProduct;

// Uzunluklar DB kolonlarıyla aynı (ProductConfiguration, ProductVariantConfiguration).

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).ValidId("ürün");
        RuleFor(x => x.Name).RequiredText("Ürün adı", 2, 200);
        RuleFor(x => x.Description).OptionalText("Açıklama", 1000);
        RuleFor(x => x.CategoryId).ValidId("kategori");
        RuleFor(x => x.ImagePath).OptionalText("Resim yolu", 500);
    }
}

public sealed class AddVariantCommandValidator : AbstractValidator<AddVariantCommand>
{
    public AddVariantCommandValidator()
    {
        RuleFor(x => x.ProductId).ValidId("ürün");
        // Barkod biçimi ProductCatalog.ReserveBarcodeAsync'te (tek kaynak) doğrulanır
        RuleFor(x => x.Color).OptionalText("Renk", 50);
        RuleFor(x => x.Size).OptionalText("Beden", 20);
        RuleFor(x => x.ImagePath).OptionalText("Resim yolu", 500);
        RuleFor(x => x.InitialPrice).ValidPrice("Varyant fiyatı");
    }
}

public sealed class UpdateVariantCommandValidator : AbstractValidator<UpdateVariantCommand>
{
    public UpdateVariantCommandValidator()
    {
        RuleFor(x => x.Id).ValidId("varyant");
        RuleFor(x => x.Color).OptionalText("Renk", 50);
        RuleFor(x => x.Size).OptionalText("Beden", 20);
        RuleFor(x => x.ImagePath).OptionalText("Resim yolu", 500);
    }
}

public sealed class UpdateProductPriceCommandValidator : AbstractValidator<UpdateProductPriceCommand>
{
    public UpdateProductPriceCommandValidator()
    {
        RuleFor(x => x.VariantId).ValidId("varyant");
        RuleFor(x => x.NewPrice).ValidPrice("Yeni fiyat");
    }
}

public sealed class CreateVariantGroupCommandValidator : AbstractValidator<CreateVariantGroupCommand>
{
    public CreateVariantGroupCommandValidator()
    {
        RuleFor(x => x.ProductId).ValidId("ürün");
        RuleFor(x => x.GroupName).RequiredText("Grup adı", 1, 100);
        RuleFor(x => x.GroupType).RequiredText("Grup tipi", 1, 50);
    }
}
