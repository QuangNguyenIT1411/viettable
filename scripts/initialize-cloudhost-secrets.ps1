[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Windows DPAPI required.' }
$taskStore = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'VietTable\staging-secrets'
[void](New-Item -ItemType Directory -Path $taskStore -Force)
foreach ($taskName in @('CloudHostSigningKey','CloudHostDataProtectionCertificate','CloudHostAcceptancePassword')) {
    $taskPath = Join-Path $taskStore ($taskName + '.dpapi')
    if (Test-Path -LiteralPath $taskPath) { Write-Output "$taskName already present; preserved."; continue }
    $taskValue = $null
    if ($taskName -ne 'CloudHostDataProtectionCertificate') {
        $taskBytes = New-Object byte[] 48
        $taskRng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $taskRng.GetBytes($taskBytes); $taskValue = [Convert]::ToBase64String($taskBytes) }
        finally { $taskRng.Dispose(); [Array]::Clear($taskBytes,0,$taskBytes.Length) }
    }
    else {
        $taskRsa = [Security.Cryptography.RSA]::Create(3072)
        $taskCertificate = $null
        try {
            $taskRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
                'CN=VietTable staging key encryption', $taskRsa,
                [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
            $taskCertificate = $taskRequest.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddYears(2))
            $taskValue = [Convert]::ToBase64String($taskCertificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx))
        }
        finally { if ($taskCertificate) { $taskCertificate.Dispose() }; $taskRsa.Dispose() }
    }
    $taskSecure = ConvertTo-SecureString $taskValue -AsPlainText -Force
    try { [IO.File]::WriteAllText($taskPath,(ConvertFrom-SecureString $taskSecure)) }
    finally { $taskSecure.Dispose(); $taskValue = $null }
    Write-Output "$taskName generated and stored in current-user DPAPI outside repository; value not displayed."
}
