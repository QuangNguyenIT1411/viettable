using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Catalog.Api.Persistence;

namespace SelfRestaurant.Catalog.Api.Controllers;

/// <summary>
/// "Thuộc tính món" (owner 06/11): kind, spicy, servings and vegetarian, set by the admin. "Gợi ý món" uses only dishes
/// whose kind is set; nothing is guessed from a dish name.
/// </summary>
[ApiController]
public sealed class DishAttributesController : ControllerBase
{
    public static readonly string[] Courses = { "MAIN", "STARTER", "SOUP", "SIDE", "DESSERT", "DRINK" };

    private readonly CatalogDbContext _db;

    public DishAttributesController(CatalogDbContext db) => _db = db;

    public sealed record DishAttributesRequest(string? Course, bool? IsSpicy, int? Servings, bool? IsVegetarian);

    [HttpGet("api/admin/dish-attributes")]
    public async Task<ActionResult<IReadOnlyList<object>>> List(CancellationToken ct) =>
        Ok(await _db.Dishes.AsNoTracking().Where(x => x.IsActive ?? true)
            .OrderBy(x => x.CategoryID).ThenBy(x => x.Name)
            .Select(x => new
            {
                dishId = x.DishID, name = x.Name, price = x.Price, categoryId = x.CategoryID,
                course = x.Course, isSpicy = x.IsSpicy, servings = x.Servings, isVegetarian = x.IsVegetarian
            }).ToListAsync(ct));

    [HttpPut("api/admin/dishes/{dishId:int}/attributes")]
    public async Task<ActionResult<object>> Save(int dishId, [FromBody] DishAttributesRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { code = "validation_failed", message = "Thiếu dữ liệu." });
        var problem = Validate(request.Course, request.Servings);
        if (problem is not null) return BadRequest(new { code = "validation_failed", message = problem });
        var dish = await _db.Dishes.FirstOrDefaultAsync(x => x.DishID == dishId, ct);
        if (dish is null) return NotFound(new { code = "dish_not_found", message = "Không tìm thấy món." });
        dish.Course = string.IsNullOrWhiteSpace(request.Course) ? null : request.Course.Trim().ToUpperInvariant();
        dish.IsSpicy = request.IsSpicy;
        dish.Servings = request.Servings;
        if (request.IsVegetarian is not null) dish.IsVegetarian = request.IsVegetarian;
        dish.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        return Ok(new { dishId, course = dish.Course, isSpicy = dish.IsSpicy, servings = dish.Servings, isVegetarian = dish.IsVegetarian });
    }

    public static string? Validate(string? course, int? servings)
    {
        if (!string.IsNullOrWhiteSpace(course) && !Courses.Contains(course.Trim().ToUpperInvariant()))
            return "Loại món không hợp lệ.";
        if (servings is not null && (servings < 1 || servings > 20)) return "Khẩu phần phải từ 1 đến 20 người.";
        return null;
    }
}
