using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Customers.Api.Migrations;

public partial class AddReservationTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ReservationTables",
            columns: table => new
            {
                ReservationTableId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ReservationId = table.Column<int>(type: "int", nullable: false),
                TableId = table.Column<int>(type: "int", nullable: false),
                IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReservationTables", x => x.ReservationTableId);
                table.ForeignKey(
                    name: "FK_ReservationTables_Reservations_ReservationId",
                    column: x => x.ReservationId,
                    principalTable: "Reservations",
                    principalColumn: "ReservationId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "UX_ReservationTables_Reservation_Table",
            table: "ReservationTables",
            columns: new[] { "ReservationId", "TableId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ReservationTables_TableId",
            table: "ReservationTables",
            column: "TableId");

        migrationBuilder.Sql("""
                             INSERT INTO dbo.ReservationTables (ReservationId, TableId, IsPrimary, CreatedAtUtc)
                             SELECT r.ReservationId, r.TableId, CAST(1 AS bit), SYSUTCDATETIME()
                             FROM dbo.Reservations r
                             WHERE r.TableId IS NOT NULL
                               AND r.TableId > 0
                               AND NOT EXISTS
                               (
                                   SELECT 1
                                   FROM dbo.ReservationTables rt
                                   WHERE rt.ReservationId = r.ReservationId
                                     AND rt.TableId = r.TableId
                               );
                             """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReservationTables");
    }
}
