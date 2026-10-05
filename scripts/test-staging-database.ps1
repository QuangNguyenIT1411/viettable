param(
    [Parameter(Mandatory = $true)][string]$ProjectRef,
    [Parameter(Mandatory = $true)][string]$DatabaseHost,
    [Parameter(Mandatory = $true)][string]$DatabaseUser,
    [ValidateSet('Probe', 'Migrate', 'Verify', 'Roles', 'Bootstrap', 'CorrectReferenceTime', 'Workflow', 'StorageUpload', 'StorageVerify')][string]$Action = 'Probe',
    [switch]$DisableClientPooling,
    # Npgsql reset batches are not suitable for transaction pooling; runtime uses
    # fixed per-owner roles, AutoPrepare0 and transaction-local state only.
    [switch]$TransactionPoolerCompatibility = $true
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Windows current-user DPAPI is required.' }
if ($ProjectRef -notmatch '^[a-z0-9]+$') { throw 'Invalid project reference.' }
if (-not (($DatabaseHost -eq "db.$ProjectRef.supabase.co" -and $DatabaseUser -eq 'postgres') -or
    ($DatabaseHost -match '^[a-z0-9.-]+\.pooler\.supabase\.com$' -and $DatabaseUser -eq "postgres.$ProjectRef"))) {
    throw 'Host/user must identify the explicit Supabase staging project.'
}
$taskBackendPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\SelfRestaurant-master'))
$taskRunnerPath = Join-Path $taskBackendPath 'tools\SelfRestaurant.CloudPreparation\bin\Debug\net8.0\SelfRestaurant.CloudPreparation.dll'
if (-not (Test-Path -LiteralPath $taskRunnerPath)) { throw 'Build the cloud preparation runner first using the .NET 8 SDK.' }
$taskSecretPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'VietTable\staging-secrets\DatabasePassword.dpapi'
$taskCaPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'VietTable\staging-secrets\supabase-ca.crt'
if (-not (Test-Path -LiteralPath $taskCaPath)) { throw 'Download the provider public CA from the Supabase Database settings first.' }
if (-not (Test-Path -LiteralPath $taskSecretPath)) { throw 'Save DatabasePassword with save-staging-secret.ps1 first; do not supply it on the command line.' }
$taskSecret = ConvertTo-SecureString ([IO.File]::ReadAllText($taskSecretPath))
$taskBstr = [IntPtr]::Zero
$taskStorageBstr = [IntPtr]::Zero
$taskStorageSecret = $null
try {
    $taskBstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskSecret)
    # Plaintext exists only in process memory and stdin, never argv, env, logs or repo files.
    $taskPayload = @{
        Action = $Action; ProjectRef = $ProjectRef; Host = $DatabaseHost; Port = 5432
        Username = $DatabaseUser; Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskBstr)
        RuntimePooling = -not $DisableClientPooling.IsPresent
        RuntimeNoReset = $TransactionPoolerCompatibility.IsPresent
    }
    if ($Action -in @('Roles', 'Workflow')) {
        $taskPayload.RolePasswords = @{}
        foreach ($taskOwner in @('catalog','orders','customers','identity','billing')) {
            $taskRolePath = Join-Path (Split-Path $taskSecretPath) ("RuntimeRole-$taskOwner.dpapi")
            if (-not (Test-Path -LiteralPath $taskRolePath)) {
                if ($Action -eq 'Workflow') { throw 'Runtime role secret missing; run/audit Roles provisioning first. No implicit password rotation.' }
                $taskBytes = New-Object byte[] 32
                $taskRandom = [Security.Cryptography.RandomNumberGenerator]::Create()
                try { $taskRandom.GetBytes($taskBytes) } finally { $taskRandom.Dispose() }
                $taskGenerated = [BitConverter]::ToString($taskBytes).Replace('-', '')
                $taskRoleSecure = ConvertTo-SecureString $taskGenerated -AsPlainText -Force
                [IO.File]::WriteAllText($taskRolePath, (ConvertFrom-SecureString $taskRoleSecure))
                $taskGenerated = $null
                $taskRoleSecure.Dispose()
            }
            $taskRoleSecure = ConvertTo-SecureString ([IO.File]::ReadAllText($taskRolePath))
            $taskRoleBstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskRoleSecure)
            try { $taskPayload.RolePasswords[$taskOwner] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskRoleBstr) }
            finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskRoleBstr); $taskRoleSecure.Dispose() }
        }
    }
    $taskReceiptPath = Join-Path (Split-Path $taskSecretPath) 'storage-acceptance-receipt.txt'
    if ($Action -in @('Bootstrap', 'CorrectReferenceTime')) {
        $taskSourceZone = docker exec selfrestaurant-catalog date '+%z'
        if ($LASTEXITCODE -ne 0 -or $taskSourceZone.Trim() -ne '+0000') { throw 'Source Catalog UTC policy needs review.' }
        # Local owner export job only. Capture configured SA credential in memory;
        # never echo docker inspection output or write the source connection.
        $taskSourceEnv = (docker inspect selfrestaurant-db --format '{{json .Config.Env}}' | ConvertFrom-Json)
        if ($LASTEXITCODE -ne 0) { throw 'Local Catalog reference source unavailable.' }
        $taskSourcePassword = @($taskSourceEnv | Where-Object { $_.StartsWith('MSSQL_SA_PASSWORD=') })
        if ($taskSourcePassword.Count -ne 1) { throw 'Local source password configuration unavailable.' }
        $taskSourceBuilder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
        $taskSourceBuilder['Data Source'] = 'host.docker.internal,1433'
        $taskSourceBuilder['Initial Catalog'] = 'RESTAURANT_CATALOG'
        $taskSourceBuilder['User ID'] = 'sa'
        $taskSourceBuilder['Password'] = $taskSourcePassword[0].Substring('MSSQL_SA_PASSWORD='.Length)
        $taskSourceBuilder['Encrypt'] = $true
        $taskSourceBuilder['TrustServerCertificate'] = $true # Local SQL Server only; NOT cloud TLS.
        $taskPayload.SourceConnection = $taskSourceBuilder.ConnectionString
        $taskSourceEnv = $null; $taskSourcePassword = $null; $taskSourceBuilder = $null
    }
    if ($Action -like 'Storage*') {
        $taskStoragePath = Join-Path (Split-Path $taskSecretPath) 'StorageServiceKey.dpapi'
        if (-not (Test-Path -LiteralPath $taskStoragePath)) { throw 'Save StorageServiceKey securely first.' }
        $taskStorageSecret = ConvertTo-SecureString ([IO.File]::ReadAllText($taskStoragePath))
        $taskStorageBstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskStorageSecret)
        $taskPayload.StorageServiceKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskStorageBstr)
        if ($Action -eq 'StorageVerify') {
            if (-not (Test-Path -LiteralPath $taskReceiptPath)) { throw 'Run StorageUpload first.' }
            $taskPayload.StorageReceipt = [IO.File]::ReadAllText($taskReceiptPath).Trim()
        }
    }
    $taskInput = $taskPayload | ConvertTo-Json -Compress
    $taskOutput = $taskInput | docker run --rm -i --label viettable.cloud-preparation=database `
        --mount "type=bind,source=$taskBackendPath,target=/workspace,readonly" `
        --mount "type=bind,source=$taskCaPath,target=/staging-ca/supabase-ca.crt,readonly" `
        -w /workspace mcr.microsoft.com/dotnet/aspnet:8.0 `
        dotnet tools/SelfRestaurant.CloudPreparation/bin/Debug/net8.0/SelfRestaurant.CloudPreparation.dll
    $taskExitCode = $LASTEXITCODE
    foreach ($taskLine in $taskOutput) {
        if ($taskLine.StartsWith('STORAGE_RECEIPT:')) {
            [IO.File]::WriteAllText($taskReceiptPath, $taskLine.Substring('STORAGE_RECEIPT:'.Length))
        }
        else { Write-Output $taskLine }
    }
    if ($taskExitCode -ne 0) { throw 'Staging operation failed; no credential details printed.' }
}
finally {
    $taskInput = $null
    $taskPayload = $null
    if ($taskStorageBstr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskStorageBstr) }
    if ($null -ne $taskStorageSecret) { $taskStorageSecret.Dispose() }
    if ($taskBstr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskBstr) }
    $taskSecret.Dispose()
}
