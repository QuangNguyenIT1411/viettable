using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfRestaurant.Catalog.Api.Controllers;
using SelfRestaurant.Catalog.Api.Infrastructure.Auditing;
using SelfRestaurant.Catalog.Api.Infrastructure.Inventory;
using SelfRestaurant.Catalog.Api.Persistence;
using SelfRestaurant.Catalog.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>
/// Owner 08/11: "Bếp → Tạm ngưng món" said "Món không thuộc thực đơn chi nhánh hôm nay" for a dish on sale in the app.
/// The app's menu is every active dish of an active category (dishes belong to every branch); the chef action also
/// required a row in the branch's dated menu table, which dishes added later (and the UTC date before 07:00 in
/// Vietnam) do not have. "On sale" must mean the same thing on both sides.
/// </summary>
public sealed class ChefDishAvailabilityTests
{
    private static CatalogDbContext Database() => new(new DbContextOptionsBuilder<CatalogDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static AdminCatalogController Controller(CatalogDbContext db) => new(db, new IngredientStockAvailabilityService(db),
        new BusinessAuditLogger(db, new RequestActorContextAccessor(new HttpContextAccessor())));

    private static async Task<Dishes> Seed(CatalogDbContext db, bool inBranchMenu, DateOnly? menuDate = null)
    {
        db.Branches.Add(new Branches { BranchID = 1, Name = "Chi nhánh Quận 1", IsActive = true, RestaurantID = 1 });
        var category = new Categories { CategoryID = 3, Name = "Món chay", IsActive = true };
        db.Categories.Add(category);
        var dish = new Dishes { DishID = 55, Name = "Đậu hũ sốt cà chua", Price = 55000, CategoryID = 3, IsActive = true, Available = true };
        db.Dishes.Add(dish);
        if (inBranchMenu)
        {
            var menu = new Menus { MenuID = 9, MenuName = "Thực đơn", BranchID = 1, IsActive = true, Date = menuDate ?? DateOnly.FromDateTime(DateTime.UtcNow) };
            var mc = new MenuCategory { MenuCategoryID = 90, MenuID = 9, CategoryID = 3, IsActive = true };
            db.Menus.Add(menu);
            db.MenuCategory.Add(mc);
            db.CategoryDish.Add(new CategoryDish { CategoryDishID = 900, MenuCategoryID = 90, DishID = 55, IsAvailable = true });
        }
        await db.SaveChangesAsync();
        return dish;
    }

    private static string Message(ActionResult<AdminCatalogController.ChefDishAvailabilityResponse> r) =>
        System.Text.Json.JsonSerializer.Serialize((r.Result as ObjectResult)?.Value ?? r.Value);

    [Fact]
    public async Task Dish_for_every_branch_without_a_branch_menu_row_can_be_paused_and_resumed()
    {
        await using var db = Database();
        var dish = await Seed(db, inBranchMenu: false);
        var paused = await Controller(db).SetChefDishAvailability(1, 55, new AdminCatalogController.ChefDishAvailabilityRequest(false));
        Assert.DoesNotContain("thực đơn chi nhánh", Message(paused));
        Assert.False(dish.Available); // what the app's menu reads → shown as sold out at once
        var resumed = await Controller(db).SetChefDishAvailability(1, 55, new AdminCatalogController.ChefDishAvailabilityRequest(true));
        Assert.True(dish.Available, Message(resumed));
    }

    [Fact]
    public async Task Dish_in_the_branch_menu_also_updates_its_menu_row()
    {
        await using var db = Database();
        var dish = await Seed(db, inBranchMenu: true);
        await Controller(db).SetChefDishAvailability(1, 55, new AdminCatalogController.ChefDishAvailabilityRequest(false));
        Assert.False(dish.Available);
        Assert.False((await db.CategoryDish.SingleAsync()).IsAvailable);
    }

    [Fact]
    public async Task Branch_menu_dated_by_the_vietnam_day_works_between_23h_and_07h()
    {
        // Between 00:00 and 07:00 in Vietnam the UTC date is the day before: the branch menu row of "today" (VN) must still count.
        await using var db = Database();
        var dish = await Seed(db, inBranchMenu: true, menuDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
        var paused = await Controller(db).SetChefDishAvailability(1, 55, new AdminCatalogController.ChefDishAvailabilityRequest(false));
        Assert.False(dish.Available, Message(paused));
        Assert.Equal(1, await db.Menus.CountAsync()); // no extra dated menu is created by a pause
    }

    [Fact]
    public async Task Hidden_dish_or_category_cannot_be_resumed_and_unknown_dish_is_not_found()
    {
        await using var db = Database();
        var dish = await Seed(db, inBranchMenu: false);
        dish.IsActive = false;
        await db.SaveChangesAsync();
        var r = await Controller(db).SetChefDishAvailability(1, 55, new AdminCatalogController.ChefDishAvailabilityRequest(true));
        Assert.IsType<BadRequestObjectResult>(r.Result);
        Assert.IsType<NotFoundObjectResult>((await Controller(db).SetChefDishAvailability(1, 999, new AdminCatalogController.ChefDishAvailabilityRequest(false))).Result);
    }
}
