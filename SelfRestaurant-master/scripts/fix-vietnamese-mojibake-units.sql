SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @DishRows int = 0;
DECLARE @IngredientRows int = 0;
DECLARE @UnitRowsUpdated int = 0;
DECLARE @UnitRowsDeleted int = 0;

UPDATE dbo.Dishes
SET Unit = N'Phần',
    UpdatedAt = GETDATE()
WHERE Unit = N'Pháº§n';
SET @DishRows = @@ROWCOUNT;

UPDATE dbo.Ingredients
SET Unit = N'Phần'
WHERE Unit = N'Pháº§n';
SET @IngredientRows = @@ROWCOUNT;

IF EXISTS (SELECT 1 FROM dbo.Units WHERE Name = N'Phần')
BEGIN
    DELETE FROM dbo.Units
    WHERE Name = N'Pháº§n';
    SET @UnitRowsDeleted = @@ROWCOUNT;
END
ELSE
BEGIN
    UPDATE dbo.Units
    SET Name = N'Phần',
        UpdatedAt = GETDATE()
    WHERE Name = N'Pháº§n';
    SET @UnitRowsUpdated = @@ROWCOUNT;
END

SELECT
    @DishRows AS DishRowsUpdated,
    @IngredientRows AS IngredientRowsUpdated,
    @UnitRowsUpdated AS UnitRowsUpdated,
    @UnitRowsDeleted AS UnitRowsDeleted;

COMMIT TRANSACTION;
