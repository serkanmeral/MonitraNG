# GitLab Runner Registration Script
# Bu script, GitLab Runner'ı otomatik olarak kaydeder

param(
    [Parameter(Mandatory=$true)]
    [string]$RegistrationToken,
    
    [string]$Description = "monitrang-runner",
    [string]$Tags = "docker,windows",
    [string]$Executor = "docker",
    [string]$DockerImage = "docker:latest",
    [string]$GitLabUrl = "http://gitlab"
)

Write-Host "=== GitLab Runner Registration ===" -ForegroundColor Cyan
Write-Host ""

# Token kontrolü
if ([string]::IsNullOrWhiteSpace($RegistrationToken)) {
    Write-Host "Hata: Registration token gereklidir!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Kullanım:" -ForegroundColor Yellow
    Write-Host "  .\scripts\register-gitlab-runner.ps1 -RegistrationToken 'YOUR_TOKEN'" -ForegroundColor White
    Write-Host ""
    Write-Host "Token'ı almak için:" -ForegroundColor Yellow
    Write-Host "  - GitLab'da: Settings > CI/CD > Runners" -ForegroundColor Gray
    Write-Host "  - 'Set up a specific runner manually' bölümünden token'ı kopyalayın" -ForegroundColor Gray
    exit 1
}

Write-Host "Runner kaydediliyor..." -ForegroundColor Yellow
Write-Host "  GitLab URL: $GitLabUrl" -ForegroundColor Gray
Write-Host "  Description: $Description" -ForegroundColor Gray
Write-Host "  Executor: $Executor" -ForegroundColor Gray
Write-Host "  Docker Image: $DockerImage" -ForegroundColor Gray
Write-Host ""

# Runner'ı kaydet
docker exec gitlab-runner gitlab-runner register `
    --non-interactive `
    --url "$GitLabUrl" `
    --registration-token "$RegistrationToken" `
    --executor "$Executor" `
    --docker-image "$DockerImage" `
    --description "$Description" `
    --tag-list "$Tags" `
    --run-untagged="true" `
    --locked="false"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✓ Runner başarıyla kaydedildi!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Runner listesi:" -ForegroundColor Cyan
    docker exec gitlab-runner gitlab-runner list
    
    Write-Host ""
    Write-Host "GitLab'da kontrol edin:" -ForegroundColor Cyan
    Write-Host "  Settings > CI/CD > Runners" -ForegroundColor Yellow
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "✗ Runner kaydı başarısız oldu!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Kontrol edin:" -ForegroundColor Yellow
    Write-Host "  - GitLab container'ının çalıştığından emin olun" -ForegroundColor Gray
    Write-Host "  - Token'ın geçerli olduğundan emin olun" -ForegroundColor Gray
    Write-Host "  - GitLab URL'inin doğru olduğundan emin olun" -ForegroundColor Gray
    exit 1
}

