using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Catalog.Api.Persistence.Entities;

namespace SelfRestaurant.Catalog.Api.Persistence;

internal static class CatalogPostgresSeed
{
    public static async Task RunAsync(CatalogDbContext db, string profile, CancellationToken ct)
    {
        if (profile == "None") return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var statuses = new[] { ("AVAILABLE", "Trống"), ("OCCUPIED", "Đang dùng"),
            ("RESERVED", "Đặt trước"), ("INACTIVE", "Không hoạt động") };
        foreach (var (code, name) in statuses)
            if (!await db.TableStatus.AnyAsync(x => x.StatusCode == code, ct))
                db.TableStatus.Add(new TableStatus { StatusCode = code, StatusName = name });
        await db.SaveChangesAsync(ct);
        // Demo is explicitly opt-in and empty-Catalog only. Never repair/overwrite
        // migrated business data, create credentials, or seed transactional rows.
        if (profile == "Demo" && !await db.Branches.AnyAsync(ct))
        {
            var now = DateTime.UtcNow;
            var restaurant = new Restaurants { Name = "VietTable", IsActive = true, CreatedAt = now };
            db.Set<Restaurants>().Add(restaurant);
            await db.SaveChangesAsync(ct);
            var branch = new Branches { RestaurantID = restaurant.RestaurantID, Name = "Chi nhánh Quận 1",
                Location = "Quận 1, TP. Hồ Chí Minh", IsActive = true, CreatedAt = now,
                Image = "/images/branches/branch-quan-1-cover.webp" };
            var category = new Categories { Name = "Món chính", IsActive = true, CreatedAt = now };
            db.Branches.Add(branch); db.Categories.Add(category);
            await db.SaveChangesAsync(ct);
            var available = await db.TableStatus.SingleAsync(x => x.StatusCode == "AVAILABLE", ct);
            db.DiningTables.Add(new DiningTables { BranchID = branch.BranchID, StatusID = available.StatusID,
                TableNumber = 1, NumberOfSeats = 4, QRCode = "VT-DEMO-" + Guid.NewGuid().ToString("N"),
                IsActive = true, CreatedAt = now });
            var dish = new Dishes { CategoryID = category.CategoryID, Name = "Bánh mì ốp la", Price = 35000m,
                Unit = "Phần", Available = true, IsActive = true, CreatedAt = now,
                Image = "/images/dishes/banh-mi-op-la.jpg" };
            var menu = new Menus { BranchID = branch.BranchID, MenuName = "Thực đơn demo", IsActive = true,
                CreatedAt = now }; // No date expiry: a demo menu, not a fake daily schedule.
            db.Dishes.Add(dish); db.Menus.Add(menu);
            await db.SaveChangesAsync(ct);
            var menuCategory = new MenuCategory { MenuID = menu.MenuID, CategoryID = category.CategoryID,
                IsActive = true, CreatedAt = now };
            db.MenuCategory.Add(menuCategory);
            await db.SaveChangesAsync(ct);
            db.CategoryDish.Add(new CategoryDish { MenuCategoryID = menuCategory.MenuCategoryID,
                DishID = dish.DishID, IsAvailable = true, CreatedAt = now });
            var ingredients = new[] { ("Bánh mì", "ổ", 1m), ("Trứng gà", "quả", 2m), ("Dưa chuột", "kg", .04m) };
            foreach (var (name, unit, quantity) in ingredients)
            {
                var ingredient = new Ingredients { Name = name, Unit = unit, CurrentStock = 100m,
                    ReorderLevel = 10m, IsActive = true, IssueMethod = "FIFO" };
                db.Ingredients.Add(ingredient);
                await db.SaveChangesAsync(ct);
                db.DishIngredients.Add(new DishIngredients { DishID = dish.DishID,
                    IngredientID = ingredient.IngredientID, QuantityPerDish = quantity });
            }
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
