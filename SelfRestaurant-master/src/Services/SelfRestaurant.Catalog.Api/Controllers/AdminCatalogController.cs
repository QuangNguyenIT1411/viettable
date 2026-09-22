using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Catalog.Api.Infrastructure.Inventory;
using SelfRestaurant.Catalog.Api.Persistence;
using SelfRestaurant.Catalog.Api.Persistence.Entities;
using System.Data;

namespace SelfRestaurant.Catalog.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminCatalogController : ControllerBase
{
    private const int InventoryNearExpiryDays = 7;
    private readonly CatalogDbContext _db;
    private readonly IngredientStockAvailabilityService _stockAvailability;
    private readonly SelfRestaurant.Catalog.Api.Infrastructure.Auditing.BusinessAuditLogger _auditLogger;

    public AdminCatalogController(
        CatalogDbContext db,
        IngredientStockAvailabilityService stockAvailability,
        SelfRestaurant.Catalog.Api.Infrastructure.Auditing.BusinessAuditLogger auditLogger)
    {
        _db = db;
        _stockAvailability = stockAvailability;
        _auditLogger = auditLogger;
    }

    [HttpGet("dishes")]
    public async Task<ActionResult<PagedResponse<AdminDishResponse>>> GetDishes(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = true,
        [FromQuery] bool vegetarianOnly = false,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Dishes
            .AsNoTracking()
            .Include(d => d.Category)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(d => (d.IsActive ?? false) == true);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim();
            query = query.Where(d =>
                d.Name.Contains(key) ||
                (d.Description != null && d.Description.Contains(key)));
        }

        if (categoryId is > 0)
        {
            query = query.Where(d => d.CategoryID == categoryId.Value);
        }

        if (vegetarianOnly)
        {
            query = query.Where(d => (d.IsVegetarian ?? false) == true);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var rows = await query
            .OrderBy(d => d.Category.Name)
            .ThenBy(d => d.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.DishID,
                d.Name,
                d.Price,
                d.CategoryID,
                CategoryName = d.Category.Name,
                d.Description,
                d.Unit,
                d.Image,
                IsVegetarian = d.IsVegetarian ?? false,
                IsDailySpecial = d.IsDailySpecial ?? false,
                Available = d.Available ?? true,
                IsActive = d.IsActive ?? false
            })
            .ToListAsync(cancellationToken);

        var dishIds = rows.Select(d => d.DishID).ToArray();
        var ingredientRows = await _db.DishIngredients
            .AsNoTracking()
            .Include(di => di.Ingredient)
            .Where(di => dishIds.Contains(di.DishID))
            .OrderBy(di => di.Ingredient.Name)
            .Select(di => new { di.DishID, di.Ingredient.Name })
            .ToListAsync(cancellationToken);
        var ingredientSummaries = ingredientRows
            .GroupBy(x => x.DishID)
            .ToDictionary(x => x.Key, x => BuildIngredientSummary(x.Select(i => i.Name)));

        var items = rows.Select(d => new AdminDishResponse(
            d.DishID,
            d.Name,
            d.Price,
            d.CategoryID,
            d.CategoryName,
            d.Description,
            d.Unit,
            d.Image,
            d.IsVegetarian,
            d.IsDailySpecial,
            d.Available,
            d.IsActive,
            ingredientSummaries.TryGetValue(d.DishID, out var summary) ? summary : "-")).ToList();

        return Ok(new PagedResponse<AdminDishResponse>(page, pageSize, totalItems, totalPages, items));
    }

    [HttpGet("dishes/{dishId:int}")]
    public async Task<ActionResult<AdminDishResponse>> GetDishById(int dishId, CancellationToken cancellationToken = default)
    {
        var dishRow = await _db.Dishes
            .AsNoTracking()
            .Include(d => d.Category)
            .Where(d => d.DishID == dishId)
            .Select(d => new
            {
                d.DishID,
                d.Name,
                d.Price,
                d.CategoryID,
                CategoryName = d.Category.Name,
                d.Description,
                d.Unit,
                d.Image,
                IsVegetarian = d.IsVegetarian ?? false,
                IsDailySpecial = d.IsDailySpecial ?? false,
                Available = d.Available ?? true,
                IsActive = d.IsActive ?? false
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (dishRow is null)
        {
            return NotFound(new { message = "Không tìm thấy món ăn." });
        }

        var ingredients = await _db.DishIngredients
            .AsNoTracking()
            .Include(di => di.Ingredient)
            .Where(di => di.DishID == dishId)
            .OrderBy(di => di.Ingredient.Name)
            .Select(di => di.Ingredient.Name)
            .ToListAsync(cancellationToken);
        var dish = new AdminDishResponse(
            dishRow.DishID,
            dishRow.Name,
            dishRow.Price,
            dishRow.CategoryID,
            dishRow.CategoryName,
            dishRow.Description,
            dishRow.Unit,
            dishRow.Image,
            dishRow.IsVegetarian,
            dishRow.IsDailySpecial,
            dishRow.Available,
            dishRow.IsActive,
            BuildIngredientSummary(ingredients));

        return dish is null ? NotFound(new { message = "Không tìm thấy món ăn." }) : Ok(dish);
    }

    [HttpPost("dishes")]
    public async Task<ActionResult> CreateDish([FromBody] AdminUpsertDishRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateDishRequest(request, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var entity = new Dishes
        {
            Name = request.Name!.Trim(),
            Price = request.Price!.Value,
            CategoryID = request.CategoryId!.Value,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
            Image = string.IsNullOrWhiteSpace(request.Image) ? null : request.Image.Trim(),
            IsVegetarian = request.IsVegetarian ?? false,
            IsDailySpecial = request.IsDailySpecial ?? false,
            Available = request.Available ?? true,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _db.Dishes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        _auditLogger.Add(
            actionType: "DISH_CREATED",
            entityType: "DISH",
            entityId: entity.DishID.ToString(),
            dishId: entity.DishID,
            beforeState: null,
            afterState: new
            {
                entity.Name,
                entity.Price,
                entity.CategoryID,
                entity.Available,
                entity.IsActive
            });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã tạo món ăn.", dishId = entity.DishID });
    }

    [HttpPost("branches/{branchId:int}/chef/dishes")]
    public async Task<ActionResult<ChefDishMutationResponse>> CreateChefDishForBranch(
        int branchId,
        [FromBody] AdminUpsertDishRequest request,
        CancellationToken cancellationToken = default)
    {
        if (branchId > 0)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Chef role cannot create dishes." });
        }

        var branchExists = await _db.Branches.AnyAsync(
            b => b.BranchID == branchId && (b.IsActive ?? false),
            cancellationToken);
        if (!branchExists)
        {
            return BadRequest(new { message = "Chi nhánh không hợp lệ." });
        }

        var validation = await ValidateDishRequest(request, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var entity = new Dishes
        {
            Name = request.Name!.Trim(),
            Price = request.Price!.Value,
            CategoryID = request.CategoryId!.Value,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
            Image = string.IsNullOrWhiteSpace(request.Image) ? null : request.Image.Trim(),
            IsVegetarian = request.IsVegetarian ?? false,
            IsDailySpecial = request.IsDailySpecial ?? false,
            Available = request.Available ?? true,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _db.Dishes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        _auditLogger.Add(
            actionType: "DISH_CREATED_FOR_BRANCH",
            entityType: "DISH",
            entityId: entity.DishID.ToString(),
            dishId: entity.DishID,
            beforeState: null,
            afterState: new
            {
                branchId,
                entity.Name,
                entity.Price,
                entity.CategoryID,
                entity.Available,
                entity.IsActive
            });
        await _db.SaveChangesAsync(cancellationToken);

        var todayMenu = await EnsureTodayMenuAsync(branchId, cancellationToken);
        var menuCategory = await EnsureMenuCategoryAsync(todayMenu.MenuID, entity.CategoryID, cancellationToken);
        await EnsureCategoryDishAsync(menuCategory.MenuCategoryID, entity.DishID, entity.Available ?? true, cancellationToken);

        return Ok(new ChefDishMutationResponse(entity.DishID, "Created and attached to today's menu."));
    }

    [HttpPut("branches/{branchId:int}/chef/dishes/{dishId:int}")]
    public async Task<ActionResult<ChefDishMutationResponse>> UpdateChefDishForBranch(
        int branchId,
        int dishId,
        [FromBody] AdminUpsertDishRequest request,
        CancellationToken cancellationToken = default)
    {
        if (branchId > 0)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Chef role cannot edit dishes." });
        }

        var branchExists = await _db.Branches.AnyAsync(
            b => b.BranchID == branchId && (b.IsActive ?? false),
            cancellationToken);
        if (!branchExists)
        {
            return BadRequest(new { message = "Chi nhánh không hợp lệ." });
        }

        var entity = await _db.Dishes.FirstOrDefaultAsync(d => d.DishID == dishId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy món ăn." });
        }

        var validation = await ValidateDishRequest(request, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var beforeAudit = new
        {
            entity.Name,
            entity.Price,
            entity.CategoryID,
            entity.Available,
            entity.IsActive
        };

        entity.Name = request.Name!.Trim();
        entity.Price = request.Price!.Value;
        entity.CategoryID = request.CategoryId!.Value;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim();
        entity.Image = string.IsNullOrWhiteSpace(request.Image) ? null : request.Image.Trim();
        entity.IsVegetarian = request.IsVegetarian ?? false;
        entity.IsDailySpecial = request.IsDailySpecial ?? false;
        entity.Available = request.Available ?? true;
        entity.IsActive = request.IsActive ?? true;
        entity.UpdatedAt = DateTime.Now;
        _auditLogger.Add(
            actionType: "DISH_UPDATED_FOR_BRANCH",
            entityType: "DISH",
            entityId: entity.DishID.ToString(),
            dishId: entity.DishID,
            beforeState: beforeAudit,
            afterState: new
            {
                request.Name,
                request.Price,
                request.CategoryId,
                entity.Available,
                entity.IsActive,
                branchId
            });

        var todayMenu = await EnsureTodayMenuAsync(branchId, cancellationToken);
        var targetCategory = await EnsureMenuCategoryAsync(todayMenu.MenuID, entity.CategoryID, cancellationToken);
        await EnsureCategoryDishAsync(targetCategory.MenuCategoryID, entity.DishID, entity.Available ?? true, cancellationToken);

        var staleLinks = await _db.CategoryDish
            .Include(cd => cd.MenuCategory)
            .Where(cd =>
                cd.DishID == entity.DishID &&
                cd.MenuCategory.MenuID == todayMenu.MenuID &&
                cd.MenuCategoryID != targetCategory.MenuCategoryID)
            .ToListAsync(cancellationToken);

        if (staleLinks.Count > 0)
        {
            _db.CategoryDish.RemoveRange(staleLinks);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new ChefDishMutationResponse(entity.DishID, "Updated and synced to today's menu."));
    }

    [HttpPost("branches/{branchId:int}/chef/dishes/{dishId:int}/availability")]
    public async Task<ActionResult<ChefDishAvailabilityResponse>> SetChefDishAvailability(
        int branchId,
        int dishId,
        [FromBody] ChefDishAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var branchExists = await _db.Branches.AnyAsync(
            b => b.BranchID == branchId && (b.IsActive ?? false),
            cancellationToken);
        if (!branchExists)
        {
            return BadRequest(new { message = "Chi nhánh không hợp lệ." });
        }

        var dish = await _db.Dishes
            .Include(d => d.Category)
            .FirstOrDefaultAsync(d => d.DishID == dishId, cancellationToken);
        if (dish is null)
        {
            return NotFound(new { message = "Không tìm thấy món ăn." });
        }

        var todayMenu = await EnsureTodayMenuAsync(branchId, cancellationToken);
        var menuLink = await _db.CategoryDish
            .Include(cd => cd.MenuCategory)
            .ThenInclude(mc => mc.Category)
            .FirstOrDefaultAsync(cd =>
                cd.DishID == dishId &&
                cd.MenuCategory.MenuID == todayMenu.MenuID,
                cancellationToken);
        if (menuLink is null)
        {
            return BadRequest(new { message = "Món không thuộc thực đơn chi nhánh hôm nay." });
        }

        if (request.Available)
        {
            if (!(dish.IsActive ?? true))
            {
                return BadRequest(new { message = "Món ăn đang bị vô hiệu, Admin cần kích hoạt lại trước khi bán." });
            }

            if (!(dish.Category?.IsActive ?? true) || !(menuLink.MenuCategory.Category.IsActive ?? true))
            {
                return BadRequest(new { message = "Danh mục của món đang bị vô hiệu, Admin cần kích hoạt danh mục trước khi bán." });
            }

            if (!(menuLink.MenuCategory.IsActive ?? true))
            {
                return BadRequest(new { message = "Danh mục trong thực đơn hôm nay đang bị vô hiệu." });
            }

            var recipeRows = await _db.DishIngredients
                .AsNoTracking()
                .Include(di => di.Ingredient)
                .Where(di => di.DishID == dishId)
                .Select(di => new
                {
                    di.IngredientID,
                    di.Ingredient.Name,
                    di.Ingredient.Unit,
                    di.QuantityPerDish,
                    di.Ingredient.IsActive
                })
                .ToListAsync(cancellationToken);

            var availabilityMap = await _stockAvailability.BuildIngredientStockAvailabilityMapAsync(
                recipeRows.Select(r => r.IngredientID),
                cancellationToken);
            var ingredientBlockers = recipeRows
                .GroupBy(r => r.IngredientID)
                .Select(g =>
                {
                    var first = g.First();
                    var availableQuantity = availabilityMap.TryGetValue(g.Key, out var stock)
                        ? stock.AvailabilityStock
                        : 0;
                    return new
                    {
                        first.Name,
                        first.Unit,
                        first.IsActive,
                        RequiredQuantity = g.Sum(x => x.QuantityPerDish),
                        AvailableQuantity = availableQuantity
                    };
                })
                .Where(x => !x.IsActive || x.AvailableQuantity < x.RequiredQuantity)
                .ToList();

            if (ingredientBlockers.Count > 0)
            {
                var first = ingredientBlockers[0];
                var reason = first.IsActive
                    ? $"Nguyên liệu {first.Name} không đủ tồn kho khả dụng: cần {first.RequiredQuantity:0.##} {first.Unit}, hiện còn {first.AvailableQuantity:0.##} {first.Unit}."
                    : $"Nguyên liệu {first.Name} đang bị vô hiệu.";
                return BadRequest(new { message = reason });
            }
        }

        dish.Available = request.Available;
        dish.UpdatedAt = DateTime.Now;
        menuLink.IsAvailable = request.Available;
        menuLink.UpdatedAt = DateTime.Now;

        _auditLogger.Add(
            actionType: request.Available ? "DISH_SELLING_RESUMED_BY_CHEF" : "DISH_SELLING_PAUSED_BY_CHEF",
            entityType: "DISH",
            entityId: dish.DishID.ToString(),
            branchId: branchId,
            dishId: dish.DishID,
            beforeState: null,
            afterState: new
            {
                branchId,
                menuId = todayMenu.MenuID,
                categoryDishId = menuLink.CategoryDishID,
                available = request.Available,
                dishName = dish.Name
            });

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new ChefDishAvailabilityResponse(
            true,
            request.Available ? "Đã hiển thị lại món ăn." : "Đã ẩn món khỏi thực đơn.",
            request.Available));
    }

    [HttpGet("branches/{branchId:int}/chef/{chefId:int}/activity-logs")]
    public async Task<ActionResult<object>> GetChefActivityLogs(
        int branchId,
        int chefId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        days = Math.Clamp(days, 1, 365);
        var cutoffDate = DateTime.UtcNow.AddDays(-days);

        var query = _db.BusinessAuditLogs
            .AsNoTracking()
            .Where(x => x.BranchId == branchId 
                && x.ActorId == chefId
                && x.CreatedAtUtc >= cutoffDate
                && (x.ActionType == "DISH_SELLING_PAUSED_BY_CHEF" || x.ActionType == "DISH_SELLING_RESUMED_BY_CHEF"));

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var logs = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                auditId = x.BusinessAuditLogId,
                timestampUtc = x.CreatedAtUtc,
                actionType = x.ActionType,
                dishId = x.DishId,
                actorType = x.ActorType,
                actorId = x.ActorId,
                actorName = x.ActorName,
                notes = x.Notes,
                afterState = x.AfterState
            })
            .ToListAsync(cancellationToken);

        return Ok(new { page, pageSize, totalItems, totalPages, logs });
    }

    [HttpPut("dishes/{dishId:int}")]
    public async Task<ActionResult> UpdateDish(int dishId, [FromBody] AdminUpsertDishRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Dishes.FirstOrDefaultAsync(d => d.DishID == dishId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy món ăn." });
        }

        var validation = await ValidateDishRequest(request, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var beforeAudit = new
        {
            entity.Name,
            entity.Price,
            entity.CategoryID,
            entity.Available,
            entity.IsActive
        };

        entity.Name = request.Name!.Trim();
        entity.Price = request.Price!.Value;
        entity.CategoryID = request.CategoryId!.Value;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim();
        entity.Image = string.IsNullOrWhiteSpace(request.Image) ? null : request.Image.Trim();
        entity.IsVegetarian = request.IsVegetarian ?? false;
        entity.IsDailySpecial = request.IsDailySpecial ?? false;
        entity.Available = request.Available ?? true;
        entity.IsActive = request.IsActive ?? true;
        entity.UpdatedAt = DateTime.Now;
        _auditLogger.Add(
            actionType: "DISH_UPDATED",
            entityType: "DISH",
            entityId: entity.DishID.ToString(),
            dishId: entity.DishID,
            beforeState: beforeAudit,
            afterState: new
            {
                request.Name,
                request.Price,
                request.CategoryId,
                entity.Available,
                entity.IsActive
            });

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã cập nhật món ăn." });
    }

    [HttpPost("dishes/{dishId:int}/deactivate")]
    public async Task<IActionResult> DeactivateDish(int dishId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Dishes.FirstOrDefaultAsync(d => d.DishID == dishId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy món ăn." });
        }

        var beforeAudit = new { entity.IsActive, entity.Available };
        entity.IsActive = false;
        entity.Available = false;
        entity.UpdatedAt = DateTime.Now;
        _auditLogger.Add(
            actionType: "DISH_DEACTIVATED",
            entityType: "DISH",
            entityId: entity.DishID.ToString(),
            dishId: entity.DishID,
            beforeState: beforeAudit,
            afterState: new { isActive = false, available = false });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã vô hiệu món ăn." });
    }

    [HttpDelete("dishes/{dishId:int}")]
    public async Task<IActionResult> DeleteDish(int dishId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Dishes
            .Include(d => d.CategoryDish)
            .Include(d => d.DishIngredients)
            .FirstOrDefaultAsync(d => d.DishID == dishId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy món ăn." });
        }

        if ((entity.Available ?? true) == true)
        {
            return Conflict(new { message = "Vui lòng tạm ngừng món ăn trước khi xóa" });
        }

        var beforeAudit = new
        {
            entity.Name,
            entity.Price,
            entity.CategoryID,
            entity.Available,
            entity.IsActive,
            linkedMenuCount = entity.CategoryDish.Count,
            ingredientCount = entity.DishIngredients.Count
        };

        var auditLogs = await _db.BusinessAuditLogs
            .Where(x => x.DishId == entity.DishID || (x.EntityType == "DISH" && x.EntityId == entity.DishID.ToString()))
            .ToListAsync(cancellationToken);

        if (auditLogs.Count > 0)
        {
            _db.BusinessAuditLogs.RemoveRange(auditLogs);
        }

        if (entity.CategoryDish.Count > 0)
        {
            _db.CategoryDish.RemoveRange(entity.CategoryDish);
        }

        if (entity.DishIngredients.Count > 0)
        {
            _db.DishIngredients.RemoveRange(entity.DishIngredients);
        }

        _db.Dishes.Remove(entity);
        _auditLogger.Add(
            actionType: "DISH_DELETED",
            entityType: "DISH",
            entityId: entity.DishID.ToString(),
            dishId: entity.DishID,
            beforeState: beforeAudit,
            afterState: null);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã xóa món ăn." });
    }

    [HttpGet("dishes/{dishId:int}/ingredients")]
    public async Task<ActionResult<AdminDishIngredientsResponse>> GetDishIngredients(int dishId, CancellationToken cancellationToken = default)
    {
        var dish = await _db.Dishes
            .AsNoTracking()
            .Where(d => d.DishID == dishId)
            .Select(d => new { d.DishID, d.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (dish is null)
        {
            return NotFound(new { message = "Kh?ng t?m th?y m?n ?n." });
        }

        var ingredients = await _db.DishIngredients
            .AsNoTracking()
            .Where(di => di.DishID == dishId)
            .Include(di => di.Ingredient)
            .OrderBy(di => di.Ingredient.Name)
            .Select(di => new AdminDishIngredientLineResponse(
                di.IngredientID,
                di.Ingredient.Name,
                di.Ingredient.Unit,
                di.Ingredient.CurrentStock,
                di.Ingredient.IsActive,
                true,
                di.QuantityPerDish))
            .ToListAsync(cancellationToken);

        return Ok(new AdminDishIngredientsResponse(dish.DishID, dish.Name, ingredients));
    }

    [HttpPut("dishes/{dishId:int}/ingredients")]
    public async Task<IActionResult> UpdateDishIngredients(
        int dishId,
        [FromBody] UpdateDishIngredientsRequest request,
        CancellationToken cancellationToken = default)
    {
        var dishExists = await _db.Dishes.AnyAsync(d => d.DishID == dishId, cancellationToken);
        if (!dishExists)
        {
            return NotFound(new { message = "Không tìm thấy món ăn." });
        }

        var incoming = request.Items ?? Array.Empty<UpdateDishIngredientItem>();
        var cleaned = incoming
            .Where(x => x.IngredientId > 0 && x.QuantityPerDish > 0)
            .GroupBy(x => x.IngredientId)
            .Select(g => g.OrderByDescending(x => x.QuantityPerDish).First())
            .ToList();

        var incomingIds = cleaned.Select(x => x.IngredientId).ToList();
        if (incomingIds.Count > 0)
        {
            var exists = await _db.Ingredients
                .Where(i => incomingIds.Contains(i.IngredientID))
                .Select(i => i.IngredientID)
                .ToListAsync(cancellationToken);
            if (exists.Count != incomingIds.Count)
            {
                return BadRequest(new { message = "Có nguyên liệu không hợp lệ." });
            }
        }

        var current = await _db.DishIngredients.Where(di => di.DishID == dishId).ToListAsync(cancellationToken);
        _db.DishIngredients.RemoveRange(current);

        foreach (var item in cleaned)
        {
            _db.DishIngredients.Add(new DishIngredients
            {
                DishID = dishId,
                IngredientID = item.IngredientId,
                QuantityPerDish = item.QuantityPerDish
            });
        }

        _auditLogger.Add(
            actionType: "DISH_INGREDIENTS_UPDATED",
            entityType: "DISH",
            entityId: dishId.ToString(),
            dishId: dishId,
            beforeState: new { ingredientCount = current.Count },
            afterState: new
            {
                ingredientCount = cleaned.Count,
                ingredientIds = cleaned.Select(x => x.IngredientId).ToArray()
            });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã cập nhật nguyên liệu món ăn." });
    }

    [HttpGet("ingredients")]
    public async Task<ActionResult<PagedResponse<AdminIngredientResponse>>> GetIngredients(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = true,
        [FromQuery] string? stockStatus = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Ingredients.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(i => i.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim();
            query = query.Where(i => i.Name.Contains(key));
        }

        if (string.Equals(stockStatus, "LOW", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.CurrentStock <= i.ReorderLevel);
        }
        else if (string.Equals(stockStatus, "NORMAL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.CurrentStock > i.ReorderLevel);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var rows = await query
            .OrderBy(i => i.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var summaries = await BuildIngredientBatchSummariesAsync(rows.Select(i => i.IngredientID), cancellationToken);

        var items = rows
            .Select(i =>
            {
                summaries.TryGetValue(i.IngredientID, out var summary);
                return new AdminIngredientResponse(
                    i.IngredientID,
                    i.Name,
                    i.Unit,
                    i.CurrentStock,
                    i.ReorderLevel,
                    i.IssueMethod,
                    i.IsActive,
                    summary.TotalBatchStock,
                    summary.UsableBatchStock,
                    summary.NearestExpiryDate,
                    summary.ExpiredBatchCount,
                    summary.NearExpiryBatchCount);
            })
            .ToList();

        return Ok(new PagedResponse<AdminIngredientResponse>(page, pageSize, totalItems, totalPages, items));
    }


    [HttpGet("ingredients/{ingredientId:int}/related-dishes")]
    public async Task<ActionResult<IReadOnlyList<AdminRelatedIngredientDishResponse>>> GetIngredientRelatedDishes(int ingredientId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Ingredients.AnyAsync(i => i.IngredientID == ingredientId, cancellationToken);
        if (!exists)
        {
            return NotFound(new { message = "Không tìm thấy nguyên liệu." });
        }

        var rows = await _db.DishIngredients
            .AsNoTracking()
            .Where(di => di.IngredientID == ingredientId)
            .Include(di => di.Dish)
                .ThenInclude(d => d.Category)
            .OrderBy(di => di.Dish.Name)
            .Select(di => new AdminRelatedIngredientDishResponse(
                di.DishID,
                di.Dish.Name,
                di.Dish.Category != null ? di.Dish.Category.Name : null,
                di.QuantityPerDish,
                di.Ingredient.Unit,
                di.Dish.Available ?? false,
                di.Dish.IsActive ?? false))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("ingredients/{ingredientId:int}")]
    public async Task<ActionResult<AdminIngredientResponse>> GetIngredientById(int ingredientId, CancellationToken cancellationToken = default)
    {
        var ingredient = await _db.Ingredients
            .AsNoTracking()
            .Where(i => i.IngredientID == ingredientId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ingredient is null)
        {
            return NotFound(new { message = "Không tìm thấy nguyên liệu." });
        }

        var summaries = await BuildIngredientBatchSummariesAsync([ingredient.IngredientID], cancellationToken);
        summaries.TryGetValue(ingredient.IngredientID, out var summary);
        return Ok(new AdminIngredientResponse(
            ingredient.IngredientID,
            ingredient.Name,
            ingredient.Unit,
            ingredient.CurrentStock,
            ingredient.ReorderLevel,
            ingredient.IssueMethod,
            ingredient.IsActive,
            summary.TotalBatchStock,
            summary.UsableBatchStock,
            summary.NearestExpiryDate,
            summary.ExpiredBatchCount,
            summary.NearExpiryBatchCount));
    }

    [HttpPost("ingredients")]
    public async Task<ActionResult> CreateIngredient([FromBody] AdminUpsertIngredientRequest request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateIngredientRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var entity = new Ingredients
        {
            Name = request.Name!.Trim(),
            Unit = request.Unit!.Trim(),
            CurrentStock = request.CurrentStock!.Value,
            ReorderLevel = request.ReorderLevel!.Value,
            IssueMethod = NormalizeIssueMethod(request.IssueMethod),
            IsActive = request.IsActive ?? true
        };

        _db.Ingredients.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        _auditLogger.Add(
            actionType: "INGREDIENT_CREATED",
            entityType: "INGREDIENT",
            entityId: entity.IngredientID.ToString(),
            beforeState: null,
            afterState: new
            {
                entity.Name,
                entity.Unit,
                entity.CurrentStock,
                entity.ReorderLevel,
                entity.IssueMethod,
                entity.IsActive
            });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã tạo nguyên liệu.", ingredientId = entity.IngredientID });
    }

    [HttpPut("ingredients/{ingredientId:int}")]
    public async Task<ActionResult> UpdateIngredient(
        int ingredientId,
        [FromBody] AdminUpsertIngredientRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Ingredients.FirstOrDefaultAsync(i => i.IngredientID == ingredientId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy nguyên liệu." });
        }

        var validation = ValidateIngredientRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var beforeAudit = new
        {
            entity.Name,
            entity.Unit,
            entity.CurrentStock,
            entity.ReorderLevel,
            entity.IssueMethod,
            entity.IsActive
        };

        entity.Name = request.Name!.Trim();
        entity.Unit = request.Unit!.Trim();
        entity.CurrentStock = request.CurrentStock!.Value;
        entity.ReorderLevel = request.ReorderLevel!.Value;
        entity.IssueMethod = NormalizeIssueMethod(request.IssueMethod);
        entity.IsActive = request.IsActive ?? true;

        _auditLogger.Add(
            actionType: "INGREDIENT_UPDATED",
            entityType: "INGREDIENT",
            entityId: entity.IngredientID.ToString(),
            beforeState: beforeAudit,
            afterState: new
            {
                entity.Name,
                entity.Unit,
                entity.CurrentStock,
                entity.ReorderLevel,
                entity.IssueMethod,
                entity.IsActive
            });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã cập nhật nguyên liệu." });
    }

    [HttpPost("ingredients/{ingredientId:int}/deactivate")]
    public async Task<IActionResult> DeactivateIngredient(int ingredientId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Ingredients.FirstOrDefaultAsync(i => i.IngredientID == ingredientId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy nguyên liệu." });
        }

        var beforeAudit = new { entity.IsActive };
        entity.IsActive = false;
        _auditLogger.Add(
            actionType: "INGREDIENT_DEACTIVATED",
            entityType: "INGREDIENT",
            entityId: entity.IngredientID.ToString(),
            beforeState: beforeAudit,
            afterState: new { isActive = false });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã vô hiệu nguyên liệu." });
    }

    [HttpDelete("ingredients/{ingredientId:int}")]
    public async Task<IActionResult> DeleteIngredient(int ingredientId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Ingredients
            .Include(i => i.DishIngredients)
            .FirstOrDefaultAsync(i => i.IngredientID == ingredientId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy nguyên liệu." });
        }

        if (entity.IsActive)
        {
            return Conflict(new { message = "Vui lòng vô hiệu hóa trước khi xóa." });
        }

        if (entity.DishIngredients.Count > 0)
        {
            return Conflict(new
            {
                message = "Nguyên liệu đang được dùng trong công thức món ăn. Hãy dùng \"Vô hiệu\" nếu bạn muốn ngừng sử dụng."
            });
        }

        var beforeAudit = new
        {
            entity.Name,
            entity.Unit,
            entity.CurrentStock,
            entity.ReorderLevel,
            entity.IsActive
        };

        _db.Ingredients.Remove(entity);
        _auditLogger.Add(
            actionType: "INGREDIENT_DELETED",
            entityType: "INGREDIENT",
            entityId: entity.IngredientID.ToString(),
            beforeState: beforeAudit,
            afterState: null);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã xóa nguyên liệu." });
    }

    [HttpGet("ingredients/{ingredientId:int}/batches")]
    public async Task<ActionResult<IReadOnlyList<AdminIngredientBatchResponse>>> GetIngredientBatches(
        int ingredientId,
        CancellationToken cancellationToken = default)
    {
        var ingredientExists = await _db.Ingredients
            .AsNoTracking()
            .AnyAsync(i => i.IngredientID == ingredientId, cancellationToken);
        if (!ingredientExists)
        {
            return NotFound(new { message = "Không tìm thấy nguyên liệu." });
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var nearExpiryDate = today.AddDays(7);
        var items = await _db.IngredientBatches
            .AsNoTracking()
            .Where(b => b.IngredientID == ingredientId)
            .OrderByDescending(b => b.IsActive)
            .ThenBy(b => b.ExpiryDate)
            .ThenBy(b => b.ReceivedDate)
            .ThenBy(b => b.BatchID)
            .Select(b => new AdminIngredientBatchResponse(
                b.BatchID,
                b.IngredientID,
                b.BatchCode,
                b.QuantityInitial,
                b.QuantityRemaining,
                b.Unit,
                b.ExpiryDate,
                b.ReceivedDate,
                b.SupplierName,
                b.IsActive,
                b.CreatedAt,
                b.UpdatedAt,
                !b.IsActive ? "Đã vô hiệu" :
                b.ExpiryDate < today ? "Đã hết hạn" :
                b.ExpiryDate <= nearExpiryDate ? "Sắp hết hạn" :
                "Còn hạn"))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("ingredients/{ingredientId:int}/batches")]
    public async Task<ActionResult> CreateIngredientBatch(
        int ingredientId,
        [FromBody] CreateIngredientBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var quantityInitial = request.QuantityInitial;
        var quantityRemaining = request.QuantityRemaining ?? quantityInitial;
        var validation = ValidateIngredientBatchRequest(quantityInitial, quantityRemaining, request.ExpiryDate, request.ReceivedDate);
        if (validation is not null)
        {
            return validation;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var ingredient = await _db.Ingredients.FirstOrDefaultAsync(i => i.IngredientID == ingredientId, cancellationToken);
        if (ingredient is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return NotFound(new { message = "Không tìm thấy nguyên liệu." });
        }

        if (!ingredient.IsActive)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = "Nguyên liệu đang bị vô hiệu, không thể nhập thêm lô." });
        }

        var unit = string.IsNullOrWhiteSpace(request.Unit) ? ingredient.Unit : request.Unit.Trim();
        var now = DateTime.UtcNow;
        var batch = new IngredientBatches
        {
            IngredientID = ingredient.IngredientID,
            BatchCode = NormalizeOptionalText(request.BatchCode, 100),
            QuantityInitial = quantityInitial!.Value,
            QuantityRemaining = quantityRemaining!.Value,
            Unit = unit,
            ExpiryDate = request.ExpiryDate!.Value,
            ReceivedDate = request.ReceivedDate!.Value,
            SupplierName = NormalizeOptionalText(request.SupplierName, 200),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = null
        };

        _db.IngredientBatches.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);

        await IncreaseIngredientCurrentStockAsync(ingredient.IngredientID, batch.QuantityInitial, cancellationToken);
        _db.IngredientStockMovements.Add(new IngredientStockMovements
        {
            IngredientID = ingredient.IngredientID,
            BatchID = batch.BatchID,
            QuantityChange = batch.QuantityInitial,
            MovementType = IngredientMovementTypes.Receive,
            ReferenceType = "ADMIN",
            CreatedAt = now,
            Note = "Nhập lô nguyên liệu"
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { message = "Đã thêm lô nguyên liệu.", batchId = batch.BatchID });
    }

    [HttpPut("ingredients/{ingredientId:int}/batches/{batchId:int}")]
    public async Task<ActionResult> UpdateIngredientBatch(
        int ingredientId,
        int batchId,
        [FromBody] UpdateIngredientBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var batch = await _db.IngredientBatches
            .FirstOrDefaultAsync(b => b.IngredientID == ingredientId && b.BatchID == batchId, cancellationToken);
        if (batch is null)
        {
            return NotFound(new { message = "Không tìm thấy lô nguyên liệu." });
        }

        if (request.ExpiryDate is null)
        {
            return BadRequest(new { message = "Hạn sử dụng là bắt buộc." });
        }

        if (request.ReceivedDate is null)
        {
            return BadRequest(new { message = "Ngày nhập là bắt buộc." });
        }

        if (request.IsActive is false && batch.QuantityRemaining != 0)
        {
            return Conflict(new { message = "Không thể vô hiệu hóa lô còn tồn kho. Vui lòng điều chỉnh/xử lý tồn kho trước." });
        }

        var wasActive = batch.IsActive;
        batch.BatchCode = NormalizeOptionalText(request.BatchCode, 100);
        batch.ExpiryDate = request.ExpiryDate.Value;
        batch.ReceivedDate = request.ReceivedDate.Value;
        batch.SupplierName = NormalizeOptionalText(request.SupplierName, 200);
        batch.IsActive = request.IsActive ?? batch.IsActive;
        batch.UpdatedAt = DateTime.UtcNow;

        if (wasActive && !batch.IsActive)
        {
            _db.IngredientStockMovements.Add(new IngredientStockMovements
            {
                IngredientID = ingredientId,
                BatchID = batch.BatchID,
                QuantityChange = 0,
                MovementType = IngredientMovementTypes.DeactivateBatch,
                ReferenceType = "ADMIN",
                CreatedAt = DateTime.UtcNow,
                Note = "Vô hiệu hóa lô nguyên liệu"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã cập nhật lô nguyên liệu." });
    }

    [HttpPost("ingredients/{ingredientId:int}/batches/{batchId:int}/deactivate")]
    public async Task<ActionResult> DeactivateIngredientBatch(
        int ingredientId,
        int batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _db.IngredientBatches
            .FirstOrDefaultAsync(b => b.IngredientID == ingredientId && b.BatchID == batchId, cancellationToken);
        if (batch is null)
        {
            return NotFound(new { message = "Không tìm thấy lô nguyên liệu." });
        }

        if (batch.QuantityRemaining != 0)
        {
            return Conflict(new { message = "Không thể vô hiệu hóa lô còn tồn kho. Vui lòng điều chỉnh/xử lý tồn kho trước." });
        }

        if (batch.IsActive)
        {
            batch.IsActive = false;
            batch.UpdatedAt = DateTime.UtcNow;
            _db.IngredientStockMovements.Add(new IngredientStockMovements
            {
                IngredientID = ingredientId,
                BatchID = batch.BatchID,
                QuantityChange = 0,
                MovementType = IngredientMovementTypes.DeactivateBatch,
                ReferenceType = "ADMIN",
                CreatedAt = DateTime.UtcNow,
                Note = "Vô hiệu hóa lô nguyên liệu"
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = "Đã vô hiệu hóa lô nguyên liệu." });
    }

    [HttpGet("ingredients/{ingredientId:int}/stock-movements")]
    public async Task<ActionResult<PagedResponse<IngredientStockMovementResponse>>> GetIngredientStockMovements(
        int ingredientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var ingredientExists = await _db.Ingredients
            .AsNoTracking()
            .AnyAsync(i => i.IngredientID == ingredientId, cancellationToken);
        if (!ingredientExists)
        {
            return NotFound(new { message = "Không tìm thấy nguyên liệu." });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.IngredientStockMovements
            .AsNoTracking()
            .Where(m => m.IngredientID == ingredientId);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.MovementID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new IngredientStockMovementResponse(
                m.MovementID,
                m.IngredientID,
                m.BatchID,
                m.QuantityChange,
                m.MovementType,
                m.ReferenceType,
                m.ReferenceID,
                m.OrderID,
                m.OrderItemID,
                m.DishID,
                m.CreatedAt,
                m.Note))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<IngredientStockMovementResponse>(page, pageSize, totalItems, totalPages, items));
    }

    [HttpGet("inventory/summary")]
    public async Task<ActionResult<InventorySummaryResponse>> GetInventorySummary(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var nearExpiryDate = today.AddDays(InventoryNearExpiryDays);

        var totalActiveIngredients = await _db.Ingredients
            .AsNoTracking()
            .CountAsync(i => i.IsActive, cancellationToken);
        var activeStockBatches = _db.IngredientBatches
            .AsNoTracking()
            .Where(b => b.IsActive && b.QuantityRemaining > 0);
        var expiredBatchCount = await activeStockBatches.CountAsync(b => b.ExpiryDate < today, cancellationToken);
        var nearExpiryBatchCount = await activeStockBatches.CountAsync(b => b.ExpiryDate >= today && b.ExpiryDate <= nearExpiryDate, cancellationToken);
        var totalBatchesWithStock = await activeStockBatches.CountAsync(cancellationToken);
        var totalUsableBatchStock = await activeStockBatches
            .Where(b => b.ExpiryDate >= today)
            .SumAsync(b => (decimal?)b.QuantityRemaining, cancellationToken) ?? 0m;

        var ingredientIds = await _db.Ingredients
            .AsNoTracking()
            .Where(i => i.IsActive)
            .Select(i => i.IngredientID)
            .ToListAsync(cancellationToken);
        var availability = await _stockAvailability.BuildIngredientStockAvailabilityMapAsync(ingredientIds, cancellationToken);
        var reorderRows = await _db.Ingredients
            .AsNoTracking()
            .Where(i => i.IsActive)
            .Select(i => new { i.IngredientID, i.ReorderLevel })
            .ToListAsync(cancellationToken);
        var lowStockIngredientCount = reorderRows.Count(i =>
            i.ReorderLevel > 0 &&
            availability.TryGetValue(i.IngredientID, out var stock) &&
            stock.AvailabilityStock <= i.ReorderLevel);

        return Ok(new InventorySummaryResponse(
            totalActiveIngredients,
            expiredBatchCount,
            nearExpiryBatchCount,
            totalBatchesWithStock,
            totalUsableBatchStock,
            lowStockIngredientCount,
            InventoryNearExpiryDays));
    }

    [HttpGet("inventory/batches")]
    public async Task<ActionResult<PagedResponse<InventoryBatchResponse>>> GetInventoryBatches(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int? ingredientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var nearExpiryDate = today.AddDays(InventoryNearExpiryDays);
        var normalizedStatus = NormalizeInventoryBatchStatusFilter(status);

        var query = _db.IngredientBatches
            .AsNoTracking()
            .Include(b => b.Ingredient)
            .AsQueryable();

        if (ingredientId is > 0)
        {
            query = query.Where(b => b.IngredientID == ingredientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim();
            query = query.Where(b =>
                b.Ingredient.Name.Contains(key) ||
                (b.BatchCode != null && b.BatchCode.Contains(key)) ||
                (b.SupplierName != null && b.SupplierName.Contains(key)));
        }

        query = normalizedStatus switch
        {
            "expired" => query.Where(b => b.IsActive && b.QuantityRemaining > 0 && b.ExpiryDate < today),
            "near-expiry" => query.Where(b => b.IsActive && b.QuantityRemaining > 0 && b.ExpiryDate >= today && b.ExpiryDate <= nearExpiryDate),
            "valid" => query.Where(b => b.IsActive && b.QuantityRemaining > 0 && b.ExpiryDate > nearExpiryDate),
            "empty" => query.Where(b => b.IsActive && b.QuantityRemaining == 0),
            "inactive" => query.Where(b => !b.IsActive),
            _ => query
        };

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var rows = await query
            .OrderByDescending(b => b.IsActive)
            .ThenBy(b => b.ExpiryDate)
            .ThenBy(b => b.ReceivedDate)
            .ThenBy(b => b.BatchID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.BatchID,
                b.IngredientID,
                IngredientName = b.Ingredient.Name,
                b.BatchCode,
                b.QuantityInitial,
                b.QuantityRemaining,
                b.Unit,
                b.ReceivedDate,
                b.ExpiryDate,
                b.SupplierName,
                b.IsActive
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(b => new InventoryBatchResponse(
                b.BatchID,
                b.IngredientID,
                b.IngredientName,
                b.BatchCode,
                b.QuantityInitial,
                b.QuantityRemaining,
                b.Unit,
                b.ReceivedDate,
                b.ExpiryDate,
                b.SupplierName,
                b.IsActive,
                GetInventoryBatchStatus(b.IsActive, b.QuantityRemaining, b.ExpiryDate, today),
                b.ExpiryDate.DayNumber - today.DayNumber))
            .ToList();

        return Ok(new PagedResponse<InventoryBatchResponse>(page, pageSize, totalItems, totalPages, items));
    }

    [HttpPost("inventory/stock-in")]
    public async Task<ActionResult> StockIn([FromBody] InventoryStockInRequest request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateInventoryStockInRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var ingredient = await _db.Ingredients.FirstOrDefaultAsync(i => i.IngredientID == request.IngredientId, cancellationToken);
        if (ingredient is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return NotFound(new { message = "Không tìm thấy nguyên liệu." });
        }

        if (!ingredient.IsActive)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = "Nguyên liệu đang bị vô hiệu, không thể nhập kho." });
        }

        var now = DateTime.UtcNow;
        var batch = new IngredientBatches
        {
            IngredientID = ingredient.IngredientID,
            BatchCode = NormalizeOptionalText(request.BatchCode, 100),
            QuantityInitial = request.Quantity!.Value,
            QuantityRemaining = request.Quantity.Value,
            Unit = ingredient.Unit,
            ExpiryDate = request.ExpiryDate!.Value,
            ReceivedDate = request.ReceivedDate!.Value,
            SupplierName = NormalizeOptionalText(request.SupplierName, 200),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = null
        };

        _db.IngredientBatches.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);

        await IncreaseIngredientCurrentStockAsync(ingredient.IngredientID, batch.QuantityInitial, cancellationToken);
        _db.IngredientStockMovements.Add(new IngredientStockMovements
        {
            IngredientID = ingredient.IngredientID,
            BatchID = batch.BatchID,
            QuantityChange = batch.QuantityInitial,
            MovementType = IngredientMovementTypes.Receive,
            ReferenceType = "ADMIN",
            CreatedAt = now,
            Note = NormalizeOptionalText(request.Note, 500) ?? "Nhập kho nguyên liệu"
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { message = "Đã nhập kho nguyên liệu.", batchId = batch.BatchID });
    }

    [HttpPost("inventory/stock-out")]
    public async Task<ActionResult> StockOut([FromBody] InventoryStockOutRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity is null || request.Quantity <= 0)
        {
            return BadRequest(new { message = "Số lượng xuất kho phải lớn hơn 0." });
        }

        var reason = NormalizeStockOutReason(request.Reason);
        var movementType = reason == "ADJUST" ? IngredientMovementTypes.Adjust : IngredientMovementTypes.Waste;
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var ingredient = await _db.Ingredients.FirstOrDefaultAsync(i => i.IngredientID == request.IngredientId, cancellationToken);
        if (ingredient is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return NotFound(new { message = "Không tìm thấy nguyên liệu." });
        }

        var now = DateTime.UtcNow;
        var remaining = request.Quantity.Value;
        var note = NormalizeOptionalText(request.Note, 500) ?? GetStockOutReasonLabel(reason);

        if (request.BatchId is > 0)
        {
            var batch = await _db.IngredientBatches
                .FirstOrDefaultAsync(b => b.IngredientID == ingredient.IngredientID && b.BatchID == request.BatchId.Value, cancellationToken);
            if (batch is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return NotFound(new { message = "Không tìm thấy lô nguyên liệu." });
            }

            if (!batch.IsActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict(new { message = "Không thể xuất kho từ lô đã vô hiệu." });
            }

            if (batch.QuantityRemaining < remaining)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict(new { message = "Số lượng xuất kho lớn hơn số lượng còn lại của lô." });
            }

            DeductInventoryBatch(batch, remaining, movementType, note, now);
            remaining = 0;
        }
        else
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var query = _db.IngredientBatches
                .Where(b => b.IngredientID == ingredient.IngredientID && b.IsActive && b.QuantityRemaining > 0);

            query = reason == "EXPIRED_DISPOSAL"
                ? query.Where(b => b.ExpiryDate < today)
                : query.Where(b => b.ExpiryDate >= today);

            var batches = await query
                .OrderBy(b => b.ExpiryDate)
                .ThenBy(b => b.ReceivedDate)
                .ThenBy(b => b.BatchID)
                .ToListAsync(cancellationToken);

            if (batches.Count > 0)
            {
                foreach (var batch in batches)
                {
                    if (remaining <= 0) break;
                    var deducted = Math.Min(batch.QuantityRemaining, remaining);
                    DeductInventoryBatch(batch, deducted, movementType, note, now);
                    remaining -= deducted;
                }
            }
            else
            {
                var hasAnyActiveBatch = await _db.IngredientBatches
                    .AnyAsync(b => b.IngredientID == ingredient.IngredientID && b.IsActive, cancellationToken);
                if (!hasAnyActiveBatch)
                {
                    var updatedRows = await TryDecreaseIngredientCurrentStockAsync(ingredient.IngredientID, remaining, cancellationToken);
                    if (updatedRows == 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Conflict(new { message = "Tồn kho hiện tại không đủ để xuất kho." });
                    }

                    _db.IngredientStockMovements.Add(new IngredientStockMovements
                    {
                        IngredientID = ingredient.IngredientID,
                        BatchID = null,
                        QuantityChange = -remaining,
                        MovementType = movementType,
                        ReferenceType = "ADMIN",
                        CreatedAt = now,
                        Note = note
                    });
                    remaining = 0;
                }
            }
        }

        if (remaining > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = reason == "EXPIRED_DISPOSAL" ? "Không đủ tồn kho hết hạn để hủy." : "Không đủ tồn kho theo lô để xuất kho." });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await SyncIngredientCurrentStockAsync(ingredient, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { message = "Đã xuất kho nguyên liệu." });
    }

    [HttpGet("inventory/movements")]
    public async Task<ActionResult<PagedResponse<InventoryMovementResponse>>> GetInventoryMovements(
        [FromQuery] int? ingredientId,
        [FromQuery] int? batchId,
        [FromQuery] string? movementType,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.IngredientStockMovements
            .AsNoTracking()
            .Include(m => m.Ingredient)
            .Include(m => m.Batch)
            .AsQueryable();

        if (ingredientId is > 0) query = query.Where(m => m.IngredientID == ingredientId.Value);
        if (batchId is > 0) query = query.Where(m => m.BatchID == batchId.Value);
        if (!string.IsNullOrWhiteSpace(movementType)) query = query.Where(m => m.MovementType == movementType.Trim());
        if (dateFrom is not null) query = query.Where(m => m.CreatedAt >= dateFrom.Value.ToDateTime(TimeOnly.MinValue));
        if (dateTo is not null) query = query.Where(m => m.CreatedAt < dateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim();
            query = query.Where(m =>
                m.Ingredient.Name.Contains(key) ||
                (m.Batch != null && m.Batch.BatchCode != null && m.Batch.BatchCode.Contains(key)) ||
                (m.Note != null && m.Note.Contains(key)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.MovementID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new InventoryMovementResponse(
                m.MovementID,
                m.IngredientID,
                m.Ingredient.Name,
                m.Ingredient.Unit,
                m.BatchID,
                m.Batch != null ? m.Batch.BatchCode : null,
                m.Batch != null ? m.Batch.ExpiryDate : null,
                m.Batch != null ? m.Batch.SupplierName : null,
                m.QuantityChange,
                m.MovementType,
                m.ReferenceType,
                m.ReferenceID,
                m.OrderID,
                m.OrderItemID,
                m.DishID,
                m.CreatedAt,
                m.Note))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<InventoryMovementResponse>(page, pageSize, totalItems, totalPages, items));
    }

    [HttpGet("table-statuses")]
    public async Task<ActionResult<IReadOnlyList<TableStatusResponse>>> GetTableStatuses(CancellationToken cancellationToken = default)
    {
        var items = await _db.TableStatus
            .AsNoTracking()
            .OrderBy(s => s.StatusName)
            .Select(s => new TableStatusResponse(s.StatusID, s.StatusCode, s.StatusName))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("units")]
    public async Task<ActionResult<PagedResponse<AdminUnitResponse>>> GetUnits(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = true,
        [FromQuery] string? stockStatus = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Units.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(u => u.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim();
            query = query.Where(u => u.Name.Contains(key) || (u.Description != null && u.Description.Contains(key)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var usage = await BuildUnitUsageAsync(cancellationToken);
        var items = await query
            .OrderBy(u => u.DisplayOrder)
            .ThenBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.UnitID,
                u.Name,
                u.Description,
                u.DisplayOrder,
                u.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<AdminUnitResponse>(
            page,
            pageSize,
            totalItems,
            totalPages,
            items.Select(u =>
            {
                var counts = usage.TryGetValue(u.Name, out var current) ? current : new UnitUsage(0, 0);
                return new AdminUnitResponse(
                    u.UnitID,
                    u.Name,
                    u.Description,
                    u.DisplayOrder,
                    u.IsActive,
                    counts.DishCount,
                    counts.IngredientCount);
            }).ToList()));
    }

    [HttpGet("units/{unitId:int}")]
    public async Task<ActionResult<AdminUnitResponse>> GetUnitById(int unitId, CancellationToken cancellationToken = default)
    {
        var unit = await _db.Units
            .AsNoTracking()
            .Where(u => u.UnitID == unitId)
            .Select(u => new
            {
                u.UnitID,
                u.Name,
                u.Description,
                u.DisplayOrder,
                u.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (unit is null)
        {
            return NotFound(new { message = "Không tìm thấy đơn vị." });
        }

        var usage = await GetUnitUsageAsync(unit.Name, cancellationToken);
        return Ok(new AdminUnitResponse(
            unit.UnitID,
            unit.Name,
            unit.Description,
            unit.DisplayOrder,
            unit.IsActive,
            usage.DishCount,
            usage.IngredientCount));
    }

    [HttpPost("units")]
    public async Task<ActionResult> CreateUnit([FromBody] AdminUpsertUnitRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Tên đơn vị không được để trống." });
        }

        var exists = await _db.Units.AnyAsync(u => u.Name == name, cancellationToken);
        if (exists)
        {
            return Conflict(new { message = "Đơn vị này đã tồn tại." });
        }

        var entity = new Units
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DisplayOrder = request.DisplayOrder ?? 0,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _db.Units.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetUnitById), new { unitId = entity.UnitID }, new { unitId = entity.UnitID });
    }

    [HttpPut("units/{unitId:int}")]
    public async Task<ActionResult> UpdateUnit(int unitId, [FromBody] AdminUpsertUnitRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Units.FirstOrDefaultAsync(u => u.UnitID == unitId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy đơn vị." });
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Tên đơn vị không được để trống." });
        }

        var duplicate = await _db.Units.AnyAsync(u => u.UnitID != unitId && u.Name == name, cancellationToken);
        if (duplicate)
        {
            return Conflict(new { message = "Đơn vị này đã tồn tại." });
        }

        var oldName = entity.Name;
        entity.Name = name;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.DisplayOrder = request.DisplayOrder ?? entity.DisplayOrder;
        entity.IsActive = request.IsActive ?? entity.IsActive;
        entity.UpdatedAt = DateTime.Now;

        if (!string.Equals(oldName, name, StringComparison.Ordinal))
        {
            await _db.Dishes
                .Where(d => d.Unit == oldName)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(d => d.Unit, name)
                    .SetProperty(d => d.UpdatedAt, DateTime.Now), cancellationToken);

            await _db.Ingredients
                .Where(i => i.Unit == oldName)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.Unit, name), cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("units/{unitId:int}")]
    public async Task<ActionResult> DeleteUnit(int unitId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Units.FirstOrDefaultAsync(u => u.UnitID == unitId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy đơn vị." });
        }

        if (entity.IsActive)
        {
            return Conflict(new { message = "Vui lòng vô hiệu hóa trước khi xóa." });
        }

        var usage = await GetUnitUsageDetailsAsync(entity.Name, cancellationToken);
        if (usage.DishNames.Count > 0 || usage.IngredientNames.Count > 0)
        {
            return Conflict(new
            {
                message = BuildUnitInUseMessage(entity.Name, usage.DishNames, usage.IngredientNames),
                unitName = entity.Name,
                dishNames = usage.DishNames,
                ingredientNames = usage.IngredientNames,
                dishCount = usage.DishNames.Count,
                ingredientCount = usage.IngredientNames.Count
            });
        }

        _db.Units.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("tables")]
    public async Task<ActionResult<PagedResponse<AdminTableResponse>>> GetTables(
        [FromQuery] int? branchId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = true,
        [FromQuery] string? stockStatus = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.DiningTables
            .AsNoTracking()
            .Include(t => t.Branch)
            .Include(t => t.Status)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(t => (t.IsActive ?? false) == true);
        }

        if (branchId is > 0)
        {
            query = query.Where(t => t.BranchID == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim();
            query = query.Where(t =>
                (t.QRCode != null && t.QRCode.Contains(key)) ||
                t.TableID.ToString().Contains(key) ||
                t.TableNumber.ToString().Contains(key) ||
                t.NumberOfSeats.ToString().Contains(key) ||
                t.Branch.Name.Contains(key) ||
                t.Status.StatusName.Contains(key));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await query
            .OrderBy(t => t.Branch.Name)
            .ThenBy(t => t.TableNumber)
            .ThenBy(t => t.TableID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new AdminTableResponse(
                t.TableID,
                t.TableNumber,
                t.BranchID,
                t.Branch.Name,
                t.NumberOfSeats,
                t.QRCode,
                t.StatusID,
                t.Status.StatusCode,
                t.Status.StatusName,
                t.IsActive ?? false))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<AdminTableResponse>(page, pageSize, totalItems, totalPages, items));
    }

    [HttpGet("tables/{tableId:int}")]
    public async Task<ActionResult<AdminTableResponse>> GetTableById(int tableId, CancellationToken cancellationToken = default)
    {
        var table = await _db.DiningTables
            .AsNoTracking()
            .Include(t => t.Branch)
            .Include(t => t.Status)
            .Where(t => t.TableID == tableId)
            .Select(t => new AdminTableResponse(
                t.TableID,
                t.TableNumber,
                t.BranchID,
                t.Branch.Name,
                t.NumberOfSeats,
                t.QRCode,
                t.StatusID,
                t.Status.StatusCode,
                t.Status.StatusName,
                t.IsActive ?? false))
            .FirstOrDefaultAsync(cancellationToken);

        return table is null ? NotFound(new { message = "Không tìm thấy bàn." }) : Ok(table);
    }

    [HttpPost("tables")]
    public async Task<ActionResult> CreateTable([FromBody] AdminUpsertTableRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateTableRequest(request, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var statusId = request.StatusId ?? await _db.TableStatus
            .Where(s => s.StatusCode == "AVAILABLE")
            .Select(s => (int?)s.StatusID)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await _db.TableStatus
                .OrderBy(s => s.StatusID)
                .Select(s => (int?)s.StatusID)
                .FirstOrDefaultAsync(cancellationToken)
            ?? 1;

        var entity = new DiningTables
        {
            BranchID = request.BranchId!.Value,
            TableNumber = await GetNextTableNumberAsync(request.BranchId.Value, cancellationToken),
            NumberOfSeats = request.NumberOfSeats!.Value,
            StatusID = statusId,
            QRCode = string.IsNullOrWhiteSpace(request.QRCode) ? null : request.QRCode.Trim(),
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _db.DiningTables.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        _auditLogger.Add(
            actionType: "TABLE_CREATED",
            entityType: "TABLE",
            entityId: entity.TableID.ToString(),
            tableId: entity.TableID,
            beforeState: null,
            afterState: new
            {
                entity.BranchID,
                entity.TableNumber,
                entity.NumberOfSeats,
                entity.StatusID,
                entity.QRCode,
                entity.IsActive
            });
        await _db.SaveChangesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(entity.QRCode))
        {
            entity.QRCode = $"BR{entity.BranchID}-TB{entity.TableNumber:D2}";
            entity.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = "Đã tạo bàn.", tableId = entity.TableID });
    }

    [HttpPut("tables/{tableId:int}")]
    public async Task<ActionResult> UpdateTable(
        int tableId,
        [FromBody] AdminUpsertTableRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.DiningTables.FirstOrDefaultAsync(t => t.TableID == tableId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy bàn." });
        }

        var validation = await ValidateTableRequest(request, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var beforeAudit = new
        {
            entity.BranchID,
            entity.TableNumber,
            entity.NumberOfSeats,
            entity.StatusID,
            entity.QRCode,
            entity.IsActive
        };

        entity.BranchID = request.BranchId!.Value;
        entity.NumberOfSeats = request.NumberOfSeats!.Value;
        entity.StatusID = request.StatusId ?? entity.StatusID;
        entity.QRCode = string.IsNullOrWhiteSpace(request.QRCode) ? $"BR{entity.BranchID}-TB{entity.TableNumber:D2}" : request.QRCode.Trim();
        entity.IsActive = request.IsActive ?? true;
        entity.UpdatedAt = DateTime.Now;

        _auditLogger.Add(
            actionType: "TABLE_UPDATED",
            entityType: "TABLE",
            entityId: entity.TableID.ToString(),
            tableId: entity.TableID,
            beforeState: beforeAudit,
            afterState: new
            {
                entity.BranchID,
                entity.TableNumber,
                entity.NumberOfSeats,
                entity.StatusID,
                entity.QRCode,
                entity.IsActive
            });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã cập nhật bàn." });
    }

    [HttpPost("tables/{tableId:int}/deactivate")]
    public async Task<IActionResult> DeactivateTable(int tableId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.DiningTables.FirstOrDefaultAsync(t => t.TableID == tableId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy bàn." });
        }

        var beforeAudit = new { entity.IsActive, entity.StatusID };
        entity.IsActive = false;
        entity.UpdatedAt = DateTime.Now;
        _auditLogger.Add(
            actionType: "TABLE_HIDDEN",
            entityType: "TABLE",
            entityId: entity.TableID.ToString(),
            tableId: entity.TableID,
            beforeState: beforeAudit,
            afterState: new { isActive = false, entity.StatusID });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã ẩn bàn." });
    }

    [HttpDelete("tables/{tableId:int}")]
    public async Task<ActionResult> DeleteTable(int tableId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.DiningTables.FirstOrDefaultAsync(t => t.TableID == tableId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy bàn." });
        }

        if ((entity.IsActive ?? false) == true)
        {
            return Conflict(new { message = "Vui lòng ẩn bàn trước khi xóa" });
        }

        var beforeAudit = new
        {
            entity.BranchID,
            entity.TableNumber,
            entity.NumberOfSeats,
            entity.StatusID,
            entity.QRCode,
            entity.IsActive
        };

        _db.DiningTables.Remove(entity);
        _auditLogger.Add(
            actionType: "TABLE_DELETED",
            entityType: "TABLE",
            entityId: entity.TableID.ToString(),
            tableId: entity.TableID,
            beforeState: beforeAudit,
            afterState: null);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã xóa bàn." });
    }

    [HttpGet("internal/audit-logs")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetAuditLogs(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] int? dishId,
        [FromQuery] int? tableId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        var query = _db.BusinessAuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(x => x.EntityType == entityType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            query = query.Where(x => x.EntityId == entityId.Trim());
        }

        if (dishId is > 0)
        {
            query = query.Where(x => x.DishId == dishId.Value);
        }

        if (tableId is > 0)
        {
            query = query.Where(x => x.TableId == tableId.Value);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new
            {
                auditId = x.BusinessAuditLogId,
                timestampUtc = x.CreatedAtUtc,
                actorType = x.ActorType,
                actorId = x.ActorId,
                actorCode = x.ActorCode,
                actorName = x.ActorName,
                actorRoleCode = x.ActorRoleCode,
                actionType = x.ActionType,
                entityType = x.EntityType,
                entityId = x.EntityId,
                tableId = x.TableId,
                orderId = x.OrderId,
                orderItemId = x.OrderItemId,
                dishId = x.DishId,
                billId = x.BillId,
                diningSessionCode = x.DiningSessionCode,
                correlationId = x.CorrelationId,
                idempotencyKey = x.IdempotencyKey,
                notes = x.Notes,
                beforeState = x.BeforeState,
                afterState = x.AfterState
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    private async Task<ActionResult?> ValidateDishRequest(AdminUpsertDishRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Tên món không được để trống." });
        }

        if (request.Price is null || request.Price < 0)
        {
            return BadRequest(new { message = "Giá bán không hợp lệ." });
        }

        if (request.CategoryId is null || request.CategoryId <= 0)
        {
            return BadRequest(new { message = "Vui lòng chọn danh mục." });
        }

        var categoryExists = await _db.Categories.AnyAsync(c => c.CategoryID == request.CategoryId && (c.IsActive ?? false), cancellationToken);
        if (!categoryExists)
        {
            return BadRequest(new { message = "Danh mục không hợp lệ." });
        }

        return null;
    }

    private async Task<Menus> EnsureTodayMenuAsync(int branchId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var latestMenu = await _db.Menus
            .Where(m => m.BranchID == branchId && (m.IsActive ?? true))
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.MenuID)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestMenu is not null && latestMenu.Date == today)
        {
            await BackfillMenuFromPreviousAsync(latestMenu, cancellationToken);
            return latestMenu;
        }

        var branchName = await _db.Branches
            .Where(b => b.BranchID == branchId)
            .Select(b => b.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"Chi nhánh {branchId}";

        var menu = new Menus
        {
            MenuName = $"Thực đơn {branchName} - {today:dd/MM/yyyy}",
            Date = today,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            BranchID = branchId
        };

        _db.Menus.Add(menu);
        await _db.SaveChangesAsync(cancellationToken);
        await BackfillMenuFromPreviousAsync(menu, cancellationToken);
        return menu;
    }

    private async Task BackfillMenuFromPreviousAsync(Menus targetMenu, CancellationToken cancellationToken)
    {
        var previousMenu = await _db.Menus
            .AsNoTracking()
            .Where(m => m.BranchID == targetMenu.BranchID
                && (m.IsActive ?? true)
                && m.MenuID != targetMenu.MenuID
                && (m.Date == null || m.Date <= targetMenu.Date))
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.MenuID)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousMenu is null)
        {
            return;
        }

        var previousCategories = await _db.MenuCategory
            .AsNoTracking()
            .Where(mc => mc.MenuID == previousMenu.MenuID && (mc.IsActive ?? true))
            .Select(mc => new
            {
                mc.CategoryID,
                Dishes = mc.CategoryDish
                    .Where(cd => cd.IsAvailable ?? true)
                    .Select(cd => new
                    {
                        cd.DishID,
                        cd.DisplayOrder,
                        IsAvailable = cd.IsAvailable ?? true
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        foreach (var category in previousCategories)
        {
            var targetCategory = await EnsureMenuCategoryAsync(targetMenu.MenuID, category.CategoryID, cancellationToken);
            foreach (var dish in category.Dishes.OrderBy(x => x.DisplayOrder))
            {
                await EnsureCategoryDishAsync(targetCategory.MenuCategoryID, dish.DishID, dish.IsAvailable, cancellationToken);
            }
        }
    }

    private async Task<MenuCategory> EnsureMenuCategoryAsync(int menuId, int categoryId, CancellationToken cancellationToken)
    {
        var existing = await _db.MenuCategory
            .FirstOrDefaultAsync(mc => mc.MenuID == menuId && mc.CategoryID == categoryId, cancellationToken);
        if (existing is not null)
        {
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var menuCategory = new MenuCategory
        {
            MenuID = menuId,
            CategoryID = categoryId,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _db.MenuCategory.Add(menuCategory);
        await _db.SaveChangesAsync(cancellationToken);
        return menuCategory;
    }

    private async Task EnsureCategoryDishAsync(int menuCategoryId, int dishId, bool isAvailable, CancellationToken cancellationToken)
    {
        var existing = await _db.CategoryDish
            .FirstOrDefaultAsync(cd => cd.MenuCategoryID == menuCategoryId && cd.DishID == dishId, cancellationToken);
        if (existing is not null)
        {
            existing.IsAvailable = isAvailable;
            existing.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var maxDisplayOrder = await _db.CategoryDish
            .Where(cd => cd.MenuCategoryID == menuCategoryId)
            .MaxAsync(cd => (int?)cd.DisplayOrder, cancellationToken) ?? 0;

        _db.CategoryDish.Add(new CategoryDish
        {
            MenuCategoryID = menuCategoryId,
            DishID = dishId,
            DisplayOrder = maxDisplayOrder + 1,
            IsAvailable = isAvailable,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static ActionResult? ValidateIngredientRequest(AdminUpsertIngredientRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new BadRequestObjectResult(new { message = "Tên nguyên liệu không được để trống." });
        }

        if (string.IsNullOrWhiteSpace(request.Unit))
        {
            return new BadRequestObjectResult(new { message = "Đơn vị không được để trống." });
        }

        if (request.CurrentStock is null || request.CurrentStock < 0)
        {
            return new BadRequestObjectResult(new { message = "Tồn kho không hợp lệ." });
        }

        if (request.ReorderLevel is null || request.ReorderLevel < 0)
        {
            return new BadRequestObjectResult(new { message = "Mức cảnh báo không hợp lệ." });
        }

        return null;
    }

    private static string NormalizeIssueMethod(string? issueMethod)
        => string.Equals(issueMethod, "FIFO", StringComparison.OrdinalIgnoreCase) ? "FIFO" : "FEFO";

    private async Task<ActionResult?> ValidateTableRequest(AdminUpsertTableRequest request, CancellationToken cancellationToken)
    {
        if (request.BranchId is null || request.BranchId <= 0)
        {
            return BadRequest(new { message = "Vui lòng chọn chi nhánh." });
        }

        var branchExists = await _db.Branches.AnyAsync(b => b.BranchID == request.BranchId && (b.IsActive ?? false), cancellationToken);
        if (!branchExists)
        {
            return BadRequest(new { message = "Chi nhánh không hợp lệ." });
        }

        if (request.NumberOfSeats is null || request.NumberOfSeats <= 0)
        {
            return BadRequest(new { message = "Số ghế không hợp lệ." });
        }

        if (request.StatusId is > 0)
        {
            var statusExists = await _db.TableStatus.AnyAsync(s => s.StatusID == request.StatusId, cancellationToken);
            if (!statusExists)
            {
                return BadRequest(new { message = "Trạng thái bàn không hợp lệ." });
            }
        }

        return null;
    }

    private async Task<int> GetNextTableNumberAsync(int branchId, CancellationToken cancellationToken)
    {
        var currentMax = await _db.DiningTables
            .Where(t => t.BranchID == branchId)
            .Select(t => (int?)t.TableNumber)
            .MaxAsync(cancellationToken);

        return (currentMax ?? 0) + 1;
    }

    private async Task<Dictionary<string, UnitUsage>> BuildUnitUsageAsync(CancellationToken cancellationToken)
    {
        var usage = new Dictionary<string, UnitUsage>(StringComparer.Ordinal);

        var dishCounts = await _db.Dishes
            .AsNoTracking()
            .Where(d => (d.IsActive ?? false) && d.Unit != null && d.Unit != "")
            .GroupBy(d => d.Unit!)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        foreach (var row in dishCounts)
        {
            usage[row.Name] = usage.TryGetValue(row.Name, out var current)
                ? current with { DishCount = row.Count }
                : new UnitUsage(row.Count, 0);
        }

        var ingredientCounts = await _db.Ingredients
            .AsNoTracking()
            .Where(i => i.IsActive && i.Unit != "")
            .GroupBy(i => i.Unit)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        foreach (var row in ingredientCounts)
        {
            usage[row.Name] = usage.TryGetValue(row.Name, out var current)
                ? current with { IngredientCount = row.Count }
                : new UnitUsage(0, row.Count);
        }

        return usage;
    }

    private async Task<UnitUsage> GetUnitUsageAsync(string name, CancellationToken cancellationToken)
    {
        var dishCount = await _db.Dishes.CountAsync(d => d.Unit == name, cancellationToken);
        var ingredientCount = await _db.Ingredients.CountAsync(i => i.Unit == name, cancellationToken);
        return new UnitUsage(dishCount, ingredientCount);
    }

    private async Task<UnitUsageDetails> GetUnitUsageDetailsAsync(string name, CancellationToken cancellationToken)
    {
        var dishNames = await _db.Dishes
            .AsNoTracking()
            .Where(d => d.Unit == name)
            .OrderBy(d => d.Name)
            .Select(d => d.Name)
            .ToListAsync(cancellationToken);

        var ingredientNames = await _db.Ingredients
            .AsNoTracking()
            .Where(i => i.Unit == name)
            .OrderBy(i => i.Name)
            .Select(i => i.Name)
            .ToListAsync(cancellationToken);

        return new UnitUsageDetails(name, dishNames, ingredientNames);
    }

    private static string BuildUnitInUseMessage(string unitName, IReadOnlyList<string> dishNames, IReadOnlyList<string> ingredientNames)
    {
        var lines = new List<string>
        {
            $"Không thể xóa đơn vị \"{unitName}\" vì đang được dùng bởi:"
        };

        if (dishNames.Count > 0)
        {
            lines.Add($"* Món ăn: {FormatUnitUsageNames(dishNames)}");
        }

        if (ingredientNames.Count > 0)
        {
            lines.Add($"* Nguyên liệu: {FormatUnitUsageNames(ingredientNames)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatUnitUsageNames(IReadOnlyList<string> names)
    {
        var visible = names.Take(5).ToList();
        var suffix = names.Count > visible.Count ? $", ... và {names.Count - visible.Count} mục khác" : string.Empty;
        return string.Join(", ", visible) + suffix;
    }

    private static ActionResult? ValidateInventoryStockInRequest(InventoryStockInRequest request)
    {
        if (request.IngredientId <= 0)
        {
            return new BadRequestObjectResult(new { message = "Nguyên liệu không hợp lệ." });
        }

        if (request.Quantity is null || request.Quantity <= 0)
        {
            return new BadRequestObjectResult(new { message = "Số lượng nhập kho phải lớn hơn 0." });
        }

        if (request.ReceivedDate is null)
        {
            return new BadRequestObjectResult(new { message = "Ngày nhập là bắt buộc." });
        }

        if (request.ExpiryDate is null)
        {
            return new BadRequestObjectResult(new { message = "Hạn sử dụng là bắt buộc." });
        }

        return null;
    }

    private void DeductInventoryBatch(
        IngredientBatches batch,
        decimal quantity,
        string movementType,
        string note,
        DateTime now)
    {
        batch.QuantityRemaining -= quantity;
        batch.UpdatedAt = now;
        _db.IngredientStockMovements.Add(new IngredientStockMovements
        {
            IngredientID = batch.IngredientID,
            BatchID = batch.BatchID,
            QuantityChange = -quantity,
            MovementType = movementType,
            ReferenceType = "ADMIN",
            CreatedAt = now,
            Note = note
        });
    }

    private async Task SyncIngredientCurrentStockAsync(Ingredients ingredient, CancellationToken cancellationToken)
    {
        var hasActiveBatches = await _db.IngredientBatches
            .AnyAsync(b => b.IngredientID == ingredient.IngredientID && b.IsActive, cancellationToken);
        if (!hasActiveBatches)
        {
            return;
        }

        ingredient.CurrentStock = await _db.IngredientBatches
            .Where(b => b.IngredientID == ingredient.IngredientID && b.IsActive && b.QuantityRemaining > 0)
            .SumAsync(b => b.QuantityRemaining, cancellationToken);
    }

    private async Task IncreaseIngredientCurrentStockAsync(int ingredientId, decimal quantity, CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.Ingredients SET CurrentStock = CurrentStock + {quantity} WHERE IngredientID = {ingredientId}",
            cancellationToken);
    }

    private async Task<int> TryDecreaseIngredientCurrentStockAsync(int ingredientId, decimal quantity, CancellationToken cancellationToken)
    {
        return await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.Ingredients SET CurrentStock = CurrentStock - {quantity} WHERE IngredientID = {ingredientId} AND CurrentStock >= {quantity}",
            cancellationToken);
    }

    private static string NormalizeInventoryBatchStatusFilter(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        return normalized is "expired" or "near-expiry" or "valid" or "empty" or "inactive" ? normalized : "all";
    }

    private static string GetInventoryBatchStatus(bool isActive, decimal quantityRemaining, DateOnly expiryDate, DateOnly today)
    {
        if (!isActive) return "Đã vô hiệu";
        if (quantityRemaining <= 0) return "Đã hết";
        if (expiryDate < today) return "Đã hết hạn";
        if (expiryDate <= today.AddDays(InventoryNearExpiryDays)) return "Sắp hết hạn";
        return "Còn hạn";
    }

    private static string NormalizeStockOutReason(string? reason)
    {
        var normalized = reason?.Trim().ToUpperInvariant();
        return normalized is "EXPIRED_DISPOSAL" or "WASTE" or "ADJUST" or "OTHER" ? normalized : "OTHER";
    }

    private static string GetStockOutReasonLabel(string reason) => reason switch
    {
        "EXPIRED_DISPOSAL" => "Hủy do hết hạn",
        "WASTE" => "Hao hụt / hư hỏng",
        "ADJUST" => "Điều chỉnh tồn kho",
        _ => "Xuất kho thủ công"
    };

    private static string BuildIngredientSummary(IEnumerable<string?> names)
    {
        var distinctNames = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctNames.Count == 0)
        {
            return "-";
        }

        var visible = distinctNames.Take(3).ToList();
        return distinctNames.Count > visible.Count
            ? string.Join(", ", visible) + ", ..."
            : string.Join(", ", visible);
    }

    private async Task<Dictionary<int, IngredientBatchSummary>> BuildIngredientBatchSummariesAsync(
        IEnumerable<int> ingredientIds,
        CancellationToken cancellationToken)
    {
        var ids = ingredientIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, IngredientBatchSummary>();
        }

        var availability = await _stockAvailability.BuildIngredientStockAvailabilityMapAsync(ids, cancellationToken);
        return availability.ToDictionary(
            item => item.Key,
            item => new IngredientBatchSummary(
                item.Value.TotalBatchStock,
                item.Value.UsableBatchStock,
                item.Value.NearestExpiryDate,
                item.Value.ExpiredBatchCount,
                item.Value.NearExpiryBatchCount));
    }

    private static ActionResult? ValidateIngredientBatchRequest(
        decimal? quantityInitial,
        decimal? quantityRemaining,
        DateOnly? expiryDate,
        DateOnly? receivedDate)
    {
        if (quantityInitial is null || quantityInitial <= 0)
        {
            return new BadRequestObjectResult(new { message = "Số lượng nhập phải lớn hơn 0." });
        }

        if (quantityRemaining is null || quantityRemaining < 0)
        {
            return new BadRequestObjectResult(new { message = "Số lượng còn lại không hợp lệ." });
        }

        if (quantityRemaining > quantityInitial)
        {
            return new BadRequestObjectResult(new { message = "Số lượng còn lại không được lớn hơn số lượng nhập." });
        }

        if (expiryDate is null)
        {
            return new BadRequestObjectResult(new { message = "Hạn sử dụng là bắt buộc." });
        }

        if (receivedDate is null)
        {
            return new BadRequestObjectResult(new { message = "Ngày nhập là bắt buộc." });
        }

        return null;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public sealed record PagedResponse<T>(int Page, int PageSize, int TotalItems, int TotalPages, IReadOnlyList<T> Items);
    private sealed record UnitUsage(int DishCount, int IngredientCount);
    private sealed record UnitUsageDetails(string UnitName, IReadOnlyList<string> DishNames, IReadOnlyList<string> IngredientNames);
    private readonly record struct IngredientBatchSummary(
        decimal TotalBatchStock,
        decimal UsableBatchStock,
        DateOnly? NearestExpiryDate,
        int ExpiredBatchCount,
        int NearExpiryBatchCount);

    private static class IngredientMovementTypes
    {
        public const string Receive = "RECEIVE";
        public const string Adjust = "ADJUST";
        public const string Waste = "WASTE";
        public const string DeactivateBatch = "DEACTIVATE_BATCH";
    }

    public sealed record AdminDishResponse(
        int DishId,
        string Name,
        decimal Price,
        int CategoryId,
        string CategoryName,
        string? Description,
        string? Unit,
        string? Image,
        bool IsVegetarian,
        bool IsDailySpecial,
        bool Available,
        bool IsActive,
        string IngredientsSummary);
    public sealed record AdminUpsertDishRequest(
        string? Name,
        decimal? Price,
        int? CategoryId,
        string? Description,
        string? Unit,
        string? Image,
        bool? IsVegetarian,
        bool? IsDailySpecial,
        bool? Available,
        bool? IsActive);
    public sealed record ChefDishMutationResponse(int DishId, string Message);
    public sealed record AdminDishIngredientsResponse(
        int DishId,
        string DishName,
        IReadOnlyList<AdminDishIngredientLineResponse> Ingredients);
    public sealed record AdminDishIngredientLineResponse(
        int IngredientId,
        string Name,
        string Unit,
        decimal CurrentStock,
        bool IsActive,
        bool Selected,
        decimal QuantityPerDish);
    public sealed record UpdateDishIngredientsRequest(IReadOnlyList<UpdateDishIngredientItem>? Items);
    public sealed record UpdateDishIngredientItem(int IngredientId, decimal QuantityPerDish);

    public sealed record AdminRelatedIngredientDishResponse(
        int DishId,
        string Name,
        string? CategoryName,
        decimal QuantityPerDish,
        string Unit,
        bool Available,
        bool IsActive);
    public sealed record ChefDishAvailabilityRequest(bool Available);
    public sealed record ChefDishAvailabilityResponse(bool Success, string Message, bool Available);

    public sealed record AdminIngredientResponse(
        int IngredientId,
        string Name,
        string Unit,
        decimal CurrentStock,
        decimal ReorderLevel,
        string IssueMethod,
        bool IsActive,
        decimal TotalBatchStock,
        decimal UsableBatchStock,
        DateOnly? NearestExpiryDate,
        int ExpiredBatchCount,
        int NearExpiryBatchCount);
    public sealed record AdminUpsertIngredientRequest(
        string? Name,
        string? Unit,
        decimal? CurrentStock,
        decimal? ReorderLevel,
        string? IssueMethod,
        bool? IsActive);
    public sealed record AdminIngredientBatchResponse(
        int BatchId,
        int IngredientId,
        string? BatchCode,
        decimal QuantityInitial,
        decimal QuantityRemaining,
        string Unit,
        DateOnly ExpiryDate,
        DateOnly ReceivedDate,
        string? SupplierName,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        string Status);
    public sealed record CreateIngredientBatchRequest(
        string? BatchCode,
        decimal? QuantityInitial,
        decimal? QuantityRemaining,
        string? Unit,
        DateOnly? ExpiryDate,
        DateOnly? ReceivedDate,
        string? SupplierName);
    public sealed record UpdateIngredientBatchRequest(
        string? BatchCode,
        DateOnly? ExpiryDate,
        DateOnly? ReceivedDate,
        string? SupplierName,
        bool? IsActive);
    public sealed record IngredientStockMovementResponse(
        long MovementId,
        int IngredientId,
        int? BatchId,
        decimal QuantityChange,
        string MovementType,
        string? ReferenceType,
        int? ReferenceId,
        int? OrderId,
        int? OrderItemId,
        int? DishId,
        DateTime CreatedAt,
        string? Note);

    public sealed record InventorySummaryResponse(
        int TotalActiveIngredients,
        int ExpiredBatchCount,
        int NearExpiryBatchCount,
        int TotalBatchesWithStock,
        decimal TotalUsableBatchStock,
        int LowStockIngredientCount,
        int NearExpiryDays);
    public sealed record InventoryBatchResponse(
        int BatchId,
        int IngredientId,
        string IngredientName,
        string? BatchCode,
        decimal QuantityInitial,
        decimal QuantityRemaining,
        string Unit,
        DateOnly ReceivedDate,
        DateOnly ExpiryDate,
        string? SupplierName,
        bool IsActive,
        string Status,
        int DaysUntilExpiry);
    public sealed record InventoryStockInRequest(
        int IngredientId,
        decimal? Quantity,
        DateOnly? ReceivedDate,
        DateOnly? ExpiryDate,
        string? BatchCode,
        string? SupplierName,
        string? Note);
    public sealed record InventoryStockOutRequest(
        int IngredientId,
        decimal? Quantity,
        int? BatchId,
        string? Reason,
        string? Note);
    public sealed record InventoryMovementResponse(
        long MovementId,
        int IngredientId,
        string IngredientName,
        string Unit,
        int? BatchId,
        string? BatchCode,
        DateOnly? ExpiryDate,
        string? SupplierName,
        decimal QuantityChange,
        string MovementType,
        string? ReferenceType,
        int? ReferenceId,
        int? OrderId,
        int? OrderItemId,
        int? DishId,
        DateTime CreatedAt,
        string? Note);

    public sealed record TableStatusResponse(int StatusId, string StatusCode, string StatusName);
    public sealed record AdminUnitResponse(
        int UnitId,
        string Name,
        string? Description,
        int DisplayOrder,
        bool IsActive,
        int DishCount,
        int IngredientCount);
    public sealed record AdminUpsertUnitRequest(string? Name, string? Description, int? DisplayOrder, bool? IsActive);
    public sealed record AdminTableResponse(
        int TableId,
        int TableNumber,
        int BranchId,
        string BranchName,
        int NumberOfSeats,
        string? QRCode,
        int StatusId,
        string StatusCode,
        string StatusName,
        bool IsActive);
    public sealed record AdminUpsertTableRequest(
        int? BranchId,
        int? NumberOfSeats,
        string? QRCode,
        int? StatusId,
        bool? IsActive);
}
