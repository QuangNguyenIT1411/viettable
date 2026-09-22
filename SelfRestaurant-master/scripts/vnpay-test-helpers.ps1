function Invoke-VnPayTestIpn {
    param(
        [Parameter(Mandatory = $true)][string]$BillingBaseUrl,
        [Parameter(Mandatory = $true)][string]$ProviderReference,
        [Parameter(Mandatory = $true)][decimal]$Amount,
        [Parameter(Mandatory = $true)][string]$ResponseCode,
        [Parameter(Mandatory = $true)][string]$TransactionNo,
        [Parameter(Mandatory = $true)][string]$HashSecret,
        [string]$Currency = "VND",
        [switch]$InvalidSignature
    )

    if ([string]::IsNullOrWhiteSpace($HashSecret)) {
        throw "VNPAY test hash secret is required. Pass -VnPayHashSecret or set VNPAY_HASH_SECRET."
    }

    $values = @{
        vnp_Amount = ([long]($Amount * 100)).ToString([Globalization.CultureInfo]::InvariantCulture)
        vnp_CurrCode = $Currency
        vnp_ResponseCode = $ResponseCode
        vnp_TransactionNo = $TransactionNo
        vnp_TransactionStatus = $ResponseCode
        vnp_TxnRef = $ProviderReference
    }
    $canonical = (($values.Keys | Sort-Object | ForEach-Object {
        "{0}={1}" -f [Net.WebUtility]::UrlEncode($_), [Net.WebUtility]::UrlEncode([string]$values[$_])
    }) -join '&')
    $hmac = [Security.Cryptography.HMACSHA512]::new([Text.Encoding]::UTF8.GetBytes($HashSecret))
    try {
        $signature = ([BitConverter]::ToString($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))) -replace '-', '').ToLowerInvariant()
    }
    finally { $hmac.Dispose() }
    if ($InvalidSignature) { $signature = ('0' * 128) }
    Invoke-RestMethod -Method Get -Uri "$($BillingBaseUrl.TrimEnd('/'))/api/payments/providers/vnpay/ipn?$canonical&vnp_SecureHash=$signature"
}
