# Dilim 1 — agent local DLP evaluate (Outlook yok)
#
# Agent çalışıyor olmalı (Local UI 5092). Key: %ProgramData%\MngLogs\Agent\dlp-local.key
#
#   .\scripts\tests\MngLogs\dlp\test-dlp-evaluate.ps1

param(
    [string]$BaseUrl = "http://127.0.0.1:5092",
    [string]$KeyFile = "",
    [string]$ClassificationId = "cl-gizli",
    [string]$Recipient = "dis@gmail.com"
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($KeyFile)) {
    $KeyFile = Join-Path $env:ProgramData "MngLogs\Agent\dlp-local.key"
}

if (-not (Test-Path $KeyFile)) {
    Write-Host "dlp-local.key yok: $KeyFile (agent bir kez çalışmış olmalı)" -ForegroundColor Red
    exit 1
}

$key = (Get-Content $KeyFile -Raw).Trim()
$headers = @{ "X-MngLogs-DlpKey" = $key; "Content-Type" = "application/json" }
$body = @{
    action = "email.send"
    windowsUser = "$env:USERDOMAIN\$env:USERNAME"
    recipients = @($Recipient)
    attachments = @(@{ classificationId = $ClassificationId })
    client = @{ kind = "lab-powershell"; version = "0.1.0" }
} | ConvertTo-Json -Compress

Write-Host "POST $BaseUrl/dlp/evaluate  class=$ClassificationId  to=$Recipient" -ForegroundColor Cyan
try {
    $r = Invoke-RestMethod -Uri "$BaseUrl/dlp/evaluate" -Method POST -Headers $headers -Body $body
    $r | ConvertTo-Json -Depth 8
    if ($r.allowSend -ne $true) {
        Write-Host "Beklenen Dilim 1: allowSend=true (auditOnly)" -ForegroundColor Yellow
        exit 2
    }
    Write-Host "OK decision=$($r.decision) effect=$($r.effect) wouldBlock=$($r.wouldBlock) rule=$($r.matchedRuleId)" -ForegroundColor Green
}
catch {
    Write-Host "HATA: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message }
    exit 1
}
