namespace SelfRestaurant.Gateway.Api.Models;

public sealed record AdminDashboardDto(
    StaffSessionUserDto Staff,
    AdminDashboardStatsDto Stats,
    IReadOnlyList<AdminEmployeeDto> LatestEmployees,
    IReadOnlyList<BranchDto> Branches,
    IReadOnlyList<EmployeeRoleDto> Roles,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<TableStatusDto> TableStatuses,
    AdminSettingsDto Settings);

public sealed record AdminDashboardStatsDto(
    int TotalEmployees,
    int ActiveEmployees,
    int BranchCount,
    int TodayOrders,
    int PendingOrders,
    decimal TodayRevenue);

public sealed record AdminCategorySummaryDto(string Unit, int DishCount);

public sealed record AdminCategoriesScreenDto(
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<AdminCategorySummaryDto> Units);

public sealed record AdminDishesScreenDto(
    AdminDishPagedResponse Dishes,
    IReadOnlyList<CategoryDto> Categories);

public sealed record AdminIngredientsScreenDto(AdminIngredientPagedResponse Ingredients);

public sealed record AdminTablesScreenDto(
    AdminTablePagedResponse Tables,
    IReadOnlyList<BranchDto> Branches,
    IReadOnlyList<TableStatusDto> TableStatuses);

public sealed record AdminEmployeesScreenDto(
    AdminEmployeePagedResponse Employees,
    IReadOnlyList<BranchDto> Branches,
    IReadOnlyList<EmployeeRoleDto> Roles);

public sealed record AdminCustomersScreenDto(AdminCustomerPagedResponse Customers);

public sealed record AdminReportsScreenDto(
    int RevenueDays,
    int TopDishDays,
    int TopDishTake,
    AdminRevenueReportDto Revenue,
    AdminTopDishReportDto TopDishes);

public sealed record AdminSettingsDto(
    int EmployeeId,
    string Name,
    string Username,
    string? Phone,
    string? Email,
    string BranchName,
    string RoleName);

public sealed record AdminSettingsUpdateApiRequest(string Name, string Phone, string? Email = null);
public sealed record AdminChangePasswordApiRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
public sealed record AdminToggleAvailabilityApiRequest(bool Available);
