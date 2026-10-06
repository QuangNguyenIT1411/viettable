using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SelfRestaurant.Billing.Api.Persistence.Postgres.Migrations;

[DbContext(typeof(BillingDbContext))]
[Migration("20261006120000_InvoiceRecoveryAttempts")]
public sealed class InvoiceRecoveryAttempts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "RecoveryAttemptCount", schema: "billing", table: "Invoices",
            type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTime>(
            name: "RecoveryLastAttemptAtUtc", schema: "billing", table: "Invoices",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_Invoices_RecoveryPending", schema: "billing", table: "Invoices",
            columns: new[] { "SettlementCompletedAtUtc", "RecoveryLastAttemptAtUtc", "InvoiceId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Invoices_RecoveryPending", schema: "billing", table: "Invoices");
        migrationBuilder.DropColumn(name: "RecoveryAttemptCount", schema: "billing", table: "Invoices");
        migrationBuilder.DropColumn(name: "RecoveryLastAttemptAtUtc", schema: "billing", table: "Invoices");
    }
}
