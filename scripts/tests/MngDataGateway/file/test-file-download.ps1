# File Download Test Script
# Tests file download functionality via FilesController

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$authScript = Join-Path $scriptPath "..\auth\load-token.ps1"
. $authScript

$baseUrl = "https://localhost:5010"
$token = $global:Token

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token not found. Please run get-token.ps1 first." -ForegroundColor Red
    exit 1
}

Write-Host "`n🧪 Testing File Download Endpoint" -ForegroundColor Cyan
Write-Host "==================================`n" -ForegroundColor Cyan

# Check if file path is provided as parameter or use uploaded file
$filePath = $args[0]
if ([string]::IsNullOrEmpty($filePath)) {
    Write-Host "Usage: .\test-file-download.ps1 <filePath>" -ForegroundColor Yellow
    Write-Host "Example: .\test-file-download.ps1 '/mng-meral/data/@test_files/record-id/test/file-uuid.pdf'" -ForegroundColor Yellow
    exit 1
}

Write-Host "Test: Download file" -ForegroundColor Yellow
Write-Host "   File Path: $filePath" -ForegroundColor Gray

try {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/v1/files/download?filePath=$([System.Web.HttpUtility]::UrlEncode($filePath))" `
        -Method GET `
        -Headers @{
            "Authorization" = "Bearer $token"
        } `
        -SkipCertificateCheck `
        -OutFile "downloaded_file.pdf"

    Write-Host "✅ Download successful!" -ForegroundColor Green
    Write-Host "   File saved to: downloaded_file.pdf" -ForegroundColor Gray
    Write-Host "   Content-Type: $($response.Headers['Content-Type'])" -ForegroundColor Gray
    Write-Host "   Content-Length: $($response.Headers['Content-Length']) bytes" -ForegroundColor Gray
    
    # Check file size
    $fileInfo = Get-Item "downloaded_file.pdf"
    Write-Host "   Actual file size: $($fileInfo.Length) bytes" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Download failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
}

Write-Host "`n✅ File Download Test Complete!" -ForegroundColor Green
