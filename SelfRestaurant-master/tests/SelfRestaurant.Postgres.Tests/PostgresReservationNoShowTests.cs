using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Controllers;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence.Entities;
using Xunit;

namespace SelfRestaurant.Postgres.Tests;

// Disposable developer database only (VIETTABLE_POSTGRES_TEST_CONNECTION), never Supabase.
public sealed class PostgresReservationNoShowTests
{
    // Reservation review 25/10 (A3): the set-based NoShow UPDATE runs on PostgreSQL and touches only overdue
    // Pending/Confirmed rows of that customer, never a check-in in progress.
    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task Reading_reservations_marks_only_overdue_open_ones_NoShow()
    {
        var connection = Environment.GetEnvironmentVariable("VIETTABLE_POSTGRES_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Disposable PostgreSQL test connection is required.");
        await using var db = (CustomersDbContext)PostgresModelTests.Context("customers", connection);
        await db.Database.MigrateAsync();
        var customerId = Random.Shared.Next(900_000_000, int.MaxValue);
        var now = DateTime.UtcNow;
        Reservation Row(string status, DateTime at, int? owner = null) => new()
        {
            ReservationCode = "PG-NS-" + Guid.NewGuid().ToString("N")[..12], CustomerId = owner ?? customerId,
            CustomerName = "Disposable no-show test", PhoneNumber = "0900000000", BranchId = 1, TableId = null,
            PartySize = 2, ReservedAt = at, ArrivalWindowMinutes = 30, Status = status, CreatedAtUtc = now
        };
        var overdue = Row("Confirmed", now.AddMinutes(-45));
        var pendingOverdue = Row("Pending", now.AddHours(-3));
        var lateButInWindow = Row("Confirmed", now.AddMinutes(-20));
        var checkingIn = Row("CheckingIn", now.AddHours(-2));
        var otherCustomer = Row("Confirmed", now.AddHours(-2), customerId - 1);
        db.Reservations.AddRange(overdue, pendingOverdue, lateButInWindow, checkingIn, otherCustomer);
        await db.SaveChangesAsync();
        try
        {
            var result = await new CustomersController(db, null!, null!).GetCustomerReservations(customerId, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);

            db.ChangeTracker.Clear();
            var status = await db.Reservations.AsNoTracking()
                .Where(x => new[] { overdue.ReservationId, pendingOverdue.ReservationId, lateButInWindow.ReservationId,
                    checkingIn.ReservationId, otherCustomer.ReservationId }.Contains(x.ReservationId))
                .ToDictionaryAsync(x => x.ReservationId, x => x.Status);
            Assert.Equal("NoShow", status[overdue.ReservationId]);
            Assert.Equal("NoShow", status[pendingOverdue.ReservationId]);
            Assert.Equal("Confirmed", status[lateButInWindow.ReservationId]);
            Assert.Equal("CheckingIn", status[checkingIn.ReservationId]);
            Assert.Equal("Confirmed", status[otherCustomer.ReservationId]);    // another customer's read handles it
        }
        finally
        {
            await db.Reservations.Where(x => x.CustomerName == "Disposable no-show test").ExecuteDeleteAsync();
        }
    }
}
