# Get admin token for test domain
$baseUrl = "https://localhost:5001/api"
$domainName = "testdomain20251216192458"

Write-Host "`n=== ADMIN USER TOKEN ===" -ForegroundColor Green
Write-Host ""

$tokenBody = @{
    username = "${domainName}_admin"
    password = "Admin123!"
    domain = $domainName
} | ConvertTo-Json

try {
    $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/auth/token" -Method POST -Body $tokenBody -ContentType "application/json" -SkipCertificateCheck
    
    Write-Host "Username: " -NoNewline -ForegroundColor Cyan
    Write-Host "${domainName}_admin" -ForegroundColor White
    Write-Host "Domain: " -NoNewline -ForegroundColor Cyan
    Write-Host $domainName -ForegroundColor White
    Write-Host ""
    Write-Host "Access Token:" -ForegroundColor Yellow
    Write-Host $tokenResponse.accessToken -ForegroundColor Gray
    Write-Host ""
    
    # Parse token to show claims
    $tokenParts = $tokenResponse.accessToken.Split('.')
    $payload = $tokenParts[1]
    while ($payload.Length % 4 -ne 0) { $payload += "=" }
    $json = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload.Replace('-', '+').Replace('_', '/')))
    $claims = $json | ConvertFrom-Json
    
    Write-Host "Token Claims:" -ForegroundColor Cyan
    Write-Host "  isAdmin: " -NoNewline -ForegroundColor White
    if ($claims.isAdmin -or $claims.is_admin) {
        $isAdmin = if ($claims.isAdmin) { $claims.isAdmin } else { $claims.is_admin }
        Write-Host $isAdmin -ForegroundColor Green
    } else {
        Write-Host "false" -ForegroundColor Red
    }
    
    Write-Host "  user_groups: " -NoNewline -ForegroundColor White
    if ($claims.user_groups) {
        $groups = if ($claims.user_groups -is [System.Array]) { $claims.user_groups -join ', ' } else { $claims.user_groups }
        Write-Host $groups -ForegroundColor Green
    } else {
        Write-Host "N/A" -ForegroundColor Red
    }
    
    Write-Host "  domain_name: " -NoNewline -ForegroundColor White
    Write-Host $claims.domain_name -ForegroundColor Green
    
    Write-Host "  preferred_username: " -NoNewline -ForegroundColor White
    Write-Host $claims.preferred_username -ForegroundColor Green
    
} catch {
    Write-Host "Error: " -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

