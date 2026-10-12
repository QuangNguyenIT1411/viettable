# One-shot loopback handoff to an isolated Android emulator instrumentation test.
# No credential is written to disk, arguments, stdout, or a browser session.
$ErrorActionPreference = 'Stop'
$secretPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'VietTable\staging-secrets\CloudHostAcceptancePassword.dpapi'
$listener = $null
$client = $null
$secure = $null
$pointer = [IntPtr]::Zero
$password = $null
try {
    if (-not (Test-Path -LiteralPath $secretPath -PathType Leaf)) { throw 'Customer QA DPAPI secret is unavailable.' }
    $secure = ConvertTo-SecureString ([IO.File]::ReadAllText($secretPath))
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 48128)
    $listener.Start()
    Write-Output 'QA credential bridge ready on loopback; one request only.'
    $deadline = [Diagnostics.Stopwatch]::StartNew()
    while (-not $listener.Pending() -and $deadline.Elapsed.TotalSeconds -lt 90) {
        Start-Sleep -Milliseconds 100
    }
    if (-not $listener.Pending()) { throw 'No emulator credential request arrived.' }
    $client = $listener.AcceptTcpClient()
    $client.ReceiveTimeout = 5000
    $client.SendTimeout = 5000
    $stream = $client.GetStream()
    $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::ASCII, $false, 1024, $true)
    $line = $reader.ReadLine()
    if ($line -ne 'GET /qa-customer-credential HTTP/1.1') { throw 'Unexpected credential request.' }
    while (($line = $reader.ReadLine()) -ne '') {
        if ($null -eq $line) { throw 'Incomplete credential request.' }
    }
    $payload = [Text.Encoding]::UTF8.GetBytes((@{
        username = 'q1uat_20261012'
        password = $password
    } | ConvertTo-Json -Compress))
    $headers = [Text.Encoding]::ASCII.GetBytes("HTTP/1.1 200 OK`r`nContent-Type: application/json`r`nCache-Control: no-store`r`nContent-Length: $($payload.Length)`r`nConnection: close`r`n`r`n")
    $stream.Write($headers, 0, $headers.Length)
    $stream.Write($payload, 0, $payload.Length)
    $stream.Flush()
    [Array]::Clear($payload, 0, $payload.Length)
    Write-Output 'QA credential delivered to emulator once.'
} finally {
    $password = $null
    if ($pointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    if ($secure) { $secure.Dispose() }
    if ($client) { $client.Dispose() }
    if ($listener) { $listener.Stop() }
}
