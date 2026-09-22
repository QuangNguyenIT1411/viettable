SET NOCOUNT ON;

-- Catalog owns these tables. The legacy RESTAURANT database is used only as
-- the one-time seed source while the service databases are being split.
DECLARE @owned TABLE (Name sysname NOT NULL PRIMARY KEY);
INSERT INTO @owned (Name) VALUES
    (N'Restaurants'),
    (N'Branches'),
    (N'Categories'),
    (N'Ingredients'),
    (N'Dishes'),
    (N'TableStatus'),
    (N'DiningTables'),
    (N'Menus'),
    (N'MenuCategory'),
    (N'CategoryDish'),
    (N'DishIngredients');

DECLARE @name sysname;
DECLARE @sql nvarchar(max);
DECLARE owned_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @owned;
OPEN owned_cursor;
FETCH NEXT FROM owned_cursor INTO @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = @name)
    BEGIN
        SET @sql = N'DROP SYNONYM dbo.' + QUOTENAME(@name) + N';';
        EXEC sp_executesql @sql;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND name = @name)
    BEGIN
        SET @sql = N'SELECT * INTO dbo.' + QUOTENAME(@name) +
                   N' FROM RESTAURANT.dbo.' + QUOTENAME(@name) + N';';
        EXEC sp_executesql @sql;
    END;

    FETCH NEXT FROM owned_cursor INTO @name;
END;
CLOSE owned_cursor;
DEALLOCATE owned_cursor;

DECLARE @keys TABLE (TableName sysname NOT NULL, ColumnName sysname NOT NULL, ConstraintName sysname NOT NULL);
INSERT INTO @keys (TableName, ColumnName, ConstraintName) VALUES
    (N'Restaurants', N'RestaurantID', N'PK_Restaurants'),
    (N'Branches', N'BranchID', N'PK_Branches'),
    (N'Categories', N'CategoryID', N'PK_Categories'),
    (N'Ingredients', N'IngredientID', N'PK_Ingredients'),
    (N'Dishes', N'DishID', N'PK_Dishes'),
    (N'TableStatus', N'StatusID', N'PK_TableStatus'),
    (N'DiningTables', N'TableID', N'PK_DiningTables'),
    (N'Menus', N'MenuID', N'PK_Menus'),
    (N'MenuCategory', N'MenuCategoryID', N'PK_MenuCategory'),
    (N'CategoryDish', N'CategoryDishID', N'PK_CategoryDish'),
    (N'DishIngredients', N'DishIngredientID', N'PK_DishIngredients');

DECLARE @column sysname;
DECLARE @constraint sysname;
DECLARE key_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, ColumnName, ConstraintName FROM @keys;
OPEN key_cursor;
FETCH NEXT FROM key_cursor INTO @name, @column, @constraint;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.' + @name) AND [type] = N'PK')
    BEGIN
        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@name) + N' ADD CONSTRAINT ' +
                   QUOTENAME(@constraint) + N' PRIMARY KEY (' + QUOTENAME(@column) + N');';
        EXEC sp_executesql @sql;
    END;
    FETCH NEXT FROM key_cursor INTO @name, @column, @constraint;
END;
CLOSE key_cursor;
DEALLOCATE key_cursor;

IF OBJECT_ID(N'dbo.__CatalogOwnershipState', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.__CatalogOwnershipState
    (
        Id INT NOT NULL CONSTRAINT PK_CatalogOwnershipState PRIMARY KEY,
        MaterializedAtUtc DATETIME2 NOT NULL
            CONSTRAINT DF_CatalogOwnershipState_MaterializedAtUtc DEFAULT (SYSUTCDATETIME())
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.__CatalogOwnershipState WHERE Id = 1)
    INSERT INTO dbo.__CatalogOwnershipState (Id) VALUES (1);
