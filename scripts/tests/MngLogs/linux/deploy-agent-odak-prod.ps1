# Publish + deploy MngLogs.Agent.Linux to a host, collector = Odak PROD (192.168.20.8).
# Default deploy target is prod itself; override -Server for other Linux hosts.
# Test ortamı için: deploy-agent-odak-test.ps1
param(
    [string]$Server = "192.168.20.8",
    [string]$CollectorUrl = "http://192.168.20.8:5091",
    [string]$HostId = "monitrang-linux-pilot",
    [string]$SshUser = "odak",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "deploy-agent-odak-test.ps1") `
    -Server $Server `
    -CollectorUrl $CollectorUrl `
    -HostId $HostId `
    -SshUser $SshUser `
    -SkipPublish:$SkipPublish
