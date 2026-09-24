param([string]$Gateway = 'http://127.0.0.1:5100')

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$python = (Get-Command python -ErrorAction Stop).Source
& $python -c 'from PIL import Image' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Python Pillow is required to decode production images.' }

$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(20)
$images = @{}
$branchHashes = @{}
try {
    $branches = Invoke-RestMethod "$Gateway/api/mobile/v1/branches"
    if ($branches.Count -eq 0) { throw 'Gateway returned no active branches.' }

    function Assert-Image([string]$path, [int]$minWidth, [int]$minHeight) {
        if ([string]::IsNullOrWhiteSpace($path) -or -not $path.StartsWith('/images/')) {
            throw "Missing or non-Gateway image path: $path"
        }
        if ($images.ContainsKey($path)) { return $images[$path] }
        $response = $client.GetAsync("$Gateway$path").GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) { throw "$path returned HTTP $([int]$response.StatusCode)" }
        $mime = [string]$response.Content.Headers.ContentType.MediaType
        if ($mime -notin @('image/webp', 'image/jpeg', 'image/png')) { throw "$path returned $mime" }
        $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        if ($bytes.Length -lt 1000) { throw "$path has only $($bytes.Length) bytes" }
        $temporary = [IO.Path]::GetTempFileName()
        try {
            [IO.File]::WriteAllBytes($temporary, $bytes)
            $size = & $python -c 'from PIL import Image; import sys; im=Image.open(sys.argv[1]); im.load(); print(im.width, im.height)' $temporary
            if ($LASTEXITCODE -ne 0) { throw "$path cannot be decoded" }
            $dimensions = @($size -split ' ')
            if ([int]$dimensions[0] -lt $minWidth -or [int]$dimensions[1] -lt $minHeight) {
                throw "$path is only $size pixels"
            }
        } finally { Remove-Item -LiteralPath $temporary -Force }
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($sha256.ComputeHash($bytes)).Replace('-', '') }
        finally { $sha256.Dispose() }
        $result = @{ hash = $hash; width = [int]$dimensions[0]; height = [int]$dimensions[1] }
        $images[$path] = $result
        return $result
    }

    $menuPaths = @{}
    $null = Assert-Image '/images/viettable-home-hero.webp' 1800 600
    $null = Assert-Image '/images/viettable-about-hero.webp' 1800 600
    $null = Assert-Image '/images/viettable-contact-hero.webp' 1800 600
    foreach ($branch in $branches) {
        $detail = Invoke-RestMethod "$Gateway/api/mobile/v1/branches/$($branch.branchId)"
        if ($detail.image -ne $branch.image) { throw "Branch $($branch.branchId) list/detail image mismatch" }
        $photo = Assert-Image $branch.image 1000 600
        if ($branchHashes.ContainsKey($photo.hash)) { throw "Branches $($branchHashes[$photo.hash]) and $($branch.branchId) share the same image" }
        $branchHashes[$photo.hash] = $branch.branchId

        $menu = Invoke-RestMethod "$Gateway/api/mobile/v1/branches/$($branch.branchId)/menu"
        $dishes = @($menu.categories | ForEach-Object { $_.dishes })
        if ($dishes.Count -eq 0) { throw "Branch $($branch.branchId) has no menu" }
        foreach ($dish in $dishes) {
            $null = Assert-Image $dish.image 500 350
            $menuPaths[$dish.image] = $true
        }
        Write-Host "Branch $($branch.branchId): image $($photo.width)x$($photo.height), $($dishes.Count) menu items PASS"
    }
    Write-Host "PASS: Branches $($branches.Count)/$($branches.Count); menu images $($menuPaths.Count) unique paths; customer heroes 3/3; broken required URLs 0."
} finally { $client.Dispose() }
