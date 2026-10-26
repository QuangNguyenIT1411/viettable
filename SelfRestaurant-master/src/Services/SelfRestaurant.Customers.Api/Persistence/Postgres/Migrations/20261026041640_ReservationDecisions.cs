using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Customers.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ReservationDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StatusChangedAtUtc",
                schema: "customers",
                table: "Reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusChangedByEmployeeId",
                schema: "customers",
                table: "Reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                schema: "customers",
                table: "Reservations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusChangedAtUtc",
                schema: "customers",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "StatusChangedByEmployeeId",
                schema: "customers",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                schema: "customers",
                table: "Reservations");
        }
    }
}
