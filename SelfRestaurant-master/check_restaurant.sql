SET NOCOUNT ON;
SELECT name FROM sys.databases;
GO
SELECT COUNT(*) AS BranchCount FROM RESTAURANT.dbo.Branches;
GO
SELECT TOP 10 BranchID, Name, Location, IsActive FROM RESTAURANT.dbo.Branches ORDER BY BranchID;
GO
