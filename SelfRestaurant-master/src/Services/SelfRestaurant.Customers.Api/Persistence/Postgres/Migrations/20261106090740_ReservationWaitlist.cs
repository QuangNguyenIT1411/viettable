using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfRestaurant.Customers.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ReservationWaitlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HoldExpiresAtUtc",
                schema: "customers",
                table: "Reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReservationWaitlist",
                schema: "customers",
                columns: table => new
                {
                    WaitlistEntryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WindowEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Waiting"),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OfferedReservationId = table.Column<int>(type: "integer", nullable: true),
                    OfferedTableId = table.Column<int>(type: "integer", nullable: true),
                    OfferedReservedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OfferedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OfferExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationWaitlist", x => x.WaitlistEntryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationWaitlist_Branch_Status_Created",
                schema: "customers",
                table: "ReservationWaitlist",
                columns: new[] { "BranchId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationWaitlist_Customer_Status",
                schema: "customers",
                table: "ReservationWaitlist",
                columns: new[] { "CustomerId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationWaitlist",
                schema: "customers");

            migrationBuilder.DropColumn(
                name: "HoldExpiresAtUtc",
                schema: "customers",
                table: "Reservations");
        }
    }
}
