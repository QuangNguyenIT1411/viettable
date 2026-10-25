using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Controllers;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

// "Chuyển khoản cần xử lý": SePay transfers that were not auto-confirmed, per branch, until a cashier handles them.
public sealed class CashierBankTransferAlertsTests
{
    [Fact]
    public async Task Cashier_sees_open_warnings_of_their_branch_and_unknown_branch_only()
    {
        await using var db = await SeedAsync();
        var ids = await AlertIds(Controller(db, branch: 1, employee: 3));
        Assert.Equal(new long[] { 1, 2 }, ids.OrderBy(x => x)); // 1: too little (branch 1), 2: no code (unknown branch)
        Assert.Equal(new long[] { 2, 4 }, (await AlertIds(Controller(db, branch: 2, employee: 8))).OrderBy(x => x));
    }

    [Fact]
    public async Task Resolving_hides_the_warning_and_records_who_did_it()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, branch: 1, employee: 3);
        Assert.IsType<OkObjectResult>(await controller.Resolve(1, default));
        var row = await db.BankTransferEvents.SingleAsync(x => x.BankTransferEventId == 1);
        Assert.NotNull(row.ResolvedAtUtc);
        Assert.Equal(3, row.ResolvedByEmployeeId);
        Assert.Equal(new long[] { 2 }, await AlertIds(controller));
    }

    [Fact]
    public async Task Another_branch_or_an_auto_confirmed_transfer_cannot_be_resolved()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, branch: 1, employee: 3);
        Assert.IsType<NotFoundObjectResult>(await controller.Resolve(4, default)); // branch 2
        Assert.IsType<NotFoundObjectResult>(await controller.Resolve(3, default)); // AUTO_CONFIRMED is not a warning
        Assert.Null((await db.BankTransferEvents.SingleAsync(x => x.BankTransferEventId == 4)).ResolvedAtUtc);
    }

    private static async Task<long[]> AlertIds(CashierBankTransfersController controller) =>
        JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.Alerts(default)).Value)
            .EnumerateArray().Select(x => x.GetProperty("eventId").GetInt64()).ToArray();

    private static CashierBankTransfersController Controller(BillingDbContext db, int branch, int employee) => new(db)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim("branchId", branch.ToString()), new Claim("employeeId", employee.ToString()) }, "test")) } }
    };

    private static async Task<BillingDbContext> SeedAsync()
    {
        var db = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        BankTransferEvent Row(long id, string outcome, int? branch, int daysAgo = 0, bool resolved = false) => new()
        {
            BankTransferEventId = id, ProviderTransactionId = "t" + id, AccountNumber = "VA", Amount = 50_000m, Content = "x",
            Outcome = outcome, BranchId = branch, ReceivedAtUtc = DateTime.UtcNow.AddDays(-daysAgo),
            ResolvedAtUtc = resolved ? DateTime.UtcNow : null
        };
        db.BankTransferEvents.AddRange(
            Row(1, "AMOUNT_TOO_LOW", 1),
            Row(2, "NO_MATCH", null),
            Row(3, "AUTO_CONFIRMED", 1),
            Row(4, "BRANCH_MISMATCH", 2),
            Row(5, "PAYMENT_NOT_PENDING", 1, resolved: true),
            Row(6, "AMOUNT_TOO_LOW", 1, daysAgo: 8));
        await db.SaveChangesAsync();
        return db;
    }
}
