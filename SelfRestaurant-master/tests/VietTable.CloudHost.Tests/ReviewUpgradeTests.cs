using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfRestaurant.Customers.Api.Controllers;
using SelfRestaurant.Customers.Api.Infrastructure;
using SelfRestaurant.Customers.Api.Persistence;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>
/// Reviews upgrade (owner 05/11/2026): one review per paid invoice, editable 24 hours, criteria + stars per dish +
/// quick tags + anonymous, the restaurant's reply, hiding with a reason, and dish averages from real reviews only.
/// </summary>
public sealed class ReviewUpgradeTests
{
    private const int Guest = 21;
    private static readonly MobileEngagementController.InvoiceDish[] Dishes =
        { new(1, "Phở bò"), new(2, "Bún chả") };

    // ---- Rules ----

    [Fact]
    public void Edit_window_is_24_hours()
    {
        var created = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(ReviewRules.CanEdit(created, created.AddHours(23).AddMinutes(59)));
        Assert.True(ReviewRules.CanEdit(created, created.AddHours(24)));
        Assert.False(ReviewRules.CanEdit(created, created.AddHours(24).AddSeconds(1)));
    }

    [Fact]
    public void Tags_only_from_the_list_and_kept_in_its_order()
    {
        Assert.Equal(new[] { "Ngon", "Đáng tiền" }, ReviewRules.NormalizeTags(new[] { "Đáng tiền", " Ngon ", "Ngon" }));
        Assert.Null(ReviewRules.NormalizeTags(new[] { "Tuyệt vời ông mặt trời" }));
        Assert.Null(ReviewRules.NormalizeTags(ReviewRules.Tags.Concat(new[] { "x" })));
        Assert.Empty(ReviewRules.NormalizeTags(null)!);
    }

    [Fact]
    public void Dish_stars_only_for_dishes_of_the_invoice_with_the_invoice_name()
    {
        var invoice = new Dictionary<int, string> { [1] = "Phở bò", [2] = "Bún chả" };
        var ok = ReviewRules.NormalizeDishRatings(new[] { (1, 5), (2, 3) }, invoice)!;
        Assert.Equal("Phở bò", ok.Single(x => x.DishId == 1).DishName);
        Assert.Null(ReviewRules.NormalizeDishRatings(new[] { (9, 5) }, invoice));       // not on the bill
        Assert.Null(ReviewRules.NormalizeDishRatings(new[] { (1, 6) }, invoice));       // out of 1–5
        Assert.Null(ReviewRules.NormalizeDishRatings(new[] { (1, 5), (1, 4) }, invoice)); // twice
    }

    [Fact]
    public void Public_name_and_average()
    {
        Assert.Equal("Vinh Q.", ReviewRules.PublicName("quang nguyễn vinh", false));
        Assert.Equal("Khách ẩn danh", ReviewRules.PublicName("quang nguyễn vinh", true));
        Assert.Equal("Khách hàng", ReviewRules.PublicName(null, false));
        Assert.Equal(4.5, ReviewRules.Average(new[] { 5, 4 }));
        Assert.Null(ReviewRules.Average(Array.Empty<int>()));
    }

    // ---- Endpoints (in-memory) ----

    [Fact]
    public async Task One_review_per_invoice_with_dishes_tags_and_anonymous()
    {
        await using var db = Database();
        var api = new MobileEngagementController(db);
        var first = Json(Ok(await api.CreateReview(Guest, Create(10, "k1"), CancellationToken.None)));
        Assert.Equal(new[] { "Ngon", "Nhanh" }, first.GetProperty("tags").EnumerateArray().Select(x => x.GetString()));
        Assert.True(first.GetProperty("isAnonymous").GetBoolean());
        Assert.Equal(2, first.GetProperty("dishRatings").GetArrayLength());
        Assert.True(first.GetProperty("canEdit").GetBoolean());
        var second = await api.CreateReview(Guest, Create(10, "k2"), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(second.Result);
        Assert.Equal(1, await db.CustomerReviews.CountAsync());
        // Unknown dish or tag: refused before anything is written.
        var bad = Create(11, "k3") with { DishRatings = new[] { new MobileEngagementController.DishRatingInput(99, 5) } };
        Assert.IsType<BadRequestObjectResult>((await api.CreateReview(Guest, bad, CancellationToken.None)).Result);
    }

    [Fact]
    public async Task Edit_within_24_hours_then_locked()
    {
        await using var db = Database();
        var api = new MobileEngagementController(db);
        var created = Json(Ok(await api.CreateReview(Guest, Create(10, "k1"), CancellationToken.None)));
        var id = created.GetProperty("reviewId").GetInt32();
        var update = new MobileEngagementController.UpdateReviewRequest(3, 3, 4, 4, "Sửa lại", null, new[] { "Cần cải thiện" }, false,
            new[] { new MobileEngagementController.DishRatingInput(1, 2) });
        var edited = Json(Ok(await api.UpdateReview(Guest, id, update, CancellationToken.None)));
        Assert.Equal(3, edited.GetProperty("overallRating").GetInt32());
        Assert.Single(edited.GetProperty("dishRatings").EnumerateArray());
        Assert.False(edited.GetProperty("isAnonymous").GetBoolean());

        var row = await db.CustomerReviews.SingleAsync();
        row.CreatedAtUtc = DateTime.UtcNow.AddHours(-25);
        await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>((await api.UpdateReview(Guest, id, update, CancellationToken.None)).Result);
        Assert.IsType<ConflictObjectResult>(await api.DeleteReview(Guest, id, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_reply_reaches_the_guest_and_hiding_needs_a_reason_and_leaves_the_averages()
    {
        await using var db = Database();
        var api = new MobileEngagementController(db);
        var admin = new AdminReviewsController(db);
        var a = Json(Ok(await api.CreateReview(Guest, Create(10, "k1"), CancellationToken.None))).GetProperty("reviewId").GetInt32();
        var other = Create(11, "k2") with { DishRatings = new[] { new MobileEngagementController.DishRatingInput(1, 1) }, Comment = "Dở", IsAnonymous = false };
        var b = Json(Ok(await api.CreateReview(22, other, CancellationToken.None))).GetProperty("reviewId").GetInt32();

        Ok(await admin.Reply(a, new("Cảm ơn bạn đã ghé!", 3, "Quản lý"), CancellationToken.None));
        var mine = Json(Ok(await api.Reviews(Guest, CancellationToken.None))).GetProperty("items")[0];
        Assert.Equal("Cảm ơn bạn đã ghé!", mine.GetProperty("replyText").GetString());

        // Before hiding: dish 1 has 5★ and 1★.
        var summary = Json(Ok(await api.DishReviewSummary(1, 3, CancellationToken.None)));
        Assert.Equal(2, summary.GetProperty("count").GetInt32());
        Assert.Equal(3.0, summary.GetProperty("average").GetDouble());

        Assert.IsType<BadRequestObjectResult>((await admin.Visibility(b, new(true, " ", 3, "Quản lý"), CancellationToken.None)).Result);
        Ok(await admin.Visibility(b, new(true, "Ngôn từ không phù hợp", 3, "Quản lý"), CancellationToken.None));
        var hidden = await db.CustomerReviews.SingleAsync(x => x.CustomerReviewId == b);
        Assert.Equal("Quản lý", hidden.HiddenByName);
        Assert.NotNull(hidden.HiddenAtUtc);

        summary = Json(Ok(await api.DishReviewSummary(1, 3, CancellationToken.None)));
        Assert.Equal(1, summary.GetProperty("count").GetInt32());
        Assert.Equal(5.0, summary.GetProperty("average").GetDouble());
        Assert.Equal("Khách ẩn danh", summary.GetProperty("latest")[0].GetProperty("authorName").GetString());
        var stats = Json(Ok(await admin.Stats(CancellationToken.None)));
        Assert.Equal(1, stats.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Dish_without_reviews_says_none()
    {
        await using var db = Database();
        var summary = Json(Ok(await new MobileEngagementController(db).DishReviewSummary(5, 3, CancellationToken.None)));
        Assert.Equal(0, summary.GetProperty("count").GetInt32());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("average").ValueKind);
    }

    [Fact]
    public void Review_reminder_is_a_payment_notification()
    {
        Assert.Equal("payment", CustomerInbox.Category(CustomerInbox.ReviewReminder));
        Assert.Equal("Đánh giá bữa ăn", CustomerInbox.Title(CustomerInbox.ReviewReminder));
    }

    private static MobileEngagementController.CreateReviewRequest Create(int invoiceId, string key) => new(
        invoiceId, invoiceId + 100, 5, 5, 4, 4, "Ngon lắm", null, key, new string('a', 64),
        new[] { "Nhanh", "Ngon" }, true,
        new[] { new MobileEngagementController.DishRatingInput(1, 5), new MobileEngagementController.DishRatingInput(2, 4) },
        BranchId: 1, AuthorName: "quang nguyễn vinh", InvoiceDishes: Dishes);

    private static object Ok<T>(ActionResult<T> result) => Assert.IsType<OkObjectResult>(result.Result).Value!;
    // The API writes camelCase (ASP.NET Core web defaults); read it the same way.
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static CustomersDbContext Database() => new(new DbContextOptionsBuilder<CustomersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
}
