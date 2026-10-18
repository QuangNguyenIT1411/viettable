namespace SelfRestaurant.Gateway.Api.Models;

public sealed record CashierCheckoutApiRequest(
    decimal Discount = 0,
    int PointsUsed = 0,
    string PaymentMethod = "CASH",
    decimal PaymentAmount = 0,
    string? IdempotencyKey = null);

public sealed record CashierAccountUpdateApiRequest(string Name, string Phone, string? Email = null);
public sealed record CashierChangePasswordApiRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);

/// <summary>OpenSessionId: an app guest is seated (even if every order was cancelled); CanCloseSession enables "Đóng bàn".</summary>
public sealed record CashierTableDto(int TableId, string Number, int Seats, string Status, int? OrderId,
    int? OpenSessionId = null, bool CanCloseSession = false);

public sealed record CashierOrderItemCardDto(
    string DishName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Image,
    string StatusCode);

public sealed record CashierOrderCardDto(
    int OrderId,
    string OrderCode,
    string StatusCode,
    string StatusName,
    int? CustomerId,
    string CustomerName,
    int CustomerCreditPoints,
    decimal Subtotal,
    int ItemCount,
    IReadOnlyList<CashierOrderItemCardDto> Items);

public sealed record CashierBillHistoryItemDto(
    int BillId,
    string BillCode,
    DateTime BillTime,
    string OrderCode,
    string TableName,
    decimal Subtotal,
    decimal Discount,
    decimal PointsDiscount,
    int? PointsUsed,
    decimal TotalAmount,
    string PaymentMethod,
    decimal? PaymentAmount,
    decimal? ChangeAmount,
    string CustomerName,
    int? OrderId = null);

public sealed record CashierAccountDto(
    int EmployeeId,
    string Name,
    string Username,
    string Email,
    string Phone,
    string BranchName,
    string RoleName);

public sealed record CashierDashboardDto(
    StaffSessionUserDto Staff,
    IReadOnlyList<CashierTableDto> Tables,
    IReadOnlyList<CashierOrderCardDto> Orders,
    int TodayOrders,
    decimal TodayRevenue,
    CashierAccountDto Account);

public sealed record CashierHistoryDto(
    StaffSessionUserDto Staff,
    IReadOnlyList<CashierBillHistoryItemDto> Bills,
    CashierAccountDto Account);

public sealed record CashierReportScreenDto(
    StaffSessionUserDto Staff,
    DateOnly Date,
    int BillCount,
    decimal TotalRevenue,
    IReadOnlyList<CashierBillHistoryItemDto> Bills,
    CashierAccountDto Account,
    decimal CashRevenue = 0,
    decimal TransferRevenue = 0,
    decimal OtherRevenue = 0);

public sealed record CashierCheckoutResultDto(
    string BillCode,
    decimal TotalAmount,
    decimal ChangeAmount,
    int PointsUsed,
    int PointsEarned,
    int CustomerPoints,
    string? CustomerName,
    int PointsBefore,
    string Message);
