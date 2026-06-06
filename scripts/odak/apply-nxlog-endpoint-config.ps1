# MonitraNG NxLog endpoint config — nxlog.d/monitrang-siem.conf (yönetici gerekir)
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$SourceHost = "TERMINAL.odak.local",
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

$confDir = "C:\Program Files\nxlog\conf\nxlog.d"
$confPath = Join-Path $confDir "monitrang-siem.conf"

$confBody = @"
## MonitraNG SIEM — endpoint Security log -> Engine wec-batch
## Uygulama: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

define ENGINE_URL  $EngineUrl
define SOURCE_HOST $SourceHost

<Extension monitra_json>
    Module      xm_json
</Extension>

<Input monitra_security_dar>
    Module      im_msvistalog
    Query       <QueryList>\
                  <Query Id="0">\
                    <Select Path="Security">\
                      *[System[(EventID=4624 or EventID=4625 or EventID=4740 or EventID=4720 or EventID=4728 or EventID=4732 or EventID=4771 or EventID=5136)]]\
                    </Select>\
                  </Query>\
                </QueryList>
    SavePos     TRUE
    ReadFromLast FALSE
</Input>

<Processor monitra_sec_to_batch>
    Module      pm_transformer
    Exec        `$raw_json = to_json(`$Event);
    Exec        `$receivedAt = `$EventTime;
    Exec        `$source_host = '%SOURCE_HOST%';
</Processor>

<Output monitra_engine_http>
    Module      om_http
    URL         %ENGINE_URL%/api/SecEvents/wec-batch
    ContentType application/json
    Body        {"autoFlush":true,"items":[{"receivedAt":"`$receivedAt","source":{"type":"ad","product":"windows","host":"`$source_host"},"raw":`$raw_json}]}
</Output>

<Route monitra_endpoint_to_engine>
    Path        monitra_security_dar => monitra_sec_to_batch => monitra_engine_http
</Route>
"@

Write-Host "=== NxLog MonitraNG config ===" -ForegroundColor Cyan
Write-Host "Hedef: $confPath" -ForegroundColor DarkGray
Write-Host "Engine: $EngineUrl | Host: $SourceHost" -ForegroundColor DarkGray

if (-not $Apply) {
    Write-Host "Dry-run — uygulamak icin -Apply" -ForegroundColor Yellow
    exit 0
}

if (-not (Test-IsAdministrator)) {
    Write-Host "Yonetici gerekli; UAC..." -ForegroundColor Yellow
    Start-Process pwsh.exe -ArgumentList @(
        "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $PSCommandPath,
        "-EngineUrl", $EngineUrl, "-SourceHost", $SourceHost, "-Apply"
    ) -Verb RunAs -Wait
    exit $LASTEXITCODE
}

if (-not (Test-Path "C:\Program Files\nxlog\nxlog.exe")) {
    throw "NxLog kurulu degil"
}
if (-not (Test-Path $confDir)) {
    New-Item -ItemType Directory -Path $confDir -Force | Out-Null
}

$backup = "$confPath.bak.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
if (Test-Path $confPath) {
    Copy-Item $confPath $backup -Force
    Write-Host "Yedek: $backup" -ForegroundColor DarkGray
}

Set-Content -Path $confPath -Value $confBody -Encoding UTF8
Write-Host "Config yazildi: $confPath" -ForegroundColor Green

$svc = Get-Service nxlog -ErrorAction Stop
Restart-Service nxlog -Force
Start-Sleep -Seconds 3
$svc = Get-Service nxlog
Write-Host "nxlog: $($svc.Status)" -ForegroundColor $(if ($svc.Status -eq 'Running') { 'Green' } else { 'Red' })

$logTail = Get-Content "C:\Program Files\nxlog\data\nxlog.log" -Tail 8 -ErrorAction SilentlyContinue
if ($logTail) {
    Write-Host "`nLog (son satirlar):" -ForegroundColor DarkGray
    $logTail | ForEach-Object { Write-Host "  $_" }
}

Write-Host "`nOK config uygulandi." -ForegroundColor Green
exit 0
