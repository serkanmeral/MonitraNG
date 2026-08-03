# Publishes MngLogs.Agent.Linux as self-contained linux-x64 (P3a).
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "linux-x64",
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "Presentation\MngLogs.Agent.Linux\MngLogs.Agent.Linux.csproj"
if (-not $Output) {
    $Output = Join-Path $root "artifacts\agent\linux-x64"
}

Write-Host "Publishing $project -> $Output ($Runtime, self-contained)"
dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $Output

Write-Host "Done. On Debian/Ubuntu:"
Write-Host "  scp -r $Output user@host:/tmp/mnglogs-agent"
Write-Host "  sudo bash /tmp/mnglogs-agent/packaging/install.sh"
