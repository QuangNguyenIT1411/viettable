using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Identity.Api.Persistence.Entities;

namespace SelfRestaurant.Identity.Api.Persistence;

internal static class IdentityPostgresSeed
{
    public static async Task RunAsync(IdentityDbContext db, string profile, CancellationToken ct)
    {
        if (profile == "None") return;
        var roles = new[] { ("ADMIN", "Quản trị viên"), ("MANAGER", "Quản lý"), ("CASHIER", "Thu ngân"),
            ("WAITER", "Phục vụ"), ("CHEF", "Đầu bếp"), ("KITCHEN_STAFF", "Nhân viên bếp") };
        foreach (var (code, name) in roles)
            if (!await db.EmployeeRoles.AnyAsync(x => x.RoleCode == code, ct))
                db.EmployeeRoles.Add(new EmployeeRoles { RoleCode = code, RoleName = name });
        await db.SaveChangesAsync(ct); // No account/password seed; provision through legitimate API.
    }
}
