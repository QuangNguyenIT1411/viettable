param(
    [string]$ConnectionString = "Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
)

$ErrorActionPreference = "Stop"

$conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
$conn.Open()

try {
    $tx = $conn.BeginTransaction()
    try {
        $sql = @"
DECLARE @availableStatusId INT = (
    SELECT TOP 1 StatusID
    FROM TableStatus
    WHERE UPPER(StatusCode) = 'AVAILABLE'
    ORDER BY StatusID
);

UPDATE DiningTables
SET CurrentOrderID = NULL,
    StatusID = ISNULL(@availableStatusId, StatusID),
    UpdatedAt = GETDATE();

DELETE FROM Bills;
DELETE FROM OrderItemIngredients;
DELETE FROM Payments;
DELETE FROM OrderItems;
DELETE FROM Orders;

DBCC CHECKIDENT ('OrderItems', RESEED, 0);
DBCC CHECKIDENT ('Orders', RESEED, 0);
DBCC CHECKIDENT ('Bills', RESEED, 0);
"@

        $cmd = $conn.CreateCommand()
        $cmd.Transaction = $tx
        $cmd.CommandTimeout = 120
        $cmd.CommandText = $sql
        [void]$cmd.ExecuteNonQuery()

        $tx.Commit()
        Write-Host "Reset completed: cleared orders/bills and restored table status."
    }
    catch {
        $tx.Rollback()
        throw
    }
}
finally {
    $conn.Close()
}
