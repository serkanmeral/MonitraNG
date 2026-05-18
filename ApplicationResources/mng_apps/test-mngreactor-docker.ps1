# MngReactor Docker Smoke Test
# MngReactor container'inin API uzerinden saglikli calistigini dogrular.
#
# Kullanim: mng_apps klasorunden veya proje kokunden:
#   .\test-mngreactor-docker.ps1
#
# On kosul: mng_common + mng_apps compose calisiyor olmali, MngReactor container ayakta olmali.

$ErrorActionPreference = "Stop"
$BaseUrl = "http://localhost:5003"

Write-Host "MngReactor Docker Smoke Test - $BaseUrl`n" -ForegroundColor Cyan

function Test-Endpoint {
    param([string]$Path, [string]$Description, [bool]$RequireSuccess = $true)
    $url = "$BaseUrl$Path"
    try {
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10
        $ok = $response.StatusCode -ge 200 -and $response.StatusCode -lt 300
        $status = if ($ok) { "OK" } else { "FAIL" }
        $color = if ($ok) { "Green" } else { "Red" }
        Write-Host "  $Description : $($response.StatusCode) [$status]" -ForegroundColor $color
        if ($response.Content) {
            $preview = $response.Content.Substring(0, [Math]::Min(80, $response.Content.Length))
            if ($response.Content.Length -gt 80) { $preview += "..." }
            Write-Host "    -> $preview"
        }
        if ($RequireSuccess -and -not $ok) { exit 1 }
        return $ok
    }
    catch {
        Write-Host "  $Description : HATA - $($_.Exception.Message)" -ForegroundColor Red
        if ($RequireSuccess) { exit 1 }
        return $false
    }
}

Write-Host "1. Health endpoint..."
Test-Endpoint -Path "/api/v1/health" -Description "GET /api/v1/health" | Out-Null

Write-Host "`n2. Health live..."
Test-Endpoint -Path "/api/v1/health/live" -Description "GET /api/v1/health/live" | Out-Null

Write-Host "`n3. Health ready..."
Test-Endpoint -Path "/api/v1/health/ready" -Description "GET /api/v1/health/ready" | Out-Null

Write-Host "`n4. Engine assets (401 beklenir - auth gerekli)..."
Test-Endpoint -Path "/api/v1/Engine/assets" -Description "GET /api/v1/Engine/assets" -RequireSuccess $false | Out-Null

Write-Host "`nSmoke test tamamlandi - MngReactor container API yanit veriyor." -ForegroundColor Green
