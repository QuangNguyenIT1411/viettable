using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SelfRestaurant.Catalog.Api.Persistence.Entities;

namespace SelfRestaurant.Catalog.Api.Persistence;

public static class CatalogDbBootstrapper
{
    private static readonly string[] OwnedTables =
    [
        "Restaurants",
        "Branches",
        "Categories",
        "Ingredients",
        "Dishes",
        "TableStatus",
        "DiningTables",
        "Menus",
        "MenuCategory",
        "CategoryDish",
        "DishIngredients",
        "IngredientBatches",
        "IngredientStockMovements",
        "Units"
    ];

    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        await WaitForDatabaseAsync(db, logger, cancellationToken);
        await EnsureBusinessAuditTableAsync(db, cancellationToken);
        await EnsureTableNumberSchemaAsync(db, cancellationToken);
        await EnsureUnitsSchemaAsync(db, cancellationToken);
        await EnsureIngredientIssueMethodSchemaAsync(db, cancellationToken);
        await EnsureIngredientBatchSchemaAsync(db, cancellationToken);
        await ValidateOwnedSchemaAsync(db, logger, cancellationToken);
        await SeedReferenceDataAsync(db, logger, cancellationToken);
        await EnsureLogicalRecipeDataAsync(db, cancellationToken);
        await EnsureRecommendationMenuDataAsync(db, cancellationToken);
        await EnsureIngredientIssueMethodSchemaAsync(db, cancellationToken);
    }

    private static async Task WaitForDatabaseAsync(CatalogDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 1; attempt <= 60; attempt++)
        {
            try
            {
                await db.Database.OpenConnectionAsync(cancellationToken);
                try
                {
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandText = "SELECT 1";
                    command.CommandType = CommandType.Text;
                    _ = await command.ExecuteScalarAsync(cancellationToken);
                }
                finally
                {
                    try
                    {
                        await db.Database.CloseConnectionAsync();
                    }
                    catch
                    {
                        // ignored
                    }
                }

                return;
            }
            catch (Exception ex) when (attempt < 60)
            {
                logger.LogWarning(ex, "Database not ready (attempt {Attempt}/60). Waiting {Delay}...", attempt, delay);
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 10));
            }
        }
    }

    private static async Task EnsureUnitsSchemaAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        const string createSql = """
                                 IF OBJECT_ID(N'dbo.Units', N'U') IS NULL
                                 BEGIN
                                     CREATE TABLE dbo.Units
                                     (
                                         UnitID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Units PRIMARY KEY,
                                         Name NVARCHAR(50) NOT NULL,
                                         Description NVARCHAR(500) NULL,
                                         DisplayOrder INT NOT NULL CONSTRAINT DF_Units_DisplayOrder DEFAULT(0),
                                         IsActive BIT NOT NULL CONSTRAINT DF_Units_IsActive DEFAULT(1),
                                         CreatedAt DATETIME NOT NULL CONSTRAINT DF_Units_CreatedAt DEFAULT(GETDATE()),
                                         UpdatedAt DATETIME NOT NULL CONSTRAINT DF_Units_UpdatedAt DEFAULT(GETDATE())
                                     );
                                 END
                                 """;

        const string indexSql = """
                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Units') AND name = N'UX_Units_Name')
                                    CREATE UNIQUE INDEX UX_Units_Name ON dbo.Units(Name);

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Units') AND name = N'IX_Units_IsActive')
                                    CREATE INDEX IX_Units_IsActive ON dbo.Units(IsActive);
                                """;

        const string seedSql = """
                               ;WITH source_units AS
                               (
                                   SELECT DISTINCT LTRIM(RTRIM(Unit)) AS Name
                                   FROM dbo.Dishes
                                   WHERE Unit IS NOT NULL AND LTRIM(RTRIM(Unit)) <> N''

                                   UNION

                                   SELECT DISTINCT LTRIM(RTRIM(Unit)) AS Name
                                   FROM dbo.Ingredients
                                   WHERE Unit IS NOT NULL AND LTRIM(RTRIM(Unit)) <> N''
                               )
                               INSERT INTO dbo.Units(Name, Description, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
                               SELECT Name, NULL, 0, 1, GETDATE(), GETDATE()
                               FROM source_units source
                               WHERE NOT EXISTS
                               (
                                   SELECT 1
                                   FROM dbo.Units units
                                   WHERE units.Name = source.Name
                               );
                               """;

        await db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(indexSql, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(seedSql, cancellationToken);
    }

    private static async Task EnsureIngredientIssueMethodSchemaAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        const string ensureColumnSql = """
                                       IF COL_LENGTH(N'dbo.Ingredients', N'IssueMethod') IS NULL
                                       BEGIN
                                           ALTER TABLE dbo.Ingredients
                                           ADD IssueMethod VARCHAR(10) NOT NULL
                                               CONSTRAINT DF_Ingredients_IssueMethod DEFAULT ('FEFO');
                                       END;
                                       """;

        const string syncValuesSql = """
                                     UPDATE dbo.Ingredients
                                     SET IssueMethod = 'FEFO'
                                     WHERE IssueMethod IS NULL OR IssueMethod NOT IN ('FEFO', 'FIFO');

                                     UPDATE dbo.Ingredients
                                     SET IssueMethod = 'FIFO'
                                     WHERE Name IN
                                     (
                                         N'Gạo',
                                         N'Đường',
                                         N'Muối',
                                         N'Tiêu',
                                         N'Dầu ăn',
                                         N'Nước mắm',
                                         N'Cà phê',
                                         N'Trà',
                                         N'Sữa đặc',
                                         N'Gia vị cà ri',
                                         N'Khoai tây',
                                         N'Bánh đa nem'
                                     );

                                     UPDATE dbo.Ingredients
                                     SET IssueMethod = 'FEFO'
                                     WHERE Name IN
                                     (
                                         N'Rau cải',
                                         N'Xà lách',
                                         N'Rau xà lách',
                                         N'Rau thơm',
                                         N'Thịt gà',
                                         N'Thịt bò',
                                         N'Thịt heo',
                                         N'Cá',
                                         N'Tôm',
                                         N'Trứng',
                                         N'Sữa tươi',
                                         N'Bún',
                                         N'Bánh phở',
                                         N'Nước dùng',
                                         N'Dưa hấu',
                                         N'Bơ',
                                         N'Cam tươi'
                                     );
                                     """;

        await db.Database.ExecuteSqlRawAsync(ensureColumnSql, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(syncValuesSql, cancellationToken);
    }

    private static async Task SeedReferenceDataAsync(CatalogDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var changed = false;

        if (!await db.TableStatus.AnyAsync(cancellationToken))
        {
            db.TableStatus.AddRange(
                new TableStatus { StatusCode = "AVAILABLE", StatusName = "Trống" },
                new TableStatus { StatusCode = "OCCUPIED", StatusName = "Đang dùng" },
                new TableStatus { StatusCode = "RESERVED", StatusName = "Đặt trước" },
                new TableStatus { StatusCode = "INACTIVE", StatusName = "Không hoạt động" });
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureLogicalRecipeDataAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        const string sql = """
                           IF EXISTS (SELECT 1 FROM dbo.Ingredients WHERE Name = N'Cà rót')
                              AND NOT EXISTS (SELECT 1 FROM dbo.Ingredients WHERE Name = N'Cà rốt')
                           BEGIN
                               UPDATE dbo.Ingredients
                               SET Name = N'Cà rốt'
                               WHERE Name = N'Cà rót';
                           END;

                           IF NOT EXISTS (SELECT 1 FROM dbo.Ingredients WHERE Name = N'Nước dùng')
                           BEGIN
                               INSERT INTO dbo.Ingredients (Name, Unit, CurrentStock, ReorderLevel, IsActive)
                               VALUES (N'Nước dùng', N'lít', 60.00, 10.00, 1);
                           END;

                           IF NOT EXISTS (SELECT 1 FROM dbo.Ingredients WHERE Name = N'Đá')
                           BEGIN
                               INSERT INTO dbo.Ingredients (Name, Unit, CurrentStock, ReorderLevel, IsActive)
                               VALUES (N'Đá', N'kg', 80.00, 15.00, 1);
                           END;

                           IF NOT EXISTS (SELECT 1 FROM dbo.Ingredients WHERE Name = N'Sữa tươi')
                           BEGIN
                               INSERT INTO dbo.Ingredients (Name, Unit, CurrentStock, ReorderLevel, IsActive)
                               VALUES (N'Sữa tươi', N'lít', 25.00, 5.00, 1);
                           END;

                           IF NOT EXISTS (SELECT 1 FROM dbo.Ingredients WHERE Name = N'Cà rốt')
                           BEGIN
                               INSERT INTO dbo.Ingredients (Name, Unit, CurrentStock, ReorderLevel, IsActive)
                               VALUES (N'Cà rốt', N'kg', 8.00, 2.00, 1);
                           END;

                           IF NOT EXISTS (SELECT 1 FROM dbo.Ingredients WHERE Name = N'Dưa hấu')
                           BEGIN
                               INSERT INTO dbo.Ingredients (Name, Unit, CurrentStock, ReorderLevel, IsActive)
                               VALUES (N'Dưa hấu', N'kg', 20.00, 5.00, 1);
                           END;

                           IF NOT EXISTS (SELECT 1 FROM dbo.Ingredients WHERE Name = N'Gia vị cà ri')
                           BEGIN
                               INSERT INTO dbo.Ingredients (Name, Unit, CurrentStock, ReorderLevel, IsActive)
                               VALUES (N'Gia vị cà ri', N'kg', 5.00, 1.00, 1);
                           END;

                           IF NOT EXISTS (SELECT 1 FROM dbo.Ingredients WHERE Name = N'Khoai tây')
                           BEGIN
                               INSERT INTO dbo.Ingredients (Name, Unit, CurrentStock, ReorderLevel, IsActive)
                               VALUES (N'Khoai tây', N'kg', 20.00, 5.00, 1);
                           END;

                           DECLARE @recipes TABLE
                           (
                               DishName NVARCHAR(200) NOT NULL,
                               IngredientName NVARCHAR(200) NOT NULL,
                               QuantityPerDish DECIMAL(18,2) NOT NULL,
                               PRIMARY KEY (DishName, IngredientName)
                           );

                           INSERT INTO @recipes (DishName, IngredientName, QuantityPerDish)
                           VALUES
                               (N'Cà ri gà', N'Thịt gà', 0.22),
                               (N'Cà ri gà', N'Gia vị cà ri', 0.02),
                               (N'Cà ri gà', N'Khoai tây', 0.12),
                               (N'Cà ri gà', N'Cà rốt', 0.06),
                               (N'Cà ri gà', N'Nước cốt dừa', 0.10),
                               (N'Cà ri gà', N'Sả', 0.01),
                               (N'Mì Xào Bò', N'Mì trứng', 0.25),
                               (N'Mì Xào Bò', N'Thịt bò', 0.16),
                               (N'Mì Xào Bò', N'Cà rốt', 0.05),
                               (N'Mì Xào Bò', N'Bắp cải', 0.05),
                               (N'Mì Xào Bò', N'Hành lá', 0.02),
                               (N'Mì Xào Bò', N'Dầu ăn', 0.03),
                               (N'Dưa hấu', N'Dưa hấu', 0.30),
                               (N'Bún Chả Hà Nội', N'Thịt heo', 0.15),
                               (N'Bún Chả Hà Nội', N'Bún', 0.30),
                               (N'Bún Chả Hà Nội', N'Rau thơm', 0.08),
                               (N'Bún Chả Hà Nội', N'Dưa chuột', 0.05),
                               (N'Bún Chả Hà Nội', N'Nước mắm', 0.03),
                               (N'Cơm Sườn Bì Chả', N'Thịt heo', 0.15),
                               (N'Cơm Sườn Bì Chả', N'Gạo', 0.20),
                               (N'Cơm Sườn Bì Chả', N'Trứng gà', 1.00),
                               (N'Cơm Sườn Bì Chả', N'Dưa chuột', 0.05),
                               (N'Cơm Sườn Bì Chả', N'Dầu ăn', 0.02),
                               (N'Bún Bò Huế', N'Thịt bò', 0.18),
                               (N'Bún Bò Huế', N'Bún', 0.30),
                               (N'Bún Bò Huế', N'Nước dùng', 0.50),
                               (N'Bún Bò Huế', N'Rau thơm', 0.05),
                               (N'Bún Bò Huế', N'Hành lá', 0.02),
                               (N'Bún Bò Huế', N'Sả', 0.01),
                               (N'Hủ Tiếu Nam Vang', N'Thịt heo', 0.10),
                               (N'Hủ Tiếu Nam Vang', N'Tôm', 0.08),
                               (N'Hủ Tiếu Nam Vang', N'Hủ tiếu', 0.30),
                               (N'Hủ Tiếu Nam Vang', N'Nước dùng', 0.45),
                               (N'Hủ Tiếu Nam Vang', N'Rau thơm', 0.05),
                               (N'Hủ Tiếu Nam Vang', N'Giá đỗ', 0.05),
                               (N'Cơm Gà Xối Mỡ', N'Thịt gà', 0.25),
                               (N'Cơm Gà Xối Mỡ', N'Gạo', 0.20),
                               (N'Cơm Gà Xối Mỡ', N'Dưa chuột', 0.05),
                               (N'Cơm Gà Xối Mỡ', N'Rau thơm', 0.03),
                               (N'Nem Rán', N'Thịt heo', 0.10),
                               (N'Nem Rán', N'Bánh đa nem', 0.05),
                               (N'Nem Rán', N'Trứng gà', 0.50),
                               (N'Nem Rán', N'Dầu ăn', 0.05),
                               (N'Gỏi Cuốn', N'Tôm', 0.08),
                               (N'Gỏi Cuốn', N'Bánh tráng', 0.05),
                               (N'Gỏi Cuốn', N'Rau xà lách', 0.05),
                               (N'Gỏi Cuốn', N'Rau thơm', 0.03),
                               (N'Salad Rau Củ', N'Rau xà lách', 0.10),
                               (N'Salad Rau Củ', N'Dưa chuột', 0.05),
                               (N'Salad Rau Củ', N'Cà rốt', 0.05),
                               (N'Salad Rau Củ', N'Bắp cải', 0.05),
                               (N'Chè Khúc Bạch', N'Nước cốt dừa', 0.10),
                               (N'Chè Khúc Bạch', N'Thạch', 0.05),
                               (N'Chè Khúc Bạch', N'Đường', 0.03),
                               (N'Chè Khúc Bạch', N'Sữa tươi', 0.10),
                               (N'Bánh Flan', N'Trứng gà', 2.00),
                               (N'Bánh Flan', N'Sữa đặc', 0.10),
                               (N'Bánh Flan', N'Đường', 0.05),
                               (N'Bánh Flan', N'Sữa tươi', 0.15),
                               (N'Chè Thái', N'Thạch', 0.08),
                               (N'Chè Thái', N'Nước cốt dừa', 0.12),
                               (N'Chè Thái', N'Đường', 0.04),
                               (N'Chè Thái', N'Đậu đỏ', 0.05),
                               (N'Nước cam vắt', N'Cam tươi', 0.15),
                               (N'Nước cam vắt', N'Đường', 0.02),
                               (N'Nước cam vắt', N'Đá', 0.15),
                               (N'Trà Đá', N'Trà', 0.01),
                               (N'Trà Đá', N'Đường', 0.01),
                               (N'Trà Đá', N'Đá', 0.20),
                               (N'Cà Phê Sữa Đá', N'Cà phê', 0.02),
                               (N'Cà Phê Sữa Đá', N'Sữa đặc', 0.05),
                               (N'Cà Phê Sữa Đá', N'Đường', 0.02),
                               (N'Cà Phê Sữa Đá', N'Đá', 0.20),
                               (N'Sinh Tố Bơ', N'Bơ', 0.20),
                               (N'Sinh Tố Bơ', N'Sữa đặc', 0.08),
                               (N'Sinh Tố Bơ', N'Đường', 0.03),
                               (N'Sinh Tố Bơ', N'Đá', 0.15),
                               (N'Chả Cá', N'Cá', 0.18),
                               (N'Chả Cá', N'Hành lá', 0.02),
                               (N'Chả Cá', N'Tỏi', 0.01),
                               (N'Chả Cá', N'Hành tím', 0.01),
                               (N'Chả Cá', N'Dầu ăn', 0.03),
                               (N'Chả Cá', N'Muối', 0.01),
                               (N'Chả Cá', N'Tiêu', 0.01),
                               (N'Phở Bò', N'Thịt bò', 0.20),
                               (N'Phở Bò', N'Bánh phở', 0.30),
                               (N'Phở Bò', N'Nước dùng', 0.50),
                               (N'Phở Bò', N'Rau thơm', 0.03),
                               (N'Phở Bò', N'Hành lá', 0.02),
                               (N'Phở Bò', N'Giá đỗ', 0.05),
                               (N'Phở Bò', N'Nước mắm', 0.02);

                           INSERT INTO dbo.DishIngredients (DishID, IngredientID, QuantityPerDish)
                           SELECT d.DishID, i.IngredientID, r.QuantityPerDish
                           FROM @recipes r
                           INNER JOIN dbo.Dishes d ON d.Name = r.DishName
                           INNER JOIN dbo.Ingredients i ON i.Name = r.IngredientName
                           WHERE NOT EXISTS
                           (
                               SELECT 1
                               FROM dbo.DishIngredients di
                               WHERE di.DishID = d.DishID
                                 AND di.IngredientID = i.IngredientID
                           );
                           """;

        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task EnsureRecommendationMenuDataAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        const string sql = """
                           DECLARE @categories TABLE
                           (
                               Name NVARCHAR(200) NOT NULL,
                               Description NVARCHAR(500) NOT NULL,
                               DisplayOrder INT NOT NULL,
                               PRIMARY KEY (Name)
                           );

                           INSERT INTO @categories (Name, Description, DisplayOrder)
                           VALUES
                               (N'Món chính', N'Các món ăn chính phù hợp bữa trưa và bữa tối.', 1),
                               (N'Món nước', N'Các món nước dễ ăn, phù hợp bữa sáng hoặc bữa nhẹ.', 2),
                               (N'Món xào', N'Các món xào nóng, no lâu và nhiều năng lượng.', 3),
                               (N'Khai vị', N'Món nhẹ, ăn kèm hoặc dùng buổi chiều.', 4),
                               (N'Tráng miệng', N'Món ngọt và trái cây thanh mát sau bữa ăn.', 5),
                               (N'Đồ uống', N'Đồ uống giải khát dùng kèm món chính hoặc món cay.', 6),
                               (N'Món lẩu', N'Món dùng chung cho bữa tối hoặc nhóm đông.', 7),
                               (N'Món rau', N'Món rau cân bằng dinh dưỡng, thanh nhẹ và dễ ăn.', 8),
                               (N'Món gọi thêm', N'Các món ăn kèm hoặc phần thêm như cơm, bún, rau, trứng, nước dùng, nước chấm.', 9);

                           INSERT INTO dbo.Categories (Name, Description, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
                           SELECT c.Name, c.Description, c.DisplayOrder, 1, GETDATE(), GETDATE()
                           FROM @categories c
                           WHERE NOT EXISTS
                           (
                               SELECT 1
                               FROM dbo.Categories existing
                               WHERE LOWER(REPLACE(LTRIM(RTRIM(existing.Name)), N' ', N'')) COLLATE Latin1_General_100_CI_AI =
                                     LOWER(REPLACE(LTRIM(RTRIM(c.Name)), N' ', N'')) COLLATE Latin1_General_100_CI_AI
                           );

                           DECLARE @ingredients TABLE
                           (
                               Name NVARCHAR(200) NOT NULL,
                               Unit NVARCHAR(50) NOT NULL,
                               CurrentStock DECIMAL(18,2) NOT NULL,
                               ReorderLevel DECIMAL(18,2) NOT NULL,
                               IssueMethod VARCHAR(10) NOT NULL,
                               PRIMARY KEY (Name)
                           );

                           INSERT INTO @ingredients (Name, Unit, CurrentStock, ReorderLevel, IssueMethod)
                           VALUES
                               (N'Bánh mì', N'ổ', 80.00, 20.00, 'FIFO'),
                               (N'Bột chiên giòn', N'kg', 12.00, 3.00, 'FIFO'),
                               (N'Cải thìa', N'kg', 15.00, 4.00, 'FEFO'),
                               (N'Cải xanh', N'kg', 15.00, 4.00, 'FEFO'),
                               (N'Cá kho', N'kg', 18.00, 4.00, 'FEFO'),
                               (N'Cua', N'kg', 10.00, 2.00, 'FEFO'),
                               (N'Đậu xanh', N'kg', 10.00, 2.00, 'FIFO'),
                               (N'Hải sản', N'kg', 20.00, 5.00, 'FEFO'),
                               (N'Lá é', N'kg', 8.00, 2.00, 'FEFO'),
                               (N'Mực', N'kg', 16.00, 4.00, 'FEFO'),
                               (N'Ngó sen', N'kg', 12.00, 3.00, 'FEFO'),
                               (N'Ớt', N'kg', 6.00, 1.00, 'FEFO'),
                               (N'Rau muống', N'kg', 18.00, 5.00, 'FEFO'),
                               (N'Rau củ hỗn hợp', N'kg', 18.00, 5.00, 'FEFO'),
                               (N'Sườn heo', N'kg', 20.00, 5.00, 'FEFO'),
                               (N'Thịt bằm', N'kg', 15.00, 4.00, 'FEFO'),
                               (N'Trà đào', N'lít', 25.00, 5.00, 'FEFO'),
                               (N'Tắc', N'kg', 12.00, 3.00, 'FEFO'),
                               (N'Rau cải', N'kg', 15.00, 4.00, 'FEFO'),
                               (N'Đồ chua', N'kg', 10.00, 2.00, 'FEFO');

                           INSERT INTO dbo.Ingredients (Name, Unit, CurrentStock, ReorderLevel, IsActive, IssueMethod)
                           SELECT i.Name, i.Unit, i.CurrentStock, i.ReorderLevel, 1, i.IssueMethod
                           FROM @ingredients i
                           WHERE NOT EXISTS
                           (
                               SELECT 1
                               FROM dbo.Ingredients existing
                               WHERE LOWER(REPLACE(LTRIM(RTRIM(existing.Name)), N' ', N'')) COLLATE Latin1_General_100_CI_AI =
                                     LOWER(REPLACE(LTRIM(RTRIM(i.Name)), N' ', N'')) COLLATE Latin1_General_100_CI_AI
                           );

                           DECLARE @dishes TABLE
                           (
                               Name NVARCHAR(200) NOT NULL,
                               CategoryName NVARCHAR(200) NOT NULL,
                               Price DECIMAL(18,2) NOT NULL,
                               Unit NVARCHAR(50) NOT NULL,
                               IsVegetarian BIT NOT NULL,
                               IsDailySpecial BIT NOT NULL,
                               Description NVARCHAR(1000) NOT NULL,
                               Image NVARCHAR(500) NULL,
                               PRIMARY KEY (Name)
                           );

                           INSERT INTO @dishes (Name, CategoryName, Price, Unit, IsVegetarian, IsDailySpecial, Description, Image)
                           VALUES
                               (N'Phở gà', N'Món nước', 55000, N'Tô', 0, 0, N'Phù hợp bữa sáng, món nước dễ ăn với thịt gà mềm, nước dùng thanh và no lâu.', N'/images/pho-ga.jpg'),
                               (N'Bánh mì ốp la', N'Món chính', 35000, N'Phần', 0, 0, N'Phù hợp bữa sáng hoặc món nhẹ cho trẻ em, dễ ăn, nhanh gọn và đủ năng lượng.', N'/images/banh-mi-op-la.jpg'),
                               (N'Cháo gà', N'Món nước', 40000, N'Tô', 0, 0, N'Món sáng nhẹ bụng, ấm nóng, phù hợp trẻ em hoặc khách muốn món dễ tiêu.', N'/images/chao-ga.jpg'),
                               (N'Cơm bò lúc lắc', N'Món chính', 75000, N'Phần', 0, 1, N'Phù hợp bữa trưa, nhiều năng lượng với bò mềm, rau củ và cơm nóng.', N'/images/com-bo-luc-lac.jpg'),
                               (N'Cơm sườn nướng', N'Món chính', 65000, N'Phần', 0, 0, N'Món cơm phổ biến cho bữa trưa, sườn nướng đậm vị, dễ chọn khi đi nhóm.', N'/images/com-suon-nuong.jpg'),
                               (N'Cơm cá kho tộ', N'Món chính', 65000, N'Phần', 0, 0, N'Phù hợp bữa trưa hoặc bữa tối, vị mặn ngọt quen thuộc, ăn cùng cơm nóng.', N'/images/com-ca-kho-to.jpg'),
                               (N'Cơm chiên hải sản', N'Món chính', 70000, N'Phần', 0, 0, N'Món no lâu cho bữa trưa, hải sản thơm và dễ chia sẻ khi đi nhóm.', N'/images/com-chien-hai-san.jpg'),
                               (N'Khoai tây chiên', N'Khai vị', 35000, N'Phần', 1, 0, N'Món nhẹ buổi chiều, giòn dễ ăn, phù hợp trẻ em hoặc dùng kèm đồ uống.', N'/images/khoai-tay-chien.jpg'),
                               (N'Chả giò hải sản', N'Khai vị', 55000, N'Phần', 0, 0, N'Khai vị giòn thơm, phù hợp nhóm đông và dùng trước món chính.', N'/images/cha-gio-hai-san.jpg'),
                               (N'Súp cua', N'Khai vị', 45000, N'Chén', 0, 0, N'Món nhẹ ấm bụng, phù hợp trẻ em, bữa xế hoặc khai vị trước bữa tối.', N'/images/sup-cua.jpg'),
                               (N'Gỏi ngó sen tôm thịt', N'Khai vị', 65000, N'Phần', 0, 0, N'Món thanh mát buổi chiều, vị chua ngọt dễ ăn, phù hợp ăn kèm món cay.', N'/images/goi-ngo-sen-tom-thit.jpg'),
                               (N'Lẩu thái hải sản', N'Món lẩu', 250000, N'Nồi', 0, 1, N'Món cay, đậm vị, phù hợp bữa tối hoặc nhóm đông cùng chia sẻ.', N'/images/lau-thai-hai-san.jpg'),
                               (N'Lẩu gà lá é', N'Món lẩu', 220000, N'Nồi', 0, 0, N'Phù hợp bữa tối hoặc nhóm đông, nước lẩu thơm lá é, vị thanh ấm.', N'/images/lau-ga-la-e.jpg'),
                               (N'Gà nướng muối ớt', N'Món chính', 120000, N'Phần', 0, 1, N'Món cay đậm vị, phù hợp bữa tối, nhóm đông hoặc khách thích món nướng.', N'/images/ga-nuong-muoi-ot.jpg'),
                               (N'Bò né', N'Món chính', 85000, N'Phần', 0, 0, N'Món nóng nhiều năng lượng, phù hợp bữa sáng muộn hoặc bữa trưa.', N'/images/bo-ne.jpg'),
                               (N'Mực xào chua ngọt', N'Món xào', 95000, N'Phần', 0, 0, N'Món xào chua ngọt dễ ăn, phù hợp bữa tối hoặc ăn chung theo nhóm.', N'/images/muc-xao-chua-ngot.jpg'),
                               (N'Bò xào rau củ', N'Món xào', 90000, N'Phần', 0, 0, N'Món xào cân bằng thịt bò và rau củ, phù hợp bữa trưa hoặc bữa tối.', N'/images/bo-xao-rau-cu.jpg'),
                               (N'Rau muống xào tỏi', N'Món rau', 40000, N'Phần', 1, 0, N'Món rau phổ biến, thanh nhẹ, phù hợp ăn kèm cơm và món mặn.', N'/images/rau-muong-xao-toi.jpg'),
                               (N'Cải thìa xào dầu hào', N'Món rau', 45000, N'Phần', 1, 0, N'Món rau thanh mát, cân bằng dinh dưỡng, phù hợp ăn kèm món chính.', N'/images/cai-thia-xao-dau-hao.jpg'),
                               (N'Canh chua cá', N'Món chính', 70000, N'Tô', 0, 0, N'Món canh thanh mát cho bữa trưa hoặc bữa tối, hợp khi ăn cùng cơm.', N'/images/canh-chua-ca.jpg'),
                               (N'Canh cải thịt bằm', N'Món chính', 55000, N'Tô', 0, 0, N'Món canh nhẹ, dễ ăn, phù hợp trẻ em và bữa cơm gia đình.', N'/images/canh-cai-thit-bam.jpg'),
                               (N'Chè đậu xanh', N'Tráng miệng', 30000, N'Ly', 1, 0, N'Món tráng miệng thanh mát sau bữa ăn, vị ngọt nhẹ và dễ dùng.', N'/images/che-dau-xanh.jpg'),
                               (N'Trà đào', N'Đồ uống', 35000, N'Ly', 1, 0, N'Đồ uống giải khát buổi chiều, vị trái cây thanh mát, phù hợp ăn kèm món cay.', N'/images/tra-dao.jpg'),
                               (N'Trà tắc', N'Đồ uống', 25000, N'Ly', 1, 0, N'Đồ uống giải khát phổ biến, chua ngọt nhẹ, hợp dùng kèm món chiên hoặc món cay.', N'/images/tra-tac.jpg'),
                               (N'Thêm cơm', N'Món gọi thêm', 10000, N'Phần', 1, 0, N'Phần cơm trắng gọi thêm, phù hợp ăn kèm các món chính.', N'/images/them-com.jpg'),
                               (N'Thêm bún', N'Món gọi thêm', 10000, N'Phần', 1, 0, N'Phần bún gọi thêm, phù hợp ăn kèm món nước, lẩu hoặc món nướng.', N'/images/them-bun.jpg'),
                               (N'Thêm bánh phở', N'Món gọi thêm', 12000, N'Phần', 1, 0, N'Phần bánh phở gọi thêm, phù hợp với các món phở hoặc món nước.', N'/images/them-banh-pho.jpg'),
                               (N'Thêm mì', N'Món gọi thêm', 12000, N'Phần', 1, 0, N'Phần mì gọi thêm, phù hợp ăn kèm lẩu hoặc món nước.', N'/images/them-mi.jpg'),
                               (N'Thêm trứng', N'Món gọi thêm', 8000, N'Phần', 0, 0, N'Trứng gọi thêm, phù hợp ăn kèm cơm, mì hoặc món nước.', N'/images/them-trung.jpg'),
                               (N'Thêm rau', N'Món gọi thêm', 10000, N'Phần', 1, 0, N'Phần rau ăn kèm gọi thêm, giúp bữa ăn cân bằng và thanh mát hơn.', N'/images/them-rau.jpg'),
                               (N'Thêm nước dùng', N'Món gọi thêm', 8000, N'Phần', 1, 0, N'Phần nước dùng gọi thêm cho các món nước hoặc lẩu.', N'/images/them-nuoc-dung.jpg'),
                               (N'Thêm dưa leo', N'Món gọi thêm', 5000, N'Phần', 1, 0, N'Dưa leo ăn kèm, phù hợp với cơm và món nướng.', N'/images/them-dua-leo.jpg'),
                               (N'Thêm đồ chua', N'Món gọi thêm', 5000, N'Phần', 1, 0, N'Đồ chua ăn kèm, giúp món ăn đỡ ngấy và cân bằng vị.', N'/images/them-do-chua.jpg'),
                               (N'Thêm nước chấm', N'Món gọi thêm', 5000, N'Phần', 1, 0, N'Phần nước chấm gọi thêm, phù hợp ăn kèm món cuốn, món nướng hoặc món chiên.', N'/images/them-nuoc-cham.jpg');

                           INSERT INTO dbo.Dishes (Name, Price, Available, Image, Description, Unit, IsVegetarian, IsDailySpecial, IsActive, CreatedAt, UpdatedAt, CategoryID)
                           SELECT d.Name, d.Price, 1, d.Image, d.Description, d.Unit, d.IsVegetarian, d.IsDailySpecial, 1, GETDATE(), GETDATE(), c.CategoryID
                           FROM @dishes d
                           INNER JOIN dbo.Categories c
                               ON LOWER(REPLACE(LTRIM(RTRIM(c.Name)), N' ', N'')) COLLATE Latin1_General_100_CI_AI =
                                  LOWER(REPLACE(LTRIM(RTRIM(d.CategoryName)), N' ', N'')) COLLATE Latin1_General_100_CI_AI
                           WHERE NOT EXISTS
                           (
                               SELECT 1
                               FROM dbo.Dishes existing
                               WHERE LOWER(REPLACE(LTRIM(RTRIM(existing.Name)), N' ', N'')) COLLATE Latin1_General_100_CI_AI =
                                     LOWER(REPLACE(LTRIM(RTRIM(d.Name)), N' ', N'')) COLLATE Latin1_General_100_CI_AI
                           );

                           DECLARE @descriptions TABLE
                           (
                               MatchName NVARCHAR(200) NOT NULL,
                               Description NVARCHAR(1000) NOT NULL,
                               PRIMARY KEY (MatchName)
                           );

                           INSERT INTO @descriptions (MatchName, Description)
                           VALUES
                               (N'Phở Bò Đặc Biệt', N'Phù hợp bữa sáng, món nước phổ biến với nước dùng đậm đà, thịt bò mềm và no lâu.'),
                               (N'Bún Bò Huế', N'Món cay đậm vị, phù hợp bữa sáng hoặc bữa trưa cho khách thích hương vị miền Trung.'),
                               (N'Hủ Tiếu Nam Vang', N'Phù hợp bữa sáng, món nước dễ ăn với tôm thịt, nước dùng thanh và no vừa đủ.'),
                               (N'Cơm Gà Xối Mỡ', N'Phù hợp bữa trưa, cung cấp nhiều năng lượng với cơm nóng và gà giòn thơm.'),
                               (N'Mì Xào Bò', N'Món xào no lâu, phù hợp bữa trưa hoặc bữa tối với thịt bò và rau củ.'),
                               (N'Gỏi Cuốn', N'Món nhẹ buổi chiều, thanh mát và dễ ăn, phù hợp khách muốn lựa chọn ít dầu mỡ.'),
                               (N'Salad Rau Củ', N'Món rau thanh mát, phù hợp ăn nhẹ, cân bằng dinh dưỡng hoặc ăn kèm món chính.'),
                               (N'Chè Thái', N'Món tráng miệng thanh mát sau bữa ăn, vị ngọt béo và nhiều topping.'),
                               (N'Dưa hấu', N'Món tráng miệng trái cây thanh mát, phù hợp sau bữa ăn hoặc khi dùng món cay.'),
                               (N'Sinh Tố Bơ', N'Đồ uống bổ dưỡng, phù hợp buổi chiều, trẻ em hoặc khách muốn món béo nhẹ.'),
                               (N'Nước cam vắt', N'Đồ uống giải khát giàu vitamin, phù hợp bữa trưa hoặc ăn kèm món chiên.'),
                               (N'Cà Phê Sữa Đá', N'Đồ uống phổ biến buổi sáng hoặc buổi chiều, vị đậm và tỉnh táo.');

                           UPDATE existing
                           SET Description = d.Description,
                               UpdatedAt = GETDATE()
                           FROM dbo.Dishes existing
                           INNER JOIN @descriptions d
                               ON LOWER(REPLACE(LTRIM(RTRIM(existing.Name)), N' ', N'')) COLLATE Latin1_General_100_CI_AI =
                                  LOWER(REPLACE(LTRIM(RTRIM(d.MatchName)), N' ', N'')) COLLATE Latin1_General_100_CI_AI
                           WHERE existing.Description IS NULL
                              OR LEN(LTRIM(RTRIM(existing.Description))) < 55;

                           DECLARE @recipes TABLE
                           (
                               DishName NVARCHAR(200) NOT NULL,
                               IngredientName NVARCHAR(200) NOT NULL,
                               QuantityPerDish DECIMAL(18,2) NOT NULL,
                               PRIMARY KEY (DishName, IngredientName)
                           );

                           INSERT INTO @recipes (DishName, IngredientName, QuantityPerDish)
                           VALUES
                               (N'Phở gà', N'Thịt gà', 0.18), (N'Phở gà', N'Bánh phở', 0.30), (N'Phở gà', N'Nước dùng', 0.50), (N'Phở gà', N'Rau thơm', 0.03),
                               (N'Bánh mì ốp la', N'Bánh mì', 1.00), (N'Bánh mì ốp la', N'Trứng gà', 2.00), (N'Bánh mì ốp la', N'Dưa chuột', 0.04),
                               (N'Cháo gà', N'Thịt gà', 0.12), (N'Cháo gà', N'Gạo', 0.12), (N'Cháo gà', N'Hành lá', 0.01),
                               (N'Cơm bò lúc lắc', N'Thịt bò', 0.20), (N'Cơm bò lúc lắc', N'Gạo', 0.20), (N'Cơm bò lúc lắc', N'Rau củ hỗn hợp', 0.08),
                               (N'Cơm sườn nướng', N'Sườn heo', 0.22), (N'Cơm sườn nướng', N'Gạo', 0.20), (N'Cơm sườn nướng', N'Dưa chuột', 0.05),
                               (N'Cơm cá kho tộ', N'Cá kho', 0.20), (N'Cơm cá kho tộ', N'Gạo', 0.20), (N'Cơm cá kho tộ', N'Nước mắm', 0.02),
                               (N'Cơm chiên hải sản', N'Hải sản', 0.15), (N'Cơm chiên hải sản', N'Gạo', 0.22), (N'Cơm chiên hải sản', N'Trứng gà', 1.00),
                               (N'Khoai tây chiên', N'Khoai tây', 0.25), (N'Khoai tây chiên', N'Dầu ăn', 0.05), (N'Khoai tây chiên', N'Muối', 0.01),
                               (N'Chả giò hải sản', N'Hải sản', 0.12), (N'Chả giò hải sản', N'Bánh đa nem', 0.06), (N'Chả giò hải sản', N'Dầu ăn', 0.05),
                               (N'Súp cua', N'Cua', 0.08), (N'Súp cua', N'Trứng gà', 1.00), (N'Súp cua', N'Nước dùng', 0.30),
                               (N'Gỏi ngó sen tôm thịt', N'Ngó sen', 0.12), (N'Gỏi ngó sen tôm thịt', N'Tôm', 0.08), (N'Gỏi ngó sen tôm thịt', N'Thịt heo', 0.08),
                               (N'Lẩu thái hải sản', N'Hải sản', 0.45), (N'Lẩu thái hải sản', N'Nước dùng', 1.20), (N'Lẩu thái hải sản', N'Ớt', 0.03), (N'Lẩu thái hải sản', N'Rau thơm', 0.10),
                               (N'Lẩu gà lá é', N'Thịt gà', 0.50), (N'Lẩu gà lá é', N'Lá é', 0.08), (N'Lẩu gà lá é', N'Nước dùng', 1.20),
                               (N'Gà nướng muối ớt', N'Thịt gà', 0.45), (N'Gà nướng muối ớt', N'Ớt', 0.03), (N'Gà nướng muối ớt', N'Muối', 0.02),
                               (N'Bò né', N'Thịt bò', 0.22), (N'Bò né', N'Trứng gà', 1.00), (N'Bò né', N'Bánh mì', 1.00),
                               (N'Mực xào chua ngọt', N'Mực', 0.22), (N'Mực xào chua ngọt', N'Rau củ hỗn hợp', 0.10), (N'Mực xào chua ngọt', N'Tỏi', 0.01),
                               (N'Bò xào rau củ', N'Thịt bò', 0.20), (N'Bò xào rau củ', N'Rau củ hỗn hợp', 0.15), (N'Bò xào rau củ', N'Tỏi', 0.01),
                               (N'Rau muống xào tỏi', N'Rau muống', 0.20), (N'Rau muống xào tỏi', N'Tỏi', 0.02), (N'Rau muống xào tỏi', N'Dầu ăn', 0.02),
                               (N'Cải thìa xào dầu hào', N'Cải thìa', 0.20), (N'Cải thìa xào dầu hào', N'Dầu ăn', 0.02), (N'Cải thìa xào dầu hào', N'Tỏi', 0.01),
                               (N'Canh chua cá', N'Cá', 0.18), (N'Canh chua cá', N'Nước dùng', 0.50), (N'Canh chua cá', N'Rau thơm', 0.03),
                               (N'Canh cải thịt bằm', N'Cải xanh', 0.18), (N'Canh cải thịt bằm', N'Thịt bằm', 0.10), (N'Canh cải thịt bằm', N'Nước dùng', 0.45),
                               (N'Chè đậu xanh', N'Đậu xanh', 0.10), (N'Chè đậu xanh', N'Đường', 0.04), (N'Chè đậu xanh', N'Nước cốt dừa', 0.05),
                               (N'Trà đào', N'Trà đào', 0.20), (N'Trà đào', N'Đá', 0.15), (N'Trà đào', N'Đường', 0.02),
                               (N'Trà tắc', N'Trà', 0.01), (N'Trà tắc', N'Tắc', 0.06), (N'Trà tắc', N'Đá', 0.15),
                               (N'Thêm cơm', N'Gạo', 0.20),
                               (N'Thêm bún', N'Bún', 0.22),
                               (N'Thêm bánh phở', N'Bánh phở', 0.22),
                               (N'Thêm mì', N'Mì trứng', 0.18),
                               (N'Thêm trứng', N'Trứng gà', 1.00),
                               (N'Thêm rau', N'Rau cải', 0.10),
                               (N'Thêm rau', N'Rau thơm', 0.02),
                               (N'Thêm nước dùng', N'Nước dùng', 0.35),
                               (N'Thêm dưa leo', N'Dưa chuột', 0.09),
                               (N'Thêm đồ chua', N'Đồ chua', 0.06),
                               (N'Thêm nước chấm', N'Nước mắm', 0.03),
                               (N'Thêm nước chấm', N'Đường', 0.01),
                               (N'Thêm nước chấm', N'Tỏi', 0.01),
                               (N'Thêm nước chấm', N'Ớt', 0.01);

                           INSERT INTO dbo.DishIngredients (DishID, IngredientID, QuantityPerDish)
                           SELECT d.DishID, i.IngredientID, r.QuantityPerDish
                           FROM @recipes r
                           INNER JOIN dbo.Dishes d
                               ON LOWER(REPLACE(LTRIM(RTRIM(d.Name)), N' ', N'')) COLLATE Latin1_General_100_CI_AI =
                                  LOWER(REPLACE(LTRIM(RTRIM(r.DishName)), N' ', N'')) COLLATE Latin1_General_100_CI_AI
                           INNER JOIN dbo.Ingredients i
                               ON LOWER(REPLACE(LTRIM(RTRIM(i.Name)), N' ', N'')) COLLATE Latin1_General_100_CI_AI =
                                  LOWER(REPLACE(LTRIM(RTRIM(r.IngredientName)), N' ', N'')) COLLATE Latin1_General_100_CI_AI
                           WHERE NOT EXISTS
                           (
                               SELECT 1
                               FROM dbo.DishIngredients existing
                               WHERE existing.DishID = d.DishID
                                 AND existing.IngredientID = i.IngredientID
                           );
                           """;

        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task EnsureIngredientBatchSchemaAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        const string createBatchesSql = """
                                        IF OBJECT_ID(N'dbo.IngredientBatches', N'U') IS NULL
                                        BEGIN
                                            CREATE TABLE dbo.IngredientBatches
                                            (
                                                BatchID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IngredientBatches PRIMARY KEY,
                                                IngredientID INT NOT NULL,
                                                BatchCode NVARCHAR(100) NULL,
                                                QuantityInitial DECIMAL(18,2) NOT NULL,
                                                QuantityRemaining DECIMAL(18,2) NOT NULL,
                                                Unit NVARCHAR(50) NOT NULL,
                                                ExpiryDate DATE NOT NULL,
                                                ReceivedDate DATE NOT NULL,
                                                SupplierName NVARCHAR(200) NULL,
                                                IsActive BIT NOT NULL CONSTRAINT DF_IngredientBatches_IsActive DEFAULT(1),
                                                CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_IngredientBatches_CreatedAt DEFAULT(SYSUTCDATETIME()),
                                                UpdatedAt DATETIME2 NULL,
                                                RowVersion ROWVERSION NOT NULL,
                                                CONSTRAINT FK_IngredientBatches_Ingredients FOREIGN KEY (IngredientID) REFERENCES dbo.Ingredients(IngredientID),
                                                CONSTRAINT CK_IngredientBatches_QuantityInitial_NonNegative CHECK (QuantityInitial >= 0),
                                                CONSTRAINT CK_IngredientBatches_QuantityRemaining_NonNegative CHECK (QuantityRemaining >= 0),
                                                CONSTRAINT CK_IngredientBatches_QuantityRemaining_MaxInitial CHECK (QuantityRemaining <= QuantityInitial)
                                            );
                                        END
                                        """;

        const string createMovementsSql = """
                                          IF OBJECT_ID(N'dbo.IngredientStockMovements', N'U') IS NULL
                                          BEGIN
                                              CREATE TABLE dbo.IngredientStockMovements
                                              (
                                                  MovementID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IngredientStockMovements PRIMARY KEY,
                                                  IngredientID INT NOT NULL,
                                                  BatchID INT NULL,
                                                  QuantityChange DECIMAL(18,2) NOT NULL,
                                                  MovementType NVARCHAR(50) NOT NULL,
                                                  ReferenceType NVARCHAR(50) NULL,
                                                  ReferenceID INT NULL,
                                                  OrderID INT NULL,
                                                  OrderItemID INT NULL,
                                                  DishID INT NULL,
                                                  CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_IngredientStockMovements_CreatedAt DEFAULT(SYSUTCDATETIME()),
                                                  Note NVARCHAR(500) NULL,
                                                  CONSTRAINT FK_IngredientStockMovements_Ingredients FOREIGN KEY (IngredientID) REFERENCES dbo.Ingredients(IngredientID),
                                                  CONSTRAINT FK_IngredientStockMovements_IngredientBatches FOREIGN KEY (BatchID) REFERENCES dbo.IngredientBatches(BatchID)
                                              );
                                          END
                                          """;

        const string indexSql = """
                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.IngredientBatches') AND name = N'IX_IngredientBatches_Ingredient_Fefo')
                                    CREATE INDEX IX_IngredientBatches_Ingredient_Fefo ON dbo.IngredientBatches(IngredientID, IsActive, ExpiryDate, ReceivedDate, BatchID);

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.IngredientBatches') AND name = N'IX_IngredientBatches_ExpiryDate')
                                    CREATE INDEX IX_IngredientBatches_ExpiryDate ON dbo.IngredientBatches(ExpiryDate);

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.IngredientStockMovements') AND name = N'IX_IngredientStockMovements_Ingredient_CreatedAt')
                                    CREATE INDEX IX_IngredientStockMovements_Ingredient_CreatedAt ON dbo.IngredientStockMovements(IngredientID, CreatedAt DESC);

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.IngredientStockMovements') AND name = N'IX_IngredientStockMovements_BatchID')
                                    CREATE INDEX IX_IngredientStockMovements_BatchID ON dbo.IngredientStockMovements(BatchID);
                                """;

        await db.Database.ExecuteSqlRawAsync(createBatchesSql, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(createMovementsSql, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(indexSql, cancellationToken);
    }

    private static async Task EnsureBusinessAuditTableAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        const string createSql = """
                                 IF OBJECT_ID(N'dbo.BusinessAuditLogs', N'U') IS NULL
                                 BEGIN
                                     CREATE TABLE dbo.BusinessAuditLogs
                                     (
                                         BusinessAuditLogId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                         CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CatalogBusinessAuditLogs_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                                         ActionType VARCHAR(100) NOT NULL,
                                         EntityType VARCHAR(50) NOT NULL,
                                         EntityId NVARCHAR(100) NOT NULL,
                                         ActorType VARCHAR(30) NULL,
                                         ActorId INT NULL,
                                         ActorCode NVARCHAR(100) NULL,
                                         ActorName NVARCHAR(200) NULL,
                                         ActorRoleCode VARCHAR(50) NULL,
                                         BranchId INT NULL,
                                         TableId INT NULL,
                                         OrderId INT NULL,
                                         OrderItemId INT NULL,
                                         DishId INT NULL,
                                         BillId INT NULL,
                                         DiningSessionCode VARCHAR(64) NULL,
                                         CorrelationId VARCHAR(100) NULL,
                                         IdempotencyKey VARCHAR(100) NULL,
                                         Notes NVARCHAR(500) NULL,
                                         BeforeState NVARCHAR(MAX) NULL,
                                         AfterState NVARCHAR(MAX) NULL
                                     );
                                 END
                                 """;

        const string addBranchIdColumnSql = """
                                            IF COL_LENGTH(N'dbo.BusinessAuditLogs', N'BranchId') IS NULL
                                            BEGIN
                                                ALTER TABLE dbo.BusinessAuditLogs ADD BranchId INT NULL;
                                            END
                                            """;

        const string indexSql = """
                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_CreatedAtUtc')
                                    CREATE INDEX IX_BusinessAuditLogs_CreatedAtUtc ON dbo.BusinessAuditLogs(CreatedAtUtc);

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_Entity')
                                    CREATE INDEX IX_BusinessAuditLogs_Entity ON dbo.BusinessAuditLogs(EntityType, EntityId);

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_DishId')
                                    CREATE INDEX IX_BusinessAuditLogs_DishId ON dbo.BusinessAuditLogs(DishId);

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_TableId')
                                    CREATE INDEX IX_BusinessAuditLogs_TableId ON dbo.BusinessAuditLogs(TableId);

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_BranchId')
                                    CREATE INDEX IX_BusinessAuditLogs_BranchId ON dbo.BusinessAuditLogs(BranchId);
                                """;

        await db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(addBranchIdColumnSql, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(indexSql, cancellationToken);
    }

    private static async Task EnsureTableNumberSchemaAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        const string ensureColumnSql = """
                                       IF COL_LENGTH(N'dbo.DiningTables', N'TableNumber') IS NULL
                                       BEGIN
                                           ALTER TABLE dbo.DiningTables ADD TableNumber INT NULL;
                                       END
                                       """;

        const string backfillAndIndexSql = """
                                           ;WITH ordered AS
                                           (
                                               SELECT
                                                   TableID,
                                                   TableNumber = ROW_NUMBER() OVER (PARTITION BY BranchID ORDER BY TableID)
                                               FROM dbo.DiningTables
                                           )
                                           UPDATE dt
                                           SET
                                               dt.TableNumber = ordered.TableNumber
                                           FROM dbo.DiningTables dt
                                           INNER JOIN ordered ON ordered.TableID = dt.TableID
                                           WHERE dt.TableNumber IS NULL OR dt.TableNumber <= 0;

                                           IF EXISTS (SELECT 1 FROM dbo.DiningTables WHERE TableNumber IS NULL OR TableNumber <= 0)
                                           BEGIN
                                               THROW 50001, N'Không thể khởi tạo số bàn theo chi nhánh.', 1;
                                           END

                                           IF NOT EXISTS
                                           (
                                               SELECT 1
                                               FROM sys.indexes
                                               WHERE object_id = OBJECT_ID(N'dbo.DiningTables')
                                                 AND name = N'UQ_DiningTables_Branch_TableNumber'
                                           )
                                           BEGIN
                                               CREATE UNIQUE INDEX UQ_DiningTables_Branch_TableNumber
                                                   ON dbo.DiningTables(BranchID, TableNumber);
                                           END
                                           """;

        const string syncViewSql = """
                                   IF OBJECT_ID(N'dbo.TableNumbers', N'V') IS NOT NULL
                                   BEGIN
                                       EXEC(N'
                                           ALTER VIEW dbo.TableNumbers AS
                                           SELECT
                                               dt.TableID,
                                               dt.BranchID,
                                               b.Name AS BranchName,
                                               dt.TableNumber,
                                               dt.NumberOfSeats,
                                               dt.QRCode,
                                               ts.StatusName,
                                               dt.CurrentOrderID,
                                               dt.IsActive
                                           FROM dbo.DiningTables dt
                                           INNER JOIN dbo.Branches b ON dt.BranchID = b.BranchID
                                           INNER JOIN dbo.TableStatus ts ON dt.StatusID = ts.StatusID;
                                       ');
                                   END
                                   """;

        await db.Database.ExecuteSqlRawAsync(ensureColumnSql, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(backfillAndIndexSql, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(syncViewSql, cancellationToken);
    }

    private static async Task ValidateOwnedSchemaAsync(CatalogDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        foreach (var table in OwnedTables)
        {
            if (!await TableExistsAsync(db, table, requirePhysicalTable: true, cancellationToken))
            {
                missing.Add(table);
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Catalog storage is missing owned tables: " + string.Join(", ", missing) +
                ". Run sql/setup-service-db-shells.ps1 or complete the Catalog DB cutover before starting Catalog.Api.");
        }

        if (await TableExistsAsync(db, "__CatalogOwnershipState", requirePhysicalTable: true, cancellationToken))
        {
            logger.LogInformation("Catalog ownership state table detected.");
        }
        else
        {
            logger.LogWarning("Catalog ownership state table was not found. Initial shell materialization may not have completed.");
        }
    }

    private static async Task<bool> TableExistsAsync(CatalogDbContext db, string tableName, CancellationToken cancellationToken)
        => await TableExistsAsync(db, tableName, requirePhysicalTable: false, cancellationToken);

    private static async Task<bool> TableExistsAsync(CatalogDbContext db, string tableName, bool requirePhysicalTable, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = requirePhysicalTable
                ? """
                  SELECT 1
                  FROM sys.tables
                  WHERE schema_id = SCHEMA_ID('dbo')
                    AND name = @table
                  """
                : """
                  SELECT 1
                  FROM
                  (
                      SELECT name
                      FROM sys.tables
                      WHERE schema_id = SCHEMA_ID('dbo')

                      UNION ALL

                      SELECT name
                      FROM sys.views
                      WHERE schema_id = SCHEMA_ID('dbo')

                      UNION ALL

                      SELECT name
                      FROM sys.synonyms
                      WHERE schema_id = SCHEMA_ID('dbo')
                  ) AS objects
                  WHERE name = @table
                  """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@table";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
