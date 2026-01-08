# Comprehensive Smoke Test for Backend Services and API Gateway
# Tests: Direct Health Checks, Gateway Routes, Auth Flow, Basic Scenarios
#
# Usage:
#   .\smoke-test-backend-gateway.ps1
#   .\smoke-test-backend-gateway.ps1 -BaseUrl "https://api.monitrang.com"
#   .\smoke-test-backend-gateway.ps1 -TestDirectHealth -TestGatewayRoutes -TestAuthFlow

param(
    # Base URL for direct service access (default: localhost)
    [string]$DirectBaseUrl = "https://localhost",
    
    # Gateway URL (default: localhost)
    [string]$GatewayBaseUrl = "https://localhost:5040",
    
    # Test domain credentials
    [string]$DomainName = "meral",
    [string]$Username = "meral_admin",
    [string]$Password = "Admin123!",
    
    # Test flags
    [switch]$TestDirectHealth = $true,
    [switch]$TestGatewayRoutes = $true,
    [switch]$TestAuthFlow = $true,
    [switch]$TestBasicScenarios = $true,
    
    # Skip SSL certificate validation
    [switch]$SkipCertificateCheck = $true,
    
    # Verbose output
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

# SSL/TLS Configuration
if ($SkipCertificateCheck) {
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
}

# Color output functions
function Write-Success { param($message) Write-Host "✓ $message" -ForegroundColor Green }
function Write-Error { param($message) Write-Host "✗ $message" -ForegroundColor Red }
function Write-Info { param($message) Write-Host "ℹ $message" -ForegroundColor Cyan }
function Write-Warning { param($message) Write-Host "⚠ $message" -ForegroundColor Yellow }
function Write-Test { param($message) Write-Host "→ $message" -ForegroundColor Yellow }

# Test result tracking
$script:TestResults = @{
    Total = 0
    Passed = 0
    Failed = 0
    Skipped = 0
    Details = @()
}

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [hashtable]$Headers = @{},
        [string]$Method = "GET",
        [object]$Body = $null,
        [int[]]$ExpectedStatusCodes = @(200),
        [scriptblock]$Validator = $null
    )
    
    $script:TestResults.Total++
    Write-Test "$Name"
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            ErrorAction = "Stop"
        }
        
        if ($SkipCertificateCheck) {
            $params.SkipCertificateCheck = $true
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
            $params.ContentType = "application/json"
        }
        
        $response = Invoke-RestMethod @params
        $statusCode = 200 # Invoke-RestMethod doesn't expose status code, assume 200 if no exception
        
        # Validate status code if we can determine it
        # Note: Invoke-RestMethod doesn't expose status code easily, so we rely on exception handling
        
        # Run custom validator if provided
        if ($Validator) {
            $validationResult = & $Validator $response
            if (-not $validationResult) {
                throw "Validation failed"
            }
        }
        
        Write-Success "$Name - Status: OK"
        $script:TestResults.Passed++
        $script:TestResults.Details += @{
            Name = $Name
            Status = "Passed"
            Url = $Url
        }
        return $true
    }
    catch {
        $errorMsg = $_.Exception.Message
        if ($_.Exception.Response) {
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $responseBody = $reader.ReadToEnd()
                $errorMsg = "$errorMsg - Response: $responseBody"
            } catch {
                # Ignore error reading response
            }
        }
        
        Write-Error "$Name - Failed: $errorMsg"
        $script:TestResults.Failed++
        $script:TestResults.Details += @{
            Name = $Name
            Status = "Failed"
            Url = $Url
            Error = $errorMsg
        }
        return $false
    }
}

# ==========================================
# 1. Direct Health Check Tests
# ==========================================

function Test-DirectHealthChecks {
    Write-Info "=== 1. Direct Health Check Tests ===" 
    Write-Host ""
    
    # MngGateway Health Check (try HTTP first, then HTTPS)
    $gatewayHealthUrl = "http://localhost:5000/health"
    if ($SkipCertificateCheck) {
        # Try HTTPS on port 5443 (HTTPS port)
        $gatewayHealthUrlHttps = "$DirectBaseUrl`:5443/health"
        Test-Endpoint `
            -Name "MngGateway Health (Direct HTTPS)" `
            -Url $gatewayHealthUrlHttps `
            -Validator { param($r) $r.status -eq "healthy" -or $r.service -eq "MngGateway" }
    }
    
    # Also try HTTP port
    Test-Endpoint `
        -Name "MngGateway Health (Direct HTTP)" `
        -Url $gatewayHealthUrl `
        -Validator { param($r) $r.status -eq "healthy" -or $r.service -eq "MngGateway" }
    
    # MngKeeper Health Checks
    Test-Endpoint `
        -Name "MngKeeper Health (Direct)" `
        -Url "$DirectBaseUrl`:5001/health" `
        -Validator { param($r) $r.Status -eq "Healthy" -or $r.Service -eq "MngKeeper API" }
    
    Test-Endpoint `
        -Name "MngKeeper Version (Direct)" `
        -Url "$DirectBaseUrl`:5001/api/version/short"
    
    Test-Endpoint `
        -Name "MngKeeper Health Ready (Direct)" `
        -Url "$DirectBaseUrl`:5001/health/ready"
    
    # MngDataGateway Health Checks
    Test-Endpoint `
        -Name "MngDataGateway Health (Direct)" `
        -Url "$DirectBaseUrl`:5010/api/v1/health" `
        -Validator { param($r) $r.Status -eq "healthy" -or $r.Status -eq "degraded" }
    
    Test-Endpoint `
        -Name "MngDataGateway Health Live (Direct)" `
        -Url "$DirectBaseUrl`:5010/api/v1/health/live"
    
    Test-Endpoint `
        -Name "MngDataGateway Health Ready (Direct)" `
        -Url "$DirectBaseUrl`:5010/api/v1/health/ready"
    
    # MngHub Health Check
    Test-Endpoint `
        -Name "MngHub Health (Direct)" `
        -Url "http://localhost:5020/health" `
        -Validator { param($r) $r.status -eq "healthy" -or $r.service -eq "MngHub" }
    
    Write-Host ""
}

# ==========================================
# 2. Gateway Route Tests
# ==========================================

function Test-GatewayRoutes {
    Write-Info "=== 2. Gateway Route Tests ===" 
    Write-Host ""
    
    # Gateway Health Check (via Gateway)
    Test-Endpoint `
        -Name "Gateway Health (via Gateway)" `
        -Url "$GatewayBaseUrl/health" `
        -Validator { param($r) $r.status -eq "healthy" -or $r.service -eq "MngGateway" }
    
    # Gateway → MngKeeper Routes
    Test-Endpoint `
        -Name "Gateway → MngKeeper Health" `
        -Url "$GatewayBaseUrl/keeper/api/version/short"
    
    # Gateway → MngDataGateway Routes
    Test-Endpoint `
        -Name "Gateway → MngDataGateway Health" `
        -Url "$GatewayBaseUrl/data/api/v1/health" `
        -Validator { param($r) $r.Status -eq "healthy" -or $r.Status -eq "degraded" }
    
    # Gateway → MngHub Routes
    Test-Endpoint `
        -Name "Gateway → MngHub Health" `
        -Url "$GatewayBaseUrl/hub/health" `
        -Validator { param($r) $r.status -eq "healthy" -or $r.service -eq "MngHub" }
    
    # Gateway → Keycloak Routes (public endpoint)
    Test-Endpoint `
        -Name "Gateway → Keycloak Realm Info" `
        -Url "$GatewayBaseUrl/auth/realms/master" `
        -Validator { param($r) $r.realm -eq "master" }
    
    Write-Host ""
}

# ==========================================
# 3. Authentication Flow Tests
# ==========================================

function Test-AuthFlow {
    Write-Info "=== 3. Authentication Flow Tests ===" 
    Write-Host ""
    
    $token = $null
    
    # Test 1: Get Token via Direct MngKeeper
    Write-Test "Get Token (Direct MngKeeper)"
    try {
        $tokenBody = @{
            username = $Username
            password = $Password
            domain = $DomainName
        }
        
        $tokenResponse = Test-Endpoint `
            -Name "Get Token (Direct MngKeeper)" `
            -Url "$DirectBaseUrl`:5001/api/auth/token" `
            -Method "POST" `
            -Body $tokenBody `
            -Validator { param($r) -not [string]::IsNullOrEmpty($r.accessToken) }
        
        if ($tokenResponse) {
            $tokenResponseObj = Invoke-RestMethod `
                -Uri "$DirectBaseUrl`:5001/api/auth/token" `
                -Method "POST" `
                -Body ($tokenBody | ConvertTo-Json) `
                -ContentType "application/json" `
                -SkipCertificateCheck:$SkipCertificateCheck
            
            $token = $tokenResponseObj.accessToken
            Write-Success "Token obtained (Direct) - Preview: $($token.Substring(0, [Math]::Min(50, $token.Length)))..."
        }
    }
    catch {
        Write-Error "Failed to get token (Direct): $($_.Exception.Message)"
    }
    
    Write-Host ""
    
    # Test 2: Get Token via Gateway
    if ($token) {
        Write-Test "Get Token (via Gateway)"
        try {
            $tokenBody = @{
                username = $Username
                password = $Password
                domain = $DomainName
            }
            
            $gatewayTokenResponse = Invoke-RestMethod `
                -Uri "$GatewayBaseUrl/keeper/api/auth/token" `
                -Method "POST" `
                -Body ($tokenBody | ConvertTo-Json) `
                -ContentType "application/json" `
                -SkipCertificateCheck:$SkipCertificateCheck
            
            if ($gatewayTokenResponse.accessToken) {
                Write-Success "Token obtained (via Gateway) - Preview: $($gatewayTokenResponse.accessToken.Substring(0, [Math]::Min(50, $gatewayTokenResponse.accessToken.Length)))..."
                
                # Validate tokens match
                if ($gatewayTokenResponse.accessToken -eq $token) {
                    Write-Success "Tokens match (Direct vs Gateway)"
                } else {
                    Write-Warning "Tokens differ (Direct vs Gateway) - This might be expected"
                }
            }
        }
        catch {
            Write-Error "Failed to get token (via Gateway): $($_.Exception.Message)"
        }
    }
    
    Write-Host ""
    
    # Test 3: Authenticated Request (Direct)
    if ($token) {
        $headers = @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        }
        
        Test-Endpoint `
            -Name "Authenticated Request - Get Domains (Direct)" `
            -Url "$DirectBaseUrl`:5001/api/domain" `
            -Headers $headers
        
        Test-Endpoint `
            -Name "Authenticated Request - Get Users (Direct)" `
            -Url "$DirectBaseUrl`:5001/api/user" `
            -Headers $headers
    }
    
    Write-Host ""
    
    # Test 4: Authenticated Request (via Gateway)
    if ($token) {
        $headers = @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        }
        
        Test-Endpoint `
            -Name "Authenticated Request - Get Domains (via Gateway)" `
            -Url "$GatewayBaseUrl/keeper/api/domain" `
            -Headers $headers
        
        Test-Endpoint `
            -Name "Authenticated Request - Get Users (via Gateway)" `
            -Url "$GatewayBaseUrl/keeper/api/user" `
            -Headers $headers
    }
    
    Write-Host ""
    
    return $token
}

# ==========================================
# 4. Basic Scenario Tests
# ==========================================

function Test-BasicScenarios {
    param([string]$Token)
    
    Write-Info "=== 4. Basic Scenario Tests ===" 
    Write-Host ""
    
    if (-not $Token) {
        Write-Warning "Token not available - skipping authenticated scenario tests"
        $script:TestResults.Skipped++
        return
    }
    
    $headers = @{
        "Authorization" = "Bearer $Token"
        "Content-Type" = "application/json"
    }
    
    # Scenario 1: Domain Management Flow (via Gateway)
    Write-Test "Scenario 1: Domain Management Flow (via Gateway)"
    
    # Get domains
    $domainsResult = Test-Endpoint `
        -Name "Scenario 1.1: Get Domains" `
        -Url "$GatewayBaseUrl/keeper/api/domain" `
        -Headers $headers
    
    if ($domainsResult) {
        try {
            $domains = Invoke-RestMethod `
                -Uri "$GatewayBaseUrl/keeper/api/domain" `
                -Method "GET" `
                -Headers $headers `
                -SkipCertificateCheck:$SkipCertificateCheck
            
            if ($domains.Count -gt 0) {
                $testDomain = $domains | Where-Object { $_.name -eq $DomainName } | Select-Object -First 1
                if ($testDomain) {
                    Write-Info "  Test domain found: $($testDomain.name) (ID: $($testDomain.id))"
                    
                    # Get domain by ID
                    Test-Endpoint `
                        -Name "Scenario 1.2: Get Domain by ID" `
                        -Url "$GatewayBaseUrl/keeper/api/domain/$($testDomain.id)" `
                        -Headers $headers
                }
            }
        }
        catch {
            Write-Warning "Could not retrieve domain details: $($_.Exception.Message)"
        }
    }
    
    Write-Host ""
    
    # Scenario 2: User Management Flow (via Gateway)
    Write-Test "Scenario 2: User Management Flow (via Gateway)"
    
    Test-Endpoint `
        -Name "Scenario 2.1: Get Users" `
        -Url "$GatewayBaseUrl/keeper/api/user" `
        -Headers $headers
    
    Write-Host ""
    
    # Scenario 3: MngDataGateway Health Check (via Gateway)
    Write-Test "Scenario 3: MngDataGateway Health Check (via Gateway)"
    
    $healthResult = Test-Endpoint `
        -Name "Scenario 3.1: Get Health Status" `
        -Url "$GatewayBaseUrl/data/api/v1/health" `
        -Validator { param($r) $r.Status -in @("healthy", "degraded") }
    
    if ($healthResult) {
        try {
            $health = Invoke-RestMethod `
                -Uri "$GatewayBaseUrl/data/api/v1/health" `
                -Method "GET" `
                -SkipCertificateCheck:$SkipCertificateCheck
            
            Write-Info "  MongoDB: $($health.Checks.MongoDB.Status)"
            Write-Info "  RabbitMQ: $($health.Checks.RabbitMQ.Status)"
            Write-Info "  Disk: $($health.Checks.Disk.Status)"
        }
        catch {
            Write-Warning "Could not retrieve health details: $($_.Exception.Message)"
        }
    }
    
    Write-Host ""
}

# ==========================================
# Main Execution
# ==========================================

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Backend Services & API Gateway Smoke Test                    ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Info "Configuration:"
Write-Host "  Direct Base URL: $DirectBaseUrl" -ForegroundColor Gray
Write-Host "  Gateway Base URL: $GatewayBaseUrl" -ForegroundColor Gray
Write-Host "  Domain: $DomainName" -ForegroundColor Gray
Write-Host "  Username: $Username" -ForegroundColor Gray
Write-Host "  Skip Certificate Check: $SkipCertificateCheck" -ForegroundColor Gray
Write-Host ""

$startTime = Get-Date
$token = $null

try {
    # Run tests
    if ($TestDirectHealth) {
        Test-DirectHealthChecks
    }
    
    if ($TestGatewayRoutes) {
        Test-GatewayRoutes
    }
    
    if ($TestAuthFlow) {
        $token = Test-AuthFlow
    }
    
    if ($TestBasicScenarios) {
        Test-BasicScenarios -Token $token
    }
}
catch {
    Write-Error "Test execution failed: $($_.Exception.Message)"
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
}

$endTime = Get-Date
$duration = $endTime - $startTime

# ==========================================
# Test Summary
# ==========================================

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Test Summary                                                  ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Total Tests:  $($script:TestResults.Total)" -ForegroundColor White
Write-Host "  Passed:       $($script:TestResults.Passed)" -ForegroundColor Green
Write-Host "  Failed:       $($script:TestResults.Failed)" -ForegroundColor $(if ($script:TestResults.Failed -eq 0) { "Green" } else { "Red" })
Write-Host "  Skipped:      $($script:TestResults.Skipped)" -ForegroundColor Yellow
Write-Host "  Duration:     $($duration.TotalSeconds.ToString('F2'))s" -ForegroundColor Gray
Write-Host ""

# Show failed tests
if ($script:TestResults.Failed -gt 0) {
    Write-Host "Failed Tests:" -ForegroundColor Red
    foreach ($detail in $script:TestResults.Details | Where-Object { $_.Status -eq "Failed" }) {
        Write-Host "  ✗ $($detail.Name)" -ForegroundColor Red
        Write-Host "    URL: $($detail.Url)" -ForegroundColor Gray
        if ($detail.Error) {
            Write-Host "    Error: $($detail.Error)" -ForegroundColor Gray
        }
    }
    Write-Host ""
}

# Exit code
$exitCode = if ($script:TestResults.Failed -eq 0) { 0 } else { 1 }

if ($exitCode -eq 0) {
    Write-Success "All tests passed! ✓"
} else {
    Write-Error "Some tests failed. Please review the output above."
}

exit $exitCode

