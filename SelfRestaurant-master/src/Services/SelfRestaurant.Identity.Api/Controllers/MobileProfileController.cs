using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Identity.Api.Infrastructure;
using SelfRestaurant.Identity.Api.Persistence;
using SelfRestaurant.Identity.Api.Persistence.Entities;

namespace SelfRestaurant.Identity.Api.Controllers;

[ApiController]
[Authorize(Roles = "customer")]
[Route("api/internal/mobile/me")]
public sealed partial class MobileProfileController : ControllerBase
{
    private static readonly string[] SupportedLanguages = ["vi-VN", "en-US"];
    private readonly IdentityDbContext _db;
    private readonly CatalogApiClient _catalog;

    public MobileProfileController(IdentityDbContext db, CatalogApiClient catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    [HttpGet]
    public async Task<ActionResult<MobileProfileResponse>> GetProfile(CancellationToken ct)
    {
        var customer = await FindCurrentCustomerAsync(ct);
        return customer is null
            ? Error("customer_not_found", "Không tìm thấy hồ sơ khách hàng.", 404)
            : Ok(MapProfile(customer));
    }

    [HttpPut]
    public async Task<ActionResult<MobileProfileResponse>> UpdateProfile(MobileProfileUpdateRequest request, CancellationToken ct)
    {
        var customerId = CurrentCustomerId();
        if (customerId is null) return Error("invalid_actor", "Access token không chứa khách hàng hợp lệ.", 401);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
            return Error("validation_failed", "Họ tên không hợp lệ.", 400, new { fields = new { name = new[] { "Họ tên là bắt buộc và tối đa 100 ký tự." } } });

        var phone = NormalizePhone(request.PhoneNumber);
        if (!PhoneRegex().IsMatch(phone))
            return Error("validation_failed", "Số điện thoại không hợp lệ.", 400, new { fields = new { phoneNumber = new[] { "Số điện thoại phải có từ 8 đến 15 chữ số." } } });
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
            return Error("validation_failed", "Email không hợp lệ.", 400, new { fields = new { email = new[] { "Vui lòng nhập email hợp lệ." } } });
        if (!string.IsNullOrWhiteSpace(request.Gender)
            && request.Gender.Trim().Length > 10)
            return Error("validation_failed", "Giới tính không hợp lệ.", 400);
        if (request.DateOfBirth is { } birthDate && (birthDate > DateOnly.FromDateTime(DateTime.UtcNow) || birthDate.Year < 1900))
            return Error("validation_failed", "Ngày sinh không hợp lệ.", 400);

        if (await _db.Customers.AnyAsync(x => x.CustomerID != customerId && x.PhoneNumber == phone, ct))
            return Error("phone_already_exists", "Số điện thoại đã được sử dụng.", 409);
        if (await _db.Customers.AnyAsync(x => x.CustomerID != customerId && x.Email == email, ct))
            return Error("email_already_exists", "Email đã được sử dụng.", 409);

        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.CustomerID == customerId && (x.IsActive ?? false), ct);
        if (customer is null) return Error("customer_not_found", "Không tìm thấy hồ sơ khách hàng.", 404);
        customer.Name = request.Name.Trim();
        customer.PhoneNumber = phone;
        customer.Email = email;
        customer.Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim();
        customer.DateOfBirth = request.DateOfBirth;
        customer.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        customer.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        return Ok(MapProfile(customer));
    }

    [HttpGet("loyalty")]
    public async Task<ActionResult<MobileLoyaltyResponse>> GetLoyalty(CancellationToken ct)
    {
        var customer = await FindCurrentCustomerAsync(ct);
        if (customer is null) return Error("customer_not_found", "Không tìm thấy khách hàng.", 404);
        var card = await _db.CustomerLoyalty.AsNoTracking()
            .Where(x => x.CustomerID == customer.CustomerID)
            .OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.CardID)
            .FirstOrDefaultAsync(ct);
        return Ok(MapLoyalty(customer, card));
    }

    [HttpGet("settings")]
    public async Task<ActionResult<MobileSettingsResponse>> GetSettings(CancellationToken ct)
    {
        var customer = await FindCurrentCustomerAsync(ct);
        if (customer is null) return Error("customer_not_found", "Không tìm thấy khách hàng.", 404);
        var settings = await GetOrCreateSettingsAsync(customer.CustomerID, ct);
        return Ok(MapSettings(settings));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<MobileSettingsResponse>> UpdateSettings(MobileSettingsUpdateRequest request, CancellationToken ct)
    {
        var customer = await FindCurrentCustomerAsync(ct);
        if (customer is null) return Error("customer_not_found", "Không tìm thấy khách hàng.", 404);
        var language = request.LanguageCode?.Trim() ?? "";
        if (!SupportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
            return Error("unsupported_language", "Ngôn ngữ chưa được hỗ trợ.", 400,
                new { fields = new { languageCode = new[] { "Chỉ hỗ trợ vi-VN hoặc en-US." } } });
        if (request.DefaultBranchId is > 0)
        {
            var branch = await _catalog.GetBranchAsync(request.DefaultBranchId.Value, ct);
            if (branch is null || !branch.IsActive)
                return Error("branch_not_found", "Chi nhánh mặc định không tồn tại hoặc đã ngừng hoạt động.", 404);
        }

        var settings = await GetOrCreateSettingsAsync(customer.CustomerID, ct);
        settings.PushNotificationsEnabled = request.PushNotificationsEnabled;
        settings.SmsReservationRemindersEnabled = request.SmsReservationRemindersEnabled;
        settings.BiometricLoginEnabled = request.BiometricLoginEnabled;
        settings.LanguageCode = SupportedLanguages.First(x => string.Equals(x, language, StringComparison.OrdinalIgnoreCase));
        settings.DefaultBranchId = request.DefaultBranchId is > 0 ? request.DefaultBranchId : null;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(MapSettings(settings));
    }

    private int? CurrentCustomerId()
        => int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) && id > 0 ? id : null;

    private async Task<Customers?> FindCurrentCustomerAsync(CancellationToken ct)
    {
        var id = CurrentCustomerId();
        return id is null ? null : await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CustomerID == id && (x.IsActive ?? false), ct);
    }

    private async Task<CustomerSettings> GetOrCreateSettingsAsync(int customerId, CancellationToken ct)
    {
        var settings = await _db.CustomerSettings.FirstOrDefaultAsync(x => x.CustomerId == customerId, ct);
        if (settings is not null) return settings;
        settings = new CustomerSettings { CustomerId = customerId, UpdatedAtUtc = DateTime.UtcNow };
        _db.CustomerSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private static MobileProfileResponse MapProfile(Customers customer) => new(
        customer.CustomerID, customer.Username, customer.Name, customer.PhoneNumber, customer.Email,
        customer.Gender, customer.DateOfBirth, customer.Address, customer.LoyaltyPoints ?? 0,
        GetTier(customer.LoyaltyPoints ?? 0).Code);

    private static MobileLoyaltyResponse MapLoyalty(Customers customer, CustomerLoyalty? card)
    {
        var points = Math.Max(0, customer.LoyaltyPoints ?? 0);
        var tier = GetTier(points);
        var progress = tier.NextThreshold is null ? 100 : (int)Math.Clamp(
            (points - tier.Threshold) * 100d / Math.Max(1, tier.NextThreshold.Value - tier.Threshold), 0, 100);
        return new MobileLoyaltyResponse(customer.CustomerID, customer.Name, points,
            tier.Code, tier.Name, tier.NextCode, tier.NextThreshold,
            tier.NextThreshold is null ? 0 : Math.Max(0, tier.NextThreshold.Value - points), progress,
            card?.CardID is > 0 ? $"VT-{card.CardID:000000}" : $"VT-{customer.CustomerID:000000}",
            card?.IssueDate, card?.ExpiryDate, tier.Benefits);
    }

    private static Tier GetTier(int points) => points switch
    {
        >= 20000 => new("DIAMOND", "Kim Cương", 20000, null, null,
            ["Tích điểm 5% giá trị hóa đơn", "Quà sinh nhật", "Ưu tiên xếp bàn", "Ưu đãi thành viên cao nhất"]),
        >= 5000 => new("GOLD", "Vàng", 5000, "DIAMOND", 20000,
            ["Tích điểm 5% giá trị hóa đơn", "Quà sinh nhật", "Ưu tiên xếp bàn"]),
        >= 1000 => new("SILVER", "Bạc", 1000, "GOLD", 5000,
            ["Tích điểm theo hóa đơn", "Ưu đãi dành cho thành viên"]),
        _ => new("MEMBER", "Thành viên", 0, "SILVER", 1000,
            ["Tích điểm theo hóa đơn"]),
    };

    private static MobileSettingsResponse MapSettings(CustomerSettings settings) => new(
        settings.PushNotificationsEnabled, settings.SmsReservationRemindersEnabled,
        settings.BiometricLoginEnabled, settings.LanguageCode, settings.DefaultBranchId, settings.UpdatedAtUtc);

    private static string NormalizePhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? "" : Regex.Replace(phone.Trim(), "[\\s.-]", "");

    private static bool IsValidEmail(string email)
    {
        try { return new MailAddress(email).Address == email && email.Length <= 100; }
        catch { return false; }
    }

    [GeneratedRegex("^\\+?[0-9]{8,15}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();

    private ObjectResult Error(string code, string message, int status, object? details = null)
        => StatusCode(status, new { code, message, details });

    private sealed record Tier(string Code, string Name, int Threshold, string? NextCode,
        int? NextThreshold, IReadOnlyList<string> Benefits);
    public sealed record MobileProfileUpdateRequest(string Name, string PhoneNumber, string Email,
        string? Gender = null, DateOnly? DateOfBirth = null, string? Address = null);
    public sealed record MobileProfileResponse(int CustomerId, string Username, string Name, string PhoneNumber,
        string? Email, string? Gender, DateOnly? DateOfBirth, string? Address, int LoyaltyPoints, string MembershipTier);
    public sealed record MobileLoyaltyResponse(int CustomerId, string CustomerName, int AvailablePoints,
        string TierCode, string TierName, string? NextTierCode, int? NextTierAtPoints, int PointsToNextTier,
        int ProgressPercent, string CardNumber, DateOnly? IssueDate, DateOnly? ExpiryDate, IReadOnlyList<string> Benefits);
    public sealed record MobileSettingsUpdateRequest(bool PushNotificationsEnabled, bool SmsReservationRemindersEnabled,
        bool BiometricLoginEnabled, string LanguageCode, int? DefaultBranchId = null);
    public sealed record MobileSettingsResponse(bool PushNotificationsEnabled, bool SmsReservationRemindersEnabled,
        bool BiometricLoginEnabled, string LanguageCode, int? DefaultBranchId, DateTime UpdatedAtUtc);
}
