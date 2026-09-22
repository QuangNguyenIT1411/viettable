$urls = @(
  'http://localhost:5100',
  'https://localhost:7100',
  'http://localhost:5101/readyz',
  'http://localhost:5102/readyz',
  'http://localhost:5103/readyz',
  'http://localhost:5104/readyz',
  'http://localhost:5105/readyz'
)
foreach($u in $urls){
  try {
    $r = Invoke-WebRequest -Uri $u -UseBasicParsing -TimeoutSec 4
    Write-Output "$u => $($r.StatusCode)"
  }
  catch {
    Write-Output "$u => ERR: $($_.Exception.Message)"
  }
}
