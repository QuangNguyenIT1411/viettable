[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BaseUrl,
    [string]$AccessToken = $env:VIETTABLE_PROD_TEST_ACCESS_TOKEN,
    [switch]$CheckPrivatePorts
)

$ErrorActionPreference = 'Stop'
try { $origin = [Uri]$BaseUrl } catch { throw 'BaseUrl must be a valid HTTPS Gateway origin.' }
$hostName = $origin.DnsSafeHost.ToLowerInvariant()
if ($origin.Scheme -ne 'https' -or !$origin.IsDefaultPort -or $origin.AbsolutePath -ne '/' -or
    $origin.Query -or $origin.Fragment -or $origin.UserInfo -or
    [Uri]::CheckHostName($hostName) -ne [UriHostNameType]::Dns -or
    $hostName -eq 'localhost' -or $hostName.EndsWith('.localhost') -or $hostName.EndsWith('.invalid')) {
    throw 'BaseUrl must be a real public HTTPS DNS origin, without port/path/query.'
}
$base = "https://$hostName"

function Get-Json([string]$path, [hashtable]$headers=@{}) {
    Invoke-RestMethod -Method Get -Uri "$base$path" -Headers $headers -TimeoutSec 15
}

$health = Get-Json '/healthz'
if ($health.status -ne 'ok') { throw 'Public Gateway health response is invalid.' }
Write-Output 'PASS trusted HTTPS Gateway health'

$branches = @(Get-Json '/api/mobile/v1/branches')
if ($branches.Count -lt 1 -or !$branches[0].branchId) { throw 'No public branch returned through Gateway.' }
$menu = Get-Json "/api/mobile/v1/branches/$($branches[0].branchId)/menu"
if (!$menu.categories) { throw 'Public Gateway branch menu is incomplete.' }
$faqs = Get-Json '/api/mobile/v1/support/faqs'
if (!$faqs.items) { throw 'Public Gateway FAQ is incomplete.' }
Write-Output 'PASS public branch/menu and FAQ through Gateway'

try {
    Invoke-WebRequest -Uri "http://$hostName/healthz" -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 15 | Out-Null
    throw 'Plain HTTP served a response instead of redirecting.'
} catch {
    $response = $_.Exception.Response
    if ($null -eq $response -or [int]$response.StatusCode -notin @(301, 308) -or
        !$response.Headers['Location'].StartsWith($base + '/')) {
        throw 'Port 80 did not redirect to this HTTPS host.'
    }
}
Write-Output 'PASS HTTP redirects to HTTPS'

foreach ($path in @('/readyz', '/api/internal/diagnostics/eventing')) {
    try {
        Invoke-WebRequest -Uri "$base$path" -UseBasicParsing -TimeoutSec 15 | Out-Null
        throw "Private endpoint exposed: $path"
    } catch {
        if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 404) { throw }
    }
}
Write-Output 'PASS private diagnostics are not public'

if ($AccessToken) {
    $headers = @{ Authorization = "Bearer $AccessToken" }
    $profile = Get-Json '/api/mobile/v1/me' $headers
    if (!$profile) { throw 'Authenticated profile response is empty.' }
    $negotiate = Invoke-RestMethod -Method Post -Uri "$base/hubs/mobile-notifications/negotiate?negotiateVersion=1" `
        -Headers $headers -TimeoutSec 15
    if (!$negotiate.connectionId) { throw 'SignalR negotiation did not return a connection.' }
    Write-Output 'PASS authenticated profile and HTTPS SignalR negotiation (not WSS acceptance)'
} else {
    Write-Output 'SKIP authenticated profile/SignalR negotiation: set VIETTABLE_PROD_TEST_ACCESS_TOKEN'
}

if ($CheckPrivatePorts) {
    foreach ($port in @(1433, 5672, 15672, 5100, 5101, 5102, 5103, 5104, 5105)) {
        $client = [Net.Sockets.TcpClient]::new()
        try {
            $attempt = $client.BeginConnect($hostName, $port, $null, $null)
            if ($attempt.AsyncWaitHandle.WaitOne(1500)) {
                try { $client.EndConnect($attempt); throw "Private port $port is reachable on public host." }
                catch [Net.Sockets.SocketException] { }
            }
        } finally { $client.Dispose() }
    }
    Write-Output 'PASS private service/SQL/RabbitMQ ports are not reachable on the public host'
}

Write-Output 'Selected public HTTPS smoke passed. WSS, VNPAY and Samsung public-network acceptance remain separate checks.'
