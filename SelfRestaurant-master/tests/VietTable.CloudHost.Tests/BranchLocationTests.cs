using SelfRestaurant.Catalog.Api.Infrastructure;
using SelfRestaurant.Gateway.Api.Services;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>06/11: admin-entered branch position for "Chỉ đường" and distance; nothing is guessed.</summary>
public sealed class BranchLocationTests
{
    [Fact]
    public void A_real_position_or_none_is_accepted()
    {
        Assert.Null(BranchLocationRules.Validate("123 Nguyễn Huệ", 10.776889m, 106.700806m));
        Assert.Null(BranchLocationRules.Validate("123 Nguyễn Huệ", null, null));
    }

    [Theory]
    [InlineData(10.7, null)]
    [InlineData(91.0, 106.0)]
    [InlineData(10.0, 181.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(10.1234567, 106.0)]
    public void Half_missing_out_of_range_zero_or_too_precise_is_refused(double latitude, double? longitude)
    {
        Assert.NotNull(BranchLocationRules.Validate(null, (decimal)latitude, longitude is null ? null : (decimal)longitude.Value));
    }

    [Fact]
    public void Address_longer_than_the_column_is_refused()
    {
        Assert.NotNull(BranchLocationRules.Validate(new string('a', 501), null, null));
    }

    [Fact]
    public void Distance_needs_both_positions()
    {
        Assert.Null(GeoDistance.Km(null, 106.7, 10.8m, 106.7m));
        Assert.Null(GeoDistance.Km(10.7, 106.7, null, null));
        // Quận 1 (Nhà hát TP) → Bình Thạnh (Landmark 81): about 3 km in a straight line.
        var km = GeoDistance.Km(10.776889, 106.703056, 10.794952m, 106.721867m);
        Assert.NotNull(km);
        Assert.InRange(km!.Value, 2.5, 3.2);
    }
}
