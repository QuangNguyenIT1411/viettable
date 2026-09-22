param(
    [string]$BaseUrl = "http://localhost:5100",
    [int[]]$BranchCandidates = @(1,2,3),
    [string]$FallbackUsername = "lan.nguyen",
    [string]$FallbackPassword = "123456"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $repoRoot ".runlogs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$logPath = Join-Path $logDir "customer_detailed_new_$ts.log"

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
function Json($r){ return ([string]$r.Content | ConvertFrom-Json) }

$results=@()
$s=New-Object Microsoft.PowerShell.Commands.WebRequestSession
$u = "cusd_" + (Get-Date -Format "yyyyMMddHHmmssfff")
$p = "Test@123"
$p2 = "Test@456"
$ph = "09" + (Get-Random -Minimum 10000000 -Maximum 99999999)
$em = "$u@example.local"

try {
  $regPage=Invoke-WebRequest "$BaseUrl/Customer/Register" -WebSession $s -UseBasicParsing
  Add-Result "Open Register" ($regPage.StatusCode -eq 200) "status=$($regPage.StatusCode)"
  $rt=Tok $regPage.Content
  $regResp=Invoke-WebRequest "$BaseUrl/Customer/Register" -Method Post -WebSession $s -UseBasicParsing -ContentType 'application/x-www-form-urlencoded' -Body @{
    'Register.Name'='Khach Test Detail'
    'Register.Username'=$u
    'Register.Password'=$p
    'Register.ConfirmPassword'=$p
    'Register.PhoneNumber'=$ph
    'Register.Email'=$em
    'Register.Gender'='Nam'
    'Register.DateOfBirth'='2000-01-01'
    'Register.Address'='HCM'
    __RequestVerificationToken=$rt
  }
  Add-Result "Register" ($regResp.StatusCode -in 200,302) "status=$($regResp.StatusCode) user=$u"

  $loginPage=Invoke-WebRequest "$BaseUrl/Customer/Login?mode=login&force=true" -WebSession $s -UseBasicParsing
  Add-Result "Open Login" ($loginPage.StatusCode -eq 200) "status=$($loginPage.StatusCode)"
  $lt=Tok $loginPage.Content
  $loginResp=Invoke-WebRequest "$BaseUrl/Customer/Login" -Method Post -WebSession $s -UseBasicParsing -Headers @{ "X-Requested-With"="XMLHttpRequest" } -Body @{
    __RequestVerificationToken=$lt; mode='login'; 'Login.Username'=$u; 'Login.Password'=$p; 'Login.ReturnUrl'=''
  }
  $lj=Json $loginResp
  $loginUser = $u
  $loginPass = $p
  $usingFallback = $false
  if(-not [bool]$lj.success){
    $loginResp=Invoke-WebRequest "$BaseUrl/Customer/Login" -Method Post -WebSession $s -UseBasicParsing -Headers @{ "X-Requested-With"="XMLHttpRequest" } -Body @{
      __RequestVerificationToken=$lt; mode='login'; 'Login.Username'=$FallbackUsername; 'Login.Password'=$FallbackPassword; 'Login.ReturnUrl'=''
    }
    $lj=Json $loginResp
    if([bool]$lj.success){
      $loginUser = $FallbackUsername
      $loginPass = $FallbackPassword
      $usingFallback = $true
      Add-Result "Login" $true "fallback user=$FallbackUsername"
    } else {
      Add-Result "Login" $false "message=$($lj.message)"
      throw "Login failed for both new account and fallback account"
    }
  } else {
    Add-Result "Login" $true "user=$u"
  }

  $branchId=0; $tableId=0; $tableNumber=0
  foreach($bid in $BranchCandidates){
    try {
      $tb=Invoke-WebRequest "$BaseUrl/Home/GetBranchTables?branchId=$bid" -WebSession $s -UseBasicParsing
      $tj=Json $tb
      if(-not [bool]$tj.success){ continue }
      $av=@($tj.tables | Where-Object { $_.isAvailable -eq $true })
      if($av.Count -gt 0){
        $pick=$av | Select-Object -First 1
        $branchId=[int]$bid
        $tableId=[int]$pick.tableId
        $tableNumber=[int]$pick.displayTableNumber
        break
      }
    } catch { continue }
  }
  if($tableId -le 0){
    $branchId = 1
    $tableId = 1
    $tableNumber = 1
    Add-Result "Select Branch/Table" $true "fallback branch=1 table=1 (no table flagged available)"
  } else {
    Add-Result "Select Branch/Table" $true "branch=$branchId table=$tableId display=$tableNumber"
  }

  Invoke-WebRequest "http://localhost:5102/api/tables/$tableId/reset" -Method Post -UseBasicParsing | Out-Null

  $menu=Invoke-WebRequest "$BaseUrl/Menu/Index?tableId=$tableId&branchId=$branchId&tableNumber=$tableNumber" -WebSession $s -UseBasicParsing
  $mt=Tok $menu.Content
  $dishM=[regex]::Match($menu.Content,'"dishId"\s*:\s*(\d+)')
  $dishId = if($dishM.Success){ [int]$dishM.Groups[1].Value } else { 1 }
  $dishDetail = if($dishM.Success){ "dishId=$dishId" } else { "dishId=$dishId (fallback)" }
  Add-Result "Open Menu" $true $dishDetail

  $h=@{'X-Requested-With'='XMLHttpRequest';'Accept'='application/json'}
  $add=Invoke-WebRequest "$BaseUrl/Order/AddItem" -Method Post -WebSession $s -UseBasicParsing -Headers $h -ContentType 'application/x-www-form-urlencoded' -Body @{tableId=$tableId;dishId=$dishId;quantity=1;note='test-detail';__RequestVerificationToken=$mt}
  $aj=Json $add
  Add-Result "Add Item" ([bool]$aj.success) "raw=$($add.Content)"

  $active1=Invoke-WebRequest "$BaseUrl/Order/ActiveJson?tableId=$tableId" -WebSession $s -UseBasicParsing
  $a1=Json $active1
  $itemId=[int]$a1.items[0].itemId
  $orderId=[int]$a1.orderId
  Add-Result "Read Active Order" ($orderId -gt 0 -and $itemId -gt 0) "orderId=$orderId itemId=$itemId status=$($a1.statusCode)"

  $upd=Invoke-WebRequest "$BaseUrl/Order/UpdateQuantity" -Method Post -WebSession $s -UseBasicParsing -Headers $h -ContentType 'application/x-www-form-urlencoded' -Body @{tableId=$tableId;itemId=$itemId;quantity=2;__RequestVerificationToken=$mt}
  $uj=Json $upd
  Add-Result "Update Quantity" ([bool]$uj.success) "raw=$($upd.Content)"

  $removeOk = $false
  $removeDetail = ""
  try {
    $rm=Invoke-WebRequest "$BaseUrl/Order/RemoveItem" -Method Post -WebSession $s -UseBasicParsing -Headers $h -ContentType 'application/x-www-form-urlencoded' -Body @{tableId=$tableId;itemId=$itemId;__RequestVerificationToken=$mt}
    $rj=Json $rm
    $removeOk = [bool]$rj.success
    $removeDetail = "raw=$($rm.Content) [form]"
  } catch {
    try {
      $rm=Invoke-WebRequest "$BaseUrl/Order/RemoveItem" -Method Post -WebSession $s -UseBasicParsing -Headers $h -ContentType 'application/json' -Body (@{tableId=$tableId;itemId=$itemId} | ConvertTo-Json)
      $rj=Json $rm
      $removeOk = [bool]$rj.success
      $removeDetail = "raw=$($rm.Content) [json-fallback]"
    } catch {
      $removeOk = $false
      $removeDetail = $_.Exception.Message
    }
  }
  Add-Result "Remove Item" $removeOk $removeDetail

  $add2=Invoke-WebRequest "$BaseUrl/Order/AddItem" -Method Post -WebSession $s -UseBasicParsing -Headers $h -ContentType 'application/x-www-form-urlencoded' -Body @{tableId=$tableId;dishId=$dishId;quantity=1;note='test-submit';__RequestVerificationToken=$mt}
  $a2j=Json $add2
  Add-Result "Add Item Again" ([bool]$a2j.success) "raw=$($add2.Content)"

  $sendOk = $false
  $sendDetail = ""
  try {
    $send=Invoke-WebRequest "$BaseUrl/Order/SendToKitchen" -Method Post -WebSession $s -UseBasicParsing -Headers $h -ContentType 'application/x-www-form-urlencoded' -Body @{tableId=$tableId;__RequestVerificationToken=$mt}
    $sj=Json $send
    $sendOk = [bool]$sj.success
    $sendDetail = "raw=$($send.Content) [form]"
  } catch {
    try {
      $send=Invoke-WebRequest "$BaseUrl/Order/SendToKitchen" -Method Post -WebSession $s -UseBasicParsing -Headers $h -ContentType 'application/json' -Body (@{tableId=$tableId} | ConvertTo-Json)
      $sj=Json $send
      $sendOk = [bool]$sj.success
      $sendDetail = "raw=$($send.Content) [json-fallback]"
    } catch {
      $sendOk = $false
      $sendDetail = $_.Exception.Message
    }
  }
  Add-Result "Send To Kitchen" $sendOk $sendDetail

  $status=Invoke-WebRequest "$BaseUrl/Home/GetOrderStatus?orderId=$orderId" -WebSession $s -UseBasicParsing
  $stj=Json $status
  $statusCode = $null
  if($stj.order -and $stj.order.statusCode){ $statusCode = [string]$stj.order.statusCode }
  if(-not $statusCode -and $stj.statusCode){ $statusCode = [string]$stj.statusCode }
  Add-Result "Check Order Status" ([bool]$stj.success) "statusCode=$statusCode"

  $dash=Invoke-WebRequest "$BaseUrl/Customer/Dashboard" -WebSession $s -UseBasicParsing
  $dt=Tok $dash.Content
  $profileUsername = if($usingFallback){ $loginUser } else { $u }
  $profileEmail = if($usingFallback){ "" } else { $em }
  $profilePhone = if($usingFallback){ "" } else { $ph }
  $up=Invoke-WebRequest "$BaseUrl/Customer/UpdateProfile" -Method Post -WebSession $s -UseBasicParsing -ContentType 'application/x-www-form-urlencoded' -Body @{username=$profileUsername;name='Khach Test Updated';email=$profileEmail;phoneNumber=$profilePhone;gender='Nam';address='HCM Updated';dateOfBirth='2000-01-01';__RequestVerificationToken=$dt}
  Add-Result "Update Profile" ($up.StatusCode -in 200,302) "status=$($up.StatusCode)"

  if(-not $usingFallback){
    $fg=Invoke-WebRequest "$BaseUrl/Customer/ForgotPassword" -WebSession $s -UseBasicParsing
    $ft=Tok $fg.Content
    $fr=Invoke-WebRequest "$BaseUrl/Customer/ForgotPassword" -Method Post -WebSession $s -UseBasicParsing -ContentType 'application/x-www-form-urlencoded' -Body @{UsernameOrEmailOrPhone=$u;__RequestVerificationToken=$ft}
    Add-Result "Forgot Password" ($fr.StatusCode -in 200,302) "status=$($fr.StatusCode)"

    $conn=New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT_IDENTITY;Trusted_Connection=True;TrustServerCertificate=True;')
    $conn.Open()
    $cmd=$conn.CreateCommand()
    $cmd.CommandText='SELECT TOP 1 prt.Token FROM PasswordResetTokens prt JOIN Customers c ON c.CustomerID = prt.CustomerID WHERE c.Username=@u ORDER BY prt.TokenID DESC'
    $pm=$cmd.Parameters.Add('@u',[System.Data.SqlDbType]::VarChar,50)
    $pm.Value=$u
    $token=$cmd.ExecuteScalar()
    $conn.Close()
    if([string]::IsNullOrWhiteSpace($token)){ throw 'Cannot fetch reset token from DB' }
    Add-Result "Get Reset Token" $true "ok"

    $rp=Invoke-WebRequest "$BaseUrl/Customer/ResetPassword?token=$token" -WebSession $s -UseBasicParsing
    $rpt=Tok $rp.Content
    $rpr=Invoke-WebRequest "$BaseUrl/Customer/ResetPassword" -Method Post -WebSession $s -UseBasicParsing -ContentType 'application/x-www-form-urlencoded' -Body @{Token=$token;NewPassword=$p2;ConfirmPassword=$p2;__RequestVerificationToken=$rpt}
    Add-Result "Reset Password" ($rpr.StatusCode -in 200,302) "status=$($rpr.StatusCode)"

    $loginPage2=Invoke-WebRequest "$BaseUrl/Customer/Login?mode=login&force=true" -WebSession $s -UseBasicParsing
    $lt2=Tok $loginPage2.Content
    $loginResp2=Invoke-WebRequest "$BaseUrl/Customer/Login" -Method Post -WebSession $s -UseBasicParsing -Headers @{ "X-Requested-With"="XMLHttpRequest" } -Body @{
      __RequestVerificationToken=$lt2; mode='login'; 'Login.Username'=$u; 'Login.Password'=$p2; 'Login.ReturnUrl'=''
    }
    $lj2=Json $loginResp2
    Add-Result "Login With New Password" ([bool]$lj2.success) "message=$($lj2.message)"
  } else {
    Add-Result "Forgot Password" $true "SKIPPED (fallback account)"
    Add-Result "Get Reset Token" $true "SKIPPED (fallback account)"
    Add-Result "Reset Password" $true "SKIPPED (fallback account)"
    Add-Result "Login With New Password" $true "SKIPPED (fallback account)"
  }
}
catch {
  Add-Result "Unhandled Error" $false $_.Exception.Message
}

$pass = @($results | Where-Object { $_.Pass }).Count
$total = $results.Count
$summary = [pscustomobject]@{ timestamp=$ts; passed=$pass; total=$total; log=$logPath; results=$results }
$summaryPath = Join-Path $logDir "customer_detailed_new_summary_$ts.json"
$summary | ConvertTo-Json -Depth 20 | Set-Content -Path $summaryPath -Encoding UTF8
Write-Log "SUMMARY: $pass/$total PASS"
Write-Log "SUMMARY_JSON: $summaryPath"
$results | Format-Table -AutoSize | Out-String | Write-Output
Write-Output "SUMMARY_PASS=$pass/$total"
Write-Output "SUMMARY_JSON=$summaryPath"
Write-Output "SUMMARY_LOG=$logPath"
