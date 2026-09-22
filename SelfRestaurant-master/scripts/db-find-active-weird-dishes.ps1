param([string]$ConnectionString = 'Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True')
$ErrorActionPreference='Stop'
$conn=New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
$conn.Open()
try {
  $cmd=$conn.CreateCommand()
  $cmd.CommandText=@"
SELECT d.DishID, d.Name, d.CategoryID, c.Name AS CategoryName, d.IsActive, d.Available
FROM Dishes d
LEFT JOIN Categories c ON c.CategoryID = d.CategoryID
WHERE ISNULL(d.IsActive,1)=1 AND (d.Name LIKE 'AUTO[_]%' OR d.Name LIKE 'DBG[_]%' OR d.CategoryID > 4)
ORDER BY d.DishID
"@
  $da=New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
  $dt=New-Object System.Data.DataTable
  [void]$da.Fill($dt)
  if($dt.Rows.Count -eq 0){ Write-Output 'No active weird dishes.' } else { $dt | Format-Table -AutoSize }
}
finally { $conn.Close() }
