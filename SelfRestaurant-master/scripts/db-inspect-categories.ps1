param(
  [string]$ConnectionString = 'Data Source=.;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Application Name="SQL Server Management Studio";Command Timeout=0'
)

$ErrorActionPreference = 'Stop'

function Normalize-ConnectionString([string]$raw) {
  if ([string]::IsNullOrWhiteSpace($raw)) { return $raw }
  $pairs = $raw -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
  $pairs = $pairs | Where-Object { $_ -notmatch '^\s*Command Timeout\s*=' }
  $hasDb = $pairs | Where-Object { $_ -match '^\s*(Initial Catalog|Database)\s*=' }
  if (-not $hasDb) {
    $pairs += 'Initial Catalog=RESTAURANT'
  }
  return ($pairs -join ';')
}

function Get-OpenConnection() {
  $candidates = @(
    (Normalize-ConnectionString $ConnectionString),
    'Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True',
    'Data Source=.;Initial Catalog=RESTAURANT;Integrated Security=True;TrustServerCertificate=True;Encrypt=True',
    'Data Source=.;Initial Catalog=RESTAURANT;Integrated Security=True;TrustServerCertificate=True;Encrypt=False'
  ) | Select-Object -Unique

  foreach ($cs in $candidates) {
    try {
      $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
      $conn.Open()
      Write-Host "Using connection string: $cs"
      return $conn
    }
    catch {
      continue
    }
  }

  throw 'Cannot connect to SQL Server with provided/default connection strings.'
}

function Invoke-Query([string]$sql) {
  $conn = Get-OpenConnection
  try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 60
    $da = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dt = New-Object System.Data.DataTable
    [void]$da.Fill($dt)
    return $dt
  }
  finally {
    $conn.Close()
  }
}

Write-Host '=== Categories ==='
$cats = Invoke-Query @"
SELECT CategoryID, Name, Description, DisplayOrder, IsActive, UpdatedAt
FROM Categories
ORDER BY CategoryID
"@
$cats | Format-Table -AutoSize

Write-Host '=== MenuCategory active mapping ==='
$mc = Invoke-Query @"
SELECT mc.MenuCategoryID, mc.MenuID, mc.CategoryID, c.Name AS CategoryName, mc.IsActive, m.BranchID, m.Date
FROM MenuCategory mc
JOIN Categories c ON c.CategoryID = mc.CategoryID
LEFT JOIN Menus m ON m.MenuID = mc.MenuID
WHERE ISNULL(mc.IsActive, 1) = 1
ORDER BY mc.MenuID, mc.CategoryID
"@
$mc | Format-Table -AutoSize

Write-Host '=== Auto/Dbg categories ==='
$auto = Invoke-Query @"
SELECT CategoryID, Name, Description, DisplayOrder, IsActive
FROM Categories
WHERE Name LIKE 'AUTO[_]CAT%' OR Name LIKE 'DBG[_]CAT%'
ORDER BY CategoryID
"@
if ($auto.Rows.Count -eq 0) {
  Write-Host 'No AUTO/DBG categories found.'
} else {
  $auto | Format-Table -AutoSize
}
