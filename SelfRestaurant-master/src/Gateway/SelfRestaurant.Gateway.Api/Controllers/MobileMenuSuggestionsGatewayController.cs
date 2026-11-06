using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

/// <summary>
/// "Gợi ý món" (owner 06/11): dishes for a group and a budget from the branch's real menu, priced like the bill
/// (service fee and VAT of the branch, before discounts). Public like the menu.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/mobile/v1/menu")]
public sealed class MobileMenuSuggestionsGatewayController(CatalogClient catalog, BillingClient billing) : ControllerBase
{
    public const decimal MaxBudget = 100_000_000m;

    public sealed record SuggestRequest(int BranchId, int People, decimal Budget, int Vegetarians = 0, bool AvoidSpicy = false);
    public sealed record EstimateItem(int DishId, int Quantity);
    public sealed record EstimateRequest(int BranchId, IReadOnlyList<EstimateItem>? Items);

    [HttpPost("suggestions")]
    public async Task<ActionResult<object>> Suggest(SuggestRequest request, CancellationToken ct)
    {
        if (request.BranchId <= 0) return Error("validation_failed", "Chưa chọn chi nhánh.", 400);
        if (request.People is < 1 or > 30) return Error("validation_failed", "Số người phải từ 1 đến 30.", 400);
        if (request.Budget is <= 0 or > MaxBudget) return Error("validation_failed", "Ngân sách không hợp lệ.", 400);
        if (request.Vegetarians < 0 || request.Vegetarians > request.People)
            return Error("validation_failed", "Số người ăn chay không được lớn hơn số người.", 400);
        try
        {
            var menu = await Menu(request.BranchId, ct);
            if (menu is null) return Error("branch_not_found", "Không tìm thấy chi nhánh.", 404);
            var charges = await Estimate(request.BranchId, 0, ct);
            var plan = MenuPlanner.Suggest(menu, request.People, request.Budget, request.Vegetarians, request.AvoidSpicy,
                charges.FeePercent, charges.VatPercent);
            // The server's own bill formula has the last word (the planner uses the same rounding).
            var priced = await Estimate(request.BranchId, plan.Subtotal, ct);
            return Ok(new
            {
                feasible = plan.Feasible,
                items = plan.Lines.Select(l => new
                {
                    dishId = l.Dish.DishId, name = l.Dish.Name, price = l.Dish.Price, quantity = l.Quantity,
                    course = l.Dish.Course, reason = l.Reason, forVegetarians = l.ForVegetarians,
                    isVegetarian = l.Dish.IsVegetarian, isSpicy = l.Dish.IsSpicy,
                    alternatives = MenuPlanner.Alternatives(menu, plan, l, request.Budget, request.AvoidSpicy, charges.FeePercent, charges.VatPercent)
                        .Select(a => new { dishId = a.DishId, name = a.Name, price = a.Price, isVegetarian = a.IsVegetarian })
                }),
                subtotal = priced.Subtotal, serviceFeePercent = priced.FeePercent, serviceFee = priced.Fee,
                vatPercent = priced.VatPercent, vat = priced.Vat, total = priced.Total, budget = request.Budget,
                minimumTotal = plan.MinimumTotal, notes = plan.Notes
            });
        }
        catch (ApiClientException ex) { return Error(ex.Code ?? "menu_suggestion_failed", ex.Message, ex.StatusCode is >= 400 and < 600 ? ex.StatusCode : 502); }
    }

    /// <summary>The total of a basket after the guest swapped a dish (prices from the menu, never from the app).</summary>
    [HttpPost("estimate")]
    public async Task<ActionResult<object>> EstimateBasket(EstimateRequest request, CancellationToken ct)
    {
        var items = (request.Items ?? Array.Empty<EstimateItem>()).Where(x => x.Quantity > 0).ToArray();
        if (request.BranchId <= 0 || items.Length == 0 || items.Any(x => x.Quantity > 99))
            return Error("validation_failed", "Giỏ món không hợp lệ.", 400);
        try
        {
            var menu = await Menu(request.BranchId, ct);
            if (menu is null) return Error("branch_not_found", "Không tìm thấy chi nhánh.", 404);
            var lines = items.Select(x => (Item: x, Dish: menu.FirstOrDefault(d => d.DishId == x.DishId))).ToArray();
            if (lines.Any(x => x.Dish is null)) return Error("dish_unavailable", "Có món không còn bán.", 409);
            var subtotal = lines.Sum(x => x.Dish!.Price * x.Item.Quantity);
            var priced = await Estimate(request.BranchId, subtotal, ct);
            return Ok(new
            {
                items = lines.Select(x => new { dishId = x.Dish!.DishId, name = x.Dish.Name, price = x.Dish.Price, quantity = x.Item.Quantity }),
                subtotal = priced.Subtotal, serviceFeePercent = priced.FeePercent, serviceFee = priced.Fee,
                vatPercent = priced.VatPercent, vat = priced.Vat, total = priced.Total
            });
        }
        catch (ApiClientException ex) { return Error(ex.Code ?? "menu_estimate_failed", ex.Message, ex.StatusCode is >= 400 and < 600 ? ex.StatusCode : 502); }
    }

    private async Task<IReadOnlyList<MenuPlanner.Dish>?> Menu(int branchId, CancellationToken ct)
    {
        var menu = await catalog.GetMenuAsync(branchId, null, ct);
        return menu?.Categories.SelectMany(c => c.Dishes).Where(d => d.Available)
            .GroupBy(d => d.DishId).Select(g => g.First())
            .Select(d => new MenuPlanner.Dish(d.DishId, d.Name, d.Price, d.Course?.Trim().ToUpperInvariant(), d.IsVegetarian, d.IsSpicy, d.Servings))
            .ToArray();
    }

    private sealed record Priced(decimal Subtotal, decimal FeePercent, decimal Fee, decimal VatPercent, decimal Vat, decimal Total);

    private async Task<Priced> Estimate(int branchId, decimal subtotal, CancellationToken ct)
    {
        var e = await billing.EstimateAsync(branchId, subtotal, ct);
        decimal D(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0;
        return new Priced(D("subtotal"), D("serviceFeePercent"), D("serviceFee"), D("vatPercent"), D("vat"), D("total"));
    }

    private ObjectResult Error(string code, string message, int status)
    {
        var correlationId = Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        return StatusCode(status, MobileApiError.Create(code, message, correlationId, null));
    }
}
