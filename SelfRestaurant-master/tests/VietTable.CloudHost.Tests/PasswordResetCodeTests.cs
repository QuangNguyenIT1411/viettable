using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SelfRestaurant.Identity.Api.Controllers;
using SelfRestaurant.Identity.Api.Infrastructure;
using SelfRestaurant.Identity.Api.Persistence;
using SelfRestaurant.Identity.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>
/// Owner 08/11: "Quên mật khẩu" in the app. Production never returned the reset token, the app stopped there, and the
/// e-mailed token was a 64-character string nobody can type. Now: a 6-digit code, account + code + new password.
/// </summary>
public sealed class PasswordResetCodeTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IdentityDbContext Database() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static MobileAuthController Controller(IdentityDbContext db, string environment) => new(db, null!,
        new PasswordResetEmailSender(Options.Create(new SmtpOptions { Enabled = false }), NullLogger<PasswordResetEmailSender>.Instance),
        new ConfigurationBuilder().Build(), new Env(environment), NullLogger<MobileAuthController>.Instance);

    private static async Task<Customers> Customer(IdentityDbContext db, int id = 41)
    {
        var c = new Customers
        {
            CustomerID = id, Name = "Khách thử", PhoneNumber = "0900000041", Email = "khachthu41@example.com", Username = "khachthu41",
            IsActive = true, Password = "old-hash", AuthProvider = "Password",
        };
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    private static int Status(IActionResult r) => r is ObjectResult o ? o.StatusCode ?? 200 : 200;
    private static string Code(IActionResult r) => System.Text.Json.JsonSerializer.Serialize(((ObjectResult)r).Value);

    [Fact]
    public async Task Production_answer_never_contains_the_code_and_only_its_hash_is_stored()
    {
        await using var db = Database();
        await Customer(db);
        var result = (await Controller(db, "Production").ForgotPassword(new MobileAuthController.MobileForgotPasswordRequest("khachthu41@example.com"), CancellationToken.None)).Result;
        var body = (MobileAuthController.MobileForgotPasswordResponse)((OkObjectResult)result!).Value!;
        Assert.Null(body.ResetToken); // what the app got on Production: 200 + message, no token
        Assert.Contains("6 số", body.Message);
        var stored = await db.PasswordResetTokens.SingleAsync();
        Assert.StartsWith(PasswordResetCodes.Prefix, stored.Token);
        Assert.True(stored.ExpiryDate <= DateTime.UtcNow.AddMinutes(15).AddSeconds(5));
    }

    [Fact]
    public async Task Unknown_account_gets_the_same_answer_and_nothing_is_stored()
    {
        await using var db = Database();
        var result = (await Controller(db, "Production").ForgotPassword(new MobileAuthController.MobileForgotPasswordRequest("khongco@example.com"), CancellationToken.None)).Result;
        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(db.PasswordResetTokens);
    }

    [Fact]
    public async Task Account_code_and_new_password_reset_the_password_once()
    {
        await using var db = Database();
        var customer = await Customer(db);
        var forgot = (await Controller(db, "Development").ForgotPassword(new MobileAuthController.MobileForgotPasswordRequest("0900000041"), CancellationToken.None)).Result;
        var code = ((MobileAuthController.MobileForgotPasswordResponse)((OkObjectResult)forgot!).Value!).ResetToken!;
        Assert.Matches("^[0-9]{6}$", code);

        var ok = await Controller(db, "Production").ResetPassword(new MobileAuthController.MobileResetPasswordRequest(null, "matkhaumoi", "khachthu41", code), CancellationToken.None);
        Assert.Equal(200, Status(ok));
        var saved = (await db.Customers.SingleAsync()).Password;
        Assert.False(string.IsNullOrEmpty(saved));
        Assert.NotEqual("old-hash", saved);
        Assert.DoesNotContain("matkhaumoi", saved); // stored hashed
        var again = await Controller(db, "Production").ResetPassword(new MobileAuthController.MobileResetPasswordRequest(null, "matkhau3", "khachthu41", code), CancellationToken.None);
        Assert.Equal(400, Status(again)); // a code works once
    }

    [Fact]
    public async Task Five_wrong_codes_spend_the_code()
    {
        await using var db = Database();
        await Customer(db, 42);
        var forgot = (await Controller(db, "Development").ForgotPassword(new MobileAuthController.MobileForgotPasswordRequest("khachthu41"), CancellationToken.None)).Result;
        var code = ((MobileAuthController.MobileForgotPasswordResponse)((OkObjectResult)forgot!).Value!).ResetToken!;
        var wrong = code == "000000" ? "111111" : "000000";
        IActionResult last = null!;
        for (var i = 0; i < PasswordResetCodes.MaxAttempts; i++)
            last = await Controller(db, "Production").ResetPassword(new MobileAuthController.MobileResetPasswordRequest(null, "matkhaumoi", "khachthu41", wrong), CancellationToken.None);
        Assert.Contains("reset_code_locked", Code(last));
        var right = await Controller(db, "Production").ResetPassword(new MobileAuthController.MobileResetPasswordRequest(null, "matkhaumoi", "khachthu41", code), CancellationToken.None);
        Assert.Equal(400, Status(right)); // even the right code no longer works
    }

    [Fact]
    public void Code_hash_matches_only_the_same_customer_and_code()
    {
        var stored = PasswordResetCodes.Stored(7, "123456");
        Assert.True(PasswordResetCodes.Matches(stored, 7, "123 456"));
        Assert.False(PasswordResetCodes.Matches(stored, 8, "123456"));
        Assert.False(PasswordResetCodes.Matches(stored, 7, "123457"));
        Assert.False(PasswordResetCodes.Matches(stored, 7, "12345"));
    }

    [Fact]
    public async Task A_code_hash_is_never_accepted_as_a_link_token()
    {
        await using var db = Database();
        await Customer(db, 43);
        await Controller(db, "Production").ForgotPassword(new MobileAuthController.MobileForgotPasswordRequest("khachthu41"), CancellationToken.None);
        var stored = (await db.PasswordResetTokens.SingleAsync()).Token;
        var r = await Controller(db, "Production").ResetPassword(new MobileAuthController.MobileResetPasswordRequest(stored, "matkhaumoi"), CancellationToken.None);
        Assert.Equal(400, Status(r));
    }
}
