using System.Data;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Identity.Api.Infrastructure;
using SelfRestaurant.Identity.Api.Persistence;
using SelfRestaurant.Identity.Api.Persistence.Entities;
using SelfRestaurant.Identity.Api.Security;

namespace SelfRestaurant.Identity.Api.Controllers;

[ApiController]
[Route("api/internal/mobile/auth")]
[EnableRateLimiting("identity-auth")]
public sealed class MobileAuthController : ControllerBase
{
    private readonly IdentityDbContext _db;
    private readonly MobileTokenService _tokens;
    private readonly PasswordResetEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<MobileAuthController> _logger;

    public MobileAuthController(
        IdentityDbContext db,
        MobileTokenService tokens,
        PasswordResetEmailSender emailSender,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<MobileAuthController> logger)
    {
        _db = db;
        _tokens = tokens;
        _emailSender = emailSender;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<MobileTokenResponse>> Register(MobileRegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.PhoneNumber)
            || string.IsNullOrWhiteSpace(request.Email))
            return Error("validation_failed", "Vui lòng nhập đầy đủ thông tin.", 400);
        if (request.Password.Trim().Length < 6)
            return Error("invalid_password", "Mật khẩu phải có ít nhất 6 ký tự.", 400);

        var username = request.Username.Trim();
        var email = request.Email.Trim();
        var phone = request.PhoneNumber.Trim();
        if (await _db.Customers.AnyAsync(x => x.Username == username, ct))
            return Error("username_already_exists", "Tên đăng nhập đã tồn tại.", 409);
        if (await _db.Customers.AnyAsync(x => x.Email == email, ct))
            return Error("email_already_exists", "Email đã được sử dụng.", 409);
        if (await _db.Customers.AnyAsync(x => x.PhoneNumber == phone, ct))
            return Error("phone_already_exists", "Số điện thoại đã được sử dụng.", 409);

        var customer = new Customers
        {
            Name = request.Name.Trim(), Username = username, Password = PasswordHashing.HashPassword(request.Password),
            PhoneNumber = phone, Email = email,
            Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim(),
            DateOfBirth = request.DateOfBirth,
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            AuthProvider = "Password", LoyaltyPoints = 0, CreditPoints = 0, IsActive = true,
            CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now,
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
        return Ok(await IssueTokenPairAsync(customer, null, ct));
    }

    [HttpPost("login")]
    public async Task<ActionResult<MobileTokenResponse>> Login(MobileLoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return Error("validation_failed", "Vui lòng nhập đầy đủ thông tin.", 400);

        var key = request.Username.Trim();
        var customer = await _db.Customers.FirstOrDefaultAsync(x => (x.IsActive ?? false)
            && (x.Username == key || x.Email == key || x.PhoneNumber == key), ct);
        if (customer is null || string.IsNullOrWhiteSpace(customer.Password)
            || !PasswordHashing.Verify(customer.Password, request.Password, out var needsUpgrade))
            return Error("invalid_credentials", "Sai tài khoản hoặc mật khẩu.", 401);
        if (string.Equals(customer.AuthProvider, "Google", StringComparison.OrdinalIgnoreCase))
            return Error("password_login_unavailable", "Tài khoản này chỉ đăng nhập bằng Google.", 401);

        if (needsUpgrade)
        {
            customer.Password = PasswordHashing.HashPassword(request.Password);
            customer.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync(ct);
        }
        return Ok(await IssueTokenPairAsync(customer, null, ct));
    }

    [HttpPost("google")]
    public async Task<ActionResult<MobileTokenResponse>> Google(MobileGoogleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            return Error("validation_failed", "Thiếu mã đăng nhập Google.", 400);
        var clientId = _configuration["Authentication:Google:ClientId"]
            ?? Environment.GetEnvironmentVariable("SELFRESTAURANT_GOOGLE_CLIENT_ID");
        if (string.IsNullOrWhiteSpace(clientId))
            return Error("google_not_configured", "Chưa cấu hình Google Client ID cho hệ thống.", 503);

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken.Trim(),
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId.Trim()] });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mobile Google token validation failed.");
            return Error("invalid_google_token", "Không thể xác thực tài khoản Google.", 401);
        }
        if (string.IsNullOrWhiteSpace(payload.Email) || payload.EmailVerified != true)
            return Error("google_email_unverified", "Google chưa xác minh email này.", 401);
        if (string.IsNullOrWhiteSpace(payload.Subject))
            return Error("invalid_google_token", "Không thể xác thực tài khoản Google.", 401);

        var email = payload.Email.Trim();
        var subject = payload.Subject.Trim();
        var customer = await _db.Customers.FirstOrDefaultAsync(x => (x.IsActive ?? false)
            && x.ExternalProvider == "Google" && x.ExternalSubject == subject, ct);
        customer ??= await _db.Customers.FirstOrDefaultAsync(x => (x.IsActive ?? false) && x.Email == email, ct);
        if (customer is not null && !string.Equals(customer.AuthProvider, "Google", StringComparison.OrdinalIgnoreCase))
            return Error("email_provider_conflict", "Email này đã đăng ký bằng mật khẩu.", 409);
        if (customer is null)
        {
            var baseUsername = $"google_{Guid.NewGuid():N}";
            customer = new Customers
            {
                Name = string.IsNullOrWhiteSpace(payload.Name) ? email.Split('@')[0] : payload.Name.Trim(),
                Username = baseUsername, PhoneNumber = string.Empty, Email = email, Password = null,
                AuthProvider = "Google", ExternalProvider = "Google", ExternalSubject = subject,
                LoyaltyPoints = 0, CreditPoints = 0, IsActive = true, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now,
            };
            _db.Customers.Add(customer);
        }
        else
        {
            customer.AuthProvider = "Google";
            customer.ExternalProvider = "Google";
            customer.ExternalSubject = subject;
            customer.Password = null;
            customer.UpdatedAt = DateTime.Now;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(await IssueTokenPairAsync(customer, null, ct));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<MobileTokenResponse>> Refresh(MobileRefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Error("refresh_token_required", "Thiếu refresh token.", 400);
        var hash = _tokens.HashRefreshToken(request.RefreshToken.Trim());
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var stored = await _db.MobileRefreshTokens.Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (stored is null)
            return Error("invalid_refresh_token", "Refresh token không hợp lệ.", 401);
        if (stored.RevokedAtUtc is not null)
        {
            await RevokeFamilyAsync(stored.FamilyId, "reuse_detected", ct);
            await transaction.CommitAsync(ct);
            return Error("refresh_token_reused", "Phiên đăng nhập đã bị thu hồi.", 401);
        }
        if (stored.ExpiresAtUtc <= DateTime.UtcNow || !(stored.Customer.IsActive ?? false))
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            stored.RevokedReason = "expired_or_inactive";
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Error("refresh_token_expired", "Phiên đăng nhập đã hết hạn.", 401);
        }

        var rawReplacement = _tokens.CreateRefreshToken();
        var replacementHash = _tokens.HashRefreshToken(rawReplacement);
        stored.RevokedAtUtc = DateTime.UtcNow;
        stored.RevokedReason = "rotated";
        stored.ReplacedByTokenHash = replacementHash;
        var replacement = CreateStoredToken(stored.CustomerId, stored.FamilyId, rawReplacement);
        _db.MobileRefreshTokens.Add(replacement);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(BuildTokenResponse(stored.Customer, rawReplacement, replacement.ExpiresAtUtc));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(MobileRefreshRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = _tokens.HashRefreshToken(request.RefreshToken.Trim());
            var stored = await _db.MobileRefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
            if (stored is not null && stored.RevokedAtUtc is null)
            {
                stored.RevokedAtUtc = DateTime.UtcNow;
                stored.RevokedReason = "logout";
                await _db.SaveChangesAsync(ct);
            }
        }
        return Ok(new { success = true });
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<MobileForgotPasswordResponse>> ForgotPassword(MobileForgotPasswordRequest request, CancellationToken ct)
    {
        // Same answer whether or not the account exists (nobody learns which e-mails/phones are registered).
        const string genericMessage = "Nếu tài khoản tồn tại, mã xác nhận gồm 6 số đã được gửi tới email đã đăng ký. Mã có hiệu lực 15 phút.";
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmailOrPhone))
            return Error("validation_failed", "Vui lòng nhập email hoặc số điện thoại.", 400);
        var customer = await FindByIdentityAsync(request.UsernameOrEmailOrPhone, ct);
        if (customer is null) return Ok(new MobileForgotPasswordResponse(genericMessage, null, null));

        // Owner 08/11: a 6-digit code the customer types in the app (the old 64-character link token could not be typed).
        var code = PasswordResetCodes.NewCode();
        var expires = DateTime.UtcNow.Add(PasswordResetCodes.Lifetime);
        _db.PasswordResetTokens.Add(new PasswordResetTokens
        {
            CustomerID = customer.CustomerID, Token = PasswordResetCodes.Stored(customer.CustomerID, code), CreatedAt = DateTime.UtcNow,
            ExpiryDate = expires, IsUsed = false,
        });
        await _db.SaveChangesAsync(ct);
        PasswordResetCodes.Clear(customer.CustomerID);
        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            var sent = await _emailSender.TrySendAsync(customer.Email, customer.Name, code, null, ct);
            if (!sent) _logger.LogWarning("Password reset code e-mail not sent (SMTP off or failed). customerId={CustomerId}", customer.CustomerID);
        }
        else _logger.LogWarning("Password reset code requested for a customer without e-mail. customerId={CustomerId}", customer.CustomerID);
        return Ok(new MobileForgotPasswordResponse(genericMessage,
            _environment.IsDevelopment() ? code : null,
            _environment.IsDevelopment() ? expires : null));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(MobileResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return Error("validation_failed", "Vui lòng nhập mật khẩu mới.", 400);
        if (request.NewPassword.Trim().Length < 6)
            return Error("invalid_password", "Mật khẩu phải có ít nhất 6 ký tự.", 400);

        PasswordResetTokens? reset;
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            // App: account + 6-digit code.
            const string wrong = "Mã xác nhận không đúng hoặc đã hết hạn.";
            if (string.IsNullOrWhiteSpace(request.UsernameOrEmailOrPhone))
                return Error("validation_failed", "Vui lòng nhập email hoặc số điện thoại.", 400);
            var customer = await FindByIdentityAsync(request.UsernameOrEmailOrPhone, ct);
            if (customer is null) return Error("invalid_reset_code", wrong, 400);
            var now = DateTime.UtcNow;
            reset = await _db.PasswordResetTokens.Include(x => x.Customer)
                .Where(x => x.CustomerID == customer.CustomerID && !x.IsUsed && x.ExpiryDate > now && x.Token.StartsWith(PasswordResetCodes.Prefix))
                .OrderByDescending(x => x.TokenID).FirstOrDefaultAsync(ct);
            if (reset is null) return Error("invalid_reset_code", wrong, 400);
            if (!PasswordResetCodes.Matches(reset.Token, customer.CustomerID, request.Code))
            {
                var tries = PasswordResetCodes.RegisterFailure(customer.CustomerID);
                if (tries >= PasswordResetCodes.MaxAttempts)
                {
                    reset.IsUsed = true; // spent: a new code must be asked for
                    await _db.SaveChangesAsync(ct);
                    PasswordResetCodes.Clear(customer.CustomerID);
                    return Error("reset_code_locked", "Bạn đã nhập sai quá 5 lần. Vui lòng bấm \"Gửi lại\" để nhận mã mới.", 400);
                }
                return Error("invalid_reset_code", $"Mã xác nhận không đúng. Bạn còn {PasswordResetCodes.MaxAttempts - tries} lần thử.", 400);
            }
            PasswordResetCodes.Clear(customer.CustomerID);
        }
        else
        {
            // Older link flow (token in the e-mailed link). A code hash is never accepted as a link token.
            if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Trim().StartsWith(PasswordResetCodes.Prefix, StringComparison.Ordinal))
                return Error("validation_failed", "Vui lòng nhập mã xác nhận.", 400);
            reset = await _db.PasswordResetTokens.Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Token == request.Token.Trim(), ct);
        }
        if (reset is null || reset.IsUsed || reset.ExpiryDate <= DateTime.UtcNow || !(reset.Customer.IsActive ?? false))
            return Error("invalid_reset_token", "Mã đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.", 400);
        reset.IsUsed = true;
        reset.Customer.Password = PasswordHashing.HashPassword(request.NewPassword);
        reset.Customer.AuthProvider = "Password";
        reset.Customer.UpdatedAt = DateTime.Now;
        await RevokeCustomerTokensAsync(reset.CustomerID, "password_reset", ct);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đặt lại mật khẩu thành công." });
    }

    private Task<Customers?> FindByIdentityAsync(string identity, CancellationToken ct)
    {
        var key = identity.Trim();
        return _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => (x.IsActive ?? false)
            && (x.Username == key || x.Email == key || x.PhoneNumber == key), ct);
    }

    [HttpPost("change-password")]
    [Authorize(Roles = "customer")]
    public async Task<IActionResult> ChangePassword(MobileChangePasswordRequest request, CancellationToken ct)
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(subject, out var customerId) || customerId <= 0)
            return Error("invalid_actor", "Access token không chứa khách hàng hợp lệ.", 401);
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return Error("validation_failed", "Vui lòng nhập đầy đủ thông tin.", 400);
        if (request.NewPassword.Trim().Length < 6)
            return Error("invalid_password", "Mật khẩu mới phải có ít nhất 6 ký tự.", 400);
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.CustomerID == customerId && (x.IsActive ?? false), ct);
        if (customer is null) return Error("customer_not_found", "Không tìm thấy khách hàng.", 404);
        if (string.IsNullOrWhiteSpace(customer.Password) || !PasswordHashing.Verify(customer.Password, request.CurrentPassword, out _))
            return Error("invalid_current_password", "Mật khẩu hiện tại không đúng.", 400);
        customer.Password = PasswordHashing.HashPassword(request.NewPassword);
        customer.UpdatedAt = DateTime.Now;
        await RevokeCustomerTokensAsync(customer.CustomerID, "password_changed", ct);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại." });
    }

    private async Task<MobileTokenResponse> IssueTokenPairAsync(Customers customer, string? familyId, CancellationToken ct)
    {
        var rawRefresh = _tokens.CreateRefreshToken();
        var stored = CreateStoredToken(customer.CustomerID, familyId ?? Guid.NewGuid().ToString(), rawRefresh);
        _db.MobileRefreshTokens.Add(stored);
        await _db.SaveChangesAsync(ct);
        return BuildTokenResponse(customer, rawRefresh, stored.ExpiresAtUtc);
    }

    private MobileRefreshToken CreateStoredToken(int customerId, string familyId, string rawToken) => new()
    {
        CustomerId = customerId, TokenHash = _tokens.HashRefreshToken(rawToken), FamilyId = familyId,
        CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = _tokens.GetRefreshExpiryUtc(),
        CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
        UserAgent = Request.Headers.UserAgent.ToString() is { Length: > 500 } ua ? ua[..500] : Request.Headers.UserAgent.ToString(),
    };

    private MobileTokenResponse BuildTokenResponse(Customers customer, string refreshToken, DateTime refreshExpires)
    {
        var access = _tokens.CreateAccessToken(customer);
        return new MobileTokenResponse(true, "Bearer", access.Value, access.ExpiresAtUtc, refreshToken, refreshExpires,
            new MobileCustomer(customer.CustomerID, customer.Username, customer.Name, customer.PhoneNumber,
                customer.Email, customer.LoyaltyPoints ?? 0));
    }

    private async Task RevokeFamilyAsync(string familyId, string reason, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await _db.MobileRefreshTokens.Where(x => x.FamilyId == familyId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAtUtc, now)
                .SetProperty(x => x.RevokedReason, reason), ct);
    }

    private async Task RevokeCustomerTokensAsync(int customerId, string reason, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // Tracked update (saved with the caller's SaveChanges): same result, and testable in memory.
        foreach (var token in await _db.MobileRefreshTokens.Where(x => x.CustomerId == customerId && x.RevokedAtUtc == null).ToListAsync(ct))
        {
            token.RevokedAtUtc = now;
            token.RevokedReason = reason;
        }
    }

    private ObjectResult Error(string code, string message, int status) => StatusCode(status, new { code, message });

    public sealed record MobileLoginRequest(string Username, string Password);
    public sealed record MobileGoogleRequest(string IdToken);
    public sealed record MobileRefreshRequest(string RefreshToken);
    public sealed record MobileRegisterRequest(string Name, string Username, string Password, string PhoneNumber,
        string Email, string? Gender = null, DateOnly? DateOfBirth = null, string? Address = null);
    public sealed record MobileForgotPasswordRequest(string UsernameOrEmailOrPhone);
    public sealed record MobileForgotPasswordResponse(string Message, string? ResetToken, DateTime? ExpiresAtUtc);
    public sealed record MobileResetPasswordRequest(string? Token, string NewPassword, string? UsernameOrEmailOrPhone = null, string? Code = null);
    public sealed record MobileChangePasswordRequest(int CustomerId, string CurrentPassword, string NewPassword);
    public sealed record MobileCustomer(int CustomerId, string Username, string Name, string PhoneNumber, string? Email, int LoyaltyPoints);
    public sealed record MobileTokenResponse(bool Success, string TokenType, string AccessToken, DateTime AccessTokenExpiresAtUtc,
        string RefreshToken, DateTime RefreshTokenExpiresAtUtc, MobileCustomer User);
}
