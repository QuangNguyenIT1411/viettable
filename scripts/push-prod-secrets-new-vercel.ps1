[CmdletBinding()]
param([string]$Scope = 'viet-table', [switch]$Force)
# Owner-run only. Copies the 8 Production secrets from the local current-user DPAPI store into the
# NEW Vercel project via stdin. Prints variable names and OK/FAILED only: no value reaches argv, files or output.
# Run from the upload folder linked to the NEW project (docs/plans/DEPLOY_NEW_VERCEL.md step 2.6).
$ErrorActionPreference = 'Stop'
$linkFile = Join-Path (Get-Location) '.vercel\project.json'
if (-not (Test-Path -LiteralPath $linkFile)) { throw 'STOP: this folder is not linked to a Vercel project.' }
$link = Get-Content -LiteralPath $linkFile -Raw | ConvertFrom-Json
if ($link.orgId -eq 'team_0Zou1LjMbkg5R7iSVHybcmz2' -or $link.projectName -ne 'viettable') {
    throw 'STOP: folder is not linked to the NEW project "viettable".'
}
$dir = Join-Path $env:LOCALAPPDATA 'VietTable\staging-secrets'
$owners = 'catalog', 'orders', 'customers', 'identity', 'billing'
$needed = @($owners | ForEach-Object { "RuntimeRole-$_" }) + 'CloudHostSigningKey', 'CloudHostDataProtectionCertificate', 'StorageServiceKey'
foreach ($n in $needed) { if (-not (Test-Path -LiteralPath (Join-Path $dir "$n.dpapi"))) { throw "STOP: missing local secret $n" } }

function Read-Dpapi([string]$name) {
    $secure = ConvertTo-SecureString (Get-Content -LiteralPath (Join-Path $dir "$name.dpapi") -Raw)
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer); $secure.Dispose() }
}
function Add-Secret([string]$name, [string]$value) {
    $arguments = "/c npx.cmd --yes vercel@62.4.0 env add $name production --sensitive --scope $Scope" + $(if ($Force) { ' --force' } else { '' })
    $start = [Diagnostics.ProcessStartInfo]::new('cmd.exe', $arguments)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.Environment['CI'] = '1'
    $process = [Diagnostics.Process]::Start($start)
    $process.StandardInput.Write($value); $process.StandardInput.Close()
    $outTask = $process.StandardOutput.ReadToEndAsync(); $errTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit(); $null = $outTask.Result; $null = $errTask.Result
    if ($process.ExitCode -ne 0) { throw "FAILED: $name (exit $($process.ExitCode)). Re-run with -Force if it already exists." }
    "OK: $name"
}

foreach ($owner in $owners) {
    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder['Host'] = 'aws-0-ap-southeast-2.pooler.supabase.com'; $builder['Port'] = '6543'; $builder['Database'] = 'postgres'
    $builder['Username'] = "viettable_$owner.waxzqkuwuwubdkrzxbqu"; $builder['Password'] = Read-Dpapi "RuntimeRole-$owner"
    $builder['SSL Mode'] = 'VerifyFull'; $builder['Root Certificate'] = '/staging-ca/supabase-ca.crt'
    $builder['Pooling'] = 'true'; $builder['No Reset On Close'] = 'true'; $builder['Multiplexing'] = 'false'
    $builder['Maximum Pool Size'] = '2'; $builder['Minimum Pool Size'] = '0'; $builder['Max Auto Prepare'] = '0'
    $builder['Include Error Detail'] = 'false'; $builder['Timeout'] = '15'; $builder['Command Timeout'] = '30'
    $builder['Application Name'] = "VietTable-Prod-$owner"
    Add-Secret ('ConnectionStrings__' + $owner.Substring(0, 1).ToUpperInvariant() + $owner.Substring(1) + 'Db') $builder.ConnectionString
    $builder.Clear()
}
Add-Secret 'MobileJwt__SigningKey' (Read-Dpapi 'CloudHostSigningKey')
Add-Secret 'CloudHost__DataProtectionCertificate' (Read-Dpapi 'CloudHostDataProtectionCertificate')
Add-Secret 'MediaStorage__Supabase__ServiceKey' (Read-Dpapi 'StorageServiceKey')
'DONE: 8 Production secrets added. CRON_SECRET intentionally not added (recovery endpoint stays disabled).'
