<#
.SYNOPSIS
  TG-4 smoke: Keeper telegram-resolve + Notifier send-message (document.generated style).

.DESCRIPTION
  Does not run full DI generate. Validates recipient resolve and Telegram push used by D-N.

.EXAMPLE
  pwsh scripts/tests/MngDocument/notifications/test-telegram-document-generated.ps1
#>
[CmdletBinding()]
param(
    [string]$KeeperBaseUrl = "http://localhost:5001",
    [string]$NotifierBaseUrl = "http://localhost:5070",
    [string]$DomainId = $env:DI_NOTIFY_DOMAIN_ID,
    [string]$UserId = $env:DI_TELEGRAM_USER_ID,
    [string]$ChatId = $(if ($env:DI_TELEGRAM_CHAT_ID) { $env:DI_TELEGRAM_CHAT_ID } else { $env:TELEGRAM_DEFAULT_CHAT_ID }),
    [string]$NotifyApiKey = $env:INTERNAL_NOTIFY_API_KEY
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($DomainId)) { $DomainId = "6a5297a38b49cc6d9bc55ded" }
if ([string]::IsNullOrWhiteSpace($UserId)) { $UserId = "6a5297a48b49cc6d9bc55df3" }

Write-Host "1) Keeper telegram-resolve-recipients domain=$DomainId user=$UserId"
$headers = @{ "Content-Type" = "application/json" }
if (-not [string]::IsNullOrWhiteSpace($NotifyApiKey)) {
    $headers["X-Monitra-Notify-Key"] = $NotifyApiKey
}

$resolveBody = @{ domainId = $DomainId; userIds = @($UserId) } | ConvertTo-Json
try {
    $resolve = Invoke-RestMethod -Method Post -Uri "$($KeeperBaseUrl.TrimEnd('/'))/api/internal/telegram-resolve-recipients" `
        -Headers $headers -Body $resolveBody
    $resolve | ConvertTo-Json -Depth 6
    $resolvedChat = @($resolve.chatIds)
    if ($resolvedChat.Count -eq 0 -and $resolve.ChatIds) { $resolvedChat = @($resolve.ChatIds) }
} catch {
    Write-Host "Resolve FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message }
    exit 1
}

$to = @()
if ($resolvedChat -and $resolvedChat.Count -gt 0) { $to += $resolvedChat }
elseif (-not [string]::IsNullOrWhiteSpace($ChatId)) { $to += $ChatId.Trim() }

Write-Host "2) Notifier send-message to=$($to -join ',')"
$text = @"
MonitraNG — döküman üretildi
Belge: TG-4-SMOKE
Şablon: SMOKE
Profil: smoke
Zaman (UTC): $(Get-Date -Format 'u')
http://localhost:3000/apps/document-intelligence/r/smoke
"@

$sendBody = @{
    channel = "telegram"
    to = $to
    text = $text
    disableWebPagePreview = $true
} | ConvertTo-Json

try {
    $sent = Invoke-RestMethod -Method Post -Uri "$($NotifierBaseUrl.TrimEnd('/'))/api/v1/notifications/send-message" `
        -ContentType "application/json" -Body $sendBody
    $sent | ConvertTo-Json -Depth 6
    if ($sent.status -eq "sent") {
        Write-Host "PASS" -ForegroundColor Green
        exit 0
    }
    Write-Host "PARTIAL status=$($sent.status)" -ForegroundColor Yellow
    exit 2
} catch {
    Write-Host "Send FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message }
    exit 1
}
