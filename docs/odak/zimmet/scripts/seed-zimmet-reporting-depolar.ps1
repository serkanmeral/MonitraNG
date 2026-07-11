# Wrapper — Depolar raporu
param([string]$BaseUrl = "http://192.168.20.20:5040")
& (Join-Path $PSScriptRoot "seed-zimmet-reporting-report.ps1") -BaseUrl $BaseUrl -SeedFile "zimmet-reporting-depolar.json"
