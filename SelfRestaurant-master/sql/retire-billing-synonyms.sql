-- Run ONLY after new application mapping and runtime acceptance while aliases still exist.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'billing.Bills',N'U') IS NULL
 OR NOT EXISTS(SELECT 1 FROM billing.OwnershipMigrations WHERE MigrationId='billing-bills-v1')
    THROW 51000, 'Verified migration required before alias retirement', 1;
IF EXISTS(SELECT 1 FROM sys.sql_expression_dependencies d JOIN sys.synonyms s
          ON d.referenced_id=s.object_id OR (d.referenced_entity_name=s.name AND ISNULL(d.referenced_schema_name,'dbo')=SCHEMA_NAME(s.schema_id)))
    THROW 51000, 'SQL object still depends on a synonym', 1;
IF EXISTS(SELECT 1 FROM sys.sql_expression_dependencies WHERE referenced_database_name IS NOT NULL OR referenced_server_name IS NOT NULL)
    THROW 51000, 'Cross-database SQL dependency remains', 1;
DECLARE @sql nvarchar(max)=N'';
SELECT @sql=@sql+N'DROP SYNONYM '+QUOTENAME(SCHEMA_NAME(schema_id))+N'.'+QUOTENAME(name)+N';' FROM sys.synonyms;
EXEC sys.sp_executesql @sql;
COMMIT;
