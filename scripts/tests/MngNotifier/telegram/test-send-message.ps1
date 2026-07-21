<#
.SYNOPSIS
  TG-1 smoke: POST MngNotifier /notifications/send-message (channel=telegram).

.DESCRIPTION
  Requires Telegram Enabled + BotToken on Notifier.
  If TELEGRAM_CHAT_ID env is empty, uses server DefaultChatId (or fails with 400).

.EXAMPLE
  $env:TELEGRAM_CHAT_ID = "-1001234567890"
  pwsh scripts/tests/MngNotifier/telegram/test-send-message.ps1

.EXAMPLE
  pwsh scripts/tests/MngNotifier/telegram/test-send-message.ps1 -NotifierBaseUrl http://localhost:5070 -Text "Merhaba Odak"
#>
[CmdletBinding()]
param(
    [string]$NotifierBaseUrl = "http://localhost:5070",
    [string]$ChatId = $env:TELEGRAM_CHAT_ID,
    [string]$Text = "MonitraNG TG-1 smoke $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
    [string]$ParseMode = ""
)

$ErrorActionPreference = "Stop"
$uri = "$($NotifierBaseUrl.TrimEnd('/'))/api/v1/notifications/send-message"

$body = @{
    channel = "telegram"
    text    = $Text
    disableWebPagePreview = $true
}
if (-not [string]::IsNullOrWhiteSpace($ChatId)) {
    $body.to = @($ChatId.Trim())
}
if (-not [string]::IsNullOrWhiteSpace($ParseMode)) {
    $body.parseMode = $ParseMode
}

Write-Host "POST $uri"
Write-Host "to=$([string]::IsNullOrWhiteSpace($ChatId) ? '(DefaultChatId)' : $ChatId)"

try {
    $resp = Invoke-RestMethod -Method Post -Uri $uri -ContentType "application/json" -Body ($body | ConvertTo-Json -Depth 5)
} catch {
    $status = $_.Exception.Response?.StatusCode?.value__
    $msg = $_.ErrorDetails?.Message
    if (-not $msg -and $_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        $msg = $reader.ReadToEnd()
    }
    Write-Host "FAIL HTTP $status : $msg" -ForegroundColor Red
    if ($status -eq 503) {
        Write-Host "Hint: set MngNotifierSettings__Telegram__Enabled=true and BotToken, then restart mngnotifier." -ForegroundColor Yellow
    }
    exit 1
}

Write-Host ($resp | ConvertTo-Json -Depth 6)
if ($resp.status -eq "sent") {
    Write-Host "PASS" -ForegroundColor Green
    exit 0
}
Write-Host "PARTIAL/FAIL status=$($resp.status)" -ForegroundColor Yellow
exit 2
