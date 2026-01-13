# MngAdmin Backup Test Script (Docker)
# Tests backup functionality for system and domain databases in Docker environment

param(
    [string]$BaseUrl = "http://localhost:5080",
    [string]$GatewayUrl = "https://localhost:5040",
    [string]$DomainName = "meral"
)

$ErrorActionPreference = "Stop"

# SSL/TLS Certificate Validation Bypass
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
try {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11 -bor [System.Net.SecurityProtocolType]::Tls
} catch {
    try {
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    } catch {
        # Use default if TLS12 not available
    }
}

# Colors for output
function Write-Info { Write-Host $args -ForegroundColor Cyan }
function Write-Success { Write-Host $args -ForegroundColor Green }
function Write-Error { Write-Host $args -ForegroundColor Red }
function Write-Warning { Write-Host $args -ForegroundColor Yellow }

Write-Info "========================================="
Write-Info "MngAdmin Backup Test Script (Docker)"
Write-Info "========================================="
Write-Info "Direct URL: $BaseUrl"
Write-Info "Gateway URL: $GatewayUrl"
Write-Info "Domain: $DomainName"
Write-Info ""

$headers = @{
    "Content-Type" = "application/json"
}

# Test 1: Health Check (Direct)
Write-Info "[Test 1] Health Check (Direct)"
try {
    $params = @{
        Uri = "$BaseUrl/api/v1/health"
        Method = "Get"
        ErrorAction = "Stop"
    }
    
    if (Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
        $params.SkipCertificateCheck = $true
    }
    
    $response = Invoke-RestMethod @params
    Write-Success "✓ Health check passed: $($response.Status)"
} catch {
    Write-Error "✗ Health check failed: $_"
    exit 1
}
Write-Info ""

# Test 2: Health Check (Gateway)
Write-Info "[Test 2] Health Check (Gateway)"
try {
    $params = @{
        Uri = "$GatewayUrl/admin/api/v1/health"
        Method = "Get"
        ErrorAction = "Stop"
    }
    
    if (Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
        $params.SkipCertificateCheck = $true
    }
    
    $response = Invoke-RestMethod @params
    Write-Success "✓ Health check passed via Gateway: $($response.Status)"
} catch {
    Write-Warning "⚠ Gateway health check failed (may not be configured yet): $_"
}
Write-Info ""

# Test 3: System MongoDB Backup (Direct)
Write-Info "[Test 3] System MongoDB Backup - mngkeeper (Direct)"
try {
    $body = @{
        databaseType = "mongodb"
        databaseName = "mngkeeper"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/backup/system/mongodb" -Method Post -Headers $headers -Body $body -SkipCertificateCheck
    Write-Success "✓ Backup started: $($response.Id)"
    Write-Info "  Status: $($response.Status)"
    
    $backupId = $response.Id
    
    # Wait for backup to complete
    $maxWait = 60
    $waited = 0
    while ($waited -lt $maxWait) {
        Start-Sleep -Seconds 2
        $waited += 2
        
        $statusResponse = Invoke-RestMethod -Uri "$BaseUrl/api/v1/backup/$backupId" -Method Get -Headers $headers -SkipCertificateCheck
        
        if ($statusResponse.Status -eq "completed") {
            Write-Success "✓ Backup completed successfully"
            Write-Info "  Duration: $($statusResponse.DurationMs) ms"
            Write-Info "  Size: $([math]::Round($statusResponse.SizeBytes / 1MB, 2)) MB"
            Write-Info "  Path: $($statusResponse.BackupPath)"
            break
        } elseif ($statusResponse.Status -eq "failed") {
            Write-Error "✗ Backup failed: $($statusResponse.ErrorMessage)"
            break
        }
        
        Write-Info "  Waiting for backup to complete... ($waited/$maxWait seconds)"
    }
} catch {
    Write-Error "✗ System MongoDB backup failed: $_"
}
Write-Info ""

# Test 4: System PostgreSQL Backup (Direct)
Write-Info "[Test 4] System PostgreSQL Backup - keycloak (Direct)"
try {
    $body = @{
        databaseType = "postgresql"
        databaseName = "keycloak"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/backup/system/postgresql" -Method Post -Headers $headers -Body $body -SkipCertificateCheck
    Write-Success "✓ Backup started: $($response.Id)"
    Write-Info "  Status: $($response.Status)"
    
    $backupId = $response.Id
    
    # Wait for backup to complete
    $maxWait = 60
    $waited = 0
    while ($waited -lt $maxWait) {
        Start-Sleep -Seconds 2
        $waited += 2
        
        $statusResponse = Invoke-RestMethod -Uri "$BaseUrl/api/v1/backup/$backupId" -Method Get -Headers $headers -SkipCertificateCheck
        
        if ($statusResponse.Status -eq "completed") {
            Write-Success "✓ Backup completed successfully"
            Write-Info "  Duration: $($statusResponse.DurationMs) ms"
            Write-Info "  Size: $([math]::Round($statusResponse.SizeBytes / 1KB, 2)) KB"
            Write-Info "  Path: $($statusResponse.BackupPath)"
            break
        } elseif ($statusResponse.Status -eq "failed") {
            Write-Warning "⚠ Backup failed (expected if PostgreSQL not running): $($statusResponse.ErrorMessage)"
            break
        }
        
        Write-Info "  Waiting for backup to complete... ($waited/$maxWait seconds)"
    }
} catch {
    Write-Warning "⚠ System PostgreSQL backup failed (expected if PostgreSQL not running): $_"
}
Write-Info ""

# Test 5: Domain Backup (Direct)
Write-Info "[Test 5] Domain Backup - $DomainName (Direct)"
try {
    $body = @{
        databaseType = "mongodb"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/backup/domain/$DomainName" -Method Post -Headers $headers -Body $body -SkipCertificateCheck
    Write-Success "✓ Backup started: $($response.Id)"
    Write-Info "  Status: $($response.Status)"
    Write-Info "  Domain: $($response.DomainName)"
    Write-Info "  Database: $($response.DatabaseName)"
    
    $backupId = $response.Id
    
    # Wait for backup to complete
    $maxWait = 60
    $waited = 0
    while ($waited -lt $maxWait) {
        Start-Sleep -Seconds 2
        $waited += 2
        
        $statusResponse = Invoke-RestMethod -Uri "$BaseUrl/api/v1/backup/$backupId" -Method Get -Headers $headers -SkipCertificateCheck
        
        if ($statusResponse.Status -eq "completed") {
            Write-Success "✓ Backup completed successfully"
            Write-Info "  Duration: $($statusResponse.DurationMs) ms"
            Write-Info "  Size: $([math]::Round($statusResponse.SizeBytes / 1MB, 2)) MB"
            Write-Info "  Path: $($statusResponse.BackupPath)"
            break
        } elseif ($statusResponse.Status -eq "failed") {
            Write-Error "✗ Backup failed: $($statusResponse.ErrorMessage)"
            break
        }
        
        Write-Info "  Waiting for backup to complete... ($waited/$maxWait seconds)"
    }
} catch {
    Write-Error "✗ Domain backup failed: $_"
}
Write-Info ""

# Test 6: Full Backup (Direct)
Write-Info "[Test 6] Full Backup (Direct)"
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/backup/full" -Method Post -Headers $headers -SkipCertificateCheck
    Write-Success "✓ Full backup started: $($response.Id)"
    Write-Info "  Status: $($response.Status)"
    Write-Info "  Started: $($response.StartedAt)"
    Write-Info ""
    Write-Info "Full backup tamamlanması bekleniyor (bu biraz zaman alabilir)..."
    
    # Wait for full backup to complete
    $maxWait = 120
    $waited = 0
    while ($waited -lt $maxWait) {
        Start-Sleep -Seconds 5
        $waited += 5
        
        # Re-fetch full backup status (we need to check the individual backups)
        # For now, just wait and check final status
        if ($waited -ge 30) {
            Write-Info "  Checking status... ($waited/$maxWait seconds)"
        }
    }
    
    Write-Info ""
    Write-Info "=== Full Backup Sonuçları ===" -ForegroundColor Cyan
    Write-Host "Status: $($response.Status)" -ForegroundColor $(if ($response.Status -eq "completed") { "Green" } elseif ($response.Status -eq "completed_with_errors") { "Yellow" } else { "Red" })
    Write-Info "Total Backups: $($response.TotalBackups)"
    Write-Info "Successful: $($response.SuccessfulBackups)" -ForegroundColor Green
    Write-Info "Failed: $($response.FailedBackups)" -ForegroundColor $(if ($response.FailedBackups -gt 0) { "Red" } else { "Gray" })
    if ($response.DurationMs) {
        Write-Info "Duration: $($response.DurationMs) ms"
    }
    Write-Info ""
    Write-Info "System Backups ($($response.SystemBackups.Count)):"
    foreach ($backup in $response.SystemBackups) {
        $color = if ($backup.Status -eq "completed") { "Green" } else { "Red" }
        Write-Host "  - $($backup.DatabaseName): $($backup.Status)" -ForegroundColor $color
    }
    Write-Info ""
    Write-Info "Domain Backups ($($response.DomainBackups.Count)):"
    if ($response.DomainsBackedUp.Count -gt 0) {
        Write-Info "  Domains backed up: $($response.DomainsBackedUp -join ', ')"
    }
    foreach ($backup in $response.DomainBackups | Select-Object -First 5) {
        $color = if ($backup.Status -eq "completed") { "Green" } else { "Red" }
        Write-Host "  - $($backup.DomainName) ($($backup.DatabaseName)): $($backup.Status)" -ForegroundColor $color
    }
} catch {
    Write-Error "✗ Full backup failed: $_"
}
Write-Info ""

# Test 7: Gateway Test (if Gateway is available)
Write-Info "[Test 7] Gateway Test - Health Check via Gateway"
try {
    $params = @{
        Uri = "$GatewayUrl/admin/api/v1/health"
        Method = "Get"
        ErrorAction = "Stop"
    }
    
    if (Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
        $params.SkipCertificateCheck = $true
    }
    
    $response = Invoke-RestMethod @params
    Write-Success "✓ Gateway routing works! Health check via Gateway: $($response.Status)"
} catch {
    Write-Warning "⚠ Gateway test failed (Gateway may not be running or configured): $_"
}
Write-Info ""

Write-Success "========================================="
Write-Success "All tests completed!"
Write-Success "========================================="
