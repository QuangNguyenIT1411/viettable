param(
    [string]$BaseUrl = "http://localhost:5088",
    [int]$BranchId = 1,
    [int]$DishId = 1
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$siblingRoot = Split-Path -Parent $repoRoot
$oldSitePath = Join-Path $siblingRoot "SelfRestaurant-main_OLD\SelfRestaurant"
$iisExpressExe = "C:\Program Files\IIS Express\iisexpress.exe"
$logDir = Join-Path $repoRoot ".runlogs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$logPath = Join-Path $logDir "customer_detailed_old_$ts.log"

function Write-Log([string]$msg){
  $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $msg
  Write-Host $line
  Add-Content -Path $logPath -Value $line
}
function Add-Result($step,[bool]$pass,$detail){
  $state = if($pass){"PASS"}else{"FAIL"}
  Write-Log "[$state] $step :: $detail"
  $script:results += [pscustomobject]@{Step=$step;Pass=$pass;Detail=$detail}
}
function Tok([string]$h){
  $ps=@('name="__RequestVerificationToken"[^>]*value="([^"]+)"',"name='__RequestVerificationToken'[^>]*value='([^']+)'",'value="([^"]+)"[^>]*name="__RequestVerificationToken"')
  foreach($p in $ps){$m=[regex]::Match($h,$p,[System.Text.RegularExpressions.RegexOptions]::IgnoreCase); if($m.Success){return $m.Groups[1].Value}}
  throw 'No anti-forgery token'
}
function Json($r){ try { return ([string]$r.Content | ConvertFrom-Json) } catch { return $null } }
function Start-OldIIS {
  Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force
  $port=([Uri]$BaseUrl).Port
  $p=Start-Process -FilePath $iisExpressExe -ArgumentList "/path:$oldSitePath", "/port:$port" -PassThru
  for($i=0;$i -lt 25;$i++){
    Start-Sleep -Milliseconds 800
    try{
      $r=Invoke-WebRequest "$BaseUrl/" -UseBasicParsing -TimeoutSec 5
      if($r.StatusCode -ge 200){ return $p }
    } catch {}
  }
  throw "Cannot start old IIS"
}

$results=@()
$proc=$null
$s=New-Object Microsoft.PowerShell.Commands.WebRequestSession

try {
  $proc = Start-OldIIS
  Add-Result "Start OLD IIS" $true "pid=$($proc.Id)"

  $lp=Invoke-WebRequest "$BaseUrl/Customer/Login" -WebSession $s -UseBasicParsing
  $lt=Tok $lp.Content
  $lr=Invoke-WebRequest "$BaseUrl/Customer/Login" -Method Post -WebSession $s -UseBasicParsing -Headers @{ "X-Requested-With"="XMLHttpRequest" } -Body @{
    __RequestVerificationToken=$lt; username='lan.nguyen'; password='123456'; rememberMe='false'; returnUrl=''
  }
  $lj=Json $lr
  $loginOk = $false
  if($lj -and $lj.success -eq $true){ $loginOk = $true }
  Add-Result "Login" $loginOk "raw=$($lr.Content)"
  if(-not $loginOk){ throw "Customer login failed" }

  $tb=Invoke-WebRequest "$BaseUrl/Home/GetBranchTables?branchId=$BranchId" -WebSession $s -UseBasicParsing
  $tj=Json $tb
  $tableId=0
  if($tj -and $tj.success -eq $true -and $tj.tables){
    $av=@($tj.tables | Where-Object { $_.isAvailable -eq $true })
    if($av.Count -gt 0){ $tableId=[int]$av[0].tableId }
  }
  if($tableId -le 0){ $tableId = 1 }
  Add-Result "Select Table" $true "branch=$BranchId table=$tableId"

  $menu=Invoke-WebRequest "$BaseUrl/Menu/Index?tableId=$tableId&branchId=$BranchId&tableNumber=$tableId" -WebSession $s -UseBasicParsing
  $mt=Tok $menu.Content
  Add-Result "Open Menu" ($menu.StatusCode -eq 200) "status=$($menu.StatusCode)"
  try {
    $reset = Invoke-WebRequest "$BaseUrl/Menu/ResetTable" -Method Post -WebSession $s -UseBasicParsing -ContentType 'application/x-www-form-urlencoded' -Body @{tableId=$tableId;branchId=$BranchId;__RequestVerificationToken=$mt}
    Add-Result "Reset Table" ($reset.StatusCode -in 200,302) "status=$($reset.StatusCode)"
  } catch {
    Add-Result "Reset Table" $false $_.Exception.Message
  }

  $add=Invoke-WebRequest "$BaseUrl/Order/AddItem" -Method Post -WebSession $s -UseBasicParsing -Body @{tableId=$tableId;dishId=$DishId;quantity=1;note='old-detail'}
  $aj=Json $add
  $addOk = $false
  $orderId = 0
  if($aj -and $aj.success -eq $true){
    $addOk = $true
    if($aj.orderId){ $orderId=[int]$aj.orderId }
  }
  Add-Result "Add Item" $addOk "raw=$($add.Content)"
  if(-not $addOk){ throw "Add item failed" }

  $itemsResp=Invoke-WebRequest "$BaseUrl/Order/GetOrderItems?tableId=$tableId" -WebSession $s -UseBasicParsing
  $itemsRaw=[string]$itemsResp.Content
  $itemId=0
  if($itemsRaw -match '"itemId"\s*:\s*(\d+)'){ $itemId=[int]$Matches[1] }
  if($itemId -le 0 -and $itemsRaw -match '"ItemID"\s*:\s*(\d+)'){ $itemId=[int]$Matches[1] }
  if($orderId -le 0 -and $itemsRaw -match '"orderId"\s*:\s*(\d+)'){ $orderId=[int]$Matches[1] }
  Add-Result "Read Order Items" ($itemId -gt 0 -and $orderId -gt 0) "orderId=$orderId itemId=$itemId"

  $upd=Invoke-WebRequest "$BaseUrl/Order/UpdateQuantity" -Method Post -WebSession $s -UseBasicParsing -Body @{tableId=$tableId;itemId=$itemId;quantity=2}
  $uj=Json $upd
  Add-Result "Update Quantity" ([bool]($uj -and $uj.success -eq $true)) "raw=$($upd.Content)"

  $rm=Invoke-WebRequest "$BaseUrl/Order/RemoveItem" -Method Post -WebSession $s -UseBasicParsing -Body @{tableId=$tableId;itemId=$itemId}
  $rj=Json $rm
  Add-Result "Remove Item" ([bool]($rj -and $rj.success -eq $true)) "raw=$($rm.Content)"

  $add2=Invoke-WebRequest "$BaseUrl/Order/AddItem" -Method Post -WebSession $s -UseBasicParsing -Body @{tableId=$tableId;dishId=$DishId;quantity=1;note='old-detail-submit'}
  $a2j=Json $add2
  Add-Result "Add Item Again" ([bool]($a2j -and $a2j.success -eq $true)) "raw=$($add2.Content)"

  # Sau khi xóa item, hệ thống có thể tạo order mới; luôn cập nhật lại orderId để check trạng thái đúng order vừa gửi bếp.
  if($a2j -and $a2j.orderId){ $orderId=[int]$a2j.orderId }
  $send=Invoke-WebRequest "$BaseUrl/Order/SendToKitchen" -Method Post -WebSession $s -UseBasicParsing -Body @{tableId=$tableId}
  $sj=Json $send
  Add-Result "Send To Kitchen" ([bool]($sj -and $sj.success -eq $true)) "raw=$($send.Content)"

  $status=Invoke-WebRequest "$BaseUrl/Home/GetOrderStatus?orderId=$orderId" -WebSession $s -UseBasicParsing
  $stj=Json $status
  $statusCode = $null
  if($stj -and $stj.order -and $stj.order.statusCode){ $statusCode=[string]$stj.order.statusCode }
  if(-not $statusCode -and $stj -and $stj.statusCode){ $statusCode=[string]$stj.statusCode }
  Add-Result "Check Order Status" ([bool]($stj -and $stj.success -eq $true)) "statusCode=$statusCode"

  try {
    $dash=Invoke-WebRequest "$BaseUrl/Customer/Dashboard" -WebSession $s -UseBasicParsing
    $dt=Tok $dash.Content
    $up=Invoke-WebRequest "$BaseUrl/Customer/UpdateProfile" -Method Post -WebSession $s -UseBasicParsing -ContentType 'application/x-www-form-urlencoded' -Body @{username='lan.nguyen';name='Lan Nguyen';email='';phoneNumber='';gender='Nu';address='HCM';dateOfBirth='1999-01-01';__RequestVerificationToken=$dt}
    Add-Result "Update Profile" ($up.StatusCode -in 200,302) "status=$($up.StatusCode)"
  } catch {
    Add-Result "Update Profile" $false $_.Exception.Message
  }
}
catch {
  Add-Result "Unhandled Error" $false $_.Exception.Message
}
finally {
  if($proc){ try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch {} }
}

$pass = @($results | Where-Object { $_.Pass }).Count
$total = $results.Count
$summary = [pscustomobject]@{ timestamp=$ts; passed=$pass; total=$total; log=$logPath; results=$results }
$summaryPath = Join-Path $logDir "customer_detailed_old_summary_$ts.json"
$summary | ConvertTo-Json -Depth 20 | Set-Content -Path $summaryPath -Encoding UTF8
Write-Log "SUMMARY: $pass/$total PASS"
Write-Log "SUMMARY_JSON: $summaryPath"
$results | Format-Table -AutoSize | Out-String | Write-Output
Write-Output "SUMMARY_PASS=$pass/$total"
Write-Output "SUMMARY_JSON=$summaryPath"
Write-Output "SUMMARY_LOG=$logPath"
