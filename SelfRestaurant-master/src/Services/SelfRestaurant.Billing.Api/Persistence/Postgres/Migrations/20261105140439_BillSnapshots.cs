using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Billing.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class BillSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankTransactionRef",
                schema: "billing",
                table: "Invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchAddressSnapshot",
                schema: "billing",
                table: "Invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                schema: "billing",
                table: "Invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchNameSnapshot",
                schema: "billing",
                table: "Invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CashierEmployeeId",
                schema: "billing",
                table: "Invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CashierNameSnapshot",
                schema: "billing",
                table: "Invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAtUtc",
                schema: "billing",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodSnapshot",
                schema: "billing",
                table: "Invoices",
                type: "character varying(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PointsDiscount",
                schema: "billing",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PointsEarned",
                schema: "billing",
                table: "Invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsUsed",
                schema: "billing",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFeeAmount",
                schema: "billing",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFeePercent",
                schema: "billing",
                table: "Invoices",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "SessionStartedAtUtc",
                schema: "billing",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SnapshotVersion",
                schema: "billing",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TableNameSnapshot",
                schema: "billing",
                table: "Invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                schema: "billing",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatPercent",
                schema: "billing",
                table: "Invoices",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VoucherDiscount",
                schema: "billing",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BranchAddressSnapshot",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchNameSnapshot",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFeeAmount",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFeePercent",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "SessionStartedAtUtc",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TableNameSnapshot",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatPercent",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeNameSnapshot",
                schema: "billing",
                table: "Bills",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemsJson",
                schema: "billing",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFeeAmount",
                schema: "billing",
                table: "Bills",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFeePercent",
                schema: "billing",
                table: "Bills",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                schema: "billing",
                table: "Bills",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatPercent",
                schema: "billing",
                table: "Bills",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "BranchChargeSettings",
                schema: "billing",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    ServiceFeePercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    VatPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchChargeSettings", x => x.BranchId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Customer_Issued",
                schema: "billing",
                table: "Invoices",
                columns: new[] { "CustomerId", "IssuedAtUtc", "InvoiceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchChargeSettings",
                schema: "billing");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_Customer_Issued",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BankTransactionRef",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BranchAddressSnapshot",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BranchNameSnapshot",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CashierEmployeeId",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CashierNameSnapshot",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaidAtUtc",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaymentMethodSnapshot",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PointsDiscount",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PointsEarned",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PointsUsed",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ServiceFeeAmount",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ServiceFeePercent",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SessionStartedAtUtc",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SnapshotVersion",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TableNameSnapshot",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VatPercent",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoucherDiscount",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BranchAddressSnapshot",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "BranchNameSnapshot",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "ServiceFeeAmount",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "ServiceFeePercent",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "SessionStartedAtUtc",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "TableNameSnapshot",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "VatPercent",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "EmployeeNameSnapshot",
                schema: "billing",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ItemsJson",
                schema: "billing",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ServiceFeeAmount",
                schema: "billing",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ServiceFeePercent",
                schema: "billing",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                schema: "billing",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "VatPercent",
                schema: "billing",
                table: "Bills");
        }
    }
}
