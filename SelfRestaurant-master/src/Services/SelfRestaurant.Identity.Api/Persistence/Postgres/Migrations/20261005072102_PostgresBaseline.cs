using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfRestaurant.Identity.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class PostgresBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "BusinessAuditLogs",
                schema: "identity",
                columns: table => new
                {
                    BusinessAuditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ActionType = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: true),
                    ActorId = table.Column<int>(type: "integer", nullable: true),
                    ActorCode = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: true),
                    ActorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ActorRoleCode = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: true),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    EmployeeId = table.Column<int>(type: "integer", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", unicode: false, maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BeforeState = table.Column<string>(type: "text", nullable: true),
                    AfterState = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAuditLogs", x => x.BusinessAuditLogId);
                });

            migrationBuilder.CreateTable(
                name: "CatalogBranchSnapshots",
                schema: "identity",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RefreshedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogBranchSnapshots", x => x.BranchId);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "identity",
                columns: table => new
                {
                    CustomerID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    Gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LoyaltyPoints = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    Username = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    Password = table.Column<string>(type: "character varying(255)", unicode: false, maxLength: 255, nullable: true),
                    AuthProvider = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Password"),
                    ExternalProvider = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: true),
                    ExternalSubject = table.Column<string>(type: "character varying(120)", unicode: false, maxLength: 120, nullable: true),
                    CreditPoints = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerID);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeRoles",
                schema: "identity",
                columns: table => new
                {
                    RoleID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleCode = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    RoleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeRoles", x => x.RoleID);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltySettlements",
                schema: "identity",
                columns: table => new
                {
                    SettlementKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    PointsUsed = table.Column<int>(type: "integer", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltySettlements", x => x.SettlementKey);
                });

            migrationBuilder.CreateTable(
                name: "CustomerSettings",
                schema: "identity",
                columns: table => new
                {
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    PushNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SmsReservationRemindersEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    BiometricLoginEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "vi-VN"),
                    DefaultBranchId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerSettings", x => x.CustomerId);
                    table.ForeignKey(
                        name: "FK_CustomerSettings_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "identity",
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyCards",
                schema: "identity",
                columns: table => new
                {
                    CardID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Points = table.Column<int>(type: "integer", nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true),
                    CustomerID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyCards", x => x.CardID);
                    table.ForeignKey(
                        name: "FK_LoyaltyCards_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalSchema: "identity",
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MobileRefreshTokens",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    FamilyId = table.Column<string>(type: "character varying(36)", unicode: false, maxLength: 36, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    CreatedByIp = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobileRefreshTokens_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "identity",
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                schema: "identity",
                columns: table => new
                {
                    TokenID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerID = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "character varying(255)", unicode: false, maxLength: 255, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.TokenID);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalSchema: "identity",
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "identity",
                columns: table => new
                {
                    EmployeeID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    Password = table.Column<string>(type: "character varying(255)", unicode: false, maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    Salary = table.Column<decimal>(type: "numeric(15,2)", nullable: true),
                    Shift = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    BranchID = table.Column<int>(type: "integer", nullable: false),
                    RoleID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.EmployeeID);
                    table.ForeignKey(
                        name: "FK_Employees_EmployeeRoles_RoleID",
                        column: x => x.RoleID,
                        principalSchema: "identity",
                        principalTable: "EmployeeRoles",
                        principalColumn: "RoleID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_CreatedAtUtc",
                schema: "identity",
                table: "BusinessAuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_CustomerId",
                schema: "identity",
                table: "BusinessAuditLogs",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_EmployeeId",
                schema: "identity",
                table: "BusinessAuditLogs",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_Entity",
                schema: "identity",
                table: "BusinessAuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "idx_catalog_branch_snapshots_active",
                schema: "identity",
                table: "CatalogBranchSnapshots",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "idx_customers_active",
                schema: "identity",
                table: "Customers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "idx_customers_external_login",
                schema: "identity",
                table: "Customers",
                columns: new[] { "ExternalProvider", "ExternalSubject" });

            migrationBuilder.CreateIndex(
                name: "UQ_Customers_Username",
                schema: "identity",
                table: "Customers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRoles_RoleCode",
                schema: "identity",
                table: "EmployeeRoles",
                column: "RoleCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_employees_active",
                schema: "identity",
                table: "Employees",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "idx_employees_branch",
                schema: "identity",
                table: "Employees",
                column: "BranchID");

            migrationBuilder.CreateIndex(
                name: "idx_employees_role",
                schema: "identity",
                table: "Employees",
                column: "RoleID");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Username",
                schema: "identity",
                table: "Employees",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_CustomerID",
                schema: "identity",
                table: "LoyaltyCards",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_MobileRefreshTokens_Customer_Expiry",
                schema: "identity",
                table: "MobileRefreshTokens",
                columns: new[] { "CustomerId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MobileRefreshTokens_Family",
                schema: "identity",
                table: "MobileRefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "UQ_MobileRefreshTokens_TokenHash",
                schema: "identity",
                table: "MobileRefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_customer",
                schema: "identity",
                table: "PasswordResetTokens",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "idx_token",
                schema: "identity",
                table: "PasswordResetTokens",
                column: "Token");
            migrationBuilder.Sql("""
                CREATE VIEW identity."CustomerLoyalty" AS
                SELECT c."CustomerID", c."Name", c."PhoneNumber", c."Email", c."LoyaltyPoints",
                       lc."CardID", lc."Points" AS "CardPoints", lc."IssueDate", lc."ExpiryDate"
                FROM identity."Customers" c
                LEFT JOIN identity."LoyaltyCards" lc ON c."CustomerID" = lc."CustomerID"
                WHERE c."IsActive" = TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW identity.\"CustomerLoyalty\";");
            migrationBuilder.DropTable(
                name: "BusinessAuditLogs",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "CatalogBranchSnapshots",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "CustomerSettings",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "LoyaltyCards",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "LoyaltySettlements",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "MobileRefreshTokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "EmployeeRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "identity");
        }
    }
}
