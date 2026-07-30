#Requires -Version 7.0
<#
.SYNOPSIS
  Writes / merges %ProgramData%\MngLogs\Agent\system.json (GPO MST property stand-in).

.DESCRIPTION
  Empty string parameters leave existing values unchanged when merging.
  CollectorUrl/ApiKey empty on first write keep schema defaults / empty key.
#>
param(
    [string] $CollectorUrl = "",
    [string] $ApiKey = "",
    [string] $HostId = "",
    [int] $LocalUiPort = 0,
    [string] $LocalUiHost = "",
    [string] $DataDirectory = ""
)

$ErrorActionPreference = "Stop"

if (-not $DataDirectory) {
    $DataDirectory = Join-Path $env:ProgramData "MngLogs\Agent"
}

New-Item -ItemType Directory -Path $DataDirectory -Force | Out-Null
$path = Join-Path $DataDirectory "system.json"

$cfg = [ordered]@{
    collectorBaseUrl = "http://127.0.0.1:5091"
    apiKey           = ""
    hostId           = ""
    localUiHost      = "127.0.0.1"
    localUiPort      = 5092
    dataDirectory    = $DataDirectory
}

if (Test-Path $path) {
    try {
        $existing = Get-Content -Path $path -Raw | ConvertFrom-Json -AsHashtable
        foreach ($key in @("collectorBaseUrl", "apiKey", "hostId", "localUiHost", "localUiPort", "dataDirectory")) {
            if ($existing.ContainsKey($key) -and $null -ne $existing[$key] -and "$($existing[$key])" -ne "") {
                $cfg[$key] = $existing[$key]
            }
        }
    } catch {
        Write-Warning "Existing system.json unreadable; rewriting defaults. $_"
    }
}

if ($CollectorUrl) { $cfg.collectorBaseUrl = $CollectorUrl.TrimEnd("/") }
if ($PSBoundParameters.ContainsKey("ApiKey")) { $cfg.apiKey = $ApiKey }
if ($HostId) { $cfg.hostId = $HostId }
if ($LocalUiHost) { $cfg.localUiHost = $LocalUiHost }
if ($LocalUiPort -gt 0) { $cfg.localUiPort = $LocalUiPort }
$cfg.dataDirectory = $DataDirectory

$json = $cfg | ConvertTo-Json -Depth 5
Set-Content -Path $path -Value $json -Encoding utf8
Write-Host "Wrote $path"
Write-Host $json
exit 0
