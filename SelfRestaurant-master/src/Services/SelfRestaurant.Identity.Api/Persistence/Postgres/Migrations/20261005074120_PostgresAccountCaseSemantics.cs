using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Identity.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class PostgresAccountCaseSemantics : Migration
    {
        private const string CreateLoyaltyView = """
            CREATE VIEW identity."CustomerLoyalty" AS
            SELECT c."CustomerID", c."Name", c."PhoneNumber", c."Email", c."LoyaltyPoints",
                   lc."CardID", lc."Points" AS "CardPoints", lc."IssueDate", lc."ExpiryDate"
            FROM identity."Customers" c
            LEFT JOIN identity."LoyaltyCards" lc ON c."CustomerID" = lc."CustomerID"
            WHERE c."IsActive" = TRUE;
            """;
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW identity.\"CustomerLoyalty\";");
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                schema: "identity",
                table: "Employees",
                type: "citext",
                unicode: false,
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldUnicode: false,
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "identity",
                table: "Employees",
                type: "citext",
                unicode: false,
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldUnicode: false,
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                schema: "identity",
                table: "Customers",
                type: "citext",
                unicode: false,
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldUnicode: false,
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "identity",
                table: "Customers",
                type: "citext",
                unicode: false,
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldUnicode: false,
                oldMaxLength: 100,
                oldNullable: true);
            migrationBuilder.Sql(CreateLoyaltyView);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW identity.\"CustomerLoyalty\";");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                schema: "identity",
                table: "Employees",
                type: "character varying(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldUnicode: false,
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "identity",
                table: "Employees",
                type: "character varying(100)",
                unicode: false,
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "citext",
                oldUnicode: false,
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                schema: "identity",
                table: "Customers",
                type: "character varying(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldUnicode: false,
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "identity",
                table: "Customers",
                type: "character varying(100)",
                unicode: false,
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "citext",
                oldUnicode: false,
                oldMaxLength: 100,
                oldNullable: true);
            migrationBuilder.Sql(CreateLoyaltyView);
            // Do not remove the database-wide extension: another owning service
            // or Supabase infrastructure may also use it. No shared tables are read.
        }
    }
}
