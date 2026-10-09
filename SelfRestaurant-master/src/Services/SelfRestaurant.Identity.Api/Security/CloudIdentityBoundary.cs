using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.Repositories;
using SelfRestaurant.Identity.Api.Persistence;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using SelfRestaurant.Identity.Api.Persistence.Entities;

namespace SelfRestaurant.Identity.Api.Security;

// Owned Identity entrypoints for the composition root. Callers never receive its
// DbContext or query Identity tables from another domain container.
public sealed class CloudIdentityBoundary(IServiceScopeFactory scopes) : IXmlRepository
{
    public async Task<bool> StaffTicketRevokedAsync(string hash, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await db.CloudStaffRevocations.AsNoTracking()
            .AnyAsync(x => x.TicketHash == hash && x.ExpiresAtUtc > DateTime.UtcNow, ct);
    }

    public async Task RevokeStaffTicketAsync(string hash, DateTimeOffset expiry, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        if (await db.CloudStaffRevocations.AnyAsync(x => x.TicketHash == hash, ct)) return;
        db.CloudStaffRevocations.Add(new CloudStaffRevocation { TicketHash = hash, ExpiresAtUtc = expiry.UtcDateTime });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Concurrent logout of the same ticket is idempotent, not a failed logout.
            db.ChangeTracker.Clear();
            if (!await db.CloudStaffRevocations.AnyAsync(x => x.TicketHash == hash, ct)) throw;
        }
    }

    public async Task<string?> StaffVersionAsync(int employeeId, string role, int branch, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var employee = await db.Employees.AsNoTracking().Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.EmployeeID == employeeId && x.IsActive == true, ct);
        if (employee is null || employee.BranchID != branch || employee.Role.RoleCode != role) return null;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            employee.Password + "|" + employee.RoleID + "|" + employee.BranchID + "|" + employee.UpdatedAt?.Ticks)));
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return db.CloudDataProtectionKeys.AsNoTracking().Select(x => x.Xml).AsEnumerable()
            .Select(x => XElement.Parse(x)).ToArray();
    }
    public void StoreElement(XElement element, string friendlyName)
    {
        // Encryption certificate is mandatory in CloudHost; reject raw secret keys.
        if (element.Name.LocalName == "key" && !element.Descendants().Any(x => x.Name.LocalName == "encryptedSecret"))
            throw new InvalidOperationException("DataProtection keys must be encrypted before persistence.");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.CloudDataProtectionKeys.Add(new CloudDataProtectionKey { Id = Guid.NewGuid(), FriendlyName = friendlyName,
            Xml = element.ToString(SaveOptions.DisableFormatting) });
        db.SaveChanges();
    }
}
