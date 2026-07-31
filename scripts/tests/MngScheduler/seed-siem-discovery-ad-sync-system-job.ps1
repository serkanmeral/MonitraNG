# Seeds system-siem-discovery-ad-sync (@scheduled_jobs) — mongosh gerekir.
param(
    [string]$MongoConnection = "mongodb://admin:admin123@192.168.20.8:27017",
    [string]$DatabaseName = "mngkeeper",
    [string]$CollectionName = "@scheduled_jobs",
    [string]$CollectorBaseUrl = "http://mnglogcollector:5091",
    [string]$DomainId = "odak",
    [string]$IngestApiKey = ""
)

$jsonPath = Join-Path $PSScriptRoot "system-siem-discovery-ad-sync.job.json"
if (-not (Test-Path $jsonPath)) {
    throw "Missing: $jsonPath"
}

$job = Get-Content $jsonPath -Raw | ConvertFrom-Json
$job.endpointUrl = "$CollectorBaseUrl/api/v1/discovery/sync"
$job.payload = (@{ domainId = $DomainId; source = "ad" } | ConvertTo-Json -Compress)
$job.updatedAt = [DateTime]::UtcNow

if (-not [string]::IsNullOrWhiteSpace($IngestApiKey)) {
    $job.headers = @{ "X-MngLogs-ApiKey" = $IngestApiKey }
}

$jobJson = ($job | ConvertTo-Json -Depth 10 -Compress)
$js = "const j = $jobJson; const c = db.getCollection('$CollectionName'); const r = c.updateOne({ jobId: j.jobId }, { `$set: j }, { upsert: true }); printjson(r);"

Write-Host "Upserting SIEM discovery AD sync job in $DatabaseName.$CollectionName ..." -ForegroundColor Yellow
Write-Host "  endpoint: $($job.endpointUrl)" -ForegroundColor DarkGray
Write-Host "  payload:  $($job.payload)" -ForegroundColor DarkGray
mongosh "$MongoConnection/$DatabaseName" --eval $js
