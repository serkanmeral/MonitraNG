# Hosts File Setup Script for MonitraNG
# Adds local domain entries for development

#Requires -RunAsAdministrator

Write-Host "`n=== MonitraNG Hosts File Setup ===" -ForegroundColor Cyan

$hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"

$entries = @(
    "127.0.0.1  monitrang.local",
    "127.0.0.1  api.monitrang.local",
    "127.0.0.1  app.monitrang.local",
    "127.0.0.1  admin.monitrang.local",
    "127.0.0.1  mngkeeper.monitrang.local",
    "127.0.0.1  mngreactor.monitrang.local",
    "127.0.0.1  mngengine.monitrang.local"
)

Write-Host "`nChecking hosts file: $hostsPath" -ForegroundColor Yellow

$hostsContent = Get-Content $hostsPath

$updated = $false
$newEntries = @()

foreach ($entry in $entries) {
    $domain = $entry.Split()[1]
    
    if ($hostsContent -match [regex]::Escape($domain)) {
        Write-Host "  ✅ Already exists: $domain" -ForegroundColor Gray
    } else {
        Write-Host "  ➕ Adding: $entry" -ForegroundColor Green
        $newEntries += $entry
        $updated = $true
    }
}

if ($updated) {
    Write-Host "`nAdding new entries to hosts file..." -ForegroundColor Yellow
    
    # Backup hosts file
    $backupPath = "$hostsPath.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item $hostsPath $backupPath
    Write-Host "  ✅ Backup created: $backupPath" -ForegroundColor Green
    
    # Add new entries
    Add-Content $hostsPath "`n# MonitraNG Development Domains"
    foreach ($entry in $newEntries) {
        Add-Content $hostsPath $entry
    }
    
    Write-Host "`n✅ Hosts file updated successfully!" -ForegroundColor Green
} else {
    Write-Host "`n✅ All entries already exist. No update needed." -ForegroundColor Green
}

Write-Host "`nCurrent MonitraNG entries:" -ForegroundColor Cyan
Get-Content $hostsPath | Select-String "monitrang"

Write-Host "`n📝 Note: You may need to flush DNS cache:" -ForegroundColor Yellow
Write-Host "  ipconfig /flushdns" -ForegroundColor Gray

Write-Host ""

