using SelfRestaurant.Gateway.Api.Models;
using System.Text.Json;

namespace SelfRestaurant.Gateway.Api.Services;

public sealed class IdentityClient : ApiClientBase
{
    public IdentityClient(HttpClient http, IHttpClientFactory httpClientFactory) : base(http)
    {
    }

    public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        PostForAsync<LoginRequest, LoginResponse>("/api/identity/login", request, cancellationToken);

    public Task<LoginResponse?> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken) =>
        PostForAsync<GoogleLoginRequest, LoginResponse>("/api/identity/login/google", request, cancellationToken);

    public Task<MobileTokenResponse?> MobileLoginAsync(MobileLoginRequest request, CancellationToken cancellationToken) =>
        PostForAsync<MobileLoginRequest, MobileTokenResponse>("/api/internal/mobile/auth/login", request, cancellationToken);

    public Task<MobileTokenResponse?> MobileGoogleAsync(MobileGoogleRequest request, CancellationToken cancellationToken) =>
        PostForAsync<MobileGoogleRequest, MobileTokenResponse>("/api/internal/mobile/auth/google", request, cancellationToken);

    public Task<MobileTokenResponse?> MobileRegisterAsync(MobileRegisterRequest request, CancellationToken cancellationToken) =>
        PostForAsync<MobileRegisterRequest, MobileTokenResponse>("/api/internal/mobile/auth/register", request, cancellationToken);

    public Task<MobileTokenResponse?> MobileRefreshAsync(MobileRefreshRequest request, CancellationToken cancellationToken) =>
        PostForAsync<MobileRefreshRequest, MobileTokenResponse>("/api/internal/mobile/auth/refresh", request, cancellationToken);

    public Task MobileLogoutAsync(MobileRefreshRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/internal/mobile/auth/logout", request, cancellationToken);

    public Task<MobileForgotPasswordResponse?> MobileForgotPasswordAsync(MobileForgotPasswordRequest request, CancellationToken cancellationToken) =>
        PostForAsync<MobileForgotPasswordRequest, MobileForgotPasswordResponse>("/api/internal/mobile/auth/forgot-password", request, cancellationToken);

    public Task MobileResetPasswordAsync(MobileResetPasswordRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/internal/mobile/auth/reset-password", request, cancellationToken);

    public Task MobileChangePasswordAsync(MobileChangePasswordInternalRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/internal/mobile/auth/change-password", request, cancellationToken);

    public Task<MobileProfileResponse?> GetMobileProfileAsync(CancellationToken cancellationToken) =>
        GetAsync<MobileProfileResponse>("/api/internal/mobile/me", cancellationToken);

    public Task<MobileProfileResponse?> UpdateMobileProfileAsync(MobileProfileUpdateRequest request, CancellationToken cancellationToken) =>
        PutForAsync<MobileProfileUpdateRequest, MobileProfileResponse>("/api/internal/mobile/me", request, cancellationToken);

    public Task<MobileLoyaltyResponse?> GetMobileLoyaltyAsync(CancellationToken cancellationToken) =>
        GetAsync<MobileLoyaltyResponse>("/api/internal/mobile/me/loyalty", cancellationToken);

    public Task<MobileSettingsResponse?> GetMobileSettingsAsync(CancellationToken cancellationToken) =>
        GetAsync<MobileSettingsResponse>("/api/internal/mobile/me/settings", cancellationToken);

    public Task<MobileSettingsResponse?> UpdateMobileSettingsAsync(MobileSettingsUpdateRequest request, CancellationToken cancellationToken) =>
        PutForAsync<MobileSettingsUpdateRequest, MobileSettingsResponse>("/api/internal/mobile/me/settings", request, cancellationToken);

    public Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/identity/register", request, cancellationToken);

    public Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/identity/password/change", request, cancellationToken);

    public Task<ForgotPasswordResponse?> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken) =>
        PostForAsync<ForgotPasswordRequest, ForgotPasswordResponse>("/api/identity/password/forgot", request, cancellationToken);

    public Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/identity/password/reset", request, cancellationToken);

    public Task ValidateResetPasswordTokenAsync(string token, CancellationToken cancellationToken) =>
        GetAsync<object>($"/api/identity/password/reset/validate?token={Uri.EscapeDataString(token)}", cancellationToken);

    public Task<CustomerProfileResponse?> GetCustomerAsync(int customerId, CancellationToken cancellationToken) =>
        GetAsync<CustomerProfileResponse>($"/api/customers/{customerId}", cancellationToken);

    public Task UpdateCustomerProfileAsync(int customerId, UpdateCustomerProfileRequest request, CancellationToken cancellationToken) =>
        PutAsync($"/api/customers/{customerId}/profile", request, cancellationToken);

    public Task<AdminIdentityStatsResponse?> GetAdminStatsAsync(int? branchId, CancellationToken cancellationToken)
    {
        var query = branchId is > 0 ? $"?branchId={branchId.Value}" : string.Empty;
        return GetAsync<AdminIdentityStatsResponse>($"/api/identity/admin/stats{query}", cancellationToken);
    }

    public Task<StaffLoginResponse?> StaffLoginAsync(StaffLoginRequest request, CancellationToken cancellationToken) =>
        PostForAsync<StaffLoginRequest, StaffLoginResponse>("/api/identity/staff/login", request, cancellationToken);

    public Task StaffChangePasswordAsync(StaffChangePasswordRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/identity/staff/password/change", request, cancellationToken);

    public Task<ForgotPasswordResponse?> StaffForgotPasswordAsync(StaffForgotPasswordRequest request, CancellationToken cancellationToken) =>
        PostForAsync<StaffForgotPasswordRequest, ForgotPasswordResponse>("/api/identity/staff/password/forgot", request, cancellationToken);

    public Task StaffResetPasswordAsync(StaffResetPasswordRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/identity/staff/password/reset", request, cancellationToken);

    public Task ValidateStaffResetPasswordTokenAsync(string token, CancellationToken cancellationToken) =>
        GetAsync<object>($"/api/identity/staff/password/reset/validate?token={Uri.EscapeDataString(token)}", cancellationToken);

    public Task<StaffLoginResponse?> UpdateStaffProfileAsync(int employeeId, StaffUpdateProfileRequest request, CancellationToken cancellationToken) =>
        PutForAsync<StaffUpdateProfileRequest, StaffLoginResponse>($"/api/identity/staff/{employeeId}", request, cancellationToken);

    public async Task<IReadOnlyList<EmployeeRoleDto>> GetEmployeeRolesAsync(CancellationToken cancellationToken)
    {
        var list = await GetAsync<IReadOnlyList<EmployeeRoleDto>>("/api/identity/admin/roles", cancellationToken);
        return list ?? Array.Empty<EmployeeRoleDto>();
    }

    public Task<AdminEmployeePagedResponse?> GetAdminEmployeesAsync(
        string? search,
        int? branchId,
        int? roleId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        GetAdminEmployeesCompatAsync(search, branchId, roleId, page, pageSize, cancellationToken);

    public Task<AdminEmployeeDto?> GetAdminEmployeeByIdAsync(int employeeId, CancellationToken cancellationToken) =>
        GetAsync<AdminEmployeeDto>($"/api/identity/admin/employees/{employeeId}", cancellationToken);

    private async Task<AdminEmployeePagedResponse?> GetAdminEmployeesCompatAsync(
        string? search,
        int? branchId,
        int? roleId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var qs = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 100)}",
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            qs.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }
        if (branchId is > 0)
        {
            qs.Add($"branchId={branchId.Value}");
        }
        if (roleId is > 0)
        {
            qs.Add($"roleId={roleId.Value}");
        }

        var url = $"/api/identity/admin/employees?{string.Join("&", qs)}";
        var payload = await GetAsync<JsonElement>(url, cancellationToken);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        if (payload.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new AdminEmployeePagedResponse(
                Page: Math.Max(1, page),
                PageSize: Math.Clamp(pageSize, 1, 100),
                TotalItems: 0,
                TotalPages: 0,
                Items: Array.Empty<AdminEmployeeDto>());
        }

        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("items", out _))
        {
            var paged = payload.Deserialize<AdminEmployeePagedResponse>(options);
            if (paged is not null)
            {
                return paged;
            }
        }

        if (payload.ValueKind == JsonValueKind.Array)
        {
            var items = payload.Deserialize<IReadOnlyList<AdminEmployeeDto>>(options) ?? Array.Empty<AdminEmployeeDto>();
            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
            var totalItems = items.Count;
            var totalPages = totalItems == 0
                ? 0
                : (int)Math.Ceiling(totalItems / (double)normalizedPageSize);

            var pagedItems = items
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToArray();

            return new AdminEmployeePagedResponse(
                Page: normalizedPage,
                PageSize: normalizedPageSize,
                TotalItems: totalItems,
                TotalPages: totalPages,
                Items: pagedItems);
        }

        return new AdminEmployeePagedResponse(
            Page: Math.Max(1, page),
            PageSize: Math.Clamp(pageSize, 1, 100),
            TotalItems: 0,
            TotalPages: 0,
            Items: Array.Empty<AdminEmployeeDto>());
    }

    public Task CreateAdminEmployeeAsync(AdminUpsertEmployeeRequest request, CancellationToken cancellationToken) =>
        PostAsync("/api/identity/admin/employees", request, cancellationToken);

    public Task UpdateAdminEmployeeAsync(int employeeId, AdminUpsertEmployeeRequest request, CancellationToken cancellationToken) =>
        PutAsync($"/api/identity/admin/employees/{employeeId}", request, cancellationToken);

    public Task DeactivateAdminEmployeeAsync(int employeeId, CancellationToken cancellationToken) =>
        PostAsync<object>($"/api/identity/admin/employees/{employeeId}/deactivate", new { }, cancellationToken);

    public Task<AdminEmployeeHistoryResponse?> GetAdminEmployeeHistoryAsync(
        int employeeId,
        int activityPage,
        int cookingPage,
        int pageSize,
        int days,
        CancellationToken cancellationToken) =>
        GetAsync<AdminEmployeeHistoryResponse>(
            $"/api/identity/admin/employees/{employeeId}/history?activityPage={Math.Max(1, activityPage)}&cookingPage={Math.Max(1, cookingPage)}&pageSize={Math.Clamp(pageSize, 1, 100)}&days={Math.Clamp(days, 1, 365)}",
            cancellationToken);
}
