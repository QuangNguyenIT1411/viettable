using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Identity.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class CloudStaffReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CloudStaffResetTokens",
                schema: "identity",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ExpiryUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloudStaffResetTokens", x => x.TokenHash);
                    table.ForeignKey(
                        name: "FK_CloudStaffResetTokens_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "identity",
                        principalTable: "Employees",
                        principalColumn: "EmployeeID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CloudStaffResetTokens_EmployeeId",
                schema: "identity",
                table: "CloudStaffResetTokens",
                column: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CloudStaffResetTokens",
                schema: "identity");
        }
    }
}
