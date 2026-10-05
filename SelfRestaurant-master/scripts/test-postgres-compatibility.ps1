[CmdletBinding()]
param([switch]$KeepEnvironment)

$ErrorActionPreference = 'Stop'
$compatRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$compatMarker = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$compatSdk = "viettable-pg-sdk-$compatMarker"
$compatDb = "viettable-pg-db-$compatMarker"
$compatNetwork = "viettable-pg-net-$compatMarker"
$compatConnection = "Host=$compatDb;Database=postgres;Username=postgres"
$compatBytes = New-Object byte[] 48
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($compatBytes)
$compatSigningKey = [Convert]::ToBase64String($compatBytes)

function Invoke-CompatDocker {
    & docker @args
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL compatibility Docker step failed.' }
}

try {
    # Trust authentication is ONLY for this isolated, unpublished, disposable DB.
    # No existing container, SQL Server database or Docker volume is touched.
    Invoke-CompatDocker network create --internal --label "viettable.postgres-test=$compatMarker" $compatNetwork
    Invoke-CompatDocker run -d --name $compatDb --label "viettable.postgres-test=$compatMarker" --network $compatNetwork `
        --tmpfs /var/lib/postgresql/data -e POSTGRES_HOST_AUTH_METHOD=trust postgres:16-alpine
    Invoke-CompatDocker run -d --name $compatSdk --label "viettable.postgres-test=$compatMarker" `
        --mount "type=bind,source=$compatRoot,target=/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 tail -f /dev/null
    Invoke-CompatDocker network connect $compatNetwork $compatSdk
    foreach ($compatAttempt in 1..40) {
        & docker exec $compatDb pg_isready -U postgres | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
        if ($compatAttempt -eq 40) { throw 'Disposable PostgreSQL did not become ready.' }
        Start-Sleep -Milliseconds 500
    }
    # Build BEFORE starting API processes; do not overwrite loaded binaries.
    Invoke-CompatDocker exec $compatSdk dotnet build SelfRestaurant.Microservices.sln --nologo -v:minimal
    Invoke-CompatDocker exec $compatSdk dotnet test tests/SelfRestaurant.Backend.ContractTests/SelfRestaurant.Backend.ContractTests.csproj --nologo -v:minimal
    Invoke-CompatDocker exec $compatSdk dotnet build tests/SelfRestaurant.Postgres.Tests/SelfRestaurant.Postgres.Tests.csproj --nologo -v:minimal
    Invoke-CompatDocker exec -e "VIETTABLE_POSTGRES_TEST_CONNECTION=$compatConnection" $compatSdk dotnet test `
        tests/SelfRestaurant.Postgres.Tests/SelfRestaurant.Postgres.Tests.csproj --no-build --nologo -v:minimal --filter Category=PostgresIntegration
    $compatOwners = @('Gateway', 'Catalog', 'Orders', 'Customers', 'Identity', 'Billing')
    for ($compatIndex = 0; $compatIndex -lt $compatOwners.Length; $compatIndex++) {
        $compatOwner = $compatOwners[$compatIndex]
        $compatDirectory = if ($compatOwner -eq 'Gateway') { 'Gateway' } else { 'Services' }
        $compatArguments = @('exec', '-d', '-w', "/workspace/src/$compatDirectory/SelfRestaurant.$compatOwner.Api",
            '-e', "PORT=$(6100 + $compatIndex)", '-e', 'ASPNETCORE_ENVIRONMENT=Production',
            '-e', 'Logging__LogLevel__Default=Warning', '-e', 'Database__Provider=Postgres',
            '-e', 'Database__SeedProfile=Demo', '-e', "MobileJwt__SigningKey=$compatSigningKey",
            '-e', 'RabbitMq__Enabled=false', '-e', 'IntegrationEvents__WriteDiagnosticFiles=false',
            '-e', 'Services__Catalog=http://127.0.0.1:6101', '-e', 'Services__Orders=http://127.0.0.1:6102',
            '-e', 'Services__Customers=http://127.0.0.1:6103', '-e', 'Services__Identity=http://127.0.0.1:6104',
            '-e', 'Services__Billing=http://127.0.0.1:6105')
        if ($compatOwner -ne 'Gateway') { $compatArguments += @('-e', "ConnectionStrings__${compatOwner}Db=$compatConnection") }
        $compatArguments += @($compatSdk, 'dotnet', "bin/Debug/net8.0/SelfRestaurant.$compatOwner.Api.dll")
        Invoke-CompatDocker @compatArguments
    }
    foreach ($compatPort in 6100..6105) {
        foreach ($compatAttempt in 1..40) {
            & docker exec $compatSdk curl --fail --silent "http://127.0.0.1:$compatPort/readyz" | Out-Null
            if ($LASTEXITCODE -eq 0) { break }
            if ($compatAttempt -eq 40) { throw "Isolated API port $compatPort did not become ready." }
            Start-Sleep -Milliseconds 500
        }
    }
    Invoke-CompatDocker exec -e "VIETTABLE_POSTGRES_TEST_CONNECTION=$compatConnection" $compatSdk dotnet test `
        tests/SelfRestaurant.Postgres.Tests/SelfRestaurant.Postgres.Tests.csproj --no-build --nologo -v:minimal
    Write-Output 'PASS isolated PostgreSQL compatibility tests. No SQL Server fixture was created.'
}
finally {
    Remove-Variable compatSigningKey, compatBytes -ErrorAction SilentlyContinue
    if ($KeepEnvironment) {
        Write-Output "Disposable environment retained: $compatSdk / $compatDb / $compatNetwork (no published ports)."
    }
    else {
        # Avoid quoted Go-template index keys: Windows PowerShell 5 strips their
        # quotes when passing native arguments. Filter labels, then exact names.
        $compatOwnedContainers = @(& docker ps -a --filter "label=viettable.postgres-test=$compatMarker" --format '{{.Names}}')
        foreach ($compatContainer in @($compatSdk, $compatDb)) {
            if ($compatOwnedContainers -contains $compatContainer) {
                & docker rm -f $compatContainer | Out-Null
            }
        }
        $compatOwnedNetworks = @(& docker network ls --filter "label=viettable.postgres-test=$compatMarker" --format '{{.Name}}')
        if ($compatOwnedNetworks -contains $compatNetwork) { & docker network rm $compatNetwork | Out-Null }
        Write-Output 'Removed only the labelled disposable test environment; its temporary test data is not retained.'
    }
}
