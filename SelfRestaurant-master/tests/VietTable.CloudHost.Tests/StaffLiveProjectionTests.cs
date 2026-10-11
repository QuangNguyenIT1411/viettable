using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SelfRestaurant.Identity.Api.Persistence;
using SelfRestaurant.Identity.Api.Persistence.Entities;
using SelfRestaurant.Identity.Api.Security;
using Xunit;

namespace VietTable.CloudHost.Tests;

public sealed class StaffLiveProjectionTests
{
    [Fact]
    public async Task Combined_live_read_preserves_version_and_rejects_wrong_identity_or_revoked_ticket()
    {
        // Fix the database name across scopes, as a real owner scope factory does.
        var name = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<IdentityDbContext>(o => o.UseInMemoryDatabase(name));
        await using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.EmployeeRoles.Add(new EmployeeRoles { RoleID = 1, RoleCode = "CHEF", RoleName = "Fixture" });
            db.Employees.Add(new Employees { EmployeeID = 1, RoleID = 1, BranchID = 3, Name = "Fixture",
                Username = "fixture", Password = "test-only-hash", IsActive = true, UpdatedAt = DateTime.UtcNow });
            db.CloudStaffRevocations.Add(new CloudStaffRevocation { TicketHash = "revoked-fixture", ExpiresAtUtc = DateTime.UtcNow.AddHours(1) });
            db.CloudStaffRevocations.Add(new CloudStaffRevocation { TicketHash = "expired-fixture", ExpiresAtUtc = DateTime.UtcNow.AddHours(-1) });
            await db.SaveChangesAsync();
        }
        var boundary = new CloudIdentityBoundary(provider.GetRequiredService<IServiceScopeFactory>());
        var version = await boundary.StaffVersionAsync(1, "CHEF", 3, default);
        Assert.NotNull(version);
        Assert.Equal(version, await boundary.ValidateStaffVersionAsync(1, "CHEF", 3, "valid-fixture", default));
        Assert.Equal(version, await boundary.ValidateStaffVersionAsync(1, "CHEF", 3, "expired-fixture", default));
        Assert.Null(await boundary.ValidateStaffVersionAsync(1, "CHEF", 3, "revoked-fixture", default));
        Assert.Null(await boundary.ValidateStaffVersionAsync(1, "CHEF", 2, "valid-fixture", default));
        Assert.Null(await boundary.ValidateStaffVersionAsync(1, "chef", 3, "valid-fixture", default));
        Assert.Null(await boundary.ValidateStaffVersionAsync(99, "CHEF", 3, "valid-fixture", default));
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var employee = await db.Employees.SingleAsync();
            employee.Password = "changed-test-only-hash";
            await db.SaveChangesAsync();
        }
        Assert.NotEqual(version, await boundary.ValidateStaffVersionAsync(1, "CHEF", 3, "valid-fixture", default));
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            (await db.Employees.SingleAsync()).IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.Null(await boundary.ValidateStaffVersionAsync(1, "CHEF", 3, "valid-fixture", default));
    }
}
