[CmdletBinding()]
param([string]$OutputDirectory = 'D:\Quang\VietTable-backups', [string]$PostgresImage = 'postgres:17')
# Owner-run, read-only backup of the 5 VietTable schemas on the shared Supabase DB (pg_dump via Docker).
# The password comes from the local current-user DPAPI store and is passed to Docker by environment
# variable name only: it never appears in argv, files or output. The dump file stays outside the repo.
$ErrorActionPreference = 'Stop'
$secrets = Join-Path $env:LOCALAPPDATA 'VietTable\staging-secrets'
$ca = Join-Path $secrets 'supabase-ca.crt'
$passwordFile = Join-Path $secrets 'DatabasePassword.dpapi'
foreach ($required in $ca, $passwordFile) {
    if (-not (Test-Path -LiteralPath $required)) { throw "STOP: missing $(Split-Path $required -Leaf)" }
}
function Test-InsideDirectory([string]$path, [string]$root) {
    # Compare full paths with a trailing separator so "D:\a\VietTable-backups" is not inside "D:\a\VietTable".
    $sep = [IO.Path]::DirectorySeparatorChar
    $fullPath = [IO.Path]::GetFullPath($path).TrimEnd($sep) + $sep
    $fullRoot = [IO.Path]::GetFullPath($root).TrimEnd($sep) + $sep
    return $fullPath.StartsWith($fullRoot, [StringComparison]::OrdinalIgnoreCase)
}
if (Test-InsideDirectory $OutputDirectory (Join-Path $PSScriptRoot '..')) {
    throw 'STOP: output directory must be outside the repository.'
}
& docker version --format '{{.Server.Version}}' *> $null
if ($LASTEXITCODE -ne 0) { throw 'STOP: Docker is not running.' }
[void](New-Item -ItemType Directory -Force -Path $OutputDirectory)

$connection = 'host=aws-0-ap-southeast-2.pooler.supabase.com port=5432 dbname=postgres ' +
    'user=postgres.waxzqkuwuwubdkrzxbqu sslmode=verify-full sslrootcert=/ca.crt'
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$fileName = "viettable-$stamp.dump"
$secure = ConvertTo-SecureString (Get-Content -LiteralPath $passwordFile -Raw)
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    & docker run --rm -e PGPASSWORD -v "${ca}:/ca.crt:ro" -v "${OutputDirectory}:/backup" $PostgresImage `
        pg_dump $connection --format=custom `
        --schema=catalog --schema=orders --schema=customers --schema=identity --schema=billing `
        --file "/backup/$fileName"
    if ($LASTEXITCODE -ne 0) {
        throw "FAILED: pg_dump exit $LASTEXITCODE. If it reports a server version mismatch, re-run with -PostgresImage postgres:<server-major>."
    }
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer); $secure.Dispose()
}

$dump = Get-Item -LiteralPath (Join-Path $OutputDirectory $fileName)
if ($dump.Length -le 0) { throw 'FAILED: dump file is empty.' }
# Count table-data entries in the archive's table of contents (no row data is printed).
$entries = & docker run --rm -v "${OutputDirectory}:/backup" $PostgresImage pg_restore --list "/backup/$fileName"
if ($LASTEXITCODE -ne 0) { throw 'FAILED: dump file is not a readable pg_dump archive.' }
$tables = @($entries | Where-Object { $_ -match ' TABLE DATA ' }).Count
"OK: $($dump.FullName)"
"Size: $([math]::Round($dump.Length / 1KB, 1)) KB; table data entries: $tables"
