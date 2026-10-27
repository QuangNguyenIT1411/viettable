using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Customers.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ReservationDeposits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                schema: "customers",
                table: "Reservations",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepositDueAtUtc",
                schema: "customers",
                table: "Reservations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositAmount",
                schema: "customers",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "DepositDueAtUtc",
                schema: "customers",
                table: "Reservations");
        }
    }
}
