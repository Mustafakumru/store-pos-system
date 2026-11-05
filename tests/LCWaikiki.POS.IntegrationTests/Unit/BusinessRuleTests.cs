using Application.Common.Validation;
using Application.Features.POS.Commands.ProcessSale;
using Application.Features.POS.Common;
using Domain.Entities;
using Infrastructure.Identity;
using WebAPI.Common;

namespace LCWaikiki.POS.IntegrationTests.Unit;

/// <summary>Veritabanı gerektirmeyen saf iş kuralları (hızlı birim testleri).</summary>
public class PaymentSettlementTests
{
    private static PaymentRequest Pay(PaymentTypeDto type, decimal amount) => new(type, amount);

    [Fact]
    public void Change_is_deducted_from_cash_so_recorded_payments_equal_amount_due()
    {
        var applied = PaymentSettlement.ApplyChange(
            new[] { Pay(PaymentTypeDto.Card, 50), Pay(PaymentTypeDto.Cash, 100) }, 120, out var change);

        Assert.Equal(30, change);
        Assert.Equal(120, applied.Sum(p => p.Amount));
        Assert.Equal(70, applied.Single(p => p.PaymentType == PaymentTypeDto.Cash).Amount);
    }

    [Fact]
    public void Overpayment_by_card_is_rejected()
        => Assert.Throws<InvalidOperationException>(() =>
            PaymentSettlement.ApplyChange(new[] { Pay(PaymentTypeDto.Card, 150) }, 100, out _));

    [Fact]
    public void Cash_line_fully_consumed_by_change_is_dropped()
    {
        var applied = PaymentSettlement.ApplyChange(
            new[] { Pay(PaymentTypeDto.Card, 100), Pay(PaymentTypeDto.Cash, 20) }, 100, out var change);
        Assert.Equal(20, change);
        Assert.Single(applied);
    }
}

public class RefundPricingTests
{
    private static (Sale, SaleItem) SaleWithDocumentDiscount()
    {
        // 2 adet x 50 = 100 TL, %20 belge indirimi -> müşteri 80 TL ödedi
        var item = new SaleItem { Quantity = 2, LineTotal = 100 };
        var sale = new Sale { TotalAmount = 100, DiscountAmount = 20, Items = { item } };
        return (sale, item);
    }

    [Fact]
    public void Refund_uses_amount_actually_paid_after_document_discount()
    {
        var (sale, item) = SaleWithDocumentDiscount();
        Assert.Equal(40, RefundPricing.AmountFor(sale, item, 1));
        Assert.Equal(40, RefundPricing.UnitRefundable(sale, item));
    }

    [Fact]
    public void Last_unit_refund_absorbs_rounding_remainder()
    {
        var item = new SaleItem { Quantity = 3, LineTotal = 100 };
        var sale = new Sale { TotalAmount = 100, DiscountAmount = 0, Items = { item } };

        var first = RefundPricing.AmountFor(sale, item, 2);
        item.RefundedQuantity = 2; item.RefundedAmount = first;
        var last = RefundPricing.AmountFor(sale, item, 1);

        Assert.Equal(100, first + last);   // kuruş kaybı / fazlası yok
    }
}

public class CommonRulesTests
{
    [Theory]
    [InlineData("0555 123 45 67", "5551234567")]
    [InlineData("+90 (555) 123-4567", "5551234567")]
    [InlineData("905551234567", "5551234567")]
    [InlineData("5551234567", "5551234567")]
    public void Phone_is_normalized_to_ten_digits(string input, string expected)
        => Assert.Equal(expected, CommonRules.NormalizePhone(input));

    [Theory]
    [InlineData("0212 123 45 67", true)]
    [InlineData("0555 123 45 67", true)]
    [InlineData("abc", false)]
    [InlineData("123", false)]
    [InlineData("0155 123 45 67", false)]
    public void Phone_validity(string input, bool valid)
        => Assert.Equal(valid, CommonRules.IsValidPhone(input));

    [Theory]
    [InlineData("Şükrü Öztürk-Çelik", true)]
    [InlineData("O'Neil", true)]
    [InlineData("Ali1", false)]
    [InlineData("<script>", false)]
    [InlineData("   ", false)]
    public void Person_name(string input, bool valid)
        => Assert.Equal(valid, CommonRules.IsPersonName(input));
}

public class PinTests
{
    [Fact]
    public void New_pins_use_pbkdf2_with_random_salt()
    {
        var a = PinHasher.Hash("2580");
        var b = PinHasher.Hash("2580");
        Assert.StartsWith("v2$", a);
        Assert.NotEqual(a, b);
        Assert.True(PinHasher.Verify("2580", a, out var rehash));
        Assert.False(rehash);
        Assert.False(PinHasher.Verify("2581", a, out _));
    }

    [Fact]
    public void Legacy_salted_sha256_pin_verifies_and_requests_rehash()
    {
        const string salt = "abc123";
        var hex = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("2580" + salt)));
        Assert.True(PinHasher.Verify("2580", $"{hex}:{salt}", out var rehash));
        Assert.True(rehash);
    }

    [Theory]
    [InlineData("1111", false)]
    [InlineData("1234", false)]
    [InlineData("4321", false)]
    [InlineData("12a4", false)]
    [InlineData("123", false)]
    [InlineData("2580", true)]
    [InlineData("135790", true)]
    public void New_pin_rules(string pin, bool valid)
        => Assert.Equal(valid, PinSecurity.ValidateNewPin(pin) is null);
}
