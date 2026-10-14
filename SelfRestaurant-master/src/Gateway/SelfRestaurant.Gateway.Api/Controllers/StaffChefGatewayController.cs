using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SelfRestaurant.Gateway.Api.Infrastructure;
using SelfRestaurant.Gateway.Api.Hubs;
using SelfRestaurant.Gateway.Api.Models;
using SelfRestaurant.Gateway.Api.Services;

namespace SelfRestaurant.Gateway.Api.Controllers;

[ApiController]
[Route("api/gateway/staff")]
public sealed class StaffChefGatewayController : ControllerBase
{
    private static readonly string[] ChefRoles = ["CHEF", "KITCHEN_STAFF", "MANAGER", "ADMIN"];
    private static readonly string[] StaffRoles = ["CHEF", "KITCHEN_STAFF", "CASHIER", "ADMIN", "MANAGER"];

    private readonly OrdersClient _ordersClient;
    private readonly CatalogClient _catalogClient;
    private readonly IdentityClient _identityClient;
    private readonly IHubContext<CustomerNotificationsHub> _customerNotificationsHub;
    private readonly IHubContext<MobileNotificationsHub> _mobileNotificationsHub;
    private readonly ILogger<StaffChefGatewayController> _logger;
    private readonly SelfRestaurant.Gateway.Api.Infrastructure.Media.IDishMediaStorage _mediaStorage;

    public StaffChefGatewayController(
        OrdersClient ordersClient,
        CatalogClient catalogClient,
        IdentityClient identityClient,
        IHubContext<CustomerNotificationsHub> customerNotificationsHub,
        IHubContext<MobileNotificationsHub> mobileNotificationsHub,
        ILogger<StaffChefGatewayController> logger,
        SelfRestaurant.Gateway.Api.Infrastructure.Media.IDishMediaStorage mediaStorage)
    {
        _ordersClient = ordersClient;
        _catalogClient = catalogClient;
        _identityClient = identityClient;
        _customerNotificationsHub = customerNotificationsHub;
        _mobileNotificationsHub = mobileNotificationsHub;
        _logger = logger;
        _mediaStorage = mediaStorage;
    }

    [HttpGet("session")]
    public ActionResult<StaffSessionDto> GetSession() => Ok(BuildSessionDto());

    [HttpPost("auth/login")]
    public async Task<ActionResult<object>> Login([FromBody] StaffLoginApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Error("invalid_request", "Vui lòng nhập đầy đủ thông tin.", 400);
        }

        try
        {
            var staff = await _identityClient.StaffLoginAsync(new StaffLoginRequest(request.Username.Trim(), request.Password), cancellationToken);
            if (staff is null)
            {
                return Error("invalid_credentials", "Tên đăng nhập hoặc mật khẩu không đúng.", 401);
            }

            if (!StaffRoles.Contains(staff.RoleCode, StringComparer.OrdinalIgnoreCase))
            {
                return Error("forbidden", "Tài khoản này không có quyền vào khu vực nhân viên.", 403);
            }

            ApplyStaffSession(staff);
            return Ok(new { success = true, session = BuildSessionDto(), nextPath = ResolveStaffNextPath(staff.RoleCode) });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff chef login failed.");
            return Error("login_failed", NormalizeStaffAuthError(ex.Message), 400);
        }
    }

    [HttpPost("auth/forgot-password")]
    public async Task<ActionResult<StaffForgotPasswordResultDto>> ForgotPassword([FromBody] StaffForgotPasswordApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Error("invalid_request", "Vui lòng nhập email.", 400);
        }

        try
        {
            var result = await _identityClient.StaffForgotPasswordAsync(new StaffForgotPasswordRequest(request.Email.Trim()), cancellationToken);
            if (result is null)
            {
                return Error("forgot_password_failed", "Có lỗi xảy ra. Vui lòng thử lại sau.", 502);
            }

            var resetPath = string.IsNullOrWhiteSpace(result.ResetToken)
                ? null
                : $"/Staff/Account/ResetPassword?token={Uri.EscapeDataString(result.ResetToken)}";

            return Ok(new StaffForgotPasswordResultDto(
                NormalizeStaffForgotPasswordMessage(result.Message),
                result.ResetToken,
                result.ExpiresAt,
                resetPath));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff forgot password failed.");
            return Error("forgot_password_failed", "Có lỗi xảy ra. Vui lòng thử lại sau.", 400);
        }
    }

    [HttpGet("auth/reset-password/validate")]
    public async Task<ActionResult<object>> ValidateResetPasswordToken([FromQuery] string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Error("missing_token", "Link không hợp lệ.", 400);
        }

        try
        {
            await _identityClient.ValidateStaffResetPasswordTokenAsync(token.Trim(), cancellationToken);
            return Ok(new { valid = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff reset password token validation failed.");
            return Error("invalid_token", NormalizeStaffResetValidationError(ex.Message), 400);
        }
    }

    [HttpPost("auth/reset-password")]
    public async Task<ActionResult<object>> ResetPassword([FromBody] StaffResetPasswordApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Error("invalid_token", "Token không hợp lệ.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return Error("invalid_request", "Vui lòng điền đầy đủ thông tin.", 400);
        }

        if (request.NewPassword.Trim().Length < 6)
        {
            return Error("invalid_password", "Mật khẩu phải có ít nhất 6 ký tự.", 400);
        }

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Error("password_mismatch", "Mật khẩu xác nhận không khớp.", 400);
        }

        try
        {
            await _identityClient.StaffResetPasswordAsync(new StaffResetPasswordRequest(request.Token.Trim(), request.NewPassword), cancellationToken);
            return Ok(new
            {
                success = true,
                message = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập với mật khẩu mới.",
                nextPath = "/Staff/Account/Login"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff reset password failed.");
            return Error("reset_password_failed", NormalizeStaffResetValidationError(ex.Message), 400);
        }
    }

    [HttpPost("auth/logout")]
    public ActionResult<object> Logout()
    {
        var userName = HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeName) ?? "Người dùng";
        ClearStaffSession();
        return Ok(new
        {
            success = true,
            nextPath = $"/Staff/Account/Login?message={Uri.EscapeDataString($"T?m bi?t {userName}! B?n ?? ??ng xu?t th?nh c?ng.")}&type=success"
        });
    }

    [HttpGet("chef/dashboard")]
    public async Task<ActionResult<ChefDashboardDto>> GetChefDashboard(CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);

        var dashboard = await BuildDashboardAsync(staff, historyTake: 100, cancellationToken);
        return Ok(dashboard);
    }

    [HttpGet("chef/history")]
    public async Task<ActionResult<IReadOnlyList<ChefHistoryDto>>> GetHistory([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        var items = await _ordersClient.GetChefHistoryAsync(staff.BranchId, Math.Clamp(take, 1, 300), cancellationToken);
        return Ok(items);
    }

    [HttpGet("chef/menu")]
    public async Task<ActionResult<ChefMenuDto>> GetMenu(CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        var menu = await BuildMenuAsync(staff.BranchId, staff.BranchName, cancellationToken);
        return Ok(menu);
    }

    [HttpGet("chef/categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories(CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);

        var categories = await _catalogClient.GetCategoriesAsync(false, cancellationToken);
        return Ok(categories ?? Array.Empty<CategoryDto>());
    }

[StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Orders)]
    [HttpPost("chef/orders/{orderId:int}/start")]
    public async Task<ActionResult<object>> Start(int orderId, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (orderId <= 0) return Error("invalid_order", "Đơn hàng không hợp lệ.", 400);
        if (!await IsOrderInChefBranchAsync(staff.BranchId, orderId, cancellationToken)) return Error("forbidden", "Đơn hàng không thuộc chi nhánh của bạn.", 403);
        try
        {
            await _ordersClient.ChefStartAsync(orderId, cancellationToken);
            await BroadcastMobileOrderUpdatedAsync(orderId, cancellationToken);
            return Ok(new { success = true, message = "Đã chuyển đơn sang đang chế biến." });
        }
        catch (InvalidOperationException ex)
        {
            return Error("start_failed", NormalizeChefError(ex.Message), 400);
        }
    }

[StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Orders)]
    [HttpPost("chef/orders/{orderId:int}/ready")]
    public async Task<ActionResult<object>> Ready(int orderId, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (orderId <= 0) return Error("invalid_order", "Đơn hàng không hợp lệ.", 400);
        if (!await IsOrderInChefBranchAsync(staff.BranchId, orderId, cancellationToken)) return Error("forbidden", "Đơn hàng không thuộc chi nhánh của bạn.", 403);
        try
        {
            await _ordersClient.ChefReadyAsync(orderId, staff.EmployeeId, cancellationToken);
            await BroadcastOrderReadyAsync(orderId, null, cancellationToken);
            return Ok(new { success = true, message = "Đơn đã sẵn sàng." });
        }
        catch (InvalidOperationException ex)
        {
            return Error("ready_failed", NormalizeChefError(ex.Message), 400);
        }
    }

[StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Orders)]
    [HttpPost("chef/orders/{orderId:int}/items/{itemId:int}/start")]
    public async Task<ActionResult<object>> StartItem(int orderId, int itemId, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (orderId <= 0 || itemId <= 0) return Error("invalid_item", "Món trong đơn không hợp lệ.", 400);
        if (!await IsOrderInChefBranchAsync(staff.BranchId, orderId, cancellationToken)) return Error("forbidden", "Đơn hàng không thuộc chi nhánh của bạn.", 403);
        try
        {
            await _ordersClient.ChefStartItemAsync(orderId, itemId, staff.EmployeeId, cancellationToken);
            await BroadcastMobileOrderUpdatedAsync(orderId, cancellationToken);
            return Ok(new { success = true, message = "Đã chuyển món sang đang chế biến." });
        }
        catch (InvalidOperationException ex)
        {
            return Error("start_item_failed", NormalizeChefError(ex.Message), 409);
        }
    }

[StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Orders)]
    [HttpPost("chef/orders/{orderId:int}/items/{itemId:int}/ready")]
    public async Task<ActionResult<object>> ReadyItem(int orderId, int itemId, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (orderId <= 0 || itemId <= 0) return Error("invalid_item", "Món trong đơn không hợp lệ.", 400);
        if (!await IsOrderInChefBranchAsync(staff.BranchId, orderId, cancellationToken)) return Error("forbidden", "Đơn hàng không thuộc chi nhánh của bạn.", 403);
        try
        {
            await _ordersClient.ChefReadyItemAsync(orderId, itemId, staff.EmployeeId, cancellationToken);
            await BroadcastOrderReadyAsync(orderId, itemId, cancellationToken);
            return Ok(new { success = true, message = "Món đã sẵn sàng." });
        }
        catch (InvalidOperationException ex)
        {
            return Error("ready_item_failed", NormalizeChefError(ex.Message), 409);
        }
    }

    [HttpPost("chef/orders/{orderId:int}/serve")]
    public ActionResult<object> Serve(int orderId, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (orderId <= 0) return Error("invalid_order", "Đơn hàng không hợp lệ.", 400);
        return Error("customer_confirmation_required", "Chỉ khách hàng mới có thể xác nhận đã nhận món.", 409);
    }

[StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Orders)]
    [HttpPost("chef/orders/{orderId:int}/cancel")]
    public async Task<ActionResult<object>> Cancel(int orderId, [FromBody] ChefCancelOrderApiRequest request, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (orderId <= 0) return Error("invalid_order", "Đơn hàng không hợp lệ.", 400);
        if (string.IsNullOrWhiteSpace(request.Reason)) return Error("invalid_reason", "Vui lòng nhập lý do hủy đơn.", 400);
        if (!await IsOrderInChefBranchAsync(staff.BranchId, orderId, cancellationToken)) return Error("forbidden", "Đơn hàng không thuộc chi nhánh của bạn.", 403);
        await _ordersClient.ChefCancelAsync(orderId, request.Reason.Trim(), cancellationToken);
        return Ok(new { success = true, message = "Đã hủy đơn hàng." });
    }

[StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Orders)]
    [HttpPost("chef/orders/{orderId:int}/items/{itemId:int}/cancel")]
    public async Task<ActionResult<object>> CancelItem(int orderId, int itemId, [FromBody] ChefCancelOrderApiRequest request, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (orderId <= 0 || itemId <= 0) return Error("invalid_item", "Món trong đơn không hợp lệ.", 400);
        if (string.IsNullOrWhiteSpace(request.Reason)) return Error("invalid_reason", "Vui lòng nhập lý do hủy món.", 400);
        if (!await IsOrderInChefBranchAsync(staff.BranchId, orderId, cancellationToken)) return Error("forbidden", "Đơn hàng không thuộc chi nhánh của bạn.", 403);

        try
        {
            await _ordersClient.ChefCancelItemAsync(orderId, itemId, request.Reason.Trim(), cancellationToken);
            return Ok(new { success = true, message = "Đã hủy món." });
        }
        catch (InvalidOperationException ex)
        {
            return Error("cancel_item_failed", NormalizeChefError(ex.Message), 409);
        }
    }

[StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Orders)]
    [HttpPatch("chef/orders/{orderId:int}/items/{itemId:int}/note")]
    public async Task<ActionResult<object>> UpdateItemNote(int orderId, int itemId, [FromBody] ChefUpdateItemNoteApiRequest request, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (orderId <= 0 || itemId <= 0) return Error("invalid_item", "Món trong đơn không hợp lệ.", 400);
        if (!await IsOrderInChefBranchAsync(staff.BranchId, orderId, cancellationToken)) return Error("forbidden", "Đơn hàng không thuộc chi nhánh của bạn.", 403);
        await _ordersClient.ChefUpdateItemNoteAsync(orderId, itemId, request.Note, request.Append, cancellationToken);
        return Ok(new { success = true, message = "Đã cập nhật ghi chú món." });
    }

    [HttpGet("chef/dishes/{dishId:int}/ingredients")]
    public async Task<ActionResult<ChefDishIngredientsDto>> GetDishIngredients(int dishId, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (dishId <= 0) return Error("invalid_dish", "Món ăn không hợp lệ.", 400);

        await EnsureDishInTodayMenuAsync(staff.BranchId, dishId, cancellationToken);
        var dish = await _catalogClient.GetAdminDishByIdAsync(dishId, cancellationToken);
        if (dish is null) return Error("dish_not_found", "Không tìm thấy món ăn.", 404);

        var recipe = await _catalogClient.GetDishIngredientsAsync(dishId, cancellationToken);
        var items = (recipe?.Ingredients ?? Array.Empty<AdminDishIngredientLineDto>())
            .Where(x => x.Selected)
            .Select(x => new ChefDishIngredientItemDto(x.IngredientId, x.Name, x.Unit, x.CurrentStock, x.IsActive, x.QuantityPerDish, x.QuantityPerDish, false))
            .ToArray();

        return Ok(new ChefDishIngredientsDto(dishId, dish.Name, null, null, items));
    }


    [HttpGet("chef/orders/{orderId:int}/items/{itemId:int}/ingredients")]
    public async Task<ActionResult<ChefDishIngredientsDto>> GetOrderItemIngredients(int orderId, int itemId, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (orderId <= 0 || itemId <= 0) return Error("invalid_item", "Món trong đơn không hợp lệ.", 400);
        if (!await IsOrderInChefBranchAsync(staff.BranchId, orderId, cancellationToken)) return Error("forbidden", "Đơn hàng không thuộc chi nhánh của bạn.", 403);
        var overrides = await _ordersClient.GetOrderItemIngredientOverridesAsync(orderId, itemId, cancellationToken);
        if (overrides is null) return Error("item_not_found", "Không tìm thấy món trong đơn.", 404);
        await EnsureDishInTodayMenuAsync(staff.BranchId, overrides.DishId, cancellationToken);
        var dish = await _catalogClient.GetAdminDishByIdAsync(overrides.DishId, cancellationToken);
        if (dish is null) return Error("dish_not_found", "Không tìm thấy món ăn.", 404);
        var overrideMap = overrides.Items.ToDictionary(x => x.IngredientId, x => x.Quantity);
        var recipe = await _catalogClient.GetDishIngredientsAsync(overrides.DishId, cancellationToken);
        var items = (recipe?.Ingredients ?? Array.Empty<AdminDishIngredientLineDto>())
            .Where(x => x.Selected)
            .Select(x =>
            {
                var hasOverride = overrideMap.TryGetValue(x.IngredientId, out var quantity);
                return new ChefDishIngredientItemDto(x.IngredientId, x.Name, x.Unit, x.CurrentStock, x.IsActive, hasOverride ? quantity : x.QuantityPerDish, x.QuantityPerDish, hasOverride);
            })
            .ToArray();
        return Ok(new ChefDishIngredientsDto(overrides.DishId, dish.Name, orderId, itemId, items));
    }

[StaffBranchEvent(StaffEventSource.Staff, StaffBranchEvents.Orders)]
    [HttpPut("chef/orders/{orderId:int}/items/{itemId:int}/ingredients")]
    public async Task<ActionResult<object>> UpdateOrderItemIngredients(int orderId, int itemId, [FromBody] ChefUpdateOrderItemIngredientOverrideRequest request, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (!await IsOrderInChefBranchAsync(staff.BranchId, orderId, cancellationToken)) return Error("forbidden", "Đơn hàng không thuộc chi nhánh của bạn.", 403);
        var requestedItems = request.Items ?? Array.Empty<ChefOrderItemIngredientOverrideDto>();
        if (requestedItems.Any(x => x.IngredientId <= 0)) return Error("invalid_ingredient", "Nguyên liệu không hợp lệ.", 400);
        if (requestedItems.Any(x => x.Quantity < 0)) return Error("invalid_quantity", "Số lượng nguyên liệu không được âm.", 400);

        var current = await _ordersClient.GetOrderItemIngredientOverridesAsync(orderId, itemId, cancellationToken);
        if (current is null) return Error("item_not_found", "Không tìm thấy món trong đơn.", 404);
        await EnsureDishInTodayMenuAsync(staff.BranchId, current.DishId, cancellationToken);

        var recipe = await _catalogClient.GetDishIngredientsAsync(current.DishId, cancellationToken);
        var recipeMap = (recipe?.Ingredients ?? Array.Empty<AdminDishIngredientLineDto>())
            .Where(x => x.Selected)
            .GroupBy(x => x.IngredientId)
            .ToDictionary(x => x.Key, x => x.Last());
        var enrichedItems = requestedItems
            .GroupBy(x => x.IngredientId)
            .Select(group =>
            {
                var item = group.Last();
                recipeMap.TryGetValue(item.IngredientId, out var recipeItem);
                return new ChefOrderItemIngredientOverrideDto(
                    item.IngredientId,
                    recipeItem?.Name ?? item.IngredientName,
                    recipeItem?.Unit ?? item.Unit,
                    item.Quantity);
            })
            .ToArray();

        if (enrichedItems.Any(x => string.IsNullOrWhiteSpace(x.IngredientName) || string.IsNullOrWhiteSpace(x.Unit)))
        {
            return Error("invalid_ingredient_snapshot", "Tên và đơn vị nguyên liệu không hợp lệ.", 400);
        }

        await _ordersClient.UpdateOrderItemIngredientOverridesAsync(orderId, itemId, new ChefUpdateOrderItemIngredientOverrideRequest(enrichedItems, request.Note), cancellationToken);
        return Ok(new { success = true, message = "Đã lưu thành phần riêng cho món trong đơn này." });
    }

    [HttpPut("chef/dishes/{dishId:int}/ingredients")]
    public ActionResult<ChefDishIngredientsDto> SaveDishIngredients(int dishId, [FromBody] ChefSaveDishIngredientsApiRequest request)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (dishId > 0) return Error("forbidden", "Dau bep chi duoc xem thanh phan mon; vui long dung trang quan tri de chinh sua cong thuc.", 403);
        if (dishId <= 0) return Error("invalid_dish", "Món ăn không hợp lệ.", 400);
        return Error("forbidden", "Dau bep chi duoc xem thanh phan mon; vui long dung trang quan tri de chinh sua cong thuc.", 403);
    }

    [HttpPut("chef/dishes/{dishId:int}/image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<object>> UpdateDishImage(int dishId, [FromForm] IFormFile? imageFile, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (dishId > 0) return Error("forbidden", "Dau bep khong co quyen chinh sua hinh anh mon; vui long dung trang quan tri.", 403);
        if (dishId <= 0) return Error("invalid_dish", "Món ăn không hợp lệ.", 400);
        if (imageFile is null || imageFile.Length <= 0) return Error("invalid_image", "Vui lòng chọn ảnh hợp lệ.", 400);

        await EnsureDishInTodayMenuAsync(staff.BranchId, dishId, cancellationToken);
        var current = await _catalogClient.GetAdminDishByIdAsync(dishId, cancellationToken);
        if (current is null) return Error("dish_not_found", "Không tìm thấy món ăn.", 404);

        var imagePath = await SaveDishImageAsync(current.Name, imageFile, current.Image, cancellationToken);
        await _catalogClient.UpdateAdminDishAsync(
            dishId,
            new AdminUpsertDishRequest(
                current.Name,
                current.Price,
                current.CategoryId,
                current.Description,
                current.Unit,
                imagePath,
                current.IsVegetarian,
                current.IsDailySpecial,
                current.Available,
                current.IsActive),
            cancellationToken);

        return Ok(new { success = true, message = "Đã cập nhật ảnh món ăn.", image = imagePath });
    }

    [HttpPost("chef/dishes/{dishId:int}/availability")]
    public async Task<ActionResult<object>> SetDishAvailability(int dishId, [FromBody] ChefSetDishAvailabilityApiRequest request, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (dishId <= 0) return Error("invalid_dish", "Món ăn không hợp lệ.", 400);

        await EnsureDishInTodayMenuAsync(staff.BranchId, dishId, cancellationToken);
        var current = await _catalogClient.GetAdminDishByIdAsync(dishId, cancellationToken);
        if (current is null) return Error("dish_not_found", "Không tìm thấy món ăn.", 404);

        try
        {
            var result = await _catalogClient.SetChefDishAvailabilityAsync(staff.BranchId, dishId, request.Available, cancellationToken);
            if (result is null)
            {
                return Error("availability_failed", "Không nhận được phản hồi cập nhật trạng thái bán.", 502);
            }

            return Ok(new { success = true, message = result.Message, available = result.Available });
        }
        catch (ApiClientException ex)
        {
            return Error("availability_blocked", ex.Message, ex.StatusCode);
        }
    }

    [HttpPost("chef/dishes")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<object>> CreateDish([FromForm] AdminUpsertDishFormRequest request, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (staff.BranchId > 0) return Error("forbidden", "Dau bep khong co quyen them mon; vui long dung trang quan tri.", 403);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error("invalid_name", "Vui lòng nhập tên món ăn.", 400);
        }

        var dishName = request.Name.Trim();
        var imagePath = await ResolveDishImagePathAsync(dishName, request.ImageFile, request.Image, cancellationToken);

        await _catalogClient.CreateChefDishForBranchAsync(
            staff.BranchId,
            new AdminUpsertDishRequest(
                dishName,
                request.Price,
                request.CategoryId,
                request.Description,
                request.Unit,
                imagePath,
                request.IsVegetarian,
                request.IsDailySpecial,
                request.Available ?? true,
                request.IsActive ?? true),
            cancellationToken);

        return Ok(new { success = true, message = "?? th?m m?n m?i v?o th?c ??n h?m nay." });
    }

    [HttpPut("chef/dishes/{dishId:int}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<object>> UpdateDish(int dishId, [FromForm] AdminUpsertDishFormRequest request, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (dishId > 0) return Error("forbidden", "Dau bep khong co quyen chinh sua thong tin mon; chi duoc bat hoac tam ngung ban.", 403);
        if (dishId <= 0) return Error("invalid_dish", "Món ăn không hợp lệ.", 400);

        await EnsureDishInTodayMenuAsync(staff.BranchId, dishId, cancellationToken);
        var current = await _catalogClient.GetAdminDishByIdAsync(dishId, cancellationToken);
        if (current is null) return Error("dish_not_found", "Không tìm thấy món ăn.", 404);

        var dishName = string.IsNullOrWhiteSpace(request.Name) ? current.Name : request.Name.Trim();
        var imagePath = await ResolveDishImagePathAsync(dishName, request.ImageFile, request.Image ?? current.Image, cancellationToken, current.Image);

        await _catalogClient.UpdateChefDishForBranchAsync(
            staff.BranchId,
            dishId,
            new AdminUpsertDishRequest(
                dishName,
                request.Price ?? current.Price,
                request.CategoryId ?? current.CategoryId,
                request.Description ?? current.Description,
                request.Unit ?? current.Unit,
                imagePath,
                request.IsVegetarian ?? current.IsVegetarian,
                request.IsDailySpecial ?? current.IsDailySpecial,
                request.Available ?? current.Available,
                request.IsActive ?? current.IsActive),
            cancellationToken);

        return Ok(new { success = true, message = "Đã cập nhật thông tin món ăn." });
    }

    [HttpPut("chef/account")]
    public async Task<ActionResult<StaffSessionUserDto>> UpdateAccount([FromBody] ChefAccountUpdateApiRequest request, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Phone))
        {
            return Error("invalid_request", "Họ tên và số điện thoại là bắt buộc.", 400);
        }

        try
        {
            var profile = await _identityClient.UpdateStaffProfileAsync(
                staff.EmployeeId,
                new StaffUpdateProfileRequest(
                    request.Name.Trim(),
                    request.Phone.Trim(),
                    string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim()),
                cancellationToken);

            if (profile is not null)
            {
                HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeName, profile.Name);
                HttpContext.StaffIdentity().SetString(SessionKeys.EmployeePhone, profile.Phone ?? string.Empty);
                HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeEmail, profile.Email ?? string.Empty);
            }

            return Ok(RequireChef() ?? staff);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chef update account failed.");
            return Error("update_account_failed", ex.Message, 400);
        }
    }

    [HttpPost("chef/change-password")]
    public async Task<ActionResult<object>> ChangePassword([FromBody] ChefChangePasswordApiRequest request, CancellationToken cancellationToken)
    {
        var staff = RequireChef();
        if (staff is null) return Error("unauthorized", "Bạn cần đăng nhập bằng tài khoản bếp.", 401);
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Error("invalid_request", "Vui lòng nhập đầy đủ mật khẩu hiện tại và mật khẩu mới.", 400);
        }
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Error("password_mismatch", "Mật khẩu mới và xác nhận không khớp.", 400);
        }

        try
        {
            await _identityClient.StaffChangePasswordAsync(
                new StaffChangePasswordRequest(staff.EmployeeId, request.CurrentPassword, request.NewPassword),
                cancellationToken);
            return Ok(new { success = true, message = "??i m?t kh?u th?nh c?ng." });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chef change password failed.");
            return Error("change_password_failed", ex.Message, 400);
        }
    }

    private async Task BroadcastOrderReadyAsync(int orderId, int? orderItemId, CancellationToken cancellationToken)
    {
        var order = await _ordersClient.GetOrderByIdAsync(orderId, cancellationToken);
        if (order?.TableId is not int tableId || tableId <= 0)
        {
            return;
        }

        var item = orderItemId is int itemId
            ? order.Items.FirstOrDefault(x => x.ItemId == itemId)
            : order.Items.FirstOrDefault(x => string.Equals(x.Status, "READY", StringComparison.OrdinalIgnoreCase));

        await _customerNotificationsHub.Clients
            .Group(CustomerNotificationsHub.TableGroup(tableId))
            .SendAsync(
                "orderReady",
                new
                {
                    orderId,
                    orderItemId,
                    tableId,
                    dishId = item?.DishId,
                    dishName = item?.DishName,
                    message = item is null
                        ? "Bếp đã hoàn thành món ăn của bạn."
                        : $"{item.DishName} đã sẵn sàng.",
                },
                cancellationToken);
        await BroadcastMobileOrderUpdatedAsync(orderId, cancellationToken);
    }

    private async Task BroadcastMobileOrderUpdatedAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await _ordersClient.GetOrderByIdAsync(orderId, cancellationToken);
        if (order?.CustomerId is not int customerId || customerId <= 0) return;
        await _mobileNotificationsHub.Clients.Group(MobileNotificationsHub.CustomerGroup(customerId))
            .SendAsync("orderUpdated", new
            {
                orderId = order.OrderId,
                order.DiningSessionCode,
                order.StatusCode,
                items = order.Items.Select(x => new { x.ItemId, x.Status }).ToArray()
            }, cancellationToken);
    }

    private async Task<ChefDashboardDto> BuildDashboardAsync(StaffSessionUserDto staff, int historyTake, CancellationToken cancellationToken)
    {
        // Independent HTTP reads, each downstream owns its request-scoped DbContext.
        // Fixed fan-out of four; do not parallelize queries on a shared DbContext or raise pools.
        var ordersTask = _ordersClient.GetChefOrdersAsync(staff.BranchId, null, cancellationToken);
        var historyTask = _ordersClient.GetChefHistoryAsync(staff.BranchId, historyTake, cancellationToken);
        var menuTask = BuildMenuAsync(staff.BranchId, staff.BranchName, cancellationToken);
        var ingredientsTask = GetAllActiveIngredientsAsync(cancellationToken);
        await Task.WhenAll(ordersTask, historyTask, menuTask, ingredientsTask);
        var orders = await ordersTask;
        var history = await historyTask;
        var menu = await menuTask;
        var ingredients = await ingredientsTask;

        var pending = orders.Where(x =>
                string.Equals(x.StatusCode, "PENDING", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.StatusCode, "CONFIRMED", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var preparing = orders.Where(x => string.Equals(x.StatusCode, "PREPARING", StringComparison.OrdinalIgnoreCase)).ToArray();
        var ready = orders.Where(x =>
                string.Equals(x.StatusCode, "READY", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.StatusCode, "SERVING", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return new ChefDashboardDto(
            staff,
            pending,
            preparing,
            ready,
            history,
            menu,
            ingredients,
            new ChefDashboardSummaryDto(pending.Length, preparing.Length, ready.Length, menu.Dishes.Count, menu.Dishes.Count(x => x.Available)));
    }

    private async Task<ChefMenuDto> BuildMenuAsync(int branchId, string branchName, CancellationToken cancellationToken)
    {
        var menu = await _catalogClient.GetMenuAsync(branchId, DateOnly.FromDateTime(DateTime.Today), cancellationToken: cancellationToken);
        var dishes = menu?.Categories
            .SelectMany(c => c.Dishes.Select(d => new ChefMenuDishDto(
                d.DishId,
                d.Name,
                d.Price,
                d.Unit,
                c.CategoryId,
                c.CategoryName,
                d.Image,
                d.Description,
                d.Available,
                d.IsVegetarian,
                d.IsDailySpecial)))
            .ToArray() ?? Array.Empty<ChefMenuDishDto>();

        return new ChefMenuDto(branchId, menu?.BranchName ?? branchName, DateOnly.FromDateTime(DateTime.Today), dishes);
    }

    private async Task<IReadOnlyList<AdminIngredientDto>> GetAllActiveIngredientsAsync(CancellationToken cancellationToken)
    {
        var result = new List<AdminIngredientDto>();
        var page = 1;
        while (true)
        {
            var response = await _catalogClient.GetAdminIngredientsAsync(null, page, 100, true, null, cancellationToken);
            if (response is null || response.Items.Count == 0) break;
            result.AddRange(response.Items.Where(x => x.IsActive));
            if (page >= response.TotalPages) break;
            page++;
        }

        return result;
    }

    private async Task EnsureDishInTodayMenuAsync(int branchId, int dishId, CancellationToken cancellationToken)
    {
        var menu = await _catalogClient.GetMenuAsync(branchId, DateOnly.FromDateTime(DateTime.Today), cancellationToken: cancellationToken);
        var exists = menu?.Categories.SelectMany(x => x.Dishes).Any(x => x.DishId == dishId) ?? false;
        if (!exists)
        {
            throw new InvalidOperationException("Món không thuộc thực đơn chi nhánh hôm nay.");
        }
    }

    private async Task<string> SaveDishImageAsync(string dishName, IFormFile imageFile, string? existingImagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dishName))
        {
            throw new InvalidOperationException("Tên món ăn không hợp lệ để tạo tên file ảnh.");
        }

        var extension = Path.GetExtension(imageFile.FileName)?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        var slug = SlugifyDishName(dishName);
        var fileName = $"{slug}{extension}";
        await using var uploadStream = imageFile.OpenReadStream();
        return await _mediaStorage.SaveAsync(fileName, uploadStream, existingImagePath, cancellationToken);
    }

    private async Task<string?> ResolveDishImagePathAsync(
        string dishName,
        IFormFile? imageFile,
        string? requestedImagePath,
        CancellationToken cancellationToken,
        string? existingImagePath = null)
    {
        if (imageFile is not null && imageFile.Length > 0)
        {
            return await SaveDishImageAsync(dishName, imageFile, existingImagePath, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(requestedImagePath))
        {
            return requestedImagePath.Trim();
        }

        return existingImagePath;
    }

    private static string SlugifyDishName(string dishName)
    {
        var normalized = dishName.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousDash = false;

        foreach (var ch in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var mapped = ch switch
            {
                '\u0111' or '\u0110' => 'd',
                _ => char.ToLowerInvariant(ch)
            };

            if ((mapped >= 'a' && mapped <= 'z') || (mapped >= '0' && mapped <= '9'))
            {
                builder.Append(mapped);
                previousDash = false;
                continue;
            }

            if (previousDash || builder.Length == 0)
            {
                continue;
            }

            builder.Append('-');
            previousDash = true;
        }

        var result = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "dish-image" : result;
    }

    private void ApplyStaffSession(StaffLoginResponse login)
    {
        HttpContext.StaffIdentity().SetInt32(SessionKeys.EmployeeId, login.EmployeeId);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeUsername, login.Username);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeName, login.Name);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeePhone, login.Phone ?? string.Empty);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeEmail, login.Email ?? string.Empty);
        HttpContext.StaffIdentity().SetInt32(SessionKeys.EmployeeRoleId, login.RoleId);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeRoleCode, login.RoleCode);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeRoleName, login.RoleName);
        HttpContext.StaffIdentity().SetInt32(SessionKeys.EmployeeBranchId, login.BranchId);
        HttpContext.StaffIdentity().SetString(SessionKeys.EmployeeBranchName, login.BranchName);
    }

    private StaffSessionDto BuildSessionDto() => new(RequireStaff() is not null, RequireStaff(), "/Staff/Account/Login");

    private static string ResolveStaffNextPath(string? roleCode)
    {
        var normalized = roleCode?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "CASHIER" => "/Staff/Cashier/Index",
            "ADMIN" => "/Admin/Dashboard/Index",
            "MANAGER" => "/Admin/Dashboard/Index",
            _ => "/Staff/Chef/Index",
        };
    }

    private async Task<bool> IsOrderInChefBranchAsync(int branchId, int orderId, CancellationToken cancellationToken) =>
        (await _ordersClient.GetChefOrdersAsync(branchId, null, cancellationToken)).Any(order => order.OrderId == orderId);

    private StaffSessionUserDto? RequireChef() => RequireStaff(ChefRoles);

    private StaffSessionUserDto? RequireStaff(params string[] allowedRoles)
    {
        var employeeId = HttpContext.StaffIdentity().GetInt32(SessionKeys.EmployeeId);
        var roleCode = HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeRoleCode);
        var branchId = HttpContext.StaffIdentity().GetInt32(SessionKeys.EmployeeBranchId);
        if (employeeId is null || employeeId <= 0 || string.IsNullOrWhiteSpace(roleCode) || branchId is null || branchId <= 0)
        {
            return null;
        }

        if (allowedRoles.Length > 0 && !allowedRoles.Contains(roleCode, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return new StaffSessionUserDto(
            employeeId.Value,
            HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeUsername) ?? string.Empty,
            HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeName) ?? string.Empty,
            HttpContext.StaffIdentity().GetString(SessionKeys.EmployeePhone),
            HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeEmail),
            HttpContext.StaffIdentity().GetInt32(SessionKeys.EmployeeRoleId) ?? 0,
            roleCode,
            HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeRoleName) ?? string.Empty,
            branchId.Value,
            HttpContext.StaffIdentity().GetString(SessionKeys.EmployeeBranchName) ?? string.Empty);
    }

    private void ClearStaffSession()
    {
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeId);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeUsername);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeName);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeePhone);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeEmail);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeRoleId);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeRoleCode);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeRoleName);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeBranchId);
        HttpContext.StaffIdentity().Remove(SessionKeys.EmployeeBranchName);
    }

    private static string NormalizeChefError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Không thể xử lý thao tác bếp lúc này.";
        }

        var normalized = message.Trim();
        if (normalized.Contains("Đơn hàng không có món để chuyển sang bếp.", StringComparison.OrdinalIgnoreCase))
        {
            return "??n h?ng ch?a c? m?n h?p l? ?? b?t ??u n?u.";
        }

        if (normalized.Contains("Order is already in kitchen processing state", StringComparison.OrdinalIgnoreCase))
        {
            return "Đơn này đã được chuyển sang luồng bếp rồi.";
        }

        if (normalized.Contains("Order is not in a status that can enter kitchen processing", StringComparison.OrdinalIgnoreCase))
        {
            return "??n n?y ch?a ? tr?ng th?i c? th? b?t ??u n?u.";
        }

        if (normalized.Contains("Không thể trừ nguyên liệu trong kho", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (normalized.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
        {
            return "Không thể xử lý thao tác bếp lúc này. Vui lòng thử lại.";
        }

        return normalized;
    }

    private static string NormalizeStaffAuthError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Có lỗi xảy ra. Vui lòng thử lại sau.";
        }

        return message.Trim() switch
        {
            "Invalid credentials." => "Tên đăng nhập hoặc mật khẩu không đúng.",
            "Missing username or password." => "Vui lòng nhập đầy đủ thông tin.",
            _ => message.Trim()
        };
    }

    private static string NormalizeStaffForgotPasswordMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu.";
        }

        return message.Trim() switch
        {
            "Nếu tài khoản tồn tại, hệ thống sẽ gửi hướng dẫn đặt lại mật khẩu." => "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu.",
            "Nếu tài khoản tồn tại, hệ thống đã gửi email đặt lại mật khẩu." => "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu.",
            "EmailOrUsername is required." => "Vui lòng nhập email.",
            _ => message.Trim()
        };
    }

    private static string NormalizeStaffResetValidationError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Link không hợp lệ hoặc đã hết hạn.";
        }

        return message.Trim() switch
        {
            "Token is required." => "Link không hợp lệ.",
            "Token is invalid." => "Link đặt lại mật khẩu không hợp lệ.",
            "Token was already used." => "Link này đã được sử dụng.",
            "Token has expired." => "Link đã hết hạn. Vui lòng yêu cầu link mới.",
            "New password must be at least 6 characters." => "Mật khẩu phải có ít nhất 6 ký tự.",
            "Password has been reset." => "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập với mật khẩu mới.",
            _ => message.Trim()
        };
    }

    private ActionResult Error(string code, string message, int statusCode, object? details = null)
        => StatusCode(statusCode, new ApiErrorResponse(false, code, message, details));
}
