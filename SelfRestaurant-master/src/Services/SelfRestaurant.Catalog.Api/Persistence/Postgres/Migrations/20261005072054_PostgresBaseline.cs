using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfRestaurant.Catalog.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class PostgresBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "BusinessAuditLogs",
                schema: "catalog",
                columns: table => new
                {
                    BusinessAuditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ActionType = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: true),
                    ActorId = table.Column<int>(type: "integer", nullable: true),
                    ActorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActorRoleCode = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    TableId = table.Column<int>(type: "integer", nullable: true),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    OrderItemId = table.Column<int>(type: "integer", nullable: true),
                    DishId = table.Column<int>(type: "integer", nullable: true),
                    BillId = table.Column<int>(type: "integer", nullable: true),
                    DiningSessionCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BeforeState = table.Column<string>(type: "text", nullable: true),
                    AfterState = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAuditLogs", x => x.BusinessAuditLogId);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "catalog",
                columns: table => new
                {
                    CategoryID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.CategoryID);
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                schema: "catalog",
                columns: table => new
                {
                    IngredientID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentStock = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IssueMethod = table.Column<string>(type: "character varying(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "FEFO"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.IngredientID);
                });

            migrationBuilder.CreateTable(
                name: "Restaurants",
                schema: "catalog",
                columns: table => new
                {
                    RestaurantID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Restaurants", x => x.RestaurantID);
                });

            migrationBuilder.CreateTable(
                name: "TableStatus",
                schema: "catalog",
                columns: table => new
                {
                    StatusID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StatusCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StatusName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableStatus", x => x.StatusID);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                schema: "catalog",
                columns: table => new
                {
                    UnitID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.UnitID);
                });

            migrationBuilder.CreateTable(
                name: "Dishes",
                schema: "catalog",
                columns: table => new
                {
                    DishID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Available = table.Column<bool>(type: "boolean", nullable: true),
                    Image = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsVegetarian = table.Column<bool>(type: "boolean", nullable: true),
                    IsDailySpecial = table.Column<bool>(type: "boolean", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CategoryID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dishes", x => x.DishID);
                    table.ForeignKey(
                        name: "FK_Dishes_Categories_CategoryID",
                        column: x => x.CategoryID,
                        principalSchema: "catalog",
                        principalTable: "Categories",
                        principalColumn: "CategoryID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientBatches",
                schema: "catalog",
                columns: table => new
                {
                    BatchID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IngredientID = table.Column<int>(type: "integer", nullable: false),
                    BatchCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    QuantityInitial = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    QuantityRemaining = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientBatches", x => x.BatchID);
                    table.ForeignKey(
                        name: "FK_IngredientBatches_Ingredients_IngredientID",
                        column: x => x.IngredientID,
                        principalSchema: "catalog",
                        principalTable: "Ingredients",
                        principalColumn: "IngredientID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                schema: "catalog",
                columns: table => new
                {
                    BranchID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Image = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ManagerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    OpeningHours = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    RestaurantID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.BranchID);
                    table.ForeignKey(
                        name: "FK_Branches_Restaurants_RestaurantID",
                        column: x => x.RestaurantID,
                        principalSchema: "catalog",
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DishIngredients",
                schema: "catalog",
                columns: table => new
                {
                    DishIngredientID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DishID = table.Column<int>(type: "integer", nullable: false),
                    IngredientID = table.Column<int>(type: "integer", nullable: false),
                    QuantityPerDish = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DishIngredients", x => x.DishIngredientID);
                    table.ForeignKey(
                        name: "FK_DishIngredients_Dishes_DishID",
                        column: x => x.DishID,
                        principalSchema: "catalog",
                        principalTable: "Dishes",
                        principalColumn: "DishID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DishIngredients_Ingredients_IngredientID",
                        column: x => x.IngredientID,
                        principalSchema: "catalog",
                        principalTable: "Ingredients",
                        principalColumn: "IngredientID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientStockMovements",
                schema: "catalog",
                columns: table => new
                {
                    MovementID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IngredientID = table.Column<int>(type: "integer", nullable: false),
                    BatchID = table.Column<int>(type: "integer", nullable: true),
                    QuantityChange = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceID = table.Column<int>(type: "integer", nullable: true),
                    OrderID = table.Column<int>(type: "integer", nullable: true),
                    OrderItemID = table.Column<int>(type: "integer", nullable: true),
                    DishID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientStockMovements", x => x.MovementID);
                    table.ForeignKey(
                        name: "FK_IngredientStockMovements_IngredientBatches_BatchID",
                        column: x => x.BatchID,
                        principalSchema: "catalog",
                        principalTable: "IngredientBatches",
                        principalColumn: "BatchID");
                    table.ForeignKey(
                        name: "FK_IngredientStockMovements_Ingredients_IngredientID",
                        column: x => x.IngredientID,
                        principalSchema: "catalog",
                        principalTable: "Ingredients",
                        principalColumn: "IngredientID");
                });

            migrationBuilder.CreateTable(
                name: "DiningTables",
                schema: "catalog",
                columns: table => new
                {
                    TableID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TableNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    NumberOfSeats = table.Column<int>(type: "integer", nullable: false),
                    CurrentOrderID = table.Column<int>(type: "integer", nullable: true),
                    QRCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    BranchID = table.Column<int>(type: "integer", nullable: false),
                    StatusID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiningTables", x => x.TableID);
                    table.ForeignKey(
                        name: "FK_DiningTables_Branches_BranchID",
                        column: x => x.BranchID,
                        principalSchema: "catalog",
                        principalTable: "Branches",
                        principalColumn: "BranchID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiningTables_TableStatus_StatusID",
                        column: x => x.StatusID,
                        principalSchema: "catalog",
                        principalTable: "TableStatus",
                        principalColumn: "StatusID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Menus",
                schema: "catalog",
                columns: table => new
                {
                    MenuID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    BranchID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.MenuID);
                    table.ForeignKey(
                        name: "FK_Menus_Branches_BranchID",
                        column: x => x.BranchID,
                        principalSchema: "catalog",
                        principalTable: "Branches",
                        principalColumn: "BranchID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuCategory",
                schema: "catalog",
                columns: table => new
                {
                    MenuCategoryID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    MenuID = table.Column<int>(type: "integer", nullable: false),
                    CategoryID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuCategory", x => x.MenuCategoryID);
                    table.ForeignKey(
                        name: "FK_MenuCategory_Categories_CategoryID",
                        column: x => x.CategoryID,
                        principalSchema: "catalog",
                        principalTable: "Categories",
                        principalColumn: "CategoryID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuCategory_Menus_MenuID",
                        column: x => x.MenuID,
                        principalSchema: "catalog",
                        principalTable: "Menus",
                        principalColumn: "MenuID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoryDish",
                schema: "catalog",
                columns: table => new
                {
                    CategoryDishID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    MenuCategoryID = table.Column<int>(type: "integer", nullable: false),
                    DishID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryDish", x => x.CategoryDishID);
                    table.ForeignKey(
                        name: "FK_CategoryDish_Dishes_DishID",
                        column: x => x.DishID,
                        principalSchema: "catalog",
                        principalTable: "Dishes",
                        principalColumn: "DishID");
                    table.ForeignKey(
                        name: "FK_CategoryDish_MenuCategory_MenuCategoryID",
                        column: x => x.MenuCategoryID,
                        principalSchema: "catalog",
                        principalTable: "MenuCategory",
                        principalColumn: "MenuCategoryID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_branches_active",
                schema: "catalog",
                table: "Branches",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "idx_branches_restaurant",
                schema: "catalog",
                table: "Branches",
                column: "RestaurantID");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_BranchId",
                schema: "catalog",
                table: "BusinessAuditLogs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_CreatedAtUtc",
                schema: "catalog",
                table: "BusinessAuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_DishId",
                schema: "catalog",
                table: "BusinessAuditLogs",
                column: "DishId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_Entity",
                schema: "catalog",
                table: "BusinessAuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_TableId",
                schema: "catalog",
                table: "BusinessAuditLogs",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "idx_categories_active",
                schema: "catalog",
                table: "Categories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "idx_categories_display",
                schema: "catalog",
                table: "Categories",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "idx_categories_name",
                schema: "catalog",
                table: "Categories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "idx_categorydish_dish",
                schema: "catalog",
                table: "CategoryDish",
                column: "DishID");

            migrationBuilder.CreateIndex(
                name: "idx_categorydish_display",
                schema: "catalog",
                table: "CategoryDish",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "idx_categorydish_menucategory",
                schema: "catalog",
                table: "CategoryDish",
                column: "MenuCategoryID");

            migrationBuilder.CreateIndex(
                name: "idx_tables_active",
                schema: "catalog",
                table: "DiningTables",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "idx_tables_branch",
                schema: "catalog",
                table: "DiningTables",
                column: "BranchID");

            migrationBuilder.CreateIndex(
                name: "idx_tables_status",
                schema: "catalog",
                table: "DiningTables",
                column: "StatusID");

            migrationBuilder.CreateIndex(
                name: "UQ_DiningTables_Branch_TableNumber",
                schema: "catalog",
                table: "DiningTables",
                columns: new[] { "BranchID", "TableNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_dishes_active",
                schema: "catalog",
                table: "Dishes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "idx_dishes_available",
                schema: "catalog",
                table: "Dishes",
                column: "Available");

            migrationBuilder.CreateIndex(
                name: "idx_dishes_category",
                schema: "catalog",
                table: "Dishes",
                column: "CategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_DishIngredients_IngredientID",
                schema: "catalog",
                table: "DishIngredients",
                column: "IngredientID");

            migrationBuilder.CreateIndex(
                name: "UQ_DishIngredients_Dish_Ingredient",
                schema: "catalog",
                table: "DishIngredients",
                columns: new[] { "DishID", "IngredientID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientBatches_ExpiryDate",
                schema: "catalog",
                table: "IngredientBatches",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientBatches_Ingredient_Fefo",
                schema: "catalog",
                table: "IngredientBatches",
                columns: new[] { "IngredientID", "IsActive", "ExpiryDate", "ReceivedDate", "BatchID" });

            migrationBuilder.CreateIndex(
                name: "idx_ingredients_name",
                schema: "catalog",
                table: "Ingredients",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientStockMovements_BatchID",
                schema: "catalog",
                table: "IngredientStockMovements",
                column: "BatchID");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientStockMovements_Ingredient_CreatedAt",
                schema: "catalog",
                table: "IngredientStockMovements",
                columns: new[] { "IngredientID", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategory_CategoryID",
                schema: "catalog",
                table: "MenuCategory",
                column: "CategoryID");

            migrationBuilder.CreateIndex(
                name: "UQ_MenuCategory_Menu_Category",
                schema: "catalog",
                table: "MenuCategory",
                columns: new[] { "MenuID", "CategoryID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_menus_branch",
                schema: "catalog",
                table: "Menus",
                column: "BranchID");

            migrationBuilder.CreateIndex(
                name: "idx_menus_date",
                schema: "catalog",
                table: "Menus",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Units_IsActive",
                schema: "catalog",
                table: "Units",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "UX_Units_Name",
                schema: "catalog",
                table: "Units",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessAuditLogs",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "CategoryDish",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "DiningTables",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "DishIngredients",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "IngredientStockMovements",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Units",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "MenuCategory",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "TableStatus",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Dishes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "IngredientBatches",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Menus",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Ingredients",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Branches",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Restaurants",
                schema: "catalog");
        }
    }
}
