using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Orders.Api.Persistence.Entities;

namespace SelfRestaurant.Orders.Api.Persistence;

internal static class OrdersPostgresSeed
{
    public static async Task RunAsync(OrdersDbContext db, string profile, CancellationToken ct)
    {
        if (profile == "None") return;
        var statuses = new[] { ("PENDING", "Chờ xác nhận"), ("CONFIRMED", "Đã xác nhận"),
            ("PREPARING", "Đang chuẩn bị"), ("READY", "Sẵn sàng"), ("SERVING", "Đang phục vụ"),
            ("COMPLETED", "Hoàn tất"), ("CANCELLED", "Đã huỷ") };
        foreach (var (code, name) in statuses)
            if (!await db.OrderStatus.AnyAsync(x => x.StatusCode == code, ct))
                db.OrderStatus.Add(new OrderStatus { StatusCode = code, StatusName = name });
        await db.SaveChangesAsync(ct);
    }
}
