using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Catalog.Api.Infrastructure.Inventory;
using SelfRestaurant.Catalog.Api.Persistence;
using SelfRestaurant.Catalog.Api.Persistence.Entities;
using System.Data;

namespace SelfRestaurant.Catalog.Api.Controllers;

[ApiController]
public sealed class CatalogController : ControllerBase
{
    private const string MovementTypeConsume = "CONSUME";
    private const string MovementReferenceOrder = "ORDER";
    private readonly CatalogDbContext _db;
    private readonly IngredientStockAvailabilityService _stockAvailability;
    private readonly ILogger<CatalogController> _logger;
    private readonly IHostEnvironment _environment;

    public CatalogController(
        CatalogDbContext db,
        IngredientStockAvailabilityService stockAvailability,
        ILogger<CatalogController> logger,
        IHostEnvironment environment)
    {
        _db = db;
        _stockAvailability = stockAvailability;
        _logger = logger;
        _environment = environment;
    }

    [HttpGet("api/branches")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetBranches(CancellationToken cancellationToken)
    {
        var items = await _db.Branches
            .AsNoTracking()
            .Where(x => x.IsActive ?? true)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                branchId = x.BranchID,
                name = x.Name,
                location = x.Location,
                image = x.Image,
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("api/branches/{branchId:int}/tables")]
    public async Task<ActionResult<object>> GetBranchTables(int branchId, CancellationToken cancellationToken)
    {
        var branch = await _db.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchID == branchId && (x.IsActive ?? true), cancellationToken);
        if (branch is null)
        {
            return NotFound();
        }

        var tables = await _db.DiningTables
            .AsNoTracking()
            .Where(x => x.BranchID == branchId && (x.IsActive ?? true))
            .Include(x => x.Status)
            .OrderBy(x => x.TableNumber)
            .ThenBy(x => x.TableID)
            .Select(x => new
            {
                tableId = x.TableID,
                branchId = x.BranchID,
                displayTableNumber = x.TableNumber,
                numberOfSeats = x.NumberOfSeats,
                qrCode = x.QRCode,
                statusCode = x.Status.StatusCode,
                statusName = x.Status.StatusName,
                isAvailable = x.Status.StatusCode == "AVAILABLE",
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            branchName = branch.Name,
            tables,
        });
    }

    [HttpGet("api/branches/{branchId:int}/menu")]
    public async Task<ActionResult<object>> GetMenu(int branchId, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var branch = await _db.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchID == branchId && (x.IsActive ?? true), cancellationToken);
        if (branch is null)
        {
            return NotFound();
        }

        // Always return all active dishes grouped by category (ignore menu configuration)
        var allCategories = await _db.Categories
            .AsNoTracking()
            .Where(x => x.IsActive ?? true)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var allCategoryObjects = new List<object>();
        var categoryIds = allCategories.Select(x => x.CategoryID).ToArray();
        // One projected Catalog-owned read, not one remote DB round trip per category.
        var allDishes = await _db.Dishes.AsNoTracking()
            .Where(x => categoryIds.Contains(x.CategoryID) && (x.IsActive ?? true))
            .OrderBy(x => x.Name)
            .Select(x => new {
                categoryId = x.CategoryID, dishId = x.DishID, name = x.Name,
                description = x.Description, price = x.Price, image = x.Image, unit = x.Unit,
                isVegetarian = x.IsVegetarian ?? false, isDailySpecial = x.IsDailySpecial ?? false,
                available = x.Available ?? true,
                ingredients = x.DishIngredients.Select(i => new {
                    name = i.Ingredient.Name, quantity = i.QuantityPerDish, unit = i.Ingredient.Unit
                }).ToList()
            }).ToListAsync(cancellationToken);
        foreach (var category in allCategories)
        {
            var dishes = allDishes.Where(x => x.categoryId == category.CategoryID)
                .Select(x => new { x.dishId, x.name, x.description, x.price, x.image, x.unit,
                    x.isVegetarian, x.isDailySpecial, x.available, x.ingredients }).ToList();

            if (dishes.Count > 0)
            {
                allCategoryObjects.Add(new
                {
                    categoryId = category.CategoryID,
                    categoryName = category.Name,
                    dishes
                });
            }
        }

        return Ok(new
        {
            branchId = branch.BranchID,
            branchName = branch.Name,
            categories = allCategoryObjects,
        });
    }

    [HttpGet("api/tables/qr/{code}")]
    public async Task<ActionResult<object>> GetTableByQr(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest();
        }

        var table = await _db.DiningTables
            .AsNoTracking()
            .Where(x => x.QRCode == code && (x.IsActive ?? true))
            .Select(x => new
            {
                tableId = x.TableID,
                branchId = x.BranchID,
                displayTableNumber = x.TableNumber,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return table is null ? NotFound() : Ok(table);
    }

    [HttpGet("api/internal/tables/{tableId:int}")]
    public async Task<ActionResult<object>> GetInternalTable(int tableId, CancellationToken cancellationToken)
    {
        var table = await _db.DiningTables
            .AsNoTracking()
            .Include(x => x.Status)
            .Where(x => x.TableID == tableId && (x.IsActive ?? true))
            .Select(x => new
            {
                tableId = x.TableID,
                branchId = x.BranchID,
                tableNumber = x.TableNumber,
                qrCode = x.QRCode,
                isActive = x.IsActive ?? true,
                statusId = x.StatusID,
                statusCode = x.Status != null ? x.Status.StatusCode : null,
                statusName = x.Status != null ? x.Status.StatusName : null,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return table is null ? NotFound() : Ok(table);
    }

    [HttpGet("api/internal/tables:batch")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetInternalTablesBatch([FromQuery] int[] ids, CancellationToken cancellationToken)
    {
        var tableIds = ids.Where(x => x > 0).Distinct().ToArray();
        if (tableIds.Length == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var tables = await _db.DiningTables
            .AsNoTracking()
            .Include(x => x.Status)
            .Where(x => tableIds.Contains(x.TableID) && (x.IsActive ?? true))
            .Select(x => new
            {
                tableId = x.TableID,
                branchId = x.BranchID,
                tableNumber = x.TableNumber,
                qrCode = x.QRCode,
                isActive = x.IsActive ?? true,
                statusId = x.StatusID,
                statusCode = x.Status != null ? x.Status.StatusCode : null,
                statusName = x.Status != null ? x.Status.StatusName : null,
            })
            .ToListAsync(cancellationToken);

        return Ok(tables);
    }

    [HttpPost("api/dev/reset-test-state")]
    public async Task<ActionResult<object>> ResetDevTestState(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var availableStatusId = await _db.TableStatus
            .Where(x => x.StatusCode == "AVAILABLE")
            .Select(x => (int?)x.StatusID)
            .FirstOrDefaultAsync(cancellationToken);

        if (!availableStatusId.HasValue)
        {
            return Problem("Hệ thống chưa cấu hình trạng thái AVAILABLE cho bàn.", statusCode: StatusCodes.Status500InternalServerError);
        }

        var tables = await _db.DiningTables
            .Where(x => x.IsActive ?? true)
            .ToListAsync(cancellationToken);

        foreach (var table in tables)
        {
            table.CurrentOrderID = null;
            table.StatusID = availableStatusId.Value;
            table.UpdatedAt = DateTime.Now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            resetTables = tables.Count
        });
    }

    [HttpGet("api/internal/dishes/{dishId:int}")]
    public async Task<ActionResult<object>> GetInternalDish(int dishId, CancellationToken cancellationToken)
    {
        var orderableDishIds = await FilterOrderableDishIdsAsync(new[] { dishId }, cancellationToken);

        var dish = await _db.Dishes
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.DishID == dishId && (x.IsActive ?? true))
            .Select(x => new
            {
                dishId = x.DishID,
                name = x.Name,
                categoryId = x.CategoryID,
                categoryName = x.Category != null ? x.Category.Name : null,
                price = x.Price,
                unit = x.Unit,
                image = x.Image,
                isActive = x.IsActive ?? true,
                available = (x.Available ?? true) && orderableDishIds.Contains(x.DishID),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dish is null ? NotFound() : Ok(dish);
    }

    [HttpGet("api/internal/dishes:batch")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetInternalDishesBatch([FromQuery] int[] ids, CancellationToken cancellationToken)
    {
        var dishIds = ids.Where(x => x > 0).Distinct().ToArray();
        if (dishIds.Length == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var orderableDishIds = await FilterOrderableDishIdsAsync(dishIds, cancellationToken);
        var dishes = await _db.Dishes
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => dishIds.Contains(x.DishID) && (x.IsActive ?? true))
            .Select(x => new
            {
                dishId = x.DishID,
                name = x.Name,
                categoryId = x.CategoryID,
                categoryName = x.Category != null ? x.Category.Name : null,
                price = x.Price,
                unit = x.Unit,
                image = x.Image,
                isActive = x.IsActive ?? true,
                available = (x.Available ?? true) && orderableDishIds.Contains(x.DishID),
            })
            .ToListAsync(cancellationToken);

        return Ok(dishes);
    }

    [HttpGet("api/internal/table-statuses/{statusCode}")]
    public async Task<ActionResult<object>> GetInternalTableStatus(string statusCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(statusCode))
        {
            return BadRequest();
        }

        var normalized = statusCode.Trim().ToUpperInvariant();
        var status = await _db.TableStatus
            .AsNoTracking()
            .Where(x => x.StatusCode == normalized)
            .Select(x => new
            {
                statusId = x.StatusID,
                statusCode = x.StatusCode,
                statusName = x.StatusName,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return status is null ? NotFound() : Ok(status);
    }

    [HttpGet("api/internal/branches:batch")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetInternalBranchesBatch([FromQuery] int[] ids, CancellationToken cancellationToken)
    {
        var branchIds = ids.Where(x => x > 0).Distinct().ToArray();
        if (branchIds.Length == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var branches = await _db.Branches
            .AsNoTracking()
            .Where(x => branchIds.Contains(x.BranchID) && (x.IsActive ?? true))
            .Select(x => new
            {
                branchId = x.BranchID,
                name = x.Name,
                location = x.Location,
                isActive = x.IsActive ?? true,
            })
            .ToListAsync(cancellationToken);

        return Ok(branches);
    }

    [HttpGet("api/internal/branches/{branchId:int}/table-ids")]
    public async Task<ActionResult<IReadOnlyList<int>>> GetInternalBranchTableIds(int branchId, CancellationToken cancellationToken)
    {
        var ids = await _db.DiningTables
            .AsNoTracking()
            .Where(x => x.BranchID == branchId && (x.IsActive ?? true))
            .OrderBy(x => x.TableID)
            .Select(x => x.TableID)
            .ToListAsync(cancellationToken);

        return Ok(ids);
    }

    [HttpPost("api/internal/tables/{tableId:int}/occupy")]
    public async Task<ActionResult> OccupyInternalTable(
        int tableId,
        [FromBody] TableOccupancyRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _db.DiningTables.FirstOrDefaultAsync(x => x.TableID == tableId && (x.IsActive ?? true), cancellationToken);
        if (table is null)
        {
            return NotFound();
        }

        var occupiedId = await _db.TableStatus
            .Where(x => x.StatusCode == "OCCUPIED")
            .Select(x => (int?)x.StatusID)
            .FirstOrDefaultAsync(cancellationToken);

        if (occupiedId is null)
        {
            return BadRequest("Hệ thống chưa cấu hình trạng thái OCCUPIED cho bàn.");
        }

        table.StatusID = occupiedId.Value;
        table.CurrentOrderID = request.CurrentOrderId;
        table.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("api/internal/tables/{tableId:int}/release")]
    public async Task<ActionResult> ReleaseInternalTable(int tableId, CancellationToken cancellationToken)
    {
        var table = await _db.DiningTables.FirstOrDefaultAsync(x => x.TableID == tableId && (x.IsActive ?? true), cancellationToken);
        if (table is null)
        {
            return NotFound();
        }

        var availableId = await _db.TableStatus
            .Where(x => x.StatusCode == "AVAILABLE")
            .Select(x => (int?)x.StatusID)
            .FirstOrDefaultAsync(cancellationToken);

        if (availableId is null)
        {
            return BadRequest("Hệ thống chưa cấu hình trạng thái AVAILABLE cho bàn.");
        }

        table.StatusID = availableId.Value;
        table.CurrentOrderID = null;
        table.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("api/internal/tables/reset-all")]
    public async Task<ActionResult<object>> ResetAllInternalTables(CancellationToken cancellationToken)
    {
        var availableId = await _db.TableStatus
            .Where(x => x.StatusCode == "AVAILABLE")
            .Select(x => (int?)x.StatusID)
            .FirstOrDefaultAsync(cancellationToken);

        if (availableId is null)
        {
            return BadRequest("Hệ thống chưa cấu hình trạng thái AVAILABLE cho bàn.");
        }

        var tables = await _db.DiningTables
            .Where(x => x.IsActive ?? true)
            .ToListAsync(cancellationToken);

        var now = DateTime.Now;
        foreach (var table in tables)
        {
            table.StatusID = availableId.Value;
            table.CurrentOrderID = null;
            table.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, resetTables = tables.Count });
    }

    [HttpPost("api/internal/inventory/consume")]
    public async Task<ActionResult<IngredientConsumptionResponse>> ConsumeInventoryForOrder(
        [FromBody] IngredientConsumptionRequest request,
        CancellationToken cancellationToken)
    {
        var requestedItems = (request.Items ?? Array.Empty<IngredientConsumptionItem>())
            .Where(x => x.DishId > 0 && x.Quantity > 0)
            .GroupBy(x => new { x.DishId, OrderItemId = x.OrderItemId is > 0 ? x.OrderItemId : null })
            .Select(g => new ConsumptionOrderItem(g.Key.DishId, g.Sum(x => x.Quantity), g.Key.OrderItemId))
            .ToList();

        if (requestedItems.Count == 0)
        {
            return BadRequest(new IngredientConsumptionResponse(
                false,
                "Đơn hàng không có món hợp lệ để trừ kho.",
                Array.Empty<IngredientConsumptionIssue>()));
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var requestedOrderItemIds = requestedItems
            .Where(x => x.OrderItemId is > 0)
            .Select(x => x.OrderItemId!.Value)
            .Distinct()
            .ToArray();
        var requestedDishIds = requestedItems.Select(x => x.DishId).Distinct().ToArray();
        var existingMovements = await _db.IngredientStockMovements
            .AsNoTracking()
            .Where(m => m.MovementType == MovementTypeConsume
                && m.OrderID == request.OrderId
                && ((m.OrderItemID != null && requestedOrderItemIds.Contains(m.OrderItemID.Value))
                    || (m.OrderItemID == null && m.DishID != null && requestedDishIds.Contains(m.DishID.Value))))
            .Select(m => new { m.OrderItemID, m.DishID })
            .ToListAsync(cancellationToken);
        var consumedOrderItemIds = existingMovements
            .Where(m => m.OrderItemID is > 0)
            .Select(m => m.OrderItemID!.Value)
            .ToHashSet();
        var consumedLegacyDishIds = existingMovements
            .Where(m => m.OrderItemID is null && m.DishID is > 0)
            .Select(m => m.DishID!.Value)
            .ToHashSet();

        var items = requestedItems
            .Where(item => item.OrderItemId is > 0
                ? !consumedOrderItemIds.Contains(item.OrderItemId.Value)
                : !consumedLegacyDishIds.Contains(item.DishId))
            .ToList();

        if (items.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return Ok(new IngredientConsumptionResponse(
                true,
                "Đơn hàng đã được trừ kho nguyên liệu trước đó.",
                Array.Empty<IngredientConsumptionIssue>()));
        }

        var dishIds = items.Select(x => x.DishId).Distinct().ToArray();
        var recipes = await _db.DishIngredients
            .Include(x => x.Ingredient)
            .Where(x => dishIds.Contains(x.DishID) && x.Ingredient.IsActive)
            .ToListAsync(cancellationToken);

        if (recipes.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return Ok(new IngredientConsumptionResponse(
                true,
                "Không có công thức nguyên liệu cần trừ cho đơn hàng này.",
                Array.Empty<IngredientConsumptionIssue>()));
        }

        var recipeLines = items
            .Join(
                recipes,
                item => item.DishId,
                recipe => recipe.DishID,
                (item, recipe) => new ConsumptionRecipeLine(
                    item.DishId,
                    item.OrderItemId,
                    recipe.Ingredient,
                    recipe.QuantityPerDish * item.Quantity))
            .Where(x => x.RequiredQuantity > 0)
            .ToList();
        var requirements = recipeLines
            .GroupBy(x => x.Ingredient.IngredientID)
            .Select(g =>
            {
                var first = g.First();
                return new
                {
                    Ingredient = first.Ingredient,
                    RequiredQuantity = g.Sum(x => x.RequiredQuantity)
                };
            })
            .Where(x => x.RequiredQuantity > 0)
            .ToList();

        var ingredientIds = requirements.Select(x => x.Ingredient.IngredientID).Distinct().ToArray();
        var activeBatches = await _db.IngredientBatches
            .Where(b => ingredientIds.Contains(b.IngredientID) && b.IsActive)
            .ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var issueMethodLookup = requirements.ToDictionary(
            x => x.Ingredient.IngredientID,
            x => NormalizeIssueMethod(x.Ingredient.IssueMethod));
        var batchLookup = activeBatches
            .GroupBy(b => b.IngredientID)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var issueMethod = issueMethodLookup.TryGetValue(g.Key, out var foundIssueMethod) ? foundIssueMethod : "FEFO";
                    return issueMethod == "FIFO"
                        ? g.OrderBy(b => b.ReceivedDate).ThenBy(b => b.BatchID).ToList()
                        : g.OrderBy(b => b.ExpiryDate).ThenBy(b => b.ReceivedDate).ThenBy(b => b.BatchID).ToList();
                });

        var insufficient = new List<IngredientConsumptionIssue>();
        foreach (var requirement in requirements)
        {
            batchLookup.TryGetValue(requirement.Ingredient.IngredientID, out var batches);
            batches ??= [];
            var availableQuantity = batches.Count > 0
                ? batches.Where(b => b.QuantityRemaining > 0 && b.ExpiryDate >= today).Sum(b => b.QuantityRemaining)
                : requirement.Ingredient.CurrentStock;
            if (availableQuantity < requirement.RequiredQuantity)
            {
                insufficient.Add(new IngredientConsumptionIssue(
                    requirement.Ingredient.IngredientID,
                    requirement.Ingredient.Name,
                    requirement.RequiredQuantity,
                    availableQuantity,
                    requirement.Ingredient.Unit));
            }
        }

        if (insufficient.Count > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new IngredientConsumptionResponse(
                false,
                "Không đủ nguyên liệu để bắt đầu chế biến đơn này.",
                insufficient));
        }

        var consumedAt = DateTime.UtcNow;
        foreach (var requirement in requirements)
        {
            var issueMethod = NormalizeIssueMethod(requirement.Ingredient.IssueMethod);
            var ingredientLines = recipeLines
                .Where(x => x.Ingredient.IngredientID == requirement.Ingredient.IngredientID)
                .ToList();
            batchLookup.TryGetValue(requirement.Ingredient.IngredientID, out var batches);
            batches ??= [];

            if (batches.Count > 0)
            {
                var usableBatches = batches
                    .Where(b => b.QuantityRemaining > 0 && b.ExpiryDate >= today)
                    .ToList();

                foreach (var line in ingredientLines)
                {
                    var remainingLineQuantity = line.RequiredQuantity;
                    foreach (var batch in usableBatches)
                    {
                        if (remainingLineQuantity <= 0)
                        {
                            break;
                        }

                        if (batch.QuantityRemaining <= 0)
                        {
                            continue;
                        }

                        var deducted = Math.Min(batch.QuantityRemaining, remainingLineQuantity);
                        batch.QuantityRemaining -= deducted;
                        batch.UpdatedAt = consumedAt;
                        remainingLineQuantity -= deducted;
                        AddConsumptionMovement(requirement.Ingredient.IngredientID, batch.BatchID, deducted, line, request.OrderId, consumedAt, issueMethod);
                    }

                    if (remainingLineQuantity > 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Conflict(new IngredientConsumptionResponse(
                            false,
                            "Không đủ nguyên liệu để bắt đầu chế biến đơn này.",
                            [
                                new IngredientConsumptionIssue(
                                    requirement.Ingredient.IngredientID,
                                    requirement.Ingredient.Name,
                                    requirement.RequiredQuantity,
                                    requirement.RequiredQuantity - remainingLineQuantity,
                                    requirement.Ingredient.Unit)
                            ]));
                    }
                }

                requirement.Ingredient.CurrentStock = batches
                    .Where(b => b.IsActive && b.QuantityRemaining > 0)
                    .Sum(b => b.QuantityRemaining);
            }
            else
            {
                var updatedRows = await TryDecreaseIngredientCurrentStockAsync(
                    requirement.Ingredient.IngredientID,
                    requirement.RequiredQuantity,
                    cancellationToken);
                if (updatedRows == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Conflict(new IngredientConsumptionResponse(
                        false,
                        "Không đủ nguyên liệu để bắt đầu chế biến đơn này.",
                        [
                            new IngredientConsumptionIssue(
                                requirement.Ingredient.IngredientID,
                                requirement.Ingredient.Name,
                                requirement.RequiredQuantity,
                                requirement.Ingredient.CurrentStock,
                                requirement.Ingredient.Unit)
                        ]));
                }

                foreach (var line in ingredientLines)
                {
                    AddConsumptionMovement(requirement.Ingredient.IngredientID, null, line.RequiredQuantity, line, request.OrderId, consumedAt, issueMethod);
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new IngredientConsumptionResponse(
            true,
            "Đã trừ kho nguyên liệu cho đơn hàng.",
            Array.Empty<IngredientConsumptionIssue>()));
    }

    [HttpPost("api/internal/inventory/validate")]
    public async Task<ActionResult<IngredientConsumptionResponse>> ValidateInventoryForOrder(
        [FromBody] IngredientConsumptionRequest request,
        CancellationToken cancellationToken)
    {
        var items = (request.Items ?? Array.Empty<IngredientConsumptionItem>())
            .Where(x => x.DishId > 0 && x.Quantity > 0)
            .GroupBy(x => x.DishId)
            .Select(g => new { DishId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        if (items.Count == 0)
        {
            return BadRequest(new IngredientConsumptionResponse(
                false,
                "Đơn hàng không có món hợp lệ để kiểm tra kho.",
                Array.Empty<IngredientConsumptionIssue>()));
        }

        var dishIds = items.Select(x => x.DishId).Distinct().ToArray();
        var recipes = await _db.DishIngredients
            .Include(x => x.Ingredient)
            .Where(x => dishIds.Contains(x.DishID) && x.Ingredient.IsActive)
            .ToListAsync(cancellationToken);

        if (recipes.Count == 0)
        {
            return Ok(new IngredientConsumptionResponse(
                true,
                "Không có công thức nguyên liệu cần kiểm tra cho đơn hàng này.",
                Array.Empty<IngredientConsumptionIssue>()));
        }

        var itemLookup = items.ToDictionary(x => x.DishId, x => x.Quantity);
        var requirements = recipes
            .GroupBy(x => x.IngredientID)
            .Select(g =>
            {
                var first = g.First();
                var requiredQuantity = g.Sum(recipe => recipe.QuantityPerDish * itemLookup.GetValueOrDefault(recipe.DishID, 0));
                return new
                {
                    Ingredient = first.Ingredient,
                    RequiredQuantity = requiredQuantity
                };
            })
            .Where(x => x.RequiredQuantity > 0)
            .ToList();

        var availabilityMap = await _stockAvailability.BuildIngredientStockAvailabilityMapAsync(
            requirements.Select(x => x.Ingredient.IngredientID),
            cancellationToken);

        var insufficient = requirements
            .Select(x =>
            {
                var availableQuantity = availabilityMap.TryGetValue(x.Ingredient.IngredientID, out var stock)
                    ? stock.AvailabilityStock
                    : 0;
                return new
                {
                    x.Ingredient,
                    x.RequiredQuantity,
                    AvailableQuantity = availableQuantity
                };
            })
            .Where(x => x.AvailableQuantity < x.RequiredQuantity)
            .Select(x => new IngredientConsumptionIssue(
                x.Ingredient.IngredientID,
                x.Ingredient.Name,
                x.RequiredQuantity,
                x.AvailableQuantity,
                x.Ingredient.Unit))
            .ToList();

        if (insufficient.Count > 0)
        {
            return Conflict(new IngredientConsumptionResponse(
                false,
                "Không đủ nguyên liệu để tiếp tục gửi món này xuống bếp.",
                insufficient));
        }

        return Ok(new IngredientConsumptionResponse(
            true,
            "Đủ nguyên liệu để tiếp tục gửi món.",
            Array.Empty<IngredientConsumptionIssue>()));
    }

    [HttpGet("api/categories")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetCategories([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive ?? true);
        }

        var categories = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                categoryId = x.CategoryID,
                name = x.Name,
                description = x.Description,
                displayOrder = x.DisplayOrder ?? 0,
                isActive = x.IsActive ?? true,
            })
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }

    [HttpPost("api/categories")]
    public async Task<ActionResult> CreateCategory([FromBody] CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Vui lòng nhập tên danh mục.");
        }

        var entity = new Categories
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
        };

        _db.Categories.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetCategories), new { id = entity.CategoryID }, new { categoryId = entity.CategoryID });
    }

    [HttpPut("api/categories/{categoryId:int}")]
    public async Task<ActionResult> UpdateCategory(int categoryId, [FromBody] CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(x => x.CategoryID == categoryId, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Vui lòng nhập tên danh mục.");
        }

        entity.Name = request.Name.Trim();
        entity.Description = request.Description;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("api/categories/{categoryId:int}")]
    public async Task<ActionResult> DeleteCategory(int categoryId, CancellationToken cancellationToken)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(x => x.CategoryID == categoryId, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if ((entity.IsActive ?? false) == true)
        {
            return Conflict(new { message = "Vui lòng vô hiệu hóa trước khi xóa." });
        }

        var hasDishes = await _db.Dishes.AnyAsync(x => x.CategoryID == categoryId, cancellationToken);
        var hasMenus = await _db.MenuCategory.AnyAsync(x => x.CategoryID == categoryId, cancellationToken);
        if (hasDishes || hasMenus)
        {
            return Conflict(new { message = "Không thể xóa danh mục đang được dùng bởi món ăn hoặc thực đơn." });
        }

        _db.Categories.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    public sealed record CategoryUpsertRequest(string Name, string? Description, int DisplayOrder, bool IsActive = true);
    public sealed record TableOccupancyRequest(int? CurrentOrderId);
    public sealed record IngredientConsumptionItem(int DishId, int Quantity, int? OrderItemId = null);
    public sealed record IngredientConsumptionIssue(
        int IngredientId,
        string IngredientName,
        decimal RequiredQuantity,
        decimal AvailableQuantity,
        string? Unit);
    public sealed record IngredientConsumptionRequest(int OrderId, IReadOnlyList<IngredientConsumptionItem>? Items);
    public sealed record IngredientConsumptionResponse(
        bool Success,
        string? Message,
        IReadOnlyList<IngredientConsumptionIssue> Issues);

    private void AddConsumptionMovement(
        int ingredientId,
        int? batchId,
        decimal quantity,
        ConsumptionRecipeLine line,
        int orderId,
        DateTime consumedAt,
        string issueMethod)
    {
        _db.IngredientStockMovements.Add(new IngredientStockMovements
        {
            IngredientID = ingredientId,
            BatchID = batchId,
            QuantityChange = -quantity,
            MovementType = MovementTypeConsume,
            ReferenceType = MovementReferenceOrder,
            ReferenceID = orderId,
            OrderID = orderId,
            OrderItemID = line.OrderItemId,
            DishID = line.DishId,
            CreatedAt = consumedAt,
            Note = batchId is null ? "Consumed from CurrentStock fallback" : $"Consumed by {NormalizeIssueMethod(issueMethod)}"
        });
    }

    private static string NormalizeIssueMethod(string? issueMethod)
        => string.Equals(issueMethod, "FIFO", StringComparison.OrdinalIgnoreCase) ? "FIFO" : "FEFO";

    private async Task<int> TryDecreaseIngredientCurrentStockAsync(int ingredientId, decimal quantity, CancellationToken cancellationToken)
    {
        return await _db.Ingredients
            .Where(x => x.IngredientID == ingredientId && x.CurrentStock >= quantity)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CurrentStock, x => x.CurrentStock - quantity), cancellationToken);
    }

    private sealed record ConsumptionOrderItem(int DishId, int Quantity, int? OrderItemId);
    private sealed record ConsumptionRecipeLine(int DishId, int? OrderItemId, Ingredients Ingredient, decimal RequiredQuantity);

    private async Task<HashSet<int>> FilterOrderableDishIdsAsync(IEnumerable<int> candidateDishIds, CancellationToken cancellationToken)
    {
        var dishIds = candidateDishIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (dishIds.Length == 0)
        {
            return new HashSet<int>();
        }

        var activeDishIds = await _db.Dishes
            .AsNoTracking()
            .Where(x => dishIds.Contains(x.DishID)
                && (x.IsActive ?? true)
                && (x.Available ?? true))
            .Select(x => x.DishID)
            .ToListAsync(cancellationToken);

        if (activeDishIds.Count == 0)
        {
            return new HashSet<int>();
        }

        var recipeRows = await _db.DishIngredients
            .AsNoTracking()
            .Include(di => di.Ingredient)
            .Where(di => activeDishIds.Contains(di.DishID))
            .Select(di => new
            {
                di.DishID,
                di.IngredientID,
                di.QuantityPerDish,
                IngredientIsActive = di.Ingredient.IsActive
            })
            .ToListAsync(cancellationToken);

        var availabilityMap = await _stockAvailability.BuildIngredientStockAvailabilityMapAsync(
            recipeRows.Select(r => r.IngredientID),
            cancellationToken);
        var blockers = recipeRows
            .GroupBy(r => new { r.DishID, r.IngredientID })
            .Where(g =>
            {
                var first = g.First();
                if (!first.IngredientIsActive)
                {
                    return true;
                }

                var availableQuantity = availabilityMap.TryGetValue(g.Key.IngredientID, out var stock)
                    ? stock.AvailabilityStock
                    : 0;
                return availableQuantity < g.Sum(x => x.QuantityPerDish);
            })
            .Select(g => g.Key.DishID)
            .ToHashSet();

        return activeDishIds.Where(id => !blockers.Contains(id)).ToHashSet();
    }
}
