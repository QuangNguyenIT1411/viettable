param(
    [string]$ConnectionString = "Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True",
    [string]$WebRootPath = "src/Gateway/SelfRestaurant.Gateway.Api/wwwroot"
)

$ErrorActionPreference = "Stop"

function Normalize-ImagePath {
    param([string]$ImagePath)

    if ([string]::IsNullOrWhiteSpace($ImagePath)) {
        return $null
    }

    $value = $ImagePath.Trim().Replace('\', '/')
    if ($value.StartsWith("~/")) {
        $value = "/" + $value.Substring(2)
    }

    if (-not $value.StartsWith("/") -and
        -not $value.StartsWith("http://", [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $value.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $value.StartsWith("data:image/", [System.StringComparison]::OrdinalIgnoreCase)) {
        $value = "/" + $value
    }

    if ($value.StartsWith("/Images/", [System.StringComparison]::OrdinalIgnoreCase)) {
        $value = "/images/" + $value.Substring("/Images/".Length)
    }

    return $value
}

$root = Split-Path -Parent $PSScriptRoot
$resolvedWebRoot = Join-Path $root $WebRootPath

$conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
$conn.Open()

try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @"
SELECT DishID, Name, Image
FROM Dishes
WHERE ISNULL(IsActive, 1) = 1
  AND ISNULL(Available, 1) = 1
ORDER BY DishID;
"@

    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $table = New-Object System.Data.DataTable
    [void]$adapter.Fill($table)

    $missing = New-Object System.Collections.Generic.List[object]
    foreach ($row in $table.Rows) {
        $dishId = [int]$row["DishID"]
        $name = [string]$row["Name"]
        $rawImage = if ($row["Image"] -eq [System.DBNull]::Value) { "" } else { [string]$row["Image"] }
        $normalized = Normalize-ImagePath -ImagePath $rawImage

        if ([string]::IsNullOrWhiteSpace($normalized)) {
            $missing.Add([pscustomobject]@{
                DishID = $dishId
                Name = $name
                Image = $rawImage
                Reason = "Empty image path"
            })
            continue
        }

        if ($normalized.StartsWith("http://", [System.StringComparison]::OrdinalIgnoreCase) -or
            $normalized.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase) -or
            $normalized.StartsWith("data:image/", [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $relative = $normalized.TrimStart('/').Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $diskPath = Join-Path $resolvedWebRoot $relative
        if (-not (Test-Path -Path $diskPath -PathType Leaf)) {
            $missing.Add([pscustomobject]@{
                DishID = $dishId
                Name = $name
                Image = $normalized
                Reason = "File not found in wwwroot"
            })
        }
    }

    if ($missing.Count -eq 0) {
        Write-Host "OK: all active dishes have resolvable image files."
        exit 0
    }

    Write-Host "Found active dishes with missing image files:"
    $missing | Format-Table -AutoSize
    exit 1
}
finally {
    $conn.Close()
}

