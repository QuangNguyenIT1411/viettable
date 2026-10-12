# No standalone output. Callers retain raw bodies/credentials in RAM only.
$script:QaOrigin='https://viettable-k8j32aegw-hienkhanhvu981-7145.vercel.app'
$script:QaSamples=[Collections.Generic.List[object]]::new()
function Read-QaCredential([string]$Name){
    $file=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('VietTable\staging-secrets\'+$Name+'.dpapi')
    $secure=ConvertTo-SecureString ([IO.File]::ReadAllText($file));$pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try{return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)}finally{[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer);$secure.Dispose()}
}
function Invoke-QaPreview([string]$Path,$Body=$null,[string]$Token='', [string]$Cookie='', [string]$Key=''){
    if($script:QaOrigin -notin @('https://viettable-k8j32aegw-hienkhanhvu981-7145.vercel.app','https://viettable-staging-hienkhanhvu981.vercel.app')){throw 'Unapproved staging origin.'}
    if(-not $Path.StartsWith('/') -or $Path.StartsWith('//') -or $Path -match '(token|password|secret)=|\s'){throw 'Unsafe API path.'}
    $config="silent`ninclude`nmax-time = 65`n"
    if($null -ne $Body){
        $json=$Body|ConvertTo-Json -Depth 12 -Compress
        # ASCII-safe JSON preserves Unicode through legacy Windows PowerShell stdin.
        $json=[regex]::Replace($json,'[^\x00-\x7F]',{param($match) '\u'+([int][char]$match.Value).ToString('x4')})
        $config+="request = POST`nheader = `"Content-Type: application/json`"`ndata = `""+$json.Replace('\','\\').Replace('"','\"')+"`"`n"
    }
    foreach($header in @($(if($Token){'Authorization: Bearer '+$Token}),$(if($Cookie){'Cookie: '+$Cookie}),$(if($Key){'Idempotency-Key: '+$Key}),('X-Correlation-Id: q1qa-'+[Guid]::NewGuid().ToString('N')))){
        if($header){$config+='header = "'+$header.Replace('"','\"')+'"'+"`n"}
    }
    $node='C:\Users\crkon\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
    $cli='C:\Users\crkon\AppData\Local\npm-cache\_npx\67eb4586ca667318\node_modules\vercel\dist\index.js'
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=$node;$start.Arguments='"'+$cli+'" curl "'+$Path+'" --deployment "'+$script:QaOrigin+'" --scope hienkhanhvu981-7145 -- --config - --write-out "\nMETA%{json}"'
    $start.WorkingDirectory='C:\Users\crkon\AppData\Local\Temp\VietTable-staff-preview-d64ab1abb0f644c485e8d0064ed2fc40'
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.StandardOutputEncoding=New-Object Text.UTF8Encoding -ArgumentList $false
    $process=[Diagnostics.Process]::Start($start);$process.StandardInput.Write($config);$process.StandardInput.Close()
    $outTask=$process.StandardOutput.ReadToEndAsync();$errTask=$process.StandardError.ReadToEndAsync();$process.WaitForExit()
    $output=$outTask.Result;$null=$errTask.Result;$exit=$process.ExitCode;$process.Dispose()
    $marker=$output.LastIndexOf("`nMETA")
    if($marker -lt 0){throw 'Missing transport metadata.'}
    $meta=$output.Substring($marker+5)|ConvertFrom-Json;$raw=$output.Substring(0,$marker)
    $sample=[ordered]@{deployment=$script:QaOrigin;path=$Path;http=$meta.http_code;seconds=$meta.time_total;curlExit=$meta.exitcode;exit=$exit;
        instance=[regex]::Match($raw,'(?im)^x-viettable-instance:\s*(.+)$').Groups[1].Value.Trim();
        sequence=[regex]::Match($raw,'(?im)^x-viettable-request-sequence:\s*(.+)$').Groups[1].Value.Trim();
        timing=[regex]::Match($raw,'(?im)^server-timing:\s*(.+)$').Groups[1].Value.Trim()}
    $script:QaSamples.Add($sample)
    if($exit -ne 0 -or $meta.exitcode -ne 0){throw 'Preview network failure; no write retry.'}
    $parts=$raw -split '\r?\n\r?\n';$bodyText=$parts[-1]
    $cookies=[regex]::Matches($raw,'(?im)^set-cookie:\s*([^;\r\n]+)')|ForEach-Object {$_.Groups[1].Value}
    $value=if($bodyText.Trim()){$bodyText|ConvertFrom-Json}else{$null}
    return @{http=[int]$meta.http_code;value=$value;cookie=($cookies -join '; ');sample=$sample}
}
function Assert-QaOk($Response){
    if($Response.http -notin @(200,201,204)){
        $script:QaLastFailure=@{http=$Response.http;code=$Response.value.code;errorFields=@($Response.value.errors.PSObject.Properties.Name);detailFields=@($Response.value.details.fields.PSObject.Properties.Name);detailsKeys=@($Response.value.details.PSObject.Properties.Name)}
        throw ('QA API failed HTTP '+$Response.http)
    };return $Response.value
}
