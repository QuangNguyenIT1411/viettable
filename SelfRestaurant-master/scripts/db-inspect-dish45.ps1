param([string]$ConnectionString = 'Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True')
$ErrorActionPreference='Stop'
$conn=New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
$conn.Open()
try {
  $cmd=$conn.CreateCommand(); $cmd.CommandText=@"
SELECT d.DishID,d.Name,d.CategoryID,c.Name AS CategoryName,d.IsActive,d.Available,d.Image
FROM Dishes d LEFT JOIN Categories c ON c.CategoryID=d.CategoryID
WHERE d.DishID=45
"@
  $da=New-Object System.Data.SqlClient.SqlDataAdapter($cmd);$dt=New-Object System.Data.DataTable;[void]$da.Fill($dt);$dt|Format-Table -AutoSize

  $cmd2=$conn.CreateCommand(); $cmd2.CommandText=@"
SELECT cd.CategoryDishID, cd.MenuCategoryID, cd.DishID, cd.IsAvailable, mc.MenuID, mc.CategoryID AS MenuCatCategoryID, c.Name AS MenuCategoryName
FROM CategoryDish cd
JOIN MenuCategory mc ON mc.MenuCategoryID=cd.MenuCategoryID
JOIN Categories c ON c.CategoryID=mc.CategoryID
WHERE cd.DishID=45
ORDER BY cd.CategoryDishID
"@
  $da2=New-Object System.Data.SqlClient.SqlDataAdapter($cmd2);$dt2=New-Object System.Data.DataTable;[void]$da2.Fill($dt2);$dt2|Format-Table -AutoSize
}
finally{$conn.Close()}
