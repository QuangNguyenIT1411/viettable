$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'lib\preview-qa-http.ps1')
try{
    $password=Read-QaCredential 'CloudHostAcceptancePassword'
    $customer=Assert-QaOk (Invoke-QaPreview '/api/mobile/v1/auth/login' @{username='q1uat_20261012';password=$password})
    if($customer.user.customerId -ne 23){throw 'QA customer mismatch'}
    $actors=@()
    foreach($account in @(@{name='hostqa_8c074c45aa3ed29c022249edchef0';role='CHEF';branch=3},@{name='hostqa_8c074c45aa3ed29c022249edcashier1';role='CASHIER';branch=2})){
        $auth=Invoke-QaPreview '/api/gateway/staff/auth/login' @{username=$account.name;password=$password}
        $identity=Assert-QaOk $auth
        if($identity.session.staff.roleCode -ne $account.role -or $identity.session.staff.branchId -ne $account.branch){throw 'QA staff scope mismatch'}
        $actors+=@{cookie=$auth.cookie;role=$account.role;branchId=$account.branch}
    }
    $payload=@{origin=$script:QaOrigin;customerToken=$customer.accessToken;staff=$actors}|ConvertTo-Json -Depth 6 -Compress
    $node='C:\Users\crkon\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
    $cli='C:\Users\crkon\AppData\Local\npm-cache\_npx\67eb4586ca667318\node_modules\vercel\dist\index.js'
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=$node;$start.Arguments='"'+$cli+'" env run --project viettable-api --scope hienkhanhvu981-7145 --environment development -- "'+$node+'" "'+(Join-Path $PSScriptRoot 'q1-wss-isolation.mjs')+'"'
    $start.WorkingDirectory='C:\Users\crkon\AppData\Local\Temp\VietTable-staff-preview-d64ab1abb0f644c485e8d0064ed2fc40'
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $process=[Diagnostics.Process]::Start($start);$process.StandardInput.Write($payload);$process.StandardInput.Close()
    $err=$process.StandardError.ReadToEndAsync()
    while(-not $process.StandardOutput.EndOfStream){$line=$process.StandardOutput.ReadLine();if($line.StartsWith('{')){Write-Output $line}}
    $process.WaitForExit();$null=$err.Result;if($process.ExitCode){throw 'Negative WSS acceptance incomplete'}
}catch{Write-Output ('BLOCKED: negative WSS; type='+$_.Exception.GetBaseException().GetType().Name);exit 1}
finally{$password=$null;$customer=$null;$auth=$null;$identity=$null;$actors=$null;$payload=$null}
