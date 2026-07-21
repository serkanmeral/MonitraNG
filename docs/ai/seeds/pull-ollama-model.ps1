# Pull standard Ollama generative model for MonitraNG AI (qwen2.5:7b).
# Requires Docker Desktop and a running container named "ollama".
param(
    [string]$ContainerName = "ollama",
    [string]$Model = "qwen2.5:7b"
)

$ErrorActionPreference = "Stop"

$running = docker ps --filter "name=^/${ContainerName}$" --format "{{.Names}}" 2>$null
if (-not $running) {
    # compose may name without strict regex; try loose
    $running = docker ps --filter "name=$ContainerName" --format "{{.Names}}" | Select-Object -First 1
}

if (-not $running) {
    Write-Host "Ollama container not running (name=$ContainerName). Start mng_apps stack / ollama first." -ForegroundColor Red
    exit 1
}

Write-Host "Pulling $Model into container '$running' ..." -ForegroundColor Cyan
docker exec $running ollama pull $Model
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nInstalled models:" -ForegroundColor Cyan
docker exec $running ollama list
Write-Host "`nDone. Recreate mngllm if DefaultModel env changed." -ForegroundColor Green
