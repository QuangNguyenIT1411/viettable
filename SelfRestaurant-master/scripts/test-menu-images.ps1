param([string]$Gateway = 'http://127.0.0.1:5100')

$ErrorActionPreference = 'Stop'
$branches = $null
for ($attempt = 1; $attempt -le 20; $attempt++) {
    try { $branches = Invoke-RestMethod "$Gateway/api/mobile/v1/branches" -TimeoutSec 5; break }
    catch { if ($attempt -eq 20) { throw }; Start-Sleep -Seconds 1 }
}
if ($branches.Count -eq 0) { throw 'No branches returned by Gateway.' }

$checked = @{}
$failures = [System.Collections.Generic.List[string]]::new()
foreach ($branch in $branches) {
    $menu = Invoke-RestMethod "$Gateway/api/mobile/v1/branches/$($branch.branchId)/menu"
    $dishes = @($menu.categories | ForEach-Object { $_.dishes })
    if ($dishes.Count -eq 0) { $failures.Add("Branch $($branch.branchId): empty menu"); continue }
    foreach ($dish in $dishes) {
        if ([string]::IsNullOrWhiteSpace($dish.image) -or -not $dish.image.StartsWith('/images/')) {
            $failures.Add("Dish $($dish.dishId): invalid image path '$($dish.image)'")
            continue
        }
        if ($checked.ContainsKey($dish.image)) { continue }
        try {
            $response = Invoke-WebRequest "$Gateway$($dish.image)" -UseBasicParsing -TimeoutSec 15
            $type = [string]$response.Headers['Content-Type']
            if ($response.StatusCode -ne 200 -or -not $type.StartsWith('image/')) {
                $failures.Add("$($dish.image): HTTP $($response.StatusCode), $type")
            }
        } catch {
            $failures.Add("$($dish.image): $($_.Exception.Message)")
        }
        $checked[$dish.image] = $true
    }
    Write-Host "Branch $($branch.branchId): $($dishes.Count) dishes checked"
}

if ($failures.Count -gt 0) { $failures | ForEach-Object { Write-Error $_ }; exit 1 }
Write-Host "PASS: $($branches.Count) branches, $($checked.Count) unique menu images return HTTP 200 image content."
