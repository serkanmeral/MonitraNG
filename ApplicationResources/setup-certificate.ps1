# SSL Certificate Setup Script for MonitraNG
# Creates and configures SSL certificates for all services

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("dev", "staging", "production")]
    [string]$Environment = "dev",
    
    [Parameter(Mandatory=$false)]
    [switch]$Renew = $false,
    
    [Parameter(Mandatory=$false)]
    [string]$Domain = "monitrang.local",
    
    [Parameter(Mandatory=$false)]
    [string]$Password = "dev-password-123"
)

Write-Host "`n=== MonitraNG SSL Certificate Setup ===" -ForegroundColor Cyan
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Domain: *.$Domain" -ForegroundColor Yellow

$CertDir = "Cert/$Environment"
$CertPath = "$CertDir/$Domain.pfx"

# Create certificate directory if not exists
if (-not (Test-Path $CertDir)) {
    New-Item -Path $CertDir -ItemType Directory -Force | Out-Null
    Write-Host "✅ Created directory: $CertDir" -ForegroundColor Green
}

# Function to create .NET dev certificate
function New-DotNetDevCertificate {
    param(
        [string]$OutputPath,
        [string]$CertPassword
    )
    
    Write-Host "`n1. Creating .NET development certificate..." -ForegroundColor Yellow
    
    # Clean existing dev certs
    dotnet dev-certs https --clean
    
    # Create new certificate
    dotnet dev-certs https -ep $OutputPath -p $CertPassword --trust
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Certificate created: $OutputPath" -ForegroundColor Green
        Write-Host "✅ Certificate trusted in local machine" -ForegroundColor Green
        return $true
    } else {
        Write-Host "❌ Failed to create certificate" -ForegroundColor Red
        return $false
    }
}

# Function to update service configurations
function Update-ServiceConfiguration {
    param(
        [string]$ServiceName,
        [string]$RelativeCertPath,
        [string]$CertPassword,
        [string]$HttpsPort
    )
    
    $appsettingsPath = "$ServiceName/Presentation/$ServiceName.Api/appsettings.Development.json"
    
    if (-not (Test-Path $appsettingsPath)) {
        Write-Host "⚠️  Config not found: $appsettingsPath" -ForegroundColor Yellow
        return
    }
    
    Write-Host "  Updating: $appsettingsPath" -ForegroundColor Gray
    
    $config = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
    
    # Add Kestrel configuration if not exists
    if (-not $config.Kestrel) {
        $config | Add-Member -NotePropertyName "Kestrel" -NotePropertyValue @{} -Force
    }
    
    # Configure HTTPS endpoint
    $config.Kestrel = @{
        Endpoints = @{
            Http = @{
                Url = "http://localhost:$($HttpsPort - 442)"
            }
            Https = @{
                Url = "https://localhost:$HttpsPort"
                Certificate = @{
                    Path = $RelativeCertPath
                    Password = "`${CERT_PASSWORD}"
                }
            }
        }
    }
    
    # Save updated config
    $config | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath
    Write-Host "  ✅ Updated: $ServiceName" -ForegroundColor Green
}

# Main execution
switch ($Environment) {
    "dev" {
        Write-Host "`n=== Development Certificate Setup ===" -ForegroundColor Cyan
        
        # Check if certificate already exists
        if ((Test-Path $CertPath) -and -not $Renew) {
            Write-Host "⚠️  Certificate already exists: $CertPath" -ForegroundColor Yellow
            $confirm = Read-Host "Do you want to recreate it? (y/n)"
            if ($confirm -ne "y") {
                Write-Host "Skipping certificate creation." -ForegroundColor Yellow
                exit 0
            }
        }
        
        # Create certificate
        $success = New-DotNetDevCertificate -OutputPath $CertPath -CertPassword $Password
        
        if ($success) {
            # Save password to file (for reference)
            $Password | Out-File "$CertDir/$Domain-password.txt"
            
            Write-Host "`n2. Certificate Details:" -ForegroundColor Yellow
            Write-Host "  Path: $CertPath" -ForegroundColor Gray
            Write-Host "  Password: $Password" -ForegroundColor Gray
            Write-Host "  Type: Wildcard (*.monitrang.local)" -ForegroundColor Gray
            
            # Update service configurations
            Write-Host "`n3. Updating service configurations..." -ForegroundColor Yellow
            
            $relativePath = "../../../ApplicationResources/Cert/dev/monitrang.local.pfx"
            
            # Update MngKeeper
            Update-ServiceConfiguration -ServiceName "MngKeeper" -RelativeCertPath $relativePath -CertPassword "`${CERT_PASSWORD}" -HttpsPort 5443
            
            # Update MngReactor (when ready)
            # Update-ServiceConfiguration -ServiceName "MngReactor" -RelativeCertPath $relativePath -CertPassword "`${CERT_PASSWORD}" -HttpsPort 5444
            
            # Update MngEngine (when ready)
            # Update-ServiceConfiguration -ServiceName "MngEngine" -RelativeCertPath $relativePath -CertPassword "`${CERT_PASSWORD}" -HttpsPort 5445
            
            Write-Host "`n4. Setting environment variable..." -ForegroundColor Yellow
            [System.Environment]::SetEnvironmentVariable("CERT_PASSWORD", $Password, [System.EnvironmentVariableTarget]::User)
            Write-Host "✅ CERT_PASSWORD set in user environment variables" -ForegroundColor Green
            
            Write-Host "`n✅ CERTIFICATE SETUP COMPLETE!" -ForegroundColor Green
            Write-Host "`nNext Steps:" -ForegroundColor Cyan
            Write-Host "1. Restart your terminal to load environment variable" -ForegroundColor Gray
            Write-Host "2. Update hosts file:" -ForegroundColor Gray
            Write-Host "   Add: 127.0.0.1  api.monitrang.local app.monitrang.local" -ForegroundColor White
            Write-Host "3. Start services:" -ForegroundColor Gray
            Write-Host "   cd MngKeeper/Presentation/MngKeeper.Api" -ForegroundColor White
            Write-Host "   dotnet run" -ForegroundColor White
            Write-Host "4. Test HTTPS:" -ForegroundColor Gray
            Write-Host "   https://localhost:5443/health" -ForegroundColor White
            Write-Host "   https://api.monitrang.local:5443/health" -ForegroundColor White
        }
    }
    
    "staging" {
        Write-Host "⚠️  Staging certificate setup not implemented yet" -ForegroundColor Yellow
        Write-Host "Please use Let's Encrypt or commercial CA" -ForegroundColor Gray
    }
    
    "production" {
        Write-Host "⚠️  Production certificate setup not implemented yet" -ForegroundColor Yellow
        Write-Host "Recommended: Use Let's Encrypt with Certbot" -ForegroundColor Gray
        Write-Host "Command: certbot certonly --standalone -d monitrang.com -d *.monitrang.com" -ForegroundColor White
    }
}

Write-Host ""

