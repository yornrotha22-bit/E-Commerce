using RothaShop.Services;

namespace RothaShop.Tests;

public class CouponApplicationTests
{
    [Fact]
    public void None_IsInvalidAndChargesNothing()
    {
        var result = CouponApplication.None();

        Assert.False(result.IsValid);
        Assert.Null(result.Code);
        Assert.Equal(0m, result.DiscountAmount);
    }

    [Fact]
    public void Invalid_KeepsTheReason()
    {
        var result = CouponApplication.Invalid("Coupon has expired.");

        Assert.False(result.IsValid);
        Assert.Equal("Coupon has expired.", result.Message);
        Assert.Equal(0m, result.DiscountAmount);
    }

    [Fact]
    public void Valid_CarriesCodeAndDiscount()
    {
        var result = CouponApplication.Valid("WELCOME10", 12.5m);

        Assert.True(result.IsValid);
        Assert.Equal("WELCOME10", result.Code);
        Assert.Equal(12.5m, result.DiscountAmount);
        Assert.Contains("WELCOME10", result.Message);
        Assert.Contains("12.50", result.Message);
    }
}
