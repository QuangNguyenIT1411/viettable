using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Google.Apis.Auth;
using SelfRestaurant.Identity.Api.Persistence;
using SelfRestaurant.Identity.Api.Persistence.Entities;
using SelfRestaurant.Identity.Api.Infrastructure;
using SelfRestaurant.Identity.Api.Infrastructure.Auditing;
using SelfRestaurant.Identity.Api.Security;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SelfRestaurant.Identity.Api.Controllers;

[ApiController]
[Route("api/identity")]
public sealed class IdentityController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, StaffPasswordResetState> StaffPasswordResetTokens = new();

    private readonly IdentityDbContext _db;
    private readonly ILogger<IdentityController> _logger;
    private readonly PasswordResetEmailSender _passwordResetEmailSender;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly CatalogApiClient _catalogApi;
    private readonly OrdersApiClient _ordersApi;
    private readonly BillingApiClient _billingApi;
    private readonly BusinessAuditLogger _auditLogger;

    public IdentityController(
        IdentityDbContext db,
        ILogger<IdentityController> logger,
        PasswordResetEmailSender passwordResetEmailSender,
        IHostEnvironment environment,
        IConfiguration configuration,
        CatalogApiClient catalogApi,
        OrdersApiClient ordersApi,
        BillingApiClient billingApi,
        BusinessAuditLogger auditLogger)
    {
        _db = db;
        _logger = logger;
        _passwordResetEmailSender = passwordResetEmailSender;
        _environment = environment;
        _configuration = configuration;
        _catalogApi = catalogApi;
        _ordersApi = ordersApi;
        _billingApi = billingApi;
        _auditLogger = auditLogger;
    }

    private static string GenerateResetToken()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void CleanupExpiredStaffResetTokens()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in StaffPasswordResetTokens)
        {
            if (pair.Value.IsUsed || pair.Value.ExpiryUtc <= now)
            {
                StaffPasswordResetTokens.TryRemove(pair.Key, out _);
            }
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("identity-auth")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Vui lòng nhập đầy đủ thông tin." });
        }

        var username = request.Username.Trim();
        var customer = await _db.Customers
            .FirstOrDefaultAsync(
                c =>
                    (c.IsActive ?? false) == true
                    && (c.Username == username || c.Email == username || c.PhoneNumber == username),
                cancellationToken);

        if (customer is null)
        {
            _logger.LogWarning("Customer login failed for missing key={Key}", username);
            return NotFound(new { message = "Tên đăng nhập/Email/SĐT không tồn tại." });
        }

        if (string.Equals(customer.AuthProvider, "Google", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(customer.Password))
        {
            _logger.LogWarning("Password login blocked for Google-only customerId={CustomerId}", customer.CustomerID);
            return Unauthorized(new { message = "Tài khoản này chỉ đăng nhập bằng Google." });
        }

        if (!PasswordHashing.Verify(customer.Password, request.Password, out var needsUpgrade))
        {
            _logger.LogWarning("Customer login failed for key={Key}", username);
            return Unauthorized(new { message = "Mật khẩu không chính xác." });
        }

        if (needsUpgrade)
        {
            customer.Password = PasswordHashing.HashPassword(request.Password);
        }

        customer.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Customer login succeeded. customerId={CustomerId}", customer.CustomerID);

        _auditLogger.Add(
            actionType: "customer.login",
            entityType: "Customer",
            entityId: customer.CustomerID.ToString(),
            customerId: customer.CustomerID,
            actorType: "Customer",
            actorId: customer.CustomerID,
            actorName: customer.Name,
            notes: $"Login successful via password for username: {username}");

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new LoginResponse(
            CustomerId: customer.CustomerID,
            Username: customer.Username,
            Name: customer.Name,
            PhoneNumber: customer.PhoneNumber,
            Email: customer.Email,
            LoyaltyPoints: customer.LoyaltyPoints ?? 0));
    }

    [HttpPost("login/google")]
    [EnableRateLimiting("identity-auth")]
    public async Task<ActionResult<LoginResponse>> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new { message = "Thiếu mã đăng nhập Google." });
        }

        var googleClientId = _configuration["Authentication:Google:ClientId"]
            ?? Environment.GetEnvironmentVariable("SELFRESTAURANT_GOOGLE_CLIENT_ID");
        if (string.IsNullOrWhiteSpace(googleClientId))
        {
            return BadRequest(new { message = "Chưa cấu hình Google Client ID cho hệ thống." });
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken.Trim(),
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { googleClientId.Trim() },
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google customer login token validation failed.");
            return Unauthorized(new { message = "Không thể xác thực tài khoản Google." });
        }

        if (string.IsNullOrWhiteSpace(payload.Email) || payload.EmailVerified != true)
        {
            return Unauthorized(new { message = "Google chưa xác minh email này." });
        }

        var email = payload.Email.Trim();
        var subject = payload.Subject.Trim();
        var customer = await _db.Customers.FirstOrDefaultAsync(
            c => (c.IsActive ?? false) == true
                && c.ExternalProvider == "Google"
                && c.ExternalSubject == subject,
            cancellationToken);

        if (customer is null)
        {
            customer = await _db.Customers.FirstOrDefaultAsync(
                c => (c.IsActive ?? false) == true && c.Email == email,
                cancellationToken);

            if (customer is not null && !string.Equals(customer.AuthProvider, "Google", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { message = "Email này đã đăng ký bằng mật khẩu. Vui lòng đăng nhập bằng tài khoản và mật khẩu." });
            }
        }

        if (customer is null)
        {
            customer = new SelfRestaurant.Identity.Api.Persistence.Entities.Customers
            {
                Name = string.IsNullOrWhiteSpace(payload.Name) ? email.Split('@')[0] : payload.Name.Trim(),
                Username = await GenerateGoogleUsernameAsync(email, cancellationToken),
                Password = null,
                PhoneNumber = string.Empty,
                Email = email,
                AuthProvider = "Google",
                ExternalProvider = "Google",
                ExternalSubject = subject,
                LoyaltyPoints = 0,
                CreditPoints = 0,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };
            _db.Customers.Add(customer);
        }
        else
        {
            customer.AuthProvider = "Google";
            customer.ExternalProvider = "Google";
            customer.ExternalSubject = subject;
            customer.Password = null;
            if (string.IsNullOrWhiteSpace(customer.Email))
            {
                customer.Email = email;
            }
            if (string.IsNullOrWhiteSpace(customer.Name) && !string.IsNullOrWhiteSpace(payload.Name))
            {
                customer.Name = payload.Name.Trim();
            }
            customer.UpdatedAt = DateTime.Now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Google customer login succeeded. customerId={CustomerId}", customer.CustomerID);

        _auditLogger.Add(
            actionType: "customer.login.google",
            entityType: "Customer",
            entityId: customer.CustomerID.ToString(),
            customerId: customer.CustomerID,
            actorType: "Customer",
            actorId: customer.CustomerID,
            actorName: customer.Name,
            notes: $"Login successful via Google for email: {email}");

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new LoginResponse(
            CustomerId: customer.CustomerID,
            Username: customer.Username,
            Name: customer.Name,
            PhoneNumber: customer.PhoneNumber,
            Email: customer.Email,
            LoyaltyPoints: customer.LoyaltyPoints ?? 0));
    }

    [HttpPost("register")]
    [EnableRateLimiting("identity-auth")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.PhoneNumber)
            || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Vui lòng nhập đầy đủ thông tin." });
        }

        if (request.Password.Trim().Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });
        }

        var username = request.Username.Trim();
        if (await _db.Customers.AnyAsync(c => c.Username == username, cancellationToken))
        {
            _logger.LogWarning("Customer register conflict by username={Username}", username);
            return Conflict(new { message = "Tên đăng nhập đã tồn tại." });
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim();
            if (await _db.Customers.AnyAsync(c => c.Email == email, cancellationToken))
            {
                _logger.LogWarning("Customer register conflict by email={Email}", email);
                return Conflict(new { message = "Email đã được sử dụng." });
            }
        }

        var customer = new SelfRestaurant.Identity.Api.Persistence.Entities.Customers
        {
            Name = request.Name.Trim(),
            Username = username,
            Password = PasswordHashing.HashPassword(request.Password),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            DateOfBirth = request.DateOfBirth,
            LoyaltyPoints = 0,
            CreditPoints = 0,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Customer registered. customerId={CustomerId}", customer.CustomerID);

        _auditLogger.Add(
            actionType: "customer.register",
            entityType: "Customer",
            entityId: customer.CustomerID.ToString(),
            customerId: customer.CustomerID,
            actorType: "Customer",
            actorId: customer.CustomerID,
            actorName: customer.Name,
            afterState: new { username = customer.Username, email = customer.Email, phone = customer.PhoneNumber },
            notes: $"New customer registered: {customer.Username}");

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new RegisterResponse(customer.CustomerID));
    }

    [HttpGet("/api/internal/customers/loyalty/by-phone")]
    public async Task<ActionResult<object>> GetLoyaltyByPhone([FromQuery] string? phoneNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return BadRequest(new { message = "Vui lòng nhập số điện thoại." });
        }

        var normalizedPhone = phoneNumber.Trim();

        var customer = await _db.CustomerLoyalty
            .AsNoTracking()
            .Where(x => x.PhoneNumber == normalizedPhone)
            .OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.CardID)
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        return Ok(new
        {
            customerId = customer.CustomerID,
            name = customer.Name,
            phone = customer.PhoneNumber,
            currentPoints = customer.LoyaltyPoints ?? 0,
            cardPoints = customer.CardPoints ?? 0,
            cardId = customer.CardID
        });
    }

    [HttpGet("/api/internal/customers:batch")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetCustomersBatch([FromQuery] int[]? ids, CancellationToken cancellationToken)
    {
        var customerIds = (ids ?? Array.Empty<int>())
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (customerIds.Length == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var customers = await _db.Customers
            .AsNoTracking()
            .Where(x => customerIds.Contains(x.CustomerID) && (x.IsActive ?? true))
            .Select(x => new
            {
                customerId = x.CustomerID,
                username = x.Username,
                name = x.Name,
                phoneNumber = x.PhoneNumber,
                email = x.Email,
                gender = x.Gender,
                dateOfBirth = x.DateOfBirth,
                address = x.Address,
                loyaltyPoints = x.LoyaltyPoints ?? 0,
            })
            .ToListAsync(cancellationToken);

        return Ok(customers);
    }

    [HttpGet("/api/customers/{customerId:int}")]
    public async Task<ActionResult<object>> GetCustomer(int customerId, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .Where(x => x.CustomerID == customerId && (x.IsActive ?? true))
            .Select(x => new
            {
                customerId = x.CustomerID,
                username = x.Username,
                name = x.Name,
                phoneNumber = x.PhoneNumber,
                email = x.Email,
                gender = x.Gender,
                dateOfBirth = x.DateOfBirth,
                address = x.Address,
                loyaltyPoints = x.LoyaltyPoints ?? 0,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpGet("/api/internal/customers/{customerId:int}/order-history")]
    public async Task<ActionResult<CustomerOrderHistoryPagedResponse>> GetCustomerOrderHistory(
        int customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0)
        {
            return BadRequest(new { message = "Invalid customer ID." });
        }

        var history = await _ordersApi.GetCustomerOrderHistoryAsync(
            customerId,
            page,
            pageSize,
            days,
            cancellationToken);

        if (history is null)
        {
            return Ok(new CustomerOrderHistoryPagedResponse(
                Page: page,
                PageSize: pageSize,
                TotalItems: 0,
                TotalPages: 0,
                Items: Array.Empty<CustomerOrderHistoryItem>()));
        }

        return Ok(history);
    }

    [HttpPut("/api/customers/{customerId:int}/profile")]
    public async Task<ActionResult> UpdateCustomerProfile(
        int customerId,
        [FromBody] UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(
            x => x.CustomerID == customerId && (x.IsActive ?? true),
            cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        customer.Username = request.Username.Trim();
        customer.Name = request.Name.Trim();
        customer.PhoneNumber = request.PhoneNumber.Trim();
        customer.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        customer.Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim();
        customer.DateOfBirth = request.DateOfBirth;
        customer.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        customer.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync(cancellationToken);

        _auditLogger.Add(
            actionType: "customer.profile.update",
            entityType: "Customer",
            entityId: customerId.ToString(),
            customerId: customerId,
            actorType: "Customer",
            actorId: customerId,
            actorName: customer.Name,
            notes: "Profile updated");

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("/api/customers/{customerId:int}/loyalty/settle")]
    public async Task<ActionResult<object>> SettleLoyalty(
        int customerId,
        [FromBody] LoyaltySettlementRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SettlementKey is { Length: > 100 }) return BadRequest(new { code = "invalid_settlement_key" });
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize settlements for this customer, including legacy callers without a key.
        await _db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"loyalty:" + customerId}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51000, 'Loyalty settlement busy', 1;", cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.SettlementKey))
        {
            var replay = await _db.Set<LoyaltySettlement>().FindAsync(new object[] { request.SettlementKey }, cancellationToken);
            if (replay is not null)
            {
                if (replay.CustomerId != customerId || replay.AmountPaid != request.AmountPaid || replay.PointsUsed != request.PointsUsed)
                    return Conflict(new { code = "settlement_conflict", message = "Settlement key đã được sử dụng." });
                return Content(replay.ResponseJson, "application/json");
            }
        }
        var customer = await _db.Customers
            .FirstOrDefaultAsync(x => x.CustomerID == customerId && (x.IsActive ?? true), cancellationToken);
        if (customer is null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        var pointsBefore = customer.LoyaltyPoints ?? 0;
        var pointsUsed = request.PointsUsed < 0 ? 0 : request.PointsUsed;
        pointsUsed = Math.Min(pointsUsed, pointsBefore);
        pointsUsed = (pointsUsed / 1000) * 1000;

        var updatedPoints = pointsBefore - pointsUsed;
        if (updatedPoints < 0)
        {
            updatedPoints = 0;
        }

        var amountPaid = request.AmountPaid < 0 ? 0 : request.AmountPaid;
        var pointsEarned = amountPaid > 0 ? (int)Math.Floor(amountPaid * 0.01m) : 0;

        customer.LoyaltyPoints = updatedPoints + pointsEarned;
        customer.UpdatedAt = DateTime.Now;

        var response = new
        {
            customerId = customer.CustomerID,
            customerName = customer.Name,
            pointsBefore,
            pointsUsed,
            pointsEarned,
            customerPoints = customer.LoyaltyPoints ?? 0,
        };
        if (!string.IsNullOrWhiteSpace(request.SettlementKey))
            _db.Set<LoyaltySettlement>().Add(new LoyaltySettlement { SettlementKey = request.SettlementKey,
                CustomerId = customerId, PointsUsed = request.PointsUsed, AmountPaid = request.AmountPaid,
                ResponseJson = System.Text.Json.JsonSerializer.Serialize(response) });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost("password/change")]
    [EnableRateLimiting("identity-auth")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerId <= 0)
        {
            return BadRequest(new { message = "Không tìm thấy khách hàng." });
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "Vui lòng điền đầy đủ thông tin mật khẩu." });
        }

        if (request.NewPassword.Trim().Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự." });
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(
            c => c.CustomerID == request.CustomerId && (c.IsActive ?? false) == true,
            cancellationToken);

        if (customer is null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        if (string.IsNullOrWhiteSpace(customer.Password)
            || !PasswordHashing.Verify(customer.Password, request.CurrentPassword, out _))
        {
            return BadRequest(new { message = "Mật khẩu hiện tại không đúng." });
        }

        customer.Password = PasswordHashing.HashPassword(request.NewPassword);
        customer.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Customer password changed. customerId={CustomerId}", customer.CustomerID);

        _auditLogger.Add(
            actionType: "customer.password.change",
            entityType: "Customer",
            entityId: customer.CustomerID.ToString(),
            customerId: customer.CustomerID,
            actorType: "Customer",
            actorId: customer.CustomerID,
            actorName: customer.Name,
            notes: "Password changed successfully");

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Đổi mật khẩu thành công!" });
    }

    [HttpPost("password/forgot")]
    [EnableRateLimiting("identity-auth")]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmailOrPhone))
        {
            return BadRequest(new { message = "Vui lòng nhập email." });
        }

        var key = request.UsernameOrEmailOrPhone.Trim();
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => (c.IsActive ?? false) == true
                     && (c.Username == key || c.Email == key || c.PhoneNumber == key),
                cancellationToken);

        // Do not reveal whether customer exists.
        if (customer is null)
        {
            _logger.LogInformation("Forgot-password requested for non-existing key={Key}", key);
            return Ok(new ForgotPasswordResponse(
                Message: "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu.",
                ResetToken: null,
                ExpiresAt: null));
        }

        var token = GenerateResetToken();
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(30);

        _db.PasswordResetTokens.Add(new PasswordResetTokens
        {
            CustomerID = customer.CustomerID,
            Token = token,
            ExpiryDate = expiresAt,
            IsUsed = false,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Forgot-password token issued. customerId={CustomerId}", customer.CustomerID);

        _auditLogger.Add(
            actionType: "customer.password.forgot",
            entityType: "Customer",
            entityId: customer.CustomerID.ToString(),
            customerId: customer.CustomerID,
            actorType: "Customer",
            actorId: customer.CustomerID,
            actorName: customer.Name,
            notes: $"Password reset requested for: {key}");

        await _db.SaveChangesAsync(cancellationToken);

        var smtpEnabled = _passwordResetEmailSender.IsEnabled;
        var emailSent = false;
        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            var publicBaseUrl = (_configuration["PasswordReset:PublicBaseUrl"] ?? "http://localhost:5100").TrimEnd('/');
            var resetLink = $"{publicBaseUrl}/Customer/ResetPassword?token={Uri.EscapeDataString(token)}";
            emailSent = await _passwordResetEmailSender.TrySendAsync(
                customer.Email,
                customer.Name,
                token,
                resetLink,
                cancellationToken);
        }

        var message = emailSent
            ? "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu."
            : (smtpEnabled
                ? "Có lỗi khi gửi email. Vui lòng thử lại sau."
                : "Có lỗi khi gửi email. Vui lòng thử lại sau.");

        var exposeTokenForDevelopment = _environment.IsDevelopment();

        return Ok(new ForgotPasswordResponse(
            Message: message,
            ResetToken: exposeTokenForDevelopment ? token : null,
            ExpiresAt: exposeTokenForDevelopment ? expiresAt : null));
    }

    [HttpPost("password/reset")]
    [EnableRateLimiting("identity-auth")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { message = "Token không hợp lệ." });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "Vui lòng điền đầy đủ thông tin." });
        }

        if (request.NewPassword.Trim().Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });
        }

        var now = DateTime.UtcNow;

        var token = await _db.PasswordResetTokens
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.Token == request.Token.Trim(), cancellationToken);

        if (token is null)
        {
            return BadRequest(new { message = "Link không hợp lệ hoặc đã hết hạn." });
        }

        if (token.IsUsed)
        {
            return BadRequest(new { message = "Link không hợp lệ hoặc đã hết hạn." });
        }

        if (token.ExpiryDate <= now)
        {
            return BadRequest(new { message = "Link không hợp lệ hoặc đã hết hạn." });
        }

        if ((token.Customer.IsActive ?? false) != true)
        {
            return BadRequest(new { message = "Không tìm thấy khách hàng." });
        }

        token.Customer.Password = PasswordHashing.HashPassword(request.NewPassword);
        token.Customer.UpdatedAt = DateTime.Now;
        token.IsUsed = true;

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Customer password reset. customerId={CustomerId}", token.CustomerID);

        _auditLogger.Add(
            actionType: "customer.password.reset",
            entityType: "Customer",
            entityId: token.CustomerID.ToString(),
            customerId: token.CustomerID,
            actorType: "Customer",
            actorId: token.CustomerID,
            actorName: token.Customer.Name,
            notes: "Password reset completed successfully");

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập với mật khẩu mới." });
    }

    [HttpGet("password/reset/validate")]
    public async Task<IActionResult> ValidateResetPasswordToken([FromQuery] string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { message = "Link không hợp lệ.", code = "missing_token" });
        }

        var tokenData = await _db.PasswordResetTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Token == token.Trim(), cancellationToken);

        if (tokenData is null)
        {
            return BadRequest(new { message = "Link đặt lại mật khẩu không hợp lệ.", code = "invalid_token" });
        }

        if (tokenData.IsUsed)
        {
            return BadRequest(new { message = "Link này đã được sử dụng.", code = "used_token" });
        }

        if (tokenData.ExpiryDate <= DateTime.UtcNow)
        {
            return BadRequest(new { message = "Link đã hết hạn. Vui lòng yêu cầu link mới.", code = "expired_token" });
        }

        return Ok(new { valid = true });
    }

    [HttpPost("staff/login")]
    [EnableRateLimiting("identity-auth")]
    public async Task<ActionResult<StaffLoginResponse>> StaffLogin([FromBody] StaffLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Vui lòng nhập tên đăng nhập và mật khẩu." });
        }

        var username = request.Username.Trim();
        var employee = await _db.Employees
            .Include(e => e.Role)
            .FirstOrDefaultAsync(
                e =>
                    (e.IsActive ?? false) == true
                    && (e.Username == username || e.Email == username || e.Phone == username),
                cancellationToken);

        if (employee is null || !PasswordHashing.Verify(employee.Password, request.Password, out var needsUpgrade))
        {
            _logger.LogWarning("Staff login failed for key={Key}", username);
            return Unauthorized(new { message = "Tài khoản hoặc mật khẩu sai." });
        }

        if (needsUpgrade)
        {
            employee.Password = PasswordHashing.HashPassword(request.Password);
        }

        employee.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Staff login succeeded. employeeId={EmployeeId}", employee.EmployeeID);

        _auditLogger.Add(
            actionType: "staff.login",
            entityType: "Employee",
            entityId: employee.EmployeeID.ToString(),
            employeeId: employee.EmployeeID,
            actorType: "Employee",
            actorId: employee.EmployeeID,
            actorCode: employee.Username,
            actorName: employee.Name,
            actorRoleCode: employee.Role.RoleCode,
            notes: $"Staff login successful for username: {username}");

        await _db.SaveChangesAsync(cancellationToken);

        var branch = await _catalogApi.GetBranchAsync(employee.BranchID, cancellationToken);

        return Ok(new StaffLoginResponse(
            EmployeeId: employee.EmployeeID,
            Username: employee.Username,
            Name: employee.Name,
            Phone: employee.Phone,
            Email: employee.Email,
            RoleId: employee.RoleID,
            RoleCode: employee.Role.RoleCode,
            RoleName: employee.Role.RoleName,
            BranchId: employee.BranchID,
            BranchName: ResolveBranchName(employee.BranchID, branch)));
    }

    [HttpPost("staff/password/change")]
    [EnableRateLimiting("identity-auth")]
    public async Task<IActionResult> StaffChangePassword([FromBody] StaffChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (request.EmployeeId <= 0)
        {
            return BadRequest(new { message = "Thiếu mã nhân viên." });
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "Vui lòng nhập đầy đủ mật khẩu hiện tại và mật khẩu mới." });
        }

        if (request.NewPassword.Trim().Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự." });
        }

        var employee = await _db.Employees.FirstOrDefaultAsync(
            e => e.EmployeeID == request.EmployeeId && (e.IsActive ?? false) == true,
            cancellationToken);

        if (employee is null)
        {
            return NotFound(new { message = "Không tìm thấy nhân viên." });
        }

        if (!PasswordHashing.Verify(employee.Password, request.CurrentPassword, out _))
        {
            return BadRequest(new { message = "Mật khẩu hiện tại không đúng." });
        }

        employee.Password = PasswordHashing.HashPassword(request.NewPassword);
        employee.UpdatedAt = DateTime.Now;
        _logger.LogInformation("Staff password changed. employeeId={EmployeeId}", employee.EmployeeID);

        _auditLogger.Add(
            actionType: "staff.password.change",
            entityType: "Employee",
            entityId: employee.EmployeeID.ToString(),
            employeeId: employee.EmployeeID,
            actorType: "Employee",
            actorId: employee.EmployeeID,
            actorCode: employee.Username,
            actorName: employee.Name,
            notes: "Staff password changed successfully");

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Đổi mật khẩu thành công." });
    }

    [HttpPost("staff/password/forgot")]
    [EnableRateLimiting("identity-auth")]
    public async Task<ActionResult<ForgotPasswordResponse>> StaffForgotPassword(
        [FromBody] StaffForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EmailOrUsername))
        {
            return BadRequest(new { message = "Vui lòng nhập email hoặc tên đăng nhập." });
        }

        CleanupExpiredStaffResetTokens();

        var key = request.EmailOrUsername.Trim();
        var employee = await _db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => (e.IsActive ?? false) == true
                     && (e.Username == key || e.Email == key || e.Phone == key),
                cancellationToken);

        // Do not reveal whether employee exists.
        if (employee is null)
        {
            _logger.LogInformation("Staff forgot-password requested for non-existing key={Key}", key);
            return Ok(new ForgotPasswordResponse(
                Message: "Nếu tài khoản tồn tại, hệ thống sẽ gửi hướng dẫn đặt lại mật khẩu.",
                ResetToken: null,
                ExpiresAt: null));
        }

        var token = GenerateResetToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(30);
        StaffPasswordResetTokens[token] = new StaffPasswordResetState
        {
            EmployeeId = employee.EmployeeID,
            ExpiryUtc = expiresAt,
            IsUsed = false
        };

        _logger.LogInformation("Staff forgot-password token issued. employeeId={EmployeeId}", employee.EmployeeID);

        _auditLogger.Add(
            actionType: "staff.password.forgot",
            entityType: "Employee",
            entityId: employee.EmployeeID.ToString(),
            employeeId: employee.EmployeeID,
            actorType: "Employee",
            actorId: employee.EmployeeID,
            actorCode: employee.Username,
            actorName: employee.Name,
            notes: $"Staff password reset requested for: {key}");

        await _db.SaveChangesAsync(cancellationToken);

        var smtpEnabled = _passwordResetEmailSender.IsEnabled;
        var emailSent = false;
        if (!string.IsNullOrWhiteSpace(employee.Email))
        {
            emailSent = await _passwordResetEmailSender.TrySendAsync(
                employee.Email,
                employee.Name,
                token,
                null,
                cancellationToken);
        }

        var message = emailSent
            ? "Nếu tài khoản tồn tại, hệ thống đã gửi email đặt lại mật khẩu."
            : (smtpEnabled
                ? "Không thể gửi email lúc này. Vui lòng thử lại sau ít phút."
                : "Hệ thống chưa cấu hình gửi email. Vui lòng liên hệ quản trị viên.");

        var exposeTokenForDevelopment = _environment.IsDevelopment();

        return Ok(new ForgotPasswordResponse(
            Message: message,
            ResetToken: exposeTokenForDevelopment ? token : null,
            ExpiresAt: exposeTokenForDevelopment ? expiresAt : null));
    }

    [HttpPost("staff/password/reset")]
    [EnableRateLimiting("identity-auth")]
    public async Task<IActionResult> StaffResetPassword(
        [FromBody] StaffResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { message = "Link không hợp lệ." });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "Vui lòng nhập mật khẩu mới." });
        }

        if (request.NewPassword.Trim().Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự." });
        }

        CleanupExpiredStaffResetTokens();

        var token = request.Token.Trim();
        if (!StaffPasswordResetTokens.TryGetValue(token, out var state))
        {
            return BadRequest(new { message = "Link đặt lại mật khẩu không hợp lệ." });
        }

        if (state.IsUsed)
        {
            return BadRequest(new { message = "Link này đã được sử dụng." });
        }

        if (state.ExpiryUtc <= DateTime.UtcNow)
        {
            StaffPasswordResetTokens.TryRemove(token, out _);
            return BadRequest(new { message = "Link đã hết hạn. Vui lòng yêu cầu link mới." });
        }

        var employee = await _db.Employees.FirstOrDefaultAsync(
            e => e.EmployeeID == state.EmployeeId && (e.IsActive ?? false) == true,
            cancellationToken);

        if (employee is null)
        {
            return BadRequest(new { message = "Nhân viên không còn hoạt động hoặc không tồn tại." });
        }

        employee.Password = PasswordHashing.HashPassword(request.NewPassword);
        employee.UpdatedAt = DateTime.Now;
        state.IsUsed = true;

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Staff password reset. employeeId={EmployeeId}", employee.EmployeeID);

        _auditLogger.Add(
            actionType: "staff.password.reset",
            entityType: "Employee",
            entityId: employee.EmployeeID.ToString(),
            employeeId: employee.EmployeeID,
            actorType: "Employee",
            actorId: employee.EmployeeID,
            actorCode: employee.Username,
            actorName: employee.Name,
            notes: "Staff password reset completed successfully");

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập với mật khẩu mới." });
    }

    [HttpGet("staff/password/reset/validate")]
    [EnableRateLimiting("identity-auth")]
    public IActionResult ValidateStaffResetPasswordToken([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { message = "Link không hợp lệ." });
        }

        CleanupExpiredStaffResetTokens();

        var normalizedToken = token.Trim();
        if (!StaffPasswordResetTokens.TryGetValue(normalizedToken, out var state))
        {
            return BadRequest(new { message = "Link đặt lại mật khẩu không hợp lệ." });
        }

        if (state.IsUsed)
        {
            return BadRequest(new { message = "Link này đã được sử dụng." });
        }

        if (state.ExpiryUtc <= DateTime.UtcNow)
        {
            StaffPasswordResetTokens.TryRemove(normalizedToken, out _);
            return BadRequest(new { message = "Link đã hết hạn. Vui lòng yêu cầu link mới." });
        }

        return Ok(new { valid = true });
    }

    [HttpPut("staff/{employeeId:int}")]
    public async Task<ActionResult<StaffProfileResponse>> UpdateStaffProfile(
        int employeeId,
        [FromBody] UpdateStaffProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
        {
            return BadRequest(new { message = "Thiếu mã nhân viên." });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Phone))
        {
            return BadRequest(new { message = "Vui lòng nhập đầy đủ họ tên và số điện thoại." });
        }

        var employee = await _db.Employees
            .Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.EmployeeID == employeeId && (e.IsActive ?? false) == true, cancellationToken);

        if (employee is null)
        {
            return NotFound(new { message = "Không tìm thấy nhân viên." });
        }

        employee.Name = request.Name.Trim();
        employee.Phone = request.Phone.Trim();
        employee.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        employee.UpdatedAt = DateTime.Now;
        _logger.LogInformation("Staff profile updated. employeeId={EmployeeId}", employee.EmployeeID);

        _auditLogger.Add(
            actionType: "staff.profile.update",
            entityType: "Employee",
            entityId: employee.EmployeeID.ToString(),
            employeeId: employee.EmployeeID,
            actorType: "Employee",
            actorId: employee.EmployeeID,
            actorCode: employee.Username,
            actorName: employee.Name,
            actorRoleCode: employee.Role.RoleCode,
            notes: "Staff profile updated");

        await _db.SaveChangesAsync(cancellationToken);

        var branch = await _catalogApi.GetBranchAsync(employee.BranchID, cancellationToken);

        return Ok(new StaffProfileResponse(
            employee.EmployeeID,
            employee.Username,
            employee.Name,
            employee.Phone,
            employee.Email,
            employee.Role.RoleCode,
            employee.Role.RoleName,
            employee.BranchID,
            ResolveBranchName(employee.BranchID, branch)));
    }

    [HttpGet("admin/stats")]
    public async Task<ActionResult<AdminStatsResponse>> GetAdminStats([FromQuery] int? branchId, CancellationToken cancellationToken)
    {
        var employeeQuery = _db.Employees.AsNoTracking().AsQueryable();
        if (branchId is > 0)
        {
            employeeQuery = employeeQuery.Where(e => e.BranchID == branchId.Value);
        }

        var totalEmployees = await employeeQuery.CountAsync(cancellationToken);
        var activeEmployees = await employeeQuery.CountAsync(e => (e.IsActive ?? false) == true, cancellationToken);

        var branchCount = branchId is > 0
            ? ((await _catalogApi.GetBranchAsync(branchId.Value, cancellationToken)) is null ? 0 : 1)
            : await _catalogApi.GetActiveBranchCountAsync(cancellationToken);

        return Ok(new AdminStatsResponse(
            TotalEmployees: totalEmployees,
            ActiveEmployees: activeEmployees,
            BranchCount: branchCount));
    }

    [HttpGet("admin/roles")]
    public async Task<ActionResult<IReadOnlyList<AdminRoleResponse>>> GetAdminRoles(CancellationToken cancellationToken = default)
    {
        var roles = await _db.EmployeeRoles
            .AsNoTracking()
            .OrderBy(r => r.RoleName)
            .Select(r => new AdminRoleResponse(
                r.RoleID,
                r.RoleCode,
                r.RoleName))
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }

    [HttpGet("admin/employees")]
    public async Task<ActionResult<AdminEmployeePagedResponse>> GetAdminEmployees(
        [FromQuery] string? search,
        [FromQuery] int? branchId,
        [FromQuery] int? roleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Employees
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(e => (e.IsActive ?? false) == true);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim();
            query = query.Where(e =>
                e.Name.Contains(key) ||
                e.Username.Contains(key) ||
                (e.Phone != null && e.Phone.Contains(key)) ||
                (e.Email != null && e.Email.Contains(key)));
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(e => e.BranchID == branchId.Value);
        }

        if (roleId.HasValue && roleId.Value > 0)
        {
            query = query.Where(e => e.RoleID == roleId.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var employees = await query
            .OrderByDescending(e => e.CreatedAt ?? e.UpdatedAt ?? DateTime.MinValue)
            .ThenBy(e => e.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.EmployeeID,
                e.Name,
                e.Username,
                e.Phone,
                e.Email,
                e.Salary,
                e.Shift,
                IsActive = e.IsActive ?? false,
                e.BranchID,
                e.RoleID,
                RoleCode = e.Role.RoleCode,
                RoleName = e.Role.RoleName,
                e.CreatedAt,
                e.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var branchLookup = await GetBranchLookupAsync(employees.Select(e => e.BranchID), cancellationToken);
        var items = employees
            .Select(e => new AdminEmployeeResponse(
                e.EmployeeID,
                e.Name,
                e.Username,
                e.Phone,
                e.Email,
                e.Salary,
                e.Shift,
                e.IsActive,
                e.BranchID,
                ResolveBranchName(e.BranchID, branchLookup),
                e.RoleID,
                e.RoleCode,
                e.RoleName,
                e.CreatedAt,
                e.UpdatedAt))
            .ToList();

        return Ok(new AdminEmployeePagedResponse(
            page,
            pageSize,
            totalItems,
            totalPages,
            items));
    }

    [HttpGet("admin/employees/{employeeId:int}")]
    public async Task<ActionResult<AdminEmployeeResponse>> GetAdminEmployeeById(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _db.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeID == employeeId)
            .Select(e => new
            {
                e.EmployeeID,
                e.Name,
                e.Username,
                e.Phone,
                e.Email,
                e.Salary,
                e.Shift,
                IsActive = e.IsActive ?? false,
                e.BranchID,
                e.RoleID,
                RoleCode = e.Role.RoleCode,
                RoleName = e.Role.RoleName,
                e.CreatedAt,
                e.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return NotFound(new { message = "Không tìm thấy nhân viên." });
        }

        var branch = await _catalogApi.GetBranchAsync(employee.BranchID, cancellationToken);

        return Ok(new AdminEmployeeResponse(
            employee.EmployeeID,
            employee.Name,
            employee.Username,
            employee.Phone,
            employee.Email,
            employee.Salary,
            employee.Shift,
            employee.IsActive,
            employee.BranchID,
            ResolveBranchName(employee.BranchID, branch),
            employee.RoleID,
            employee.RoleCode,
            employee.RoleName,
            employee.CreatedAt,
            employee.UpdatedAt));
    }

    [HttpPost("admin/employees")]
    public async Task<ActionResult<AdminEmployeeResponse>> CreateAdminEmployee(
        [FromBody] CreateAdminEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAdminEmployeeAsync(
            request.Username,
            request.Password,
            request.BranchId,
            request.RoleId,
            currentId: null,
            cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var entity = new Employees
        {
            Name = request.Name.Trim(),
            Username = request.Username.Trim(),
            Password = PasswordHashing.HashPassword(request.Password.Trim()),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Salary = request.Salary,
            Shift = string.IsNullOrWhiteSpace(request.Shift) ? null : request.Shift.Trim(),
            IsActive = request.IsActive,
            BranchID = request.BranchId,
            RoleID = request.RoleId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };

        _db.Employees.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var response = await _db.Employees
            .AsNoTracking()
            .Include(e => e.Role)
            .Where(e => e.EmployeeID == entity.EmployeeID)
            .Select(e => new
            {
                e.EmployeeID,
                e.Name,
                e.Username,
                e.Phone,
                e.Email,
                e.Salary,
                e.Shift,
                IsActive = e.IsActive ?? false,
                e.BranchID,
                e.RoleID,
                RoleCode = e.Role.RoleCode,
                RoleName = e.Role.RoleName,
                e.CreatedAt,
                e.UpdatedAt
            })
            .FirstAsync(cancellationToken);

        var branch = await _catalogApi.GetBranchAsync(response.BranchID, cancellationToken);

        return Ok(new AdminEmployeeResponse(
            response.EmployeeID,
            response.Name,
            response.Username,
            response.Phone,
            response.Email,
            response.Salary,
            response.Shift,
            response.IsActive,
            response.BranchID,
            ResolveBranchName(response.BranchID, branch),
            response.RoleID,
            response.RoleCode,
            response.RoleName,
            response.CreatedAt,
            response.UpdatedAt));
    }

    [HttpPut("admin/employees/{employeeId:int}")]
    public async Task<ActionResult<AdminEmployeeResponse>> UpdateAdminEmployee(
        int employeeId,
        [FromBody] UpdateAdminEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Employees
            .Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.EmployeeID == employeeId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy nhân viên." });
        }

        var validation = await ValidateAdminEmployeeAsync(
            request.Username,
            request.Password,
            request.BranchId,
            request.RoleId,
            currentId: employeeId,
            cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        entity.Name = request.Name.Trim();
        entity.Username = request.Username.Trim();
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            entity.Password = PasswordHashing.HashPassword(request.Password.Trim());
        }

        entity.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        entity.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        entity.Salary = request.Salary;
        entity.Shift = string.IsNullOrWhiteSpace(request.Shift) ? null : request.Shift.Trim();
        entity.IsActive = request.IsActive;
        entity.BranchID = request.BranchId;
        entity.RoleID = request.RoleId;
        entity.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync(cancellationToken);

        await _db.Entry(entity).Reference(e => e.Role).LoadAsync(cancellationToken);
        var branch = await _catalogApi.GetBranchAsync(entity.BranchID, cancellationToken);

        return Ok(new AdminEmployeeResponse(
            entity.EmployeeID,
            entity.Name,
            entity.Username,
            entity.Phone,
            entity.Email,
            entity.Salary,
            entity.Shift,
            entity.IsActive ?? false,
            entity.BranchID,
            ResolveBranchName(entity.BranchID, branch),
            entity.RoleID,
            entity.Role.RoleCode,
            entity.Role.RoleName,
            entity.CreatedAt,
            entity.UpdatedAt));
    }

    [HttpPost("admin/employees/{employeeId:int}/deactivate")]
    public async Task<IActionResult> DeactivateAdminEmployee(int employeeId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeID == employeeId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy nhân viên." });
        }

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã vô hiệu hóa nhân viên." });
    }

    [HttpGet("admin/employees/{employeeId:int}/history")]
    public async Task<ActionResult<AdminEmployeeHistoryResponse>> GetAdminEmployeeHistory(
        int employeeId,
        [FromQuery] int activityPage = 1,
        [FromQuery] int cookingPage = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
    {
        var employee = await _db.Employees
            .AsNoTracking()
            .Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.EmployeeID == employeeId, cancellationToken);
        if (employee is null)
        {
            return NotFound(new { message = "Không tìm thấy nhân viên." });
        }

        activityPage = Math.Max(1, activityPage);
        cookingPage = Math.Max(1, cookingPage);
        pageSize = Math.Clamp(pageSize, 1, 100);
        days = Math.Clamp(days, 1, 365);
        
        var chefActivityLogs = new AdminChefActivityLogsPage(0, 0, 0, 0, Array.Empty<AdminChefActivityLogItem>());
        var chefItemCompletions = new AdminChefActivityLogsPage(0, 0, 0, 0, Array.Empty<AdminChefActivityLogItem>());
        var staffActivityLogs = new AdminStaffActivityLogsPage(0, 0, 0, 0, Array.Empty<AdminStaffActivityLogItem>());
        var chefCookingHistory = new AdminChefCookingHistoryPage(0, 0, 0, 0, Array.Empty<AdminChefCookingHistoryItem>());
        var cashierHistory = new AdminCashierHistoryPage(0, 0, 0, 0, Array.Empty<AdminCashierHistoryItem>());
        var roleCode = employee.Role.RoleCode;
        var branch = await _catalogApi.GetBranchAsync(employee.BranchID, cancellationToken);

        var staffAuditQuery = _db.BusinessAuditLogs
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.CreatedAtUtc);
        var staffAuditTotalItems = await staffAuditQuery.CountAsync(cancellationToken);
        var staffAuditTotalPages = (int)Math.Ceiling(staffAuditTotalItems / (double)pageSize);
        var staffAuditItems = await staffAuditQuery
            .Skip((activityPage - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminStaffActivityLogItem(
                x.BusinessAuditLogId,
                x.CreatedAtUtc,
                x.ActionType,
                x.EntityType,
                x.EntityId,
                x.ActorName,
                x.ActorRoleCode,
                x.IpAddress,
                x.UserAgent,
                x.Notes,
                x.BeforeState,
                x.AfterState))
            .ToListAsync(cancellationToken);
        staffActivityLogs = new AdminStaffActivityLogsPage(activityPage, pageSize, staffAuditTotalItems, staffAuditTotalPages, staffAuditItems);

        if (roleCode is "CHEF" or "KITCHEN_STAFF")
        {
            // Get activity logs (pause/resume dishes) from Catalog service
            var logsResponse = await _catalogApi.GetChefActivityLogsAsync(employee.BranchID, employeeId, activityPage, pageSize, days, cancellationToken);
            if (logsResponse is not null)
            {
                chefActivityLogs = new AdminChefActivityLogsPage(
                    logsResponse.Page,
                    logsResponse.PageSize,
                    logsResponse.TotalItems,
                    logsResponse.TotalPages,
                    logsResponse.Logs.Select(log => new AdminChefActivityLogItem(
                        log.AuditId,
                        log.TimestampUtc,
                        log.ActionType,
                        log.DishId,
                        log.ActorName,
                        log.AfterState)).ToList());
            }

            // Get item completion logs (when chef marks dishes as READY) from Orders service
            var completionsResponse = await _ordersApi.GetChefItemCompletionsAsync(employee.BranchID, employeeId, activityPage, pageSize, days, cancellationToken);
            if (completionsResponse is not null)
            {
                chefItemCompletions = new AdminChefActivityLogsPage(
                    completionsResponse.Page,
                    completionsResponse.PageSize,
                    completionsResponse.TotalItems,
                    completionsResponse.TotalPages,
                    completionsResponse.Logs.Select(item => new AdminChefActivityLogItem(
                        item.AuditId,
                        item.TimestampUtc,
                        "DISH_COMPLETED", // Action type for display
                        item.DishId,
                        null, // Actor name not needed (it's the employee being viewed)
                        $"{{\"dishName\":\"{item.DishName}\",\"quantity\":{item.Quantity},\"orderCode\":\"{item.OrderCode}\",\"tableName\":\"{item.TableName}\"}}")).ToList());
            }

            // Get cooking history (orders completed)
            var ordersResponse = await _ordersApi.GetChefHistoryAsync(employee.BranchID, employeeId, cookingPage, pageSize, days, cancellationToken);
            if (ordersResponse is not null)
            {
                chefCookingHistory = new AdminChefCookingHistoryPage(
                    ordersResponse.Page,
                    ordersResponse.PageSize,
                    ordersResponse.TotalItems,
                    ordersResponse.TotalPages,
                    ordersResponse.Items.Select(o => new AdminChefCookingHistoryItem(
                        o.OrderId,
                        o.OrderCode,
                        o.OrderTime,
                        o.CompletedTime,
                        o.TableName,
                        string.IsNullOrWhiteSpace(o.BranchName) ? ResolveBranchName(employee.BranchID, branch) : o.BranchName,
                        o.StatusCode,
                        o.StatusName,
                        o.DishesSummary,
                        o.Notes)).ToList());
            }
        }

        if (roleCode == "CASHIER")
        {
            var billsResponse = await _billingApi.GetCashierHistoryAsync(employeeId, activityPage, pageSize, days, cancellationToken);
            if (billsResponse is not null)
            {
                cashierHistory = new AdminCashierHistoryPage(
                    billsResponse.Page,
                    billsResponse.PageSize,
                    billsResponse.TotalItems,
                    billsResponse.TotalPages,
                    billsResponse.Items.Select(b => new AdminCashierHistoryItem(
                        b.BillId,
                        b.BillCode,
                        b.BillTime,
                        b.OrderCode,
                        b.TableName,
                        b.CustomerName,
                        b.Subtotal,
                        b.Discount,
                        b.PointsDiscount,
                        b.PointsUsed,
                        b.TotalAmount,
                        b.PaymentMethod,
                        b.PaymentAmount,
                        b.ChangeAmount)).ToList());
            }
        }

        return Ok(new AdminEmployeeHistoryResponse(
            new AdminEmployeeHistoryMeta(
                employee.EmployeeID,
                employee.Name,
                employee.Role.RoleCode,
                employee.Role.RoleName,
                employee.BranchID,
                ResolveBranchName(employee.BranchID, branch)),
            chefActivityLogs,
            chefItemCompletions,
            staffActivityLogs,
            chefCookingHistory,
            cashierHistory));
    }

    [HttpGet("admin/customers/{customerId:int}/activity-logs")]
    public async Task<ActionResult<object>> GetCustomerActivityLogs(
        int customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerID == customerId, cancellationToken);

        if (customer is null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.BusinessAuditLogs
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAtUtc);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                auditId = x.BusinessAuditLogId,
                timestampUtc = x.CreatedAtUtc,
                actionType = x.ActionType,
                entityType = x.EntityType,
                entityId = x.EntityId,
                actorType = x.ActorType,
                actorId = x.ActorId,
                actorName = x.ActorName,
                ipAddress = x.IpAddress,
                userAgent = x.UserAgent,
                notes = x.Notes,
                beforeState = x.BeforeState,
                afterState = x.AfterState
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            customer = new
            {
                customerId = customer.CustomerID,
                name = customer.Name,
                username = customer.Username,
                email = customer.Email,
                phoneNumber = customer.PhoneNumber
            },
            logs = new
            {
                page,
                pageSize,
                totalItems,
                totalPages,
                items = logs
            }
        });
    }

    private async Task<ActionResult?> ValidateAdminEmployeeAsync(
        string? username,
        string? password,
        int branchId,
        int roleId,
        int? currentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new { message = "Vui lòng nhập tên đăng nhập." });
        }

        if (!currentId.HasValue && string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new { message = "Vui lòng nhập mật khẩu." });
        }

        if (!string.IsNullOrWhiteSpace(password) && password.Trim().Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });
        }

        if (branchId <= 0)
        {
            return BadRequest(new { message = "Vui lòng chọn chi nhánh." });
        }

        var branch = await _catalogApi.GetBranchAsync(branchId, cancellationToken);
        if (branch is null || !branch.IsActive)
        {
            return BadRequest(new { message = "Chi nhánh không hợp lệ." });
        }

        if (roleId <= 0)
        {
            return BadRequest(new { message = "Vui lòng chọn vai trò." });
        }

        var roleExists = await _db.EmployeeRoles.AnyAsync(r => r.RoleID == roleId, cancellationToken);
        if (!roleExists)
        {
            return BadRequest(new { message = "Vai trò không hợp lệ." });
        }

        var normalized = username.Trim();
        var duplicated = await _db.Employees.AnyAsync(
            e => e.EmployeeID != currentId && e.Username == normalized,
            cancellationToken);
        if (duplicated)
        {
            return Conflict(new { message = "Tên đăng nhập đã tồn tại." });
        }

        return null;
    }

    [HttpGet("admin/customers")]
    public async Task<ActionResult<AdminCustomerPagedResponse>> GetAdminCustomers(
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(c => (c.IsActive ?? false) == true);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim();
            query = query.Where(c =>
                c.Name.Contains(key) ||
                c.Username.Contains(key) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(key)) ||
                (c.Email != null && c.Email.Contains(key)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new AdminCustomerResponse(
                c.CustomerID,
                c.Name,
                c.Username,
                c.PhoneNumber,
                c.Email,
                c.Address,
                c.Gender,
                c.DateOfBirth,
                c.LoyaltyPoints ?? 0,
                c.IsActive ?? false,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new AdminCustomerPagedResponse(
            page,
            pageSize,
            totalItems,
            totalPages,
            items));
    }

    [HttpGet("admin/customers/{customerId:int}")]
    public async Task<ActionResult<AdminCustomerResponse>> GetAdminCustomerById(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .Where(c => c.CustomerID == customerId)
            .Select(c => new AdminCustomerResponse(
                c.CustomerID,
                c.Name,
                c.Username,
                c.PhoneNumber,
                c.Email,
                c.Address,
                c.Gender,
                c.DateOfBirth,
                c.LoyaltyPoints ?? 0,
                c.IsActive ?? false,
                c.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        return Ok(customer);
    }

    [HttpPost("admin/customers")]
    public async Task<ActionResult<AdminCustomerResponse>> CreateAdminCustomer(
        [FromBody] CreateAdminCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAdminCustomerAsync(
            request.Username,
            request.Password,
            request.Name,
            request.PhoneNumber,
            request.Email,
            currentId: null,
            cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var entity = new Customers
        {
            Name = request.Name.Trim(),
            Username = request.Username.Trim(),
            Password = PasswordHashing.HashPassword(request.Password.Trim()),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? string.Empty : request.PhoneNumber.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim(),
            DateOfBirth = request.DateOfBirth,
            LoyaltyPoints = Math.Max(0, request.LoyaltyPoints),
            CreditPoints = 0,
            IsActive = request.IsActive,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };

        _db.Customers.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminCustomerResponse(
            entity.CustomerID,
            entity.Name,
            entity.Username,
            entity.PhoneNumber,
            entity.Email,
            entity.Address,
            entity.Gender,
            entity.DateOfBirth,
            entity.LoyaltyPoints ?? 0,
            entity.IsActive ?? false,
            entity.CreatedAt));
    }

    [HttpPut("admin/customers/{customerId:int}")]
    public async Task<ActionResult<AdminCustomerResponse>> UpdateAdminCustomer(
        int customerId,
        [FromBody] UpdateAdminCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerID == customerId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        var validation = await ValidateAdminCustomerAsync(
            request.Username,
            request.Password,
            request.Name,
            request.PhoneNumber,
            request.Email,
            currentId: customerId,
            cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        entity.Name = request.Name.Trim();
        entity.Username = request.Username.Trim();
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            entity.Password = PasswordHashing.HashPassword(request.Password.Trim());
        }

        entity.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? string.Empty : request.PhoneNumber.Trim();
        entity.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        entity.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        entity.Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim();
        entity.DateOfBirth = request.DateOfBirth;
        entity.LoyaltyPoints = Math.Max(0, request.LoyaltyPoints);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminCustomerResponse(
            entity.CustomerID,
            entity.Name,
            entity.Username,
            entity.PhoneNumber,
            entity.Email,
            entity.Address,
            entity.Gender,
            entity.DateOfBirth,
            entity.LoyaltyPoints ?? 0,
            entity.IsActive ?? false,
            entity.CreatedAt));
    }

    [HttpPost("admin/customers/{customerId:int}/deactivate")]
    public async Task<IActionResult> DeactivateAdminCustomer(int customerId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerID == customerId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã vô hiệu hóa khách hàng." });
    }

    private async Task<ActionResult?> ValidateAdminCustomerAsync(
        string? username,
        string? password,
        string? name,
        string? phoneNumber,
        string? email,
        int? currentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new { message = "Vui lòng nhập tên đăng nhập." });
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Vui lòng nhập họ tên khách hàng." });
        }

        if (!currentId.HasValue && string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new { message = "Vui lòng nhập mật khẩu." });
        }

        if (!string.IsNullOrWhiteSpace(password) && password.Trim().Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });
        }

        var normalizedUsername = username.Trim();
        var duplicatedUsername = await _db.Customers.AnyAsync(
            c => c.CustomerID != currentId && c.Username == normalizedUsername,
            cancellationToken);
        if (duplicatedUsername)
        {
            return Conflict(new { message = "Tên đăng nhập đã tồn tại." });
        }

        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            var duplicatedEmail = await _db.Customers.AnyAsync(
                c => c.CustomerID != currentId && c.Email == normalizedEmail,
                cancellationToken);
            if (duplicatedEmail)
            {
                return Conflict(new { message = "Email đã tồn tại." });
            }
        }

        return null;
    }

    private Task<IReadOnlyDictionary<int, CatalogApiClient.BranchSnapshotResponse>> GetBranchLookupAsync(
        IEnumerable<int> branchIds,
        CancellationToken cancellationToken)
        => GetBranchLookupCoreAsync(branchIds, cancellationToken);

    private async Task<IReadOnlyDictionary<int, CatalogApiClient.BranchSnapshotResponse>> GetBranchLookupCoreAsync(
        IEnumerable<int> branchIds,
        CancellationToken cancellationToken)
    {
        var lookup = (await _catalogApi.GetBranchesAsync(branchIds, cancellationToken) ?? Array.Empty<CatalogApiClient.BranchSnapshotResponse>())
            .ToDictionary(x => x.BranchId);

        return lookup;
    }

    private static string ResolveBranchName(
        int branchId,
        CatalogApiClient.BranchSnapshotResponse? branch)
        => string.IsNullOrWhiteSpace(branch?.Name) ? $"Chi nhánh {branchId}" : branch.Name;

    private static string ResolveBranchName(
        int branchId,
        IReadOnlyDictionary<int, CatalogApiClient.BranchSnapshotResponse> branchLookup)
        => branchLookup.TryGetValue(branchId, out var branch)
            ? ResolveBranchName(branchId, branch)
            : $"Chi nhánh {branchId}";

    private async Task<string> GenerateGoogleUsernameAsync(string email, CancellationToken cancellationToken)
    {
        var local = email.Split('@')[0].Trim().ToLowerInvariant();
        var safe = new string(local.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "google";
        }

        var candidate = safe.Length > 40 ? safe[..40] : safe;
        if (!await _db.Customers.AnyAsync(c => c.Username == candidate, cancellationToken))
        {
            return candidate;
        }

        for (var i = 1; i <= 999; i++)
        {
            var suffix = $"_{i}";
            var prefixLength = Math.Min(50 - suffix.Length, safe.Length);
            candidate = safe[..prefixLength] + suffix;
            if (!await _db.Customers.AnyAsync(c => c.Username == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return $"google_{Guid.NewGuid():N}"[..50];
    }

    private sealed class StaffPasswordResetState
    {
        public int EmployeeId { get; init; }
        public DateTime ExpiryUtc { get; init; }
        public bool IsUsed { get; set; }
    }

    public sealed record LoginRequest(string Username, string Password);

    public sealed record GoogleLoginRequest(string IdToken);

    public sealed record LoginResponse(
        int CustomerId,
        string Username,
        string Name,
        string PhoneNumber,
        string? Email,
        int LoyaltyPoints);

    public sealed record RegisterRequest(
        string Name,
        string Username,
        string Password,
        string PhoneNumber,
        string? Email = null,
        string? Gender = null,
        DateOnly? DateOfBirth = null,
        string? Address = null);

    public sealed record RegisterResponse(int CustomerId);

    public sealed record UpdateCustomerProfileRequest(
        string Username,
        string Name,
        string PhoneNumber,
        string? Email,
        string? Gender,
        DateOnly? DateOfBirth,
        string? Address);

    public sealed record LoyaltySettlementRequest(int PointsUsed, decimal AmountPaid, string? SettlementKey = null);

    public sealed record ChangePasswordRequest(int CustomerId, string CurrentPassword, string NewPassword);

    public sealed record ForgotPasswordRequest(string UsernameOrEmailOrPhone);

    public sealed record ForgotPasswordResponse(string Message, string? ResetToken, DateTime? ExpiresAt);

    public sealed record ResetPasswordRequest(string Token, string NewPassword);

    public sealed record StaffLoginRequest(string Username, string Password);

    public sealed record StaffLoginResponse(
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

    public sealed record StaffChangePasswordRequest(int EmployeeId, string CurrentPassword, string NewPassword);
    public sealed record StaffForgotPasswordRequest(string EmailOrUsername);
    public sealed record StaffResetPasswordRequest(string Token, string NewPassword);

    public sealed record UpdateStaffProfileRequest(string Name, string Phone, string? Email = null);

    public sealed record StaffProfileResponse(
        int EmployeeId,
        string Username,
        string Name,
        string? Phone,
        string? Email,
        string RoleCode,
        string RoleName,
        int BranchId,
        string BranchName);

    public sealed record AdminStatsResponse(int TotalEmployees, int ActiveEmployees, int BranchCount);

    public sealed record AdminRoleResponse(int RoleId, string RoleCode, string RoleName);

    public sealed record AdminEmployeeResponse(
        int EmployeeId,
        string Name,
        string Username,
        string? Phone,
        string? Email,
        decimal? Salary,
        string? Shift,
        bool IsActive,
        int BranchId,
        string BranchName,
        int RoleId,
        string RoleCode,
        string RoleName,
        DateTime? CreatedAt,
        DateTime? UpdatedAt);

    public sealed record AdminEmployeePagedResponse(
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages,
        IReadOnlyList<AdminEmployeeResponse> Items);

    public sealed record CreateAdminEmployeeRequest(
        string Name,
        string Username,
        string Password,
        string? Phone,
        string? Email,
        decimal? Salary,
        string? Shift,
        bool IsActive,
        int BranchId,
        int RoleId);

    public sealed record UpdateAdminEmployeeRequest(
        string Name,
        string Username,
        string? Password,
        string? Phone,
        string? Email,
        decimal? Salary,
        string? Shift,
        bool IsActive,
        int BranchId,
        int RoleId);

    public sealed record AdminEmployeeHistoryMeta(
        int EmployeeId,
        string EmployeeName,
        string RoleCode,
        string RoleName,
        int BranchId,
        string BranchName);

    public sealed record AdminChefActivityLogItem(
        long AuditId,
        DateTime TimestampUtc,
        string ActionType,
        int? DishId,
        string? ActorName,
        string? AfterState);

    public sealed record AdminChefActivityLogsPage(
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages,
        IReadOnlyList<AdminChefActivityLogItem> Logs);

    public sealed record AdminStaffActivityLogItem(
        long AuditId,
        DateTime TimestampUtc,
        string ActionType,
        string EntityType,
        string EntityId,
        string? ActorName,
        string? ActorRoleCode,
        string? IpAddress,
        string? UserAgent,
        string? Notes,
        string? BeforeState,
        string? AfterState);

    public sealed record AdminStaffActivityLogsPage(
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages,
        IReadOnlyList<AdminStaffActivityLogItem> Logs);

    public sealed record AdminChefCookingHistoryItem(
        int OrderId,
        string? OrderCode,
        DateTime OrderTime,
        DateTime? CompletedTime,
        string? TableName,
        string? BranchName,
        string StatusCode,
        string StatusName,
        string DishesSummary,
        string? Notes);

    public sealed record AdminCashierHistoryItem(
        int BillId,
        string BillCode,
        DateTime BillTime,
        string? OrderCode,
        string? TableName,
        string? CustomerName,
        decimal Subtotal,
        decimal Discount,
        decimal PointsDiscount,
        int? PointsUsed,
        decimal TotalAmount,
        string PaymentMethod,
        decimal? PaymentAmount,
        decimal? ChangeAmount);

    public sealed record AdminCashierHistoryPage(
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages,
        IReadOnlyList<AdminCashierHistoryItem> Items);

    public sealed record AdminChefCookingHistoryPage(
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages,
        IReadOnlyList<AdminChefCookingHistoryItem> Items);

    public sealed record AdminEmployeeHistoryResponse(
        AdminEmployeeHistoryMeta Employee,
        AdminChefActivityLogsPage ChefActivityLogs,
        AdminChefActivityLogsPage ChefItemCompletions,
        AdminStaffActivityLogsPage StaffActivityLogs,
        AdminChefCookingHistoryPage ChefCookingHistory,
        AdminCashierHistoryPage CashierHistory);

    public sealed record AdminCustomerResponse(
        int CustomerId,
        string Name,
        string Username,
        string? PhoneNumber,
        string? Email,
        string? Address,
        string? Gender,
        DateOnly? DateOfBirth,
        int LoyaltyPoints,
        bool IsActive,
        DateTime? CreatedAt);

    public sealed record AdminCustomerPagedResponse(
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages,
        IReadOnlyList<AdminCustomerResponse> Items);

    public sealed record CreateAdminCustomerRequest(
        string Name,
        string Username,
        string Password,
        string? PhoneNumber,
        string? Email = null,
        string? Address = null,
        string? Gender = null,
        DateOnly? DateOfBirth = null,
        int LoyaltyPoints = 0,
        bool IsActive = true);

    public sealed record UpdateAdminCustomerRequest(
        string Name,
        string Username,
        string? Password,
        string? PhoneNumber,
        string? Email = null,
        string? Address = null,
        string? Gender = null,
        DateOnly? DateOfBirth = null,
        int LoyaltyPoints = 0,
         bool IsActive = true);
}
