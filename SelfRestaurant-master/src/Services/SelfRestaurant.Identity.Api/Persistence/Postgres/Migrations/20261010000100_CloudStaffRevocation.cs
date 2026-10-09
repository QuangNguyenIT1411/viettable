using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SelfRestaurant.Identity.Api.Persistence;

namespace SelfRestaurant.Identity.Api.Persistence.Postgres.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261010000100_CloudStaffRevocation")]
public sealed class CloudStaffRevocationMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "CloudStaffRevocations", schema: "identity",
            columns: table => new {
                TicketHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_CloudStaffRevocations", x => x.TicketHash));
        migrationBuilder.CreateIndex(name: "IX_CloudStaffRevocations_ExpiresAtUtc", schema: "identity",
            table: "CloudStaffRevocations", column: "ExpiresAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "CloudStaffRevocations", schema: "identity");
}
