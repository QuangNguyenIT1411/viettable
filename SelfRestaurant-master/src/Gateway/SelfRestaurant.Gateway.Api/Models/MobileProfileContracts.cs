namespace SelfRestaurant.Gateway.Api.Models;

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
