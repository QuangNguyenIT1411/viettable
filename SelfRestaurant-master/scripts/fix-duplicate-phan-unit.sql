SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

DECLARE @CanonicalUnit nvarchar(50) = N'Ph' + NCHAR(7847) + N'n';
DECLARE @LowerCanonicalUnit nvarchar(50) = N'ph' + NCHAR(7847) + N'n';
DECLARE @CorruptedCanonicalUnit nvarchar(50) = N'Ph' + NCHAR(225) + NCHAR(186) + NCHAR(167) + N'n';
DECLARE @DishRows int = 0;
DECLARE @IngredientRows int = 0;
DECLARE @UnitRowsUpdated int = 0;
DECLARE @UnitRowsDeleted int = 0;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.Units WITH (UPDLOCK, HOLDLOCK)
    WHERE Name COLLATE Latin1_General_100_BIN2 = @CanonicalUnit
)
BEGIN
    UPDATE dbo.Units
    SET Name = @CanonicalUnit,
        UpdatedAt = COALESCE(UpdatedAt, GETDATE())
    WHERE Name COLLATE Latin1_General_100_BIN2 = N'phan';

    SET @UnitRowsUpdated = @@ROWCOUNT;
END;

UPDATE dbo.Dishes
SET Unit = @CanonicalUnit,
    UpdatedAt = GETDATE()
WHERE Unit COLLATE Latin1_General_100_BIN2 IN (N'phan', @LowerCanonicalUnit, @CorruptedCanonicalUnit);

SET @DishRows = @@ROWCOUNT;

UPDATE dbo.Ingredients
SET Unit = @CanonicalUnit
WHERE Unit COLLATE Latin1_General_100_BIN2 IN (N'phan', @LowerCanonicalUnit, @CorruptedCanonicalUnit);

SET @IngredientRows = @@ROWCOUNT;

IF EXISTS (
    SELECT 1
    FROM dbo.Units
    WHERE Name COLLATE Latin1_General_100_BIN2 = @CanonicalUnit
)
BEGIN
    DELETE FROM dbo.Units
    WHERE Name COLLATE Latin1_General_100_BIN2 IN (N'phan', @CorruptedCanonicalUnit)
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.Dishes
          WHERE Unit COLLATE Latin1_General_100_BIN2 IN (N'phan', @CorruptedCanonicalUnit)
      )
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.Ingredients
          WHERE Unit COLLATE Latin1_General_100_BIN2 IN (N'phan', @CorruptedCanonicalUnit)
      );

    SET @UnitRowsDeleted = @@ROWCOUNT;
END;

SELECT
    @DishRows AS DishRowsUpdated,
    @IngredientRows AS IngredientRowsUpdated,
    @UnitRowsUpdated AS UnitRowsUpdated,
    @UnitRowsDeleted AS UnitRowsDeleted;

COMMIT TRANSACTION;
