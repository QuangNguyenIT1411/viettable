$ErrorActionPreference = 'Stop'

$response = Invoke-WebRequest 'https://localhost:7100' -UseBasicParsing
if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300)
{
    throw "HTTPS gateway khong phan hoi tren https://localhost:7100 (status=$($response.StatusCode))"
}

Write-Output "HTTPS gateway responded successfully on https://localhost:7100 (status=$($response.StatusCode))"
