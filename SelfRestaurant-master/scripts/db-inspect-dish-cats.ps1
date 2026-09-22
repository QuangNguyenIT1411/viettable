param([string]$ConnectionString = 'Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True')
$ErrorActionPreference='Stop'
$conn=New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
$conn.Open()
try {
  $cmd=$conn.CreateCommand()
  $cmd.CommandText=@"
SELECT d.CategoryID, c.Name AS CategoryName, COUNT(*) AS DishCount,
       SUM(CASE WHEN ISNULL(d.IsActive,1)=1 THEN 1 ELSE 0 END) AS ActiveDishCount
FROM Dishes d
LEFT JOIN Categories c ON c.CategoryID=d.CategoryID
GROUP BY d.CategoryID, c.Name
ORDER BY d.CategoryID
"@
  $da=New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
  $dt=New-Object System.Data.DataTable
  [void]$da.Fill($dt)
  $dt | Format-Table -AutoSize

  $cmd2=$conn.CreateCommand()
  $cmd2.CommandText=@"
SELECT TOP 40 DishID, Name, CategoryID, Image, IsActive, Available
FROM Dishes
ORDER BY DishID
"@
  $da2=New-Object System.Data.SqlClient.SqlDataAdapter($cmd2)
  $dt2=New-Object System.Data.DataTable
  [void]$da2.Fill($dt2)
  $dt2 | Format-Table -AutoSize
}
finally { $conn.Close() }
