using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfRestaurant.Customers.Api.Controllers;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;
using Xunit;

namespace VietTable.CloudHost.Tests;

/// <summary>5/11: đổi lịch đặt bàn thiếu trường note thì giữ ghi chú cũ; note rỗng mới là xoá có chủ ý.</summary>
public sealed class ReservationNoteTests
{
    [Theory]
    [InlineData(null, "Gần cửa sổ")]       // thiếu trường → giữ
    [InlineData("", null)]                  // rỗng → xoá
    [InlineData("Ghế trẻ em", "Ghế trẻ em")] // có giá trị → ghi đè
    public async Task Update_reservation_keeps_note_only_when_the_field_is_missing(string? sent, string? expected)
    {
        await using var db = new CustomersDbContext(new DbContextOptionsBuilder<CustomersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        db.Reservations.Add(new Reservation { ReservationId = 30, ReservationCode = "RSV-30", CustomerId = 21, CustomerName = "A",
            PhoneNumber = "0343000273", BranchId = 1, TableId = 8, PartySize = 2, ReservedAt = DateTime.UtcNow.AddDays(1),
            ArrivalWindowMinutes = 30, Status = "Confirmed", Note = "Gần cửa sổ", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var request = new CustomersController.UpdateReservationRequest(8, 3, DateTime.UtcNow.AddDays(1).AddHours(1), sent, null, null);
        await new CustomersController(db, null!, null!).UpdateReservation(30, request, CancellationToken.None);

        var saved = await db.Reservations.AsNoTracking().SingleAsync(x => x.ReservationId == 30);
        Assert.Equal(3, saved.PartySize);
        Assert.Equal(expected, saved.Note);
    }
}
