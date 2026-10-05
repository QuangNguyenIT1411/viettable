[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskStore = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'VietTable\staging-secrets'
$taskCandidates = @((git -C $taskRoot diff --name-only HEAD), (git -C $taskRoot ls-files --others --exclude-standard)) |
    ForEach-Object { $_ } | Where-Object { $_ -and -not $_.StartsWith('.idea/') } | Sort-Object -Unique
$taskFiles = @($taskCandidates | Where-Object { [IO.Path]::GetExtension($_) -in @('.cs','.csproj','.ps1','.md','.toml','.json','.yml','.yaml') })
$taskScanned = 0
foreach ($taskSecretFile in Get-ChildItem -LiteralPath $taskStore -Filter '*.dpapi' -File) {
    $taskSecure = ConvertTo-SecureString ([IO.File]::ReadAllText($taskSecretFile.FullName))
    $taskPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskSecure)
    $taskPlain = $null
    try {
        $taskPlain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskPointer)
        foreach ($taskRelative in $taskFiles) {
            $taskAbsolute = Join-Path $taskRoot $taskRelative
            if ((Test-Path -LiteralPath $taskAbsolute -PathType Leaf) -and
                [IO.File]::ReadAllText($taskAbsolute).Contains($taskPlain)) {
                throw "Secret material detected in candidate file: $taskRelative (value redacted)."
            }
        }
        $taskScanned++
    }
    finally {
        $taskPlain = $null
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskPointer)
        $taskSecure.Dispose()
    }
}
foreach ($taskRelative in $taskFiles) {
    $taskAbsolute = Join-Path $taskRoot $taskRelative
    if (-not (Test-Path -LiteralPath $taskAbsolute -PathType Leaf)) { continue }
    $taskContent = [IO.File]::ReadAllText($taskAbsolute)
    if ($taskContent -match 'sb_secret_[A-Za-z0-9_-]{15,}' -or
        $taskContent -match 'eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}') {
        throw "Potential backend key/JWT in candidate file: $taskRelative (value redacted)."
    }
}
Write-Output "PASS: candidate text files scanned=$($taskFiles.Count); DPAPI secret values checked=$taskScanned; no secret/JWT match. .idea excluded."
