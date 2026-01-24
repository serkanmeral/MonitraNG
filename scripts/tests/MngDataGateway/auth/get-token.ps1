# Common Token Retrieval Script
# This script retrieves an authentication token from MngKeeper and saves it to a common location

param(
    [string]$KeeperBaseUrl = "https://localhost:5040",
    [string]$DomainName = "meral",
    [string]$Username = "meral_admin",
    [string]$Password = "Admin123!",
    [string]$TokenFile = "$env:TEMP\serkan_token.txt"
)

# Comprehensive SSL/TLS fixes
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Enable all TLS protocols
try {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11 -bor [System.Net.SecurityProtocolType]::Tls
} catch {
    try {
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    } catch {
        # Use default if TLS12 not available
    }
}

Write-Host ""
Write-Host "Token Alma Islemi Basliyor..." 
Write-Host "   Keeper URL: $KeeperBaseUrl" 
Write-Host "   Domain: $DomainName" 
Write-Host "   Username: $Username" 
Write-Host ""

try {
    $requestBody = @{
        username = $Username
        password = $Password
        domain = $DomainName
    } | ConvertTo-Json

    Write-Host "Token isteniyor..." 
    
    $params = @{
        Uri = "$KeeperBaseUrl/keeper/api/auth/token"
        Method = "POST"
        ContentType = "application/json"
        Body = $requestBody
        ErrorAction = "Stop"
    }
    
    # Add SkipCertificateCheck if available (PowerShell 6+)
    if (Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
        $params.SkipCertificateCheck = $true
    }
    
    $tokenResponse = Invoke-RestMethod @params

    $accessToken = $tokenResponse.accessToken

    if ([string]::IsNullOrEmpty($accessToken)) {
        Write-Host "ERROR: Token alinamadi! Response'da accessToken bulunamadi." 
        Write-Host "Response: $($tokenResponse | ConvertTo-Json -Depth 5)" 
        exit 1
    }

    # Token'i dosyaya kaydet
    $accessToken | Out-File -FilePath $TokenFile -Encoding utf8 -NoNewline
    
    Write-Host "SUCCESS: Token basariyla alindi ve kaydedildi!" 
    Write-Host "   Token dosyasi: $TokenFile" 
    Write-Host "   Token (ilk 50 karakter): $($accessToken.Substring(0, [Math]::Min(50, $accessToken.Length)))..." 
    Write-Host ""

    # Return token for use in other scripts
    return $accessToken

} catch {
    Write-Host "ERROR: Token alma hatasi!" 
    Write-Host "   Hata: $($_.Exception.Message)" 
    
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "   Status Code: $statusCode" 
        
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "   Response Body: $responseBody" 
        } catch {
            # Ignore stream read errors
        }
    }
    
    Write-Host ""
    exit 1
}
