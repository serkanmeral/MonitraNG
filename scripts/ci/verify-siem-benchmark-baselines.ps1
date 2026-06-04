# SIEM CI — commit'li benchmark JSON dosyalarinin kapı kriterlerini dogrular (Odak gerekmez)
param(
    [string]$BenchmarksDir = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
if ([string]::IsNullOrWhiteSpace($BenchmarksDir)) {
    $BenchmarksDir = Join-Path $repoRoot "docs/odak/monitoring/benchmarks"
}

# P0 kisa baseline bilgi amaclidir (pass=false olabilir); resmi P0 kapisi soak dosyasidir.
$required = @(
    @{ File = "benchmark-P0-2026-06-04.json"; Profile = "P0-short"; RequireTopPass = $false; RequireIngestPass = $true; MinEps = 0 },
    @{ File = "benchmark-soak-2026-06-04.json"; Profile = "P0-soak"; RequireTopPass = $true; RequireIngestPass = $true; MinEps = 40 },
    @{ File = "benchmark-P1-2026-06-04.json"; Profile = "P1"; RequireTopPass = $true; RequireIngestPass = $true; MinEps = 50 },
    @{ File = "benchmark-P2-2026-06-04.json"; Profile = "P2"; RequireTopPass = $true; RequireIngestPass = $true; MinEps = 52 },
    @{ File = "benchmark-engine-syslog-2026-06-04.json"; Profile = "engine-syslog"; RequireTopPass = $true; RequireIngestPass = $false; MinEps = 15 },
    @{ File = "benchmark-engine-queue-depth-2026-06-04.json"; Profile = "engine-queue"; RequireTopPass = $true; RequireIngestPass = $false; MinEps = 0 }
)

Write-Host "=== SIEM benchmark baseline verify (CI) ===" -ForegroundColor Cyan
$failed = @()

foreach ($spec in $required) {
    $path = Join-Path $BenchmarksDir $spec.File
    if (-not (Test-Path $path)) {
        $failed += "Eksik: $($spec.File)"
        continue
    }

    $doc = Get-Content $path -Raw | ConvertFrom-Json

    if ($spec.RequireTopPass -and -not $doc.pass) {
        $failed += "$($spec.File): pass=false"
        continue
    }

    if ($spec.RequireIngestPass -and $null -ne $doc.ingest -and -not $doc.ingest.pass) {
        $failed += "$($spec.File): ingest.pass=false"
        continue
    }

    if ($null -ne $doc.ingest -and $doc.ingest.PSObject.Properties.Name -contains "p95Ms") {
        if ($doc.ingest.p95Ms -gt 1000) {
            $failed += "$($spec.File): ingest P95=$($doc.ingest.p95Ms) > 1000ms"
        }
    }

    if ($spec.MinEps -gt 0 -and $null -ne $doc.achievedEps -and $doc.achievedEps -lt $spec.MinEps) {
        $failed += "$($spec.File): achievedEps=$($doc.achievedEps) < $($spec.MinEps)"
    }

    $eps = if ($null -ne $doc.achievedEps) { $doc.achievedEps } else { "n/a" }
    Write-Host "   OK $($spec.Profile) $($spec.File) eps=$eps pass=$($doc.pass)" -ForegroundColor DarkGray
}

if ($failed.Count -gt 0) {
    Write-Host "`nFAIL benchmark baseline verify:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }
    exit 1
}

Write-Host "`nOK SIEM benchmark baseline verify PASS ($($required.Count) files)" -ForegroundColor Green
exit 0
