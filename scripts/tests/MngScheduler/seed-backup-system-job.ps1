# Seeds system-backup-daily (@scheduled_jobs) — mongosh gerekir.
param(
    [string]$MongoConnection = "mongodb://admin:admin123@192.168.20.20:27017",
    [string]$DatabaseName = "mngkeeper",
    [string]$CollectionName = "@scheduled_jobs",
    [string]$MngAdminBaseUrl = "http://192.168.20.20:5080"
)

$jsonPath = Join-Path $PSScriptRoot "system-backup-daily.job.json"
if (-not (Test-Path $jsonPath)) {
    throw "Missing: $jsonPath"
}

$job = Get-Content $jsonPath -Raw | ConvertFrom-Json
$job.endpointUrl = "$MngAdminBaseUrl/api/v1/backup/full"
$job.updatedAt = [DateTime]::UtcNow

$jobJson = ($job | ConvertTo-Json -Depth 10 -Compress)
$js = "const j = $jobJson; const c = db.getCollection('$CollectionName'); const r = c.updateOne({ jobId: j.jobId }, { `$set: j }, { upsert: true }); printjson(r);"

Write-Host "Upserting backup system job in $DatabaseName.$CollectionName ..." -ForegroundColor Yellow
mongosh "$MongoConnection/$DatabaseName" --eval $js
