param([string]$Base = "http://192.168.20.8")
$urls = @(
    "$Base`:5040/health",
    "$Base`:3000/",
    "$Base`:5001/health",
    "$Base`:3001/domain/",
    "$Base`:5086/health"
)
foreach ($u in $urls) {
    try {
        $r = Invoke-WebRequest -Uri $u -UseBasicParsing -TimeoutSec 25
        Write-Host "$u -> $($r.StatusCode)"
    } catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { "ERR" }
        Write-Host "$u -> $code ($($_.Exception.Message))"
    }
}
