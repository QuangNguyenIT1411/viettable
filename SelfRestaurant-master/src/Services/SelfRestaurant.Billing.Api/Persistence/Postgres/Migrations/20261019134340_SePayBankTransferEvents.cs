using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfRestaurant.Billing.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class SePayBankTransferEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankTransferEvents",
                schema: "billing",
                columns: table => new
                {
                    BankTransferEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Provider = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    ReferenceCode = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(40)", unicode: false, maxLength: 40, nullable: false),
                    SubAccount = table.Column<string>(type: "character varying(40)", unicode: false, maxLength: 40, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Content = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TransactionDate = table.Column<string>(type: "character varying(40)", unicode: false, maxLength: 40, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    Overpaid = table.Column<bool>(type: "boolean", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    PaymentId = table.Column<int>(type: "integer", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByEmployeeId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransferEvents", x => x.BankTransferEventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransferEvents_BranchOutcome",
                schema: "billing",
                table: "BankTransferEvents",
                columns: new[] { "BranchId", "Outcome", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_BankTransferEvents_ProviderTransaction",
                schema: "billing",
                table: "BankTransferEvents",
                columns: new[] { "Provider", "ProviderTransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankTransferEvents",
                schema: "billing");
        }
    }
}
