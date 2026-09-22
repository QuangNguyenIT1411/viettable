using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Orders.Api.Migrations;

public partial class AddDiningSessionTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DiningSessionTables",
            columns: table => new
            {
                DiningSessionTableID = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DiningSessionCode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                TableID = table.Column<int>(type: "int", nullable: false),
                IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DiningSessionTables", x => x.DiningSessionTableID);
            });

        migrationBuilder.CreateIndex(
            name: "UX_DiningSessionTables_Session_Table",
            table: "DiningSessionTables",
            columns: new[] { "DiningSessionCode", "TableID" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DiningSessionTables_TableID",
            table: "DiningSessionTables",
            column: "TableID");

        migrationBuilder.Sql("""
                             ;WITH sessionTables AS
                             (
                                 SELECT
                                     o.DiningSessionCode,
                                     o.TableID,
                                     IsPrimary = CASE
                                         WHEN ROW_NUMBER() OVER (PARTITION BY o.DiningSessionCode ORDER BY MIN(o.OrderTime), o.TableID) = 1 THEN CAST(1 AS bit)
                                         ELSE CAST(0 AS bit)
                                     END
                                 FROM dbo.Orders o
                                 WHERE o.DiningSessionCode IS NOT NULL
                                   AND o.TableID IS NOT NULL
                                 GROUP BY o.DiningSessionCode, o.TableID
                             )
                             INSERT INTO dbo.DiningSessionTables (DiningSessionCode, TableID, IsPrimary, CreatedAtUtc)
                             SELECT st.DiningSessionCode, st.TableID, st.IsPrimary, SYSUTCDATETIME()
                             FROM sessionTables st
                             WHERE NOT EXISTS
                             (
                                 SELECT 1
                                 FROM dbo.DiningSessionTables dst
                                 WHERE dst.DiningSessionCode = st.DiningSessionCode
                                   AND dst.TableID = st.TableID
                             );
                             """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DiningSessionTables");
    }
}
