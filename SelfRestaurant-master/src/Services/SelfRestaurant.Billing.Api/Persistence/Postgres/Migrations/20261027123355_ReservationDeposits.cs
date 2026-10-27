using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfRestaurant.Billing.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ReservationDeposits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DepositApplied",
                schema: "billing",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositApplied",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ReservationDepositId",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReservationDeposits",
                schema: "billing",
                columns: table => new
                {
                    ReservationDepositId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReservationId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    ReservedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    AppliedCheckoutQuoteId = table.Column<int>(type: "integer", nullable: true),
                    AppliedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RefundedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationDeposits", x => x.ReservationDepositId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationDeposits_BranchStatus",
                schema: "billing",
                table: "ReservationDeposits",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_ReservationDeposits_ReservationId",
                schema: "billing",
                table: "ReservationDeposits",
                column: "ReservationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationDeposits",
                schema: "billing");

            migrationBuilder.DropColumn(
                name: "DepositApplied",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "DepositApplied",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "ReservationDepositId",
                schema: "billing",
                table: "CheckoutQuotes");
        }
    }
}
