[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskBackend = Join-Path $taskRoot 'SelfRestaurant-master'
$taskSourcePrefixes = @(
    'src/Backend/', 'src/Gateway/', 'src/Services/', 'src/Shared/',
    'src/Frontend/selfrestaurant-chef-web/',
    'src/Frontend/selfrestaurant-cashier-web/',
    'src/Frontend/selfrestaurant-admin-web/'
    'src/Frontend/shared/'
)
$taskExactFiles = @('sql/billing-owned-tables.sql', 'certs/supabase-ca.crt',
    'Dockerfile.staff-preview', 'Dockerfile.staff-preview.dockerignore',
    'src/Frontend/build-staff-static.mjs')
$taskCandidates = @(git -C $taskRoot ls-files --cached --others --exclude-standard -- SelfRestaurant-master)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate reviewed repository source.' }
$taskFiles = @($taskCandidates | Sort-Object -Unique | ForEach-Object {
    $taskRelative = $_.Substring('SelfRestaurant-master/'.Length)
    $taskAllowed = $taskRelative -in $taskExactFiles
    foreach ($taskPrefix in $taskSourcePrefixes) {
        if ($taskRelative.StartsWith($taskPrefix, [StringComparison]::Ordinal)) { $taskAllowed = $true }
    }
    if ($taskAllowed -and $taskRelative -notmatch '(^|/)(\.env[^/]*|\.git|\.idea|\.vercel|node_modules|bin|obj|dist)(/|$)' -and
        $taskRelative -notmatch '(?i)(appsettings[^/]*\.json|\.(dpapi|pfx|pem|log|tsbuildinfo))$') { $taskRelative }
})
foreach ($taskRequired in $taskExactFiles) {
    if ($taskRequired -notin $taskFiles) { throw "Required reviewed file missing: $taskRequired" }
}
# Fresh isolated context, never overwrite the API-only config or copy local .vercel/.env.
$taskOutput = Join-Path ([IO.Path]::GetTempPath()) ('VietTable-staff-preview-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $taskOutput)
foreach ($taskRelative in $taskFiles) {
    $taskSource = Join-Path $taskBackend $taskRelative
    if (-not (Test-Path -LiteralPath $taskSource -PathType Leaf)) { throw "Source missing: $taskRelative" }
    $taskAncestor = Get-Item -LiteralPath $taskSource
    while ($taskAncestor -and $taskAncestor.FullName -ne $taskRoot) {
        if ($taskAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Symlink/reparse source is not allowed.' }
        $taskAncestor = if ($taskAncestor -is [IO.DirectoryInfo]) { $taskAncestor.Parent } else { $taskAncestor.Directory }
    }
    # Vercel container entrypoints require a supported Dockerfile basename.
    # Rename only inside this isolated upload context; keep the API Dockerfile intact.
    $taskUploadRelative = switch ($taskRelative) {
        'Dockerfile.staff-preview' { 'Dockerfile' }
        'Dockerfile.staff-preview.dockerignore' { 'Dockerfile.dockerignore' }
        default { $taskRelative }
    }
    $taskDestination = Join-Path $taskOutput $taskUploadRelative
    [void](New-Item -ItemType Directory -Path (Split-Path $taskDestination) -Force)
    Copy-Item -LiteralPath $taskSource -Destination $taskDestination
}
foreach ($taskConfig in @('vercel.json', '.vercelignore')) {
    Copy-Item -LiteralPath (Join-Path $taskBackend "deploy/staff-preview/$taskConfig") -Destination (Join-Path $taskOutput $taskConfig)
}
Write-Output "PREPARED ONLY: $taskOutput"
Write-Output "Source files: $($taskFiles.Count). No deploy/link/env pull/alias/Production action was executed."
Write-Output 'Next local check: cd <prepared-context>; docker build -f Dockerfile -t viettable-staff-preview:prepared .'
