using SelfRestaurant.Customers.Api.Controllers;
using Xunit;

namespace SelfRestaurant.Backend.ContractTests;

// Reviews saved by older apps may hold the word "null"; the API must store and return a real null instead.
public sealed class ReviewCommentCleanTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData(" NULL ")]
    [InlineData("undefined")]
    public void Meaningless_comments_become_null(string? value) => Assert.Null(MobileEngagementController.Clean(value));

    [Fact]
    public void Real_comments_are_trimmed_and_kept() => Assert.Equal("Món ngon", MobileEngagementController.Clean("  Món ngon "));
}
