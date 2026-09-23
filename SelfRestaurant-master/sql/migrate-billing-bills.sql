-- Explicit operator migration only. Run schema first; stop all legacy Bill writers
-- from copy until new Billing binary is deployed. Never run from application startup.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource='billing-bills-v1', @LockMode='Exclusive', @LockOwner='Transaction';
IF @lock<0 THROW 51000, 'Migration lock unavailable', 1;
IF EXISTS(SELECT 1 FROM billing.OwnershipMigrations WHERE MigrationId='billing-bills-v1')
BEGIN
    SELECT * FROM billing.OwnershipMigrations WHERE MigrationId='billing-bills-v1';
    COMMIT;
    RETURN;
END;
IF OBJECT_ID(N'RESTAURANT.dbo.Bills', N'U') IS NULL THROW 51000, 'Source Bills table missing', 1;
IF EXISTS(SELECT 1 FROM billing.Bills) THROW 51000, 'Unrecorded target data: reconcile manually, do not overwrite', 1;
DECLARE @rows bigint, @total decimal(38,2);
SELECT @rows=COUNT_BIG(*),@total=SUM(TotalAmount) FROM RESTAURANT.dbo.Bills WITH(TABLOCKX,HOLDLOCK);
-- Preserve every source column; refuse unknown columns rather than silently lose them.
IF EXISTS(SELECT name FROM RESTAURANT.sys.columns WHERE object_id=OBJECT_ID(N'RESTAURANT.dbo.Bills')
          EXCEPT SELECT name FROM sys.columns WHERE object_id=OBJECT_ID(N'billing.Bills'))
    THROW 51000, 'Unmapped source column: review migration', 1;
DECLARE @columns nvarchar(max),@sql nvarchar(max);
SELECT @columns=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),',') WITHIN GROUP(ORDER BY column_id)
FROM RESTAURANT.sys.columns WHERE object_id=OBJECT_ID(N'RESTAURANT.dbo.Bills');
SET @sql=N'SET IDENTITY_INSERT billing.Bills ON;
INSERT billing.Bills('+@columns+') SELECT '+@columns+' FROM RESTAURANT.dbo.Bills;
SET IDENTITY_INSERT billing.Bills OFF;
IF EXISTS(SELECT '+@columns+' FROM RESTAURANT.dbo.Bills EXCEPT SELECT '+@columns+' FROM billing.Bills)
 OR EXISTS(SELECT '+@columns+' FROM billing.Bills EXCEPT SELECT '+@columns+' FROM RESTAURANT.dbo.Bills)
 THROW 51000, ''Bill column reconciliation failed'', 1;';
EXEC sys.sp_executesql @sql;
IF (SELECT COUNT_BIG(*) FROM billing.Bills)<>@rows THROW 51000, 'Bill count mismatch', 1;
IF EXISTS(SELECT @total EXCEPT SELECT SUM(TotalAmount) FROM billing.Bills) THROW 51000, 'Bill amount mismatch', 1;
-- Preserve the source identity high-water mark even if prior source rows were removed.
DECLARE @seed bigint=CONVERT(bigint,IDENT_CURRENT(N'RESTAURANT.dbo.Bills'));
IF @seed>1 BEGIN
    SET @sql=N'DBCC CHECKIDENT (''billing.Bills'', RESEED, '+CONVERT(varchar(30),@seed)+N') WITH NO_INFOMSGS;';
    EXEC sys.sp_executesql @sql;
END;
INSERT billing.OwnershipMigrations(MigrationId,SourceRows,SourceTotal) VALUES('billing-bills-v1',@rows,@total);
COMMIT;
SELECT * FROM billing.OwnershipMigrations WHERE MigrationId='billing-bills-v1';
