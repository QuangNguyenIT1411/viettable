using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SelfRestaurant.CloudCompatibility;

internal static class TransactionLocks
{
    public static async Task AcquireAsync(DbContext db, string resource, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Transaction-scoped lock requires an active transaction.");
        if (db.Database.IsNpgsql())
        {
            // Stable across processes/restarts. Do not use randomized .NET GetHashCode.
            var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(resource)));
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '10s'", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
        }
        else
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51000, 'Transaction command busy', 1;", ct);
    }
}
