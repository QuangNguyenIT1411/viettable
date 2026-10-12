[CmdletBinding()]
param([ValidateRange(1,5)][int]$Samples=3)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'lib\preview-qa-http.ps1')
try{
    $password=Read-QaCredential 'CloudHostAcceptancePassword'
    foreach($origin in @('https://viettable-k8j32aegw-hienkhanhvu981-7145.vercel.app','https://viettable-staging-hienkhanhvu981.vercel.app')){
        $script:QaOrigin=$origin
        $login=Assert-QaOk (Invoke-QaPreview '/api/mobile/v1/auth/login' @{username='q1uat_20261012';password=$password})
        if($login.user.customerId -ne 23){throw 'QA ownership mismatch'}
        $token=$login.accessToken
        $menu=Assert-QaOk (Invoke-QaPreview '/api/mobile/v1/branches/1/menu' -Token $token)
        Write-Output ('CATALOG: '+(@{origin=$origin;categories=@($menu.categories).Count;dishes=@($menu.categories|ForEach-Object {$_.dishes}).Count}|ConvertTo-Json -Compress))
        for($index=0;$index -lt $Samples;$index++){
            $invoices=Assert-QaOk (Invoke-QaPreview '/api/mobile/v1/invoices?limit=100' -Token $token)
            $invoice=@($invoices.items|Where-Object invoiceId -eq 23)
            if($invoice.Count -ne 1 -or $invoice[0].totalAmount -ne 10000){throw 'Invoice history mismatch'}
            $orders=Assert-QaOk (Invoke-QaPreview '/api/mobile/v1/orders?scope=history' -Token $token)
            foreach($id in @(42,43)){
                $order=@($orders|Where-Object orderId -eq $id)
                if($order.Count -ne 1 -or $order[0].status -ne 'COMPLETED'){throw 'Order history mismatch'}
            }
        }
        Write-Output ('PASS: '+$origin+' populated histories, '+$Samples+' reads each, exact QA IDs and totals; no business mutation.')
    }
}catch{Write-Output ('BLOCKED: read comparison; type='+$_.Exception.GetBaseException().GetType().Name);exit 1}
finally{$password=$null;$token=$null;$login=$null;Write-Output ('SAMPLES: '+($script:QaSamples|ConvertTo-Json -Depth 5 -Compress))}
