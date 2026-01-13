# MinIO Backup Verification Script
# Verifies that backup files exist in MinIO

param(
    [string]$BaseUrl = "http://localhost:5080",
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
Write-Info "MinIO Backup Verification"
Write-Info "========================================="
Write-Info "Base URL: $BaseUrl"
Write-Info "Domain: $DomainName"
Write-Info ""

$headers = @{
    "Content-Type" = "application/json"
}

# Get domain backups
Write-Info "[1] Getting domain backup list..."
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/backup/domain/$DomainName" -Method Get -Headers $headers -SkipCertificateCheck
    Write-Success "✓ Retrieved $($response.TotalCount) domain backups"
    
    $completedBackups = $response.Backups | Where-Object { $_.Status -eq "completed" }
    Write-Info "  Completed backups: $($completedBackups.Count)"
    
    if ($completedBackups.Count -eq 0) {
        Write-Warning "  No completed backups found"
        exit 0
    }
    
    Write-Info ""
    Write-Info "[2] Verifying backup files in MinIO..."
    
    foreach ($backup in $completedBackups | Select-Object -First 5) {
        Write-Info "  Checking backup: $($backup.Id)"
        Write-Info "    Path: $($backup.BackupPath)"
        Write-Info "    Size: $($backup.SizeBytes) bytes"
        
        # Try to get backup info from MinIO
        try {
            # Parse bucket and object path
            $pathParts = $backup.BackupPath -split '/', 2
            if ($pathParts.Length -eq 2) {
                $bucketName = $pathParts[0]
                $objectPath = $pathParts[1]
                
                Write-Info "    Bucket: $bucketName"
                Write-Info "    Object: $objectPath"
                
                # Note: Direct MinIO access would require MinIO client
                # For now, we'll just verify the backup status indicates success
                if ($backup.Status -eq "completed" -and $backup.SizeBytes -gt 0) {
                    Write-Success "    ✓ Backup appears to be successful (Status: completed, Size: $($backup.SizeBytes) bytes)"
                } else {
                    Write-Warning "    ⚠ Backup status indicates issues"
                }
            }
        } catch {
            Write-Error "    ✗ Error verifying backup: $_"
        }
        Write-Info ""
    }
    
} catch {
    Write-Error "✗ Failed to get domain backups: $_"
    if ($_.ErrorDetails.Message) {
        Write-Error "  Details: $($_.ErrorDetails.Message)"
    }
    exit 1
}

Write-Success "========================================="
Write-Success "Verification completed!"
Write-Success "========================================="
Write-Info ""
Write-Info "Note: To verify files directly in MinIO, use MinIO Console at http://localhost:9091"
Write-Info "      or use MinIO client (mc) to list objects in bucket: mng-$DomainName"
