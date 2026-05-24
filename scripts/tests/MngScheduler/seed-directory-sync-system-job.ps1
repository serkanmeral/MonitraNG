# Seeds K3 system job: system-directory-sync-all-domains (@scheduled_jobs)
param(
    [string]$MongoConnection = "mongodb://admin:admin123@localhost:27017",
    [string]$DatabaseName = "mngkeeper",
    [string]$CollectionName = "@scheduled_jobs"
)

$job = @{
    jobId = "system-directory-sync-all-domains"
    jobType = 0
    name = "Directory Sync (All Active Domains)"
    description = "Periyodik Keycloak → Mongo directory sync; her Active domain için MngKeeper POST"
    cronExpression = "0 0/30 * * * ?"
    endpointUrl = "orchestration://directory-sync"
    httpMethod = "POST"
    headers = $null
    payload = $null
    isActive = $true
    maxExecutionCount = $null
    totalExecutionCount = 0
    successfulExecutionCount = 0
    failedExecutionCount = 0
    timeoutSeconds = 600
    createdAt = [DateTime]::UtcNow
    updatedAt = $null
    createdBy = "system"
    domainId = $null
    lastExecution = $null
} | ConvertTo-Json -Depth 10 -Compress

$js = "const j = $job; const c = db.getCollection('$CollectionName'); const r = c.updateOne({ jobId: j.jobId }, { `$set: j }, { upsert: true }); printjson(r);"
Write-Host "Upserting directory sync system job in $DatabaseName.$CollectionName ..." -ForegroundColor Yellow
mongosh "$MongoConnection/$DatabaseName" --eval $js
