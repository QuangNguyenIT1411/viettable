using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Customers.Api.Migrations;

public partial class AddReservationCheckInState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "CheckInStartedAtUtc",
            table: "Reservations",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CheckInIdempotencyKey",
            table: "Reservations",
            type: "varchar(100)",
            unicode: false,
            maxLength: 100,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CheckInStartedAtUtc",
            table: "Reservations");

        migrationBuilder.DropColumn(
            name: "CheckInIdempotencyKey",
            table: "Reservations");
    }
}
