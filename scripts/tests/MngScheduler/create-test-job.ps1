# Test System Job Oluşturma Scripti
# MongoDB'ye direkt job ekler (test için)

param(
    [string]$MongoConnection = "mongodb://admin:admin123@localhost:27017",
    [string]$DatabaseName = "mngkeeper",
    [string]$CollectionName = "@scheduled_jobs"
)

Write-Host ""
Write-Host "Test System Job Oluşturuluyor..." -ForegroundColor Yellow
Write-Host "   MongoDB: $MongoConnection" 
Write-Host "   Database: $DatabaseName"
Write-Host "   Collection: $CollectionName"
Write-Host ""

$job = @{
    jobId = "system_job1"
    jobType = 0  # System
    name = "Test System Job 1"
    description = "Her 30 saniyede bir http://localhost:1880/system_job1 adresine POST yapar"
    cronExpression = "0/30 * * * * ?"
    endpointUrl = "http://localhost:1880/system_job1"
    httpMethod = "POST"
    payload = '{"a": 1}'
    isActive = $true
    maxExecutionCount = 10
    totalExecutionCount = 0
    successfulExecutionCount = 0
    failedExecutionCount = 0
    timeoutSeconds = 300
    createdAt = [DateTime]::UtcNow
    updatedAt = $null
    createdBy = "system"
    domainId = $null
    lastExecution = $null
}

$jobJson = $job | ConvertTo-Json -Depth 10

Write-Host "Job JSON:" -ForegroundColor Cyan
Write-Host $jobJson
Write-Host ""

# MongoDB'ye eklemek için mongoimport veya mongosh kullanılabilir
# Örnek komut:
# mongosh "$MongoConnection/$DatabaseName" --eval "db.getCollection('$CollectionName').insertOne($jobJson)"

Write-Host "MongoDB'ye job eklemek için aşağıdaki komutu kullanabilirsiniz:" -ForegroundColor Yellow
Write-Host "mongosh `"$MongoConnection/$DatabaseName`" --eval `"db.getCollection('$CollectionName').insertOne($jobJson)`"" -ForegroundColor Cyan
Write-Host ""

# Alternatif: MongoDB.Driver kullanarak (PowerShell'de)
try {
    # MongoDB.Driver NuGet paketi gerekli
    Write-Host "MongoDB.Driver ile ekleme deneniyor..." -ForegroundColor Yellow
    
    # Bu kısım MongoDB.Driver NuGet paketi gerektirir
    # Şimdilik manuel ekleme öneriyoruz
    Write-Host "Manuel ekleme gerekli. Yukarıdaki mongosh komutunu kullanın." -ForegroundColor Yellow
}
catch {
    Write-Host "MongoDB.Driver bulunamadı. Manuel ekleme gerekli." -ForegroundColor Yellow
}
