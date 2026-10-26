using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Controllers;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;
using Xunit;

namespace SelfRestaurant.Postgres.Tests;

// Disposable developer database only (VIETTABLE_POSTGRES_TEST_CONNECTION), never Supabase.
public sealed class PostgresReservationDecisionTests
{
    // Reservation item 2 (26/10): migration 20261026041640_ReservationDecisions applies, and a cashier rejecting a
    // pending booking at the moment the guest checks in by QR ends in exactly one of the two, never a mix.
    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task Reject_and_check_in_of_the_same_pending_booking_never_both_win()
    {
        var connection = Environment.GetEnvironmentVariable("VIETTABLE_POSTGRES_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Disposable PostgreSQL test connection is required.");
        await using (var setup = (CustomersDbContext)PostgresModelTests.Context("customers", connection))
            await setup.Database.MigrateAsync();

        var customerId = Random.Shared.Next(900_000_000, int.MaxValue);
        var ids = new List<int>();
        try
        {
            for (var round = 0; round < 10; round++)
            {
                int id;
                await using (var db = (CustomersDbContext)PostgresModelTests.Context("customers", connection))
                {
                    var row = new Reservation
                    {
                        ReservationCode = "PG-DC-" + Guid.NewGuid().ToString("N")[..12], CustomerId = customerId,
                        CustomerName = "Disposable decision test", PhoneNumber = "0900000000", BranchId = 1, TableId = null,
                        PartySize = 2, ReservedAt = DateTime.UtcNow.AddMinutes(10), ArrivalWindowMinutes = 30, Status = "Pending",
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    db.Reservations.Add(row);
                    await db.SaveChangesAsync();
                    id = row.ReservationId;
                    ids.Add(id);
                }

                await using var rejectDb = (CustomersDbContext)PostgresModelTests.Context("customers", connection);
                await using var checkInDb = (CustomersDbContext)PostgresModelTests.Context("customers", connection);
                var reject = Task.Run(() => new CustomersController(rejectDb, null!, null!)
                    .RejectReservation(id, new(42, "Hết bàn"), CancellationToken.None));
                var checkIn = Task.Run(() => new CustomersController(checkInDb, null!, null!)
                    .BeginReservationCheckIn(id, null, CancellationToken.None));
                var results = await Task.WhenAll(reject, checkIn);

                var rejected = results[0].Result is OkObjectResult;
                var checkedIn = results[1].Result is OkObjectResult;
                Assert.True(rejected ^ checkedIn, $"round {round}: reject={results[0].Result?.GetType().Name} checkIn={results[1].Result?.GetType().Name}");

                await using var read = (CustomersDbContext)PostgresModelTests.Context("customers", connection);
                var saved = await read.Reservations.AsNoTracking().SingleAsync(x => x.ReservationId == id);
                if (rejected)
                {
                    Assert.Equal("Rejected", saved.Status);
                    Assert.Equal("Hết bàn", saved.StatusReason);
                    Assert.Null(saved.CheckInStartedAtUtc);
                }
                else
                {
                    Assert.Equal("CheckingIn", saved.Status);
                    Assert.Equal(CustomersController.AutoConfirmedAtCheckIn, saved.StatusReason);
                    var conflict = JsonSerializer.SerializeToElement(Assert.IsType<ConflictObjectResult>(results[0].Result).Value);
                    Assert.Equal("reservation_state_conflict", conflict.GetProperty("code").GetString());
                }
                Assert.NotNull(saved.StatusChangedAtUtc);
            }
        }
        finally
        {
            await using var cleanup = (CustomersDbContext)PostgresModelTests.Context("customers", connection);
            await cleanup.ReadyDishNotifications.Where(x => x.CustomerId == customerId).ExecuteDeleteAsync();
            await cleanup.Reservations.Where(x => ids.Contains(x.ReservationId)).ExecuteDeleteAsync();
        }
    }
}
