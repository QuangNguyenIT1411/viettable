[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$sessionRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sessionMarker = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$sessionRedis = "viettable-session-redis-$sessionMarker"
$sessionSdk = "viettable-session-sdk-$sessionMarker"
$sessionNetwork = "viettable-session-net-$sessionMarker"
function Invoke-SessionDocker {
    & docker @args
    if ($LASTEXITCODE -ne 0) { throw 'Disposable session Docker test failed.' }
}
try {
    Invoke-SessionDocker network create --internal --label "viettable.session-test=$sessionMarker" $sessionNetwork
    # No published ports, no production volumes, no cloud compute.
    Invoke-SessionDocker run -d --name $sessionRedis --label "viettable.session-test=$sessionMarker" --network $sessionNetwork `
        redis:7-alpine redis-server --appendonly yes --appendfsync always --maxmemory-policy noeviction
    Invoke-SessionDocker run -d --name $sessionSdk --label "viettable.session-test=$sessionMarker" `
        --mount "type=bind,source=$sessionRoot,target=/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 tail -f /dev/null
    Invoke-SessionDocker network connect $sessionNetwork $sessionSdk
    foreach ($sessionAttempt in 1..40) {
        & docker exec $sessionRedis redis-cli ping | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
        if ($sessionAttempt -eq 40) { throw 'Disposable Redis readiness timeout.' }
        Start-Sleep -Milliseconds 500
    }
    Invoke-SessionDocker exec -e "VIETTABLE_REDIS_TEST_CONNECTION=${sessionRedis}:6379" $sessionSdk dotnet test `
        tests/SelfRestaurant.Backend.ContractTests/SelfRestaurant.Backend.ContractTests.csproj --nologo -v:minimal
    Write-Host 'PASS: real Redis Session, DataProtection, host restart, concurrent hosts and environment isolation.'
}
finally {
    $sessionOwned = @(& docker ps -a --filter "label=viettable.session-test=$sessionMarker" --format '{{.Names}}')
    foreach ($sessionContainer in @($sessionSdk, $sessionRedis)) {
        if ($sessionOwned -contains $sessionContainer) { & docker rm -fv $sessionContainer | Out-Null }
    }
    $sessionOwnedNetworks = @(& docker network ls --filter "label=viettable.session-test=$sessionMarker" --format '{{.Name}}')
    if ($sessionOwnedNetworks -contains $sessionNetwork) { & docker network rm $sessionNetwork | Out-Null }
    Write-Host 'Removed only labelled disposable session resources; existing Docker volumes were not touched.'
}
