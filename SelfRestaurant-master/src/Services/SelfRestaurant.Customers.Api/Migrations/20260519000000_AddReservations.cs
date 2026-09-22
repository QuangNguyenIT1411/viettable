using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Customers.Api.Migrations;

public partial class AddReservations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Reservations",
            columns: table => new
            {
                ReservationId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ReservationCode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                CustomerId = table.Column<int>(type: "int", nullable: true),
                CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                BranchId = table.Column<int>(type: "int", nullable: false),
                TableId = table.Column<int>(type: "int", nullable: true),
                PartySize = table.Column<int>(type: "int", nullable: false),
                ReservedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ArrivalWindowMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Pending"),
                Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ConvertedOrderId = table.Column<int>(type: "int", nullable: true),
                DiningSessionCode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                CheckedInAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CheckedInByEmployeeId = table.Column<int>(type: "int", nullable: true),
                CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                IdempotencyKey = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Reservations", x => x.ReservationId);
            });

        migrationBuilder.CreateTable(
            name: "ReservationPreOrderItems",
            columns: table => new
            {
                ReservationItemId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ReservationId = table.Column<int>(type: "int", nullable: false),
                DishId = table.Column<int>(type: "int", nullable: false),
                DishNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                UnitPriceSnapshot = table.Column<decimal>(type: "decimal(18, 2)", nullable: false),
                Quantity = table.Column<int>(type: "int", nullable: false),
                Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Pending"),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                ConvertedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReservationPreOrderItems", x => x.ReservationItemId);
                table.ForeignKey(
                    name: "FK_ReservationPreOrderItems_Reservations_ReservationId",
                    column: x => x.ReservationId,
                    principalTable: "Reservations",
                    principalColumn: "ReservationId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("UX_Reservations_ReservationCode", "Reservations", "ReservationCode", unique: true);
        migrationBuilder.CreateIndex("IX_Reservations_Branch_ReservedAt_Status", "Reservations", new[] { "BranchId", "ReservedAt", "Status" });
        migrationBuilder.CreateIndex("IX_Reservations_PhoneNumber", "Reservations", "PhoneNumber");
        migrationBuilder.CreateIndex("UX_Reservations_IdempotencyKey", "Reservations", "IdempotencyKey", unique: true, filter: "[IdempotencyKey] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_ReservationPreOrderItems_Reservation_Status", "ReservationPreOrderItems", new[] { "ReservationId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReservationPreOrderItems");
        migrationBuilder.DropTable(name: "Reservations");
    }
}
