[CmdletBinding()]
param([int]$SessionId = 25, [int]$OrderId = 44, [string]$PostgresImage = 'postgres:17')
# Owner-run, READ-ONLY status check of one dining session / order on the shared Supabase DB (psql via Docker).
# The session runs with default_transaction_read_only=on, so any write is rejected by PostgreSQL.
# Prints status codes, ids and timestamps only: no names, phone numbers, e-mails or tokens.
# The password comes from the local current-user DPAPI store and reaches Docker by environment variable name only.
$ErrorActionPreference = 'Stop'
$secrets = Join-Path $env:LOCALAPPDATA 'VietTable\staging-secrets'
$ca = Join-Path $secrets 'supabase-ca.crt'
$passwordFile = Join-Path $secrets 'DatabasePassword.dpapi'
foreach ($required in $ca, $passwordFile) {
    if (-not (Test-Path -LiteralPath $required)) { throw "STOP: missing $(Split-Path $required -Leaf)" }
}
& docker version --format '{{.Server.Version}}' *> $null
if ($LASTEXITCODE -ne 0) { throw 'STOP: Docker is not running.' }

$sql = @"
\pset footer off
\echo '--- Session'
SELECT "DiningSessionId", "SessionCode", "Status", "TableId", "CheckoutQuoteId", "PaymentId",
       "StartedAtUtc", "PaymentRequestedAtUtc", "EndedAtUtc"
FROM orders."DiningSessions" WHERE "DiningSessionId" = $SessionId;
\echo '--- Orders in session'
SELECT o."OrderID", s."StatusCode" AS order_status, o."IsActive", o."OrderTime", o."CompletedTime"
FROM orders."Orders" o JOIN orders."OrderStatus" s ON s."StatusID" = o."StatusID"
WHERE o."OrderID" = $OrderId OR o."DiningSessionCode" =
      (SELECT "SessionCode" FROM orders."DiningSessions" WHERE "DiningSessionId" = $SessionId)
ORDER BY o."OrderID";
\echo '--- Items of order'
SELECT "ItemID", "StatusCode", "Quantity" FROM orders."OrderItems" WHERE "OrderID" = $OrderId ORDER BY "ItemID";
\echo '--- Mobile checkout quotes / payments / invoices'
SELECT "CheckoutQuoteId", "Status", "TotalAmount", "CreatedAtUtc", "UpdatedAtUtc"
FROM billing."CheckoutQuotes" WHERE "DiningSessionId" = $SessionId ORDER BY "CheckoutQuoteId";
SELECT "PaymentId", "Channel", "Method", "Status", "Amount", "ConfirmedByEmployeeId", "CompletedAtUtc"
FROM billing."MobilePayments" WHERE "DiningSessionId" = $SessionId ORDER BY "PaymentId";
SELECT "InvoiceId", "CheckoutQuoteId", "PaymentId", "TotalAmount", "IssuedAtUtc", "SettlementCompletedAtUtc", "RecoveryAttemptCount"
FROM billing."Invoices" WHERE "DiningSessionId" = $SessionId ORDER BY "InvoiceId";
\echo '--- Cashier dashboard bills for the order'
SELECT "BillID", "PaymentMethod", "TotalAmount", "BillTime", "IsActive" FROM billing."Bills" WHERE "OrderID" = $OrderId;
\echo '--- Customer notifications for the order (type + time only)'
SELECT "ReadyDishNotificationId", "EventName", "Status", "CreatedAtUtc"
FROM customers."ReadyDishNotifications" WHERE "OrderId" = $OrderId ORDER BY 1;
"@

$connection = 'host=aws-0-ap-southeast-2.pooler.supabase.com port=5432 dbname=postgres ' +
    'user=postgres.waxzqkuwuwubdkrzxbqu sslmode=verify-full sslrootcert=/ca.crt ' +
    'options=-cdefault_transaction_read_only=on'
$secure = ConvertTo-SecureString (Get-Content -LiteralPath $passwordFile -Raw)
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $sql | & docker run --rm -i -e PGPASSWORD -v "${ca}:/ca.crt:ro" $PostgresImage `
        psql $connection --no-psqlrc -v ON_ERROR_STOP=1 -X -q -f -
    if ($LASTEXITCODE -ne 0) { throw "FAILED: psql exit $LASTEXITCODE" }
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer); $secure.Dispose()
}
