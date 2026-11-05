using LCWaikiki.POS.WpfClient.Core.Models.Pos;

namespace LCWaikiki.POS.WpfClient.Tests;

/// <summary>Sepet satiri: indirim, taksit farki, iade ve iptal hesaplari.</summary>
public class CartItemTests
{
    private static CartItem Line(decimal price, int qty = 1) => new() { UnitPrice = price, Quantity = qty };

    [Fact]
    public void Plain_line_total_is_price_times_quantity()
        => Assert.Equal(300m, Line(100, 3).LineTotal);

    [Fact]
    public void Percentage_discount_is_rounded_and_amount_discount_is_capped_at_line_total()
    {
        var pct = Line(99.99m);
        pct.DiscountTypeValue = 1; pct.DiscountValue = 10;
        Assert.Equal(10.00m, pct.DiscountAmount);
        Assert.Equal(89.99m, pct.LineTotal);

        var amt = Line(50);
        amt.DiscountTypeValue = 0; amt.DiscountValue = 80;
        Assert.Equal(50m, amt.DiscountAmount);
        Assert.Equal(0m, amt.LineTotal);
    }

    [Fact]
    public void Installment_surcharge_applies_after_discount()
    {
        var line = Line(200);
        line.DiscountTypeValue = 0; line.DiscountValue = 20;
        line.InstallmentCount = 4;
        Assert.Equal(9.00m, line.InstallmentSurchargeAmount);    // (200-20) * %5
        line.InstallmentCount = 8;
        Assert.Equal(18.00m, line.InstallmentSurchargeAmount);   // (200-20) * %10
    }

    [Fact]
    public void Return_line_is_negative_and_ignores_discount_and_installment()
    {
        var ret = new CartItem { UnitPrice = 150, Quantity = 2, IsReturnItem = true };
        ret.DiscountTypeValue = 1; ret.DiscountValue = 50;
        ret.InstallmentCount = 8;

        Assert.Equal(-300m, ret.LineTotal);
        Assert.Equal(0m, ret.DiscountAmount);
        Assert.Equal(0m, ret.InstallmentSurchargeAmount);
    }

    [Fact]
    public void Voided_line_counts_as_zero()
    {
        var line = Line(100, 2);
        line.IsVoided = true;
        Assert.Equal(0m, line.LineTotal);
    }
}
