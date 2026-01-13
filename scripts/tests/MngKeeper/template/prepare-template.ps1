# Template Database Hazırlama Script'i
# Meral domain'inden seçilen collection'ları mng_templates database'ine kopyalar
#
# Kullanım:
#   .\prepare-template.ps1
#   .\prepare-template.ps1 -SourceDatabase "mng_meral" -Collections "@side_menu","book","@datasets"
#
# Parametreler:
#   -SourceDatabase: Kaynak database adı (varsayılan: mng_meral)
#   -TemplateDatabase: Template database adı (varsayılan: mng_templates)
#   -Collections: Kopyalanacak collection'lar (boş ise tüm collection'lar kopyalanır)
#   -MongoConnectionString: MongoDB bağlantı string'i (varsayılan: mongodb://admin:admin123@localhost:27017)

param(
    [string]$SourceDatabase = "mng_meral",
    [string]$TemplateDatabase = "mng_templates",
    [string[]]$Collections = @(),
    [string]$MongoConnectionString = "mongodb://admin:admin123@localhost:27017",
    [string]$DockerContainer = "mongo",
    [switch]$Force
)

Write-Host ""
Write-Host "=== Template Database Hazırlama ===" -ForegroundColor Cyan
Write-Host "Kaynak Database: $SourceDatabase" -ForegroundColor Yellow
Write-Host "Template Database: $TemplateDatabase" -ForegroundColor Yellow
Write-Host ""

# MongoDB bağlantısını test et
Write-Host "MongoDB bağlantısı test ediliyor..." -ForegroundColor Yellow

try {
    # Docker container kontrolü
    $dockerContainerRunning = docker ps --filter "name=$DockerContainer" --format "{{.Names}}" | Select-String -Pattern $DockerContainer
    
    if ($dockerContainerRunning) {
        Write-Host "✓ Docker container bulundu: $DockerContainer" -ForegroundColor Green
        $useDocker = $true
    } else {
        # mongosh kontrolü (fallback)
        $mongoshPath = Get-Command mongosh -ErrorAction SilentlyContinue
        
        if (-not $mongoshPath) {
            Write-Host "✗ MongoDB container veya mongosh bulunamadı." -ForegroundColor Red
            Write-Host "  Container: $DockerContainer" -ForegroundColor Yellow
            Write-Host "  mongosh yükleme: winget install MongoDB.Shell" -ForegroundColor Yellow
            exit 1
        }
        
        Write-Host "✓ mongosh bulundu (Docker container yok, local mongosh kullanılıyor)" -ForegroundColor Green
        $useDocker = $false
    }
    
    # Source database'in var olup olmadığını kontrol et
    $checkSourceScript = @"
use admin
db.auth('admin', 'admin123')
var dbs = db.adminCommand('listDatabases').databases.map(function(db) { return db.name; })
if (dbs.indexOf('$SourceDatabase') === -1) {
    print('ERROR: Source database $SourceDatabase not found')
    quit(1)
}
print('OK: Source database found')
"@
    
    if ($useDocker) {
        $checkResult = $checkSourceScript | docker exec -i $DockerContainer mongosh $MongoConnectionString --quiet 2>&1
    } else {
        $checkResult = $checkSourceScript | & mongosh $MongoConnectionString --quiet 2>&1
    }
    
    if ($LASTEXITCODE -ne 0 -or $checkResult -match "ERROR") {
        Write-Host "✗ Kaynak database bulunamadı: $SourceDatabase" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✓ Kaynak database bulundu: $SourceDatabase" -ForegroundColor Green
    
    # Template database'i oluştur (eğer yoksa)
    Write-Host "`nTemplate database hazırlanıyor: $TemplateDatabase" -ForegroundColor Yellow
    
    # Source database'deki collection'ları listele
    $listCollectionsScript = @"
use $SourceDatabase
db.auth('admin', 'admin123')
var collections = db.getCollectionNames()
collections.forEach(function(coll) {
    print('COLLECTION: ' + coll)
})
"@
    
    if ($useDocker) {
        $collectionList = $listCollectionsScript | docker exec -i $DockerContainer mongosh $MongoConnectionString --quiet 2>&1
    } else {
        $collectionList = $listCollectionsScript | & mongosh $MongoConnectionString --quiet 2>&1
    }
    $allCollections = ($collectionList | Where-Object { $_ -match "^COLLECTION: " }) -replace "^COLLECTION: ", ""
    
    # System collection'ları hariç tut
    $systemCollections = @("@users", "@groups")
    $availableCollections = $allCollections | Where-Object { $systemCollections -notcontains $_ }
    
    if ($availableCollections.Count -eq 0) {
        Write-Host "✗ Kopyalanacak collection bulunamadı" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "`nBulunan collection'lar:" -ForegroundColor Cyan
    $availableCollections | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
    
    # Kopyalanacak collection'ları belirle
    $collectionsToCopy = if ($Collections.Count -eq 0) {
        $availableCollections
    } else {
        $Collections | Where-Object { $availableCollections -contains $_ }
    }
    
    if ($collectionsToCopy.Count -eq 0) {
        Write-Host "✗ Belirtilen collection'lar bulunamadı" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "`nKopyalanacak collection'lar:" -ForegroundColor Cyan
    $collectionsToCopy | ForEach-Object { Write-Host "  - $_" -ForegroundColor Green }
    
    # Onay iste (Force parametresi yoksa)
    if (-not $Force) {
        Write-Host "`nDevam etmek istiyor musunuz? (E/H): " -ForegroundColor Yellow -NoNewline
        $confirmation = Read-Host
        
        if ($confirmation -ne "E" -and $confirmation -ne "e" -and $confirmation -ne "Y" -and $confirmation -ne "y") {
            Write-Host "İşlem iptal edildi." -ForegroundColor Yellow
            exit 0
        }
    } else {
        Write-Host "`nForce modu aktif, onay atlanıyor..." -ForegroundColor Yellow
    }
    
    # Template database'i temizle (eğer varsa)
    Write-Host "`nTemplate database temizleniyor..." -ForegroundColor Yellow
    $dropTemplateScript = @"
use $TemplateDatabase
db.auth('admin', 'admin123')
db.dropDatabase()
print('OK: Template database dropped')
"@
    
    if ($useDocker) {
        $dropResult = $dropTemplateScript | docker exec -i $DockerContainer mongosh $MongoConnectionString --quiet 2>&1
    } else {
        $dropResult = $dropTemplateScript | & mongosh $MongoConnectionString --quiet 2>&1
    }
    Write-Host "✓ Template database temizlendi" -ForegroundColor Green
    
    # Her collection'ı kopyala
    $totalDocuments = 0
    $copiedCollections = @()
    
    foreach ($collectionName in $collectionsToCopy) {
        Write-Host "`nKopyalanıyor: $collectionName" -ForegroundColor Yellow
        
        # Collection'ı kopyala (MongoDB'nin copyTo komutu kullanılıyor)
        $copyScript = @"
use admin
db.auth('admin', 'admin123')
use $SourceDatabase
var sourceCollection = db.getCollection('$collectionName')
var count = sourceCollection.countDocuments({})
if (count > 0) {
    var copied = 0
    sourceCollection.find({}).forEach(function(doc) {
        db.getSiblingDB('$TemplateDatabase').getCollection('$collectionName').insertOne(doc)
        copied++
    })
    print('COPIED: ' + copied + ' documents')
} else {
    print('EMPTY: Collection is empty')
}
"@
        
        if ($useDocker) {
            $copyResult = $copyScript | docker exec -i $DockerContainer mongosh $MongoConnectionString --quiet 2>&1
        } else {
            $copyResult = $copyScript | & mongosh $MongoConnectionString --quiet 2>&1
        }
        
        if ($copyResult -match "COPIED: (\d+)") {
            $docCount = [int]$matches[1]
            $totalDocuments += $docCount
            $copiedCollections += $collectionName
            Write-Host "  ✓ $docCount document kopyalandı" -ForegroundColor Green
        } elseif ($copyResult -match "EMPTY") {
            Write-Host "  ⊘ Collection boş, atlandı" -ForegroundColor Gray
        } else {
            Write-Host "  ✗ Kopyalama başarısız: $copyResult" -ForegroundColor Red
        }
        
        # Index'leri kopyala
        Write-Host "  Index'ler kopyalanıyor..." -ForegroundColor Gray
        $copyIndexesScript = @"
use admin
db.auth('admin', 'admin123')
use $SourceDatabase
var sourceIndexes = db.getCollection('$collectionName').getIndexes()
var indexCount = 0
sourceIndexes.forEach(function(index) {
    if (index.name !== '_id_') {
        try {
            var keys = index.key
            var options = {}
            if (index.unique) options.unique = true
            if (index.sparse) options.sparse = true
            if (index.background) options.background = true
            options.name = index.name
            db.getSiblingDB('$TemplateDatabase').getCollection('$collectionName').createIndex(keys, options)
            indexCount++
            print('INDEX: ' + index.name + ' copied')
        } catch (e) {
            print('INDEX_ERROR: ' + index.name + ' - ' + e.message)
        }
    }
})
print('INDEXES_DONE: ' + indexCount)
"@
        
        if ($useDocker) {
            $indexResult = $copyIndexesScript | docker exec -i $DockerContainer mongosh $MongoConnectionString --quiet 2>&1
        } else {
            $indexResult = $copyIndexesScript | & mongosh $MongoConnectionString --quiet 2>&1
        }
        $indexCount = ($indexResult | Where-Object { $_ -match "^INDEX: " }).Count
        if ($indexCount -gt 0) {
            Write-Host "  ✓ $indexCount index kopyalandı" -ForegroundColor Green
        }
    }
    
    # Özet
    Write-Host "`n=== Özet ===" -ForegroundColor Cyan
    Write-Host "Template Database: $TemplateDatabase" -ForegroundColor Yellow
    Write-Host "Kopyalanan Collection'lar: $($copiedCollections.Count)" -ForegroundColor Green
    Write-Host "Toplam Document: $totalDocuments" -ForegroundColor Green
    Write-Host ""
    Write-Host "Kopyalanan collection'lar:" -ForegroundColor Cyan
    $copiedCollections | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
    Write-Host ""
    Write-Host "✓ Template database hazırlandı!" -ForegroundColor Green
    Write-Host ""
    
} catch {
    Write-Host "✗ Hata: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}
