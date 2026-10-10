param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Windows current-user DPAPI is required.' }
$taskDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'VietTable\staging-secrets'
[void](New-Item -ItemType Directory -Path $taskDirectory -Force)
$taskSecret = Read-Host 'Enter NEW staging QA password (hidden; do not paste into chat)' -AsSecureString
try {
    if ($taskSecret.Length -lt 12) { throw 'Use a new strong QA password of at least 12 characters.' }
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'QaRotationPassword.dpapi'), (ConvertFrom-SecureString $taskSecret))
} finally { $taskSecret.Dispose() }
Write-Host 'Saved new QA rotation password with DPAPI outside the repository. Existing credentials unchanged.'
