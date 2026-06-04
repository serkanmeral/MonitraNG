# WEC forwarder — Forwarded Events veya SIEM fixture → MngEngine wec-batch
param(
    [string]$EngineUrl = "http://127.0.0.1:5037",
    [string]$WecHost = $env:COMPUTERNAME,
    [ValidateSet("Fixture", "EventLog")]
    [string]$Source = "Fixture",
    [int]$MaxEvents = 25,
    [int]$BatchSize = 10,
    [int]$PollIntervalSeconds = 30,
    [switch]$Continuous,
    [int[]]$EventIds = @(4624, 4625, 4740, 4720, 4728, 4732, 4771),
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixtureDir = Join-Path $repoRoot "tests/fixtures/siem"

function Convert-EventRecordToRaw([object]$Record) {
    $props = @{}
    foreach ($p in $Record.Properties) {
        $props[$p.Name] = $p.Value
    }
    $props["EventID"] = $Record.Id
    if ($Record.TimeCreated) {
        $props["TimeCreated"] = $Record.TimeCreated.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    }
    return $props
}

function Convert-HashtableToPsObject([hashtable]$Table) {
    $obj = New-Object PSObject
    foreach ($k in $Table.Keys) {
        $obj | Add-Member -NotePropertyName $k -NotePropertyValue $Table[$k]
    }
    return $obj
}

function New-WecBatchItem([object]$Raw, [string]$ReceivedAt) {
    return @{
        receivedAt = $ReceivedAt
        source     = @{
            type    = "ad"
            product = "windows"
            host    = $WecHost
        }
        raw        = $Raw
    }
}

function Send-WecBatch([array]$Items, [bool]$AutoFlush = $true) {
    if ($Items.Count -eq 0) { return $null }

    $body = @{
        autoFlush = $AutoFlush
        items     = $Items
    } | ConvertTo-Json -Depth 10 -Compress:$false

    if ($DryRun) {
        Write-Host "[DryRun] POST $EngineUrl/api/SecEvents/wec-batch ($($Items.Count) items)" -ForegroundColor DarkGray
        return @{ enqueued = $Items.Count; flushed = $false; dryRun = $true }
    }

    return Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/wec-batch" `
        -Method POST -Body $body -ContentType "application/json" -TimeoutSec 120
}

function Get-FixtureEvents([int]$Limit) {
    $files = @(
        "windows_4625_failed_logon.json",
        "windows_4624_success_logon.json",
        "windows_4624_privileged_rdp_outside_window.json"
    )
    $events = @()
    foreach ($f in $files) {
        $path = Join-Path $fixtureDir $f
        if (-not (Test-Path $path)) { continue }
        $raw = Get-Content $path -Raw | ConvertFrom-Json
        $events += $raw
        if ($events.Count -ge $Limit) { break }
    }
    return @($events | Select-Object -First $Limit)
}

function Get-ForwardedEventRecords([int]$Limit) {
    $idFilter = ($EventIds | ForEach-Object { "EventID=$_" }) -join " or "
    $xpath = "*[System[($idFilter)]]"
    try {
        return @(Get-WinEvent -LogName "Forwarded Events" -FilterXPath $xpath -MaxEvents $Limit -ErrorAction Stop)
    } catch [System.Exception] {
        if ($_.Exception.Message -match "No events were found") { return @() }
        throw
    }
}

function Invoke-ForwardOnce {
    param([ref]$TotalSent)

    $receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    $batch = @()

    if ($Source -eq "Fixture") {
        $fixtures = Get-FixtureEvents -Limit $MaxEvents
        foreach ($raw in $fixtures) {
            $ev = ($raw | ConvertTo-Json -Depth 6 | ConvertFrom-Json)
            $ev.TimeCreated = $receivedAt
            $batch += New-WecBatchItem -Raw $ev -ReceivedAt $receivedAt
        }
    } else {
        $records = Get-ForwardedEventRecords -Limit $MaxEvents
        foreach ($rec in $records) {
            $raw = Convert-EventRecordToRaw -Record $rec
            $batch += New-WecBatchItem -Raw (Convert-HashtableToPsObject $raw) -ReceivedAt $receivedAt
        }
    }

    if ($batch.Count -eq 0) {
        Write-Host "  (olay yok — $($Source))" -ForegroundColor DarkGray
        return
    }

    for ($i = 0; $i -lt $batch.Count; $i += $BatchSize) {
        $slice = @($batch[$i..([Math]::Min($i + $BatchSize - 1, $batch.Count - 1))])
        $resp = Send-WecBatch -Items $slice
        $TotalSent.Value += $slice.Count
        Write-Host "  POST enqueued=$($resp.enqueued) flushed=$($resp.flushed) accepted=$($resp.accepted)" -ForegroundColor Green
    }
}

Write-Host "=== WEC Forwarder -> Engine ===" -ForegroundColor Cyan
Write-Host "Engine: $EngineUrl | Source: $Source | WecHost: $WecHost" -ForegroundColor DarkGray

if ($Source -eq "EventLog" -and -not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "EventLog modu icin yonetici haklari onerilir (Forwarded Events okuma)."
}

$sent = 0
do {
    Write-Host "`n$(Get-Date -Format 'u') forward cycle..." -ForegroundColor Yellow
    Invoke-ForwardOnce -TotalSent ([ref]$sent)
    if (-not $Continuous) { break }
    Start-Sleep -Seconds $PollIntervalSeconds
} while ($true)

Write-Host "`nToplam gonderilen olay: $sent" -ForegroundColor Cyan
if (-not $DryRun) {
    Write-Host "OK Forward-WecEventsToEngine tamamlandi" -ForegroundColor Green
}
