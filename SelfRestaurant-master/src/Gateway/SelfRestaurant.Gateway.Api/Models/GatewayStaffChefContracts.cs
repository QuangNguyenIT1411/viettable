namespace SelfRestaurant.Gateway.Api.Models;

public sealed record StaffSessionDto(
    bool Authenticated,
    StaffSessionUserDto? Staff,
    string? LoginPath = "/Staff/Account/Login");

public sealed record StaffSessionUserDto(
    int EmployeeId,
    string Username,
    string Name,
    string? Phone,
    string? Email,
    int RoleId,
    string RoleCode,
    string RoleName,
    int BranchId,
    string BranchName);

public sealed record StaffLoginApiRequest(string Username, string Password);
public sealed record StaffForgotPasswordApiRequest(string Email);
public sealed record StaffResetPasswordApiRequest(string Token, string NewPassword, string ConfirmPassword);
public sealed record StaffForgotPasswordResultDto(string Message, string? ResetToken, DateTime? ExpiresAt, string? ResetPath);
public sealed class ChefAccountUpdateApiRequest
{
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? Email { get; init; }
}

public sealed class ChefChangePasswordApiRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class ChefCancelOrderApiRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class ChefUpdateItemNoteApiRequest
{
    public string? Note { get; init; }
    public bool Append { get; init; } = true;
}

public sealed class ChefSetDishAvailabilityApiRequest
{
    public bool Available { get; init; }
}

public sealed class ChefSaveDishIngredientsApiRequest
{
    public IReadOnlyList<ChefSaveDishIngredientItemDto> Items { get; init; } = Array.Empty<ChefSaveDishIngredientItemDto>();
}

public sealed record ChefDashboardDto(
    StaffSessionUserDto Staff,
    IReadOnlyList<ChefOrderDto> PendingOrders,
    IReadOnlyList<ChefOrderDto> PreparingOrders,
    IReadOnlyList<ChefOrderDto> ReadyOrders,
    IReadOnlyList<ChefHistoryDto> History,
    ChefMenuDto Menu,
    IReadOnlyList<AdminIngredientDto> Ingredients,
    ChefDashboardSummaryDto Summary);

public sealed record ChefDashboardSummaryDto(
    int PendingOrders,
    int PreparingOrders,
    int ReadyOrders,
    int TotalMenuDishes,
    int AvailableMenuDishes);

public sealed record ChefMenuDto(
    int BranchId,
    string BranchName,
    DateOnly MenuDate,
    IReadOnlyList<ChefMenuDishDto> Dishes);

public sealed record ChefMenuDishDto(
    int DishId,
    string Name,
    decimal Price,
    string? Unit,
    int CategoryId,
    string CategoryName,
    string? Image,
    string? Description,
    bool Available,
    bool IsVegetarian,
    bool IsDailySpecial);

public sealed record ChefDishIngredientsDto(
    int DishId,
    string DishName,
    int? OrderId,
    int? ItemId,
    IReadOnlyList<ChefDishIngredientItemDto> Items);

public sealed record ChefDishIngredientItemDto(
    int IngredientId,
    string Name,
    string Unit,
    decimal CurrentStock,
    bool IsActive,
    decimal QuantityPerDish,
    decimal DefaultQuantityPerDish,
    bool IsOverridden);

public sealed class ChefSaveDishIngredientItemDto
{
    public int IngredientId { get; init; }
    public decimal QuantityPerDish { get; init; }
}
