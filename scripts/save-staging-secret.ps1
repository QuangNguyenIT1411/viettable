param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('DatabasePassword', 'StorageServiceKey')]
    [string]$Name
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'This helper requires Windows current-user DPAPI.' }
# This file contains only instructions. Secret material is encrypted outside the repository.
$taskSecretDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'VietTable\staging-secrets'
[void](New-Item -ItemType Directory -Path $taskSecretDirectory -Force)
$taskSecret = Read-Host "Enter staging $Name (hidden; do not paste into chat)" -AsSecureString
if ($taskSecret.Length -eq 0) { throw 'Empty secret was not saved.' }
$taskSecretFile = Join-Path $taskSecretDirectory ($Name + '.dpapi')
$taskEncrypted = ConvertFrom-SecureString -SecureString $taskSecret
[System.IO.File]::WriteAllText($taskSecretFile, $taskEncrypted)
$taskSecret.Dispose()
Write-Host "Saved $Name with Windows current-user DPAPI outside the repository. No secret value printed."
