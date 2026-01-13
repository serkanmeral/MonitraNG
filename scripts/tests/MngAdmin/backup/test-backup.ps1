# MngAdmin Backup Test Script
# Tests backup functionality for system and domain databases

param(
    [string]$BaseUrl = "http://localhost:5080",
    [string]$Token = "",
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

# Prepare headers (with or without token)
$headers = @{
    "Content-Type" = "application/json"
}

# Add Authorization header if token is provided
if (-not [string]::IsNullOrEmpty($Token)) {
    $Token = $Token.Trim()
    $headers["Authorization"] = "Bearer $Token"
    Write-Info "Token provided (length: $($Token.Length))"
} else {
    Write-Warning "No token provided. Some endpoints may require authentication."
    Write-Info "You can provide token with: -Token 'your-token-here'"
}
Write-Info ""

Write-Info "========================================="
Write-Info "MngAdmin Backup Test Script"
Write-Info "========================================="
Write-Info "Base URL: $BaseUrl"
Write-Info "Domain: $DomainName"
Write-Info ""

# Test 1: Health Check
Write-Info "[Test 1] Health Check"
try {
    $params = @{
        Uri = "$BaseUrl/api/v1/health"
        Method = "Get"
        ErrorAction = "Stop"
    }
    
    # Add SkipCertificateCheck if available (PowerShell 6+)
    if (Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
        $params.SkipCertificateCheck = $true
    }
    
    $response = Invoke-RestMethod @params
    Write-Success "✓ Health check passed: $($response.Status)"
} catch {
    Write-Error "✗ Health check failed: $_"
    if ($_.ErrorDetails.Message) {
        Write-Error "  Details: $($_.ErrorDetails.Message)"
    }
    exit 1
}
Write-Info ""

# Test 2: System MongoDB Backup (mngkeeper)
Write-Info "[Test 2] System MongoDB Backup - mngkeeper"
try {
    $body = @{
        databaseType = "mongodb"
        databaseName = "mngkeeper"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/backup/system/mongodb" -Method Post -Headers $headers -Body $body -SkipCertificateCheck
    Write-Success "✓ Backup started: $($response.Id)"
    Write-Info "  Status: $($response.Status)"
    Write-Info "  Database: $($response.DatabaseName)"
    
    $backupId = $response.Id
    
    # Wait for backup to complete (poll every 2 seconds, max 60 seconds)
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
            exit 1
        }
        
        Write-Info "  Waiting for backup to complete... ($waited/$maxWait seconds)"
    }
    
    if ($waited -ge $maxWait) {
        Write-Warning "⚠ Backup did not complete within $maxWait seconds"
    }
} catch {
    Write-Error "✗ System MongoDB backup failed: $_"
    if ($_.ErrorDetails.Message) {
        Write-Error "  Details: $($_.ErrorDetails.Message)"
    }
}
Write-Info ""

# Test 3: System PostgreSQL Backup (keycloak)
Write-Info "[Test 3] System PostgreSQL Backup - keycloak"
try {
    $body = @{
        databaseType = "postgresql"
        databaseName = "keycloak"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/backup/system/postgresql" -Method Post -Headers $headers -Body $body -SkipCertificateCheck
    Write-Success "✓ Backup started: $($response.Id)"
    Write-Info "  Status: $($response.Status)"
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
            exit 1
        }
        
        Write-Info "  Waiting for backup to complete... ($waited/$maxWait seconds)"
    }
    
    if ($waited -ge $maxWait) {
        Write-Warning "⚠ Backup did not complete within $maxWait seconds"
    }
} catch {
    Write-Error "✗ System PostgreSQL backup failed: $_"
    if ($_.ErrorDetails.Message) {
        Write-Error "  Details: $($_.ErrorDetails.Message)"
    }
}
Write-Info ""

# Test 4: Domain Backup
Write-Info "[Test 4] Domain Backup - $DomainName"
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
            exit 1
        }
        
        Write-Info "  Waiting for backup to complete... ($waited/$maxWait seconds)"
    }
    
    if ($waited -ge $maxWait) {
        Write-Warning "⚠ Backup did not complete within $maxWait seconds"
    }
} catch {
    Write-Error "✗ Domain backup failed: $_"
    if ($_.ErrorDetails.Message) {
        Write-Error "  Details: $($_.ErrorDetails.Message)"
    }
}
Write-Info ""

# Test 5: Get System Backup List
Write-Info "[Test 5] Get System Backup List"
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/backup/system" -Method Get -Headers $headers -SkipCertificateCheck
    Write-Success "✓ Retrieved $($response.TotalCount) system backups"
    foreach ($backup in $response.Backups | Select-Object -First 5) {
        Write-Info "  - $($backup.DatabaseName): $($backup.Status) ($([math]::Round($backup.SizeBytes / 1MB, 2)) MB)"
    }
} catch {
    Write-Error "✗ Get system backup list failed: $_"
}
Write-Info ""

# Test 6: Get Domain Backup List
Write-Info "[Test 6] Get Domain Backup List - $DomainName"
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/backup/domain/$DomainName" -Method Get -Headers $headers -SkipCertificateCheck
    Write-Success "✓ Retrieved $($response.TotalCount) domain backups for domain '$DomainName'"
    
    if ($response.TotalCount -eq 0) {
        Write-Warning "  No domain backups found for domain '$DomainName'"
    } else {
        Write-Info "  Domain backups:"
        foreach ($backup in $response.Backups | Select-Object -First 10) {
            $sizeInfo = if ($backup.SizeBytes) { "$([math]::Round($backup.SizeBytes / 1MB, 2)) MB" } else { "N/A" }
            $statusColor = if ($backup.Status -eq "completed") { "Green" } elseif ($backup.Status -eq "failed") { "Red" } else { "Yellow" }
            Write-Host "    - ID: $($backup.Id)" -ForegroundColor $statusColor
            Write-Host "      Database: $($backup.DatabaseName)" -ForegroundColor $statusColor
            Write-Host "      Status: $($backup.Status)" -ForegroundColor $statusColor
            Write-Host "      Size: $sizeInfo" -ForegroundColor $statusColor
            Write-Host "      Started: $($backup.StartedAt)" -ForegroundColor $statusColor
            if ($backup.CompletedAt) {
                Write-Host "      Completed: $($backup.CompletedAt)" -ForegroundColor $statusColor
            }
            if ($backup.DurationMs) {
                Write-Host "      Duration: $($backup.DurationMs) ms" -ForegroundColor $statusColor
            }
            if ($backup.BackupPath) {
                Write-Host "      Path: $($backup.BackupPath)" -ForegroundColor $statusColor
            }
            if ($backup.ErrorMessage) {
                Write-Host "      Error: $($backup.ErrorMessage)" -ForegroundColor Red
            }
            Write-Info ""
        }
    }
} catch {
    Write-Error "✗ Get domain backup list failed: $_"
    if ($_.ErrorDetails.Message) {
        Write-Error "  Details: $($_.ErrorDetails.Message)"
    }
}
Write-Info ""

Write-Success "========================================="
Write-Success "All tests completed!"
Write-Success "========================================="
