param(
    [string]$Server = "(localdb)\MSSQLLocalDB",
    [Parameter(Mandatory = $true)]
    [string]$Database,
    [Parameter(Mandatory = $true)]
    [string]$ScriptPath
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $ScriptPath)) {
    throw "Missing SQL script: $ScriptPath"
}

$sql = Get-Content -Path $ScriptPath -Raw
$connectionString = "Server=$Server;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
$connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 120
    $command.CommandText = $sql
    [void]$command.ExecuteNonQuery()
}
finally {
    $connection.Dispose()
}
