namespace SelfRestaurant.Gateway.Api.Models;

public sealed record MobileLoginRequest(string Username, string Password);
public sealed record MobileGoogleRequest(string IdToken);
public sealed record MobileRefreshRequest(string RefreshToken);
public sealed record MobileRegisterRequest(string Name, string Username, string Password, string PhoneNumber,
    string Email, string? Gender = null, DateOnly? DateOfBirth = null, string? Address = null);
public sealed record MobileForgotPasswordRequest(string UsernameOrEmailOrPhone);
public sealed record MobileForgotPasswordResponse(string Message, string? ResetToken, DateTime? ExpiresAtUtc);
public sealed record MobileResetPasswordRequest(string Token, string NewPassword, string? ConfirmPassword = null);
public sealed record MobileChangePasswordRequest(string CurrentPassword, string NewPassword, string? ConfirmPassword = null);
public sealed record MobileChangePasswordInternalRequest(int CustomerId, string CurrentPassword, string NewPassword);
public sealed record MobileCustomerDto(int CustomerId, string Username, string Name, string PhoneNumber, string? Email, int LoyaltyPoints);
public sealed record MobileTokenResponse(bool Success, string TokenType, string AccessToken, DateTime AccessTokenExpiresAtUtc,
    string RefreshToken, DateTime RefreshTokenExpiresAtUtc, MobileCustomerDto User);
