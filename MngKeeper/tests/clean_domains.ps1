# Domain Temizleme Script'i
# Tüm test domain'lerini (Keycloak realm, MongoDB database, MinIO bucket) temizler

param(
    [switch]$Force = $false,
    [switch]$SkipMongoDB = $false
)

Write-Host "`n=== DOMAIN TEMİZLEME SCRIPT'İ ===" -ForegroundColor Red
Write-Host "⚠️  DİKKAT: Bu script TÜM test domain'lerini SİLECEK!" -ForegroundColor Yellow
Write-Host ""
Write-Host "Temizlenecek Veriler:" -ForegroundColor Cyan
Write-Host "  - Keycloak: Master dışındaki TÜM realm'ler" -ForegroundColor Gray
if (-not $SkipMongoDB) {
    Write-Host "  - MongoDB: Tüm 'mng_*' database'leri" -ForegroundColor Gray
} else {
    Write-Host "  - MongoDB: Atlanacak (SkipMongoDB parametresi)" -ForegroundColor Yellow
}
Write-Host "  - MinIO: TÜM bucket'lar" -ForegroundColor Gray
Write-Host ""

# Connection settings
$mongoConnectionString = "mongodb://admin:admin123@localhost:27017"
$keycloakBaseUrl = "http://localhost:8080"
$keycloakAdminUser = "admin"
$keycloakAdminPassword = "admin123"
$minioEndpoint = "localhost:9090"
$minioAccessKey = "admin"
$minioSecretKey = "admin123"

if (-not $Force) {
    $confirm = Read-Host "Devam etmek istiyor musunuz? (yes/no)"
    if ($confirm -ne "yes") {
        Write-Host "İşlem iptal edildi." -ForegroundColor Yellow
        exit 0
    }
}

$totalDeleted = 0

# ============================================
# 1. KEYCLOAK REALM'LERİNİ TEMİZLEME
# ============================================
Write-Host "`n=== 1. KEYCLOAK REALM'LERİNİ TEMİZLEME ===" -ForegroundColor Cyan

try {
    Write-Host "Keycloak admin token alınıyor..." -ForegroundColor Yellow
    
    $body = @{
        username = $keycloakAdminUser
        password = $keycloakAdminPassword
        grant_type = "password"
        client_id = "admin-cli"
    }
    
    $response = Invoke-WebRequest -Uri "$keycloakBaseUrl/realms/master/protocol/openid-connect/token" `
        -Method POST `
        -Body $body `
        -ContentType "application/x-www-form-urlencoded" `
        -UseBasicParsing `
        -ErrorAction Stop
    
    $tokenResponse = $response.Content | ConvertFrom-Json
    $keycloakToken = $tokenResponse.access_token
    $keycloakHeaders = @{
        "Authorization" = "Bearer $keycloakToken"
        "Content-Type" = "application/json"
    }
    
    Write-Host "✓ Token alındı" -ForegroundColor Green
    
    Write-Host "Realm'ler listeleniyor..." -ForegroundColor Yellow
    $realms = Invoke-RestMethod -Uri "$keycloakBaseUrl/admin/realms" `
        -Method GET `
        -Headers $keycloakHeaders `
        -ErrorAction Stop
    
    $deletedCount = 0
    foreach ($realm in $realms) {
        $realmName = $realm.realm
        
        if ($realmName -eq "master") {
            Write-Host "  ⊘ Master realm atlandı (korunuyor)" -ForegroundColor Gray
            continue
        }
        
        try {
            Write-Host "  Realm siliniyor: $realmName" -ForegroundColor Yellow
            Invoke-RestMethod -Uri "$keycloakBaseUrl/admin/realms/$realmName" `
                -Method DELETE `
                -Headers $keycloakHeaders `
                -ErrorAction Stop
            Write-Host "    ✓ $realmName silindi" -ForegroundColor Green
            $deletedCount++
        } catch {
            Write-Host "    ✗ $realmName silinemedi: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Write-Host "✓ Keycloak: $deletedCount realm silindi" -ForegroundColor Green
    $totalDeleted += $deletedCount
    
} catch {
    Write-Host "✗ Keycloak temizliği başarısız: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Manuel temizlik için: http://localhost:8080/admin" -ForegroundColor Yellow
}

# ============================================
# 2. MONGODB DATABASE'LERİNİ TEMİZLEME
# ============================================
if ($SkipMongoDB) {
    Write-Host "`n=== 2. MONGODB DATABASE'LERİNİ TEMİZLEME ===" -ForegroundColor Cyan
    Write-Host "⊘ MongoDB cleanup atlandı (SkipMongoDB parametresi)" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "`n=== 2. MONGODB DATABASE'LERİNİ TEMİZLEME ===" -ForegroundColor Cyan

try {
    Write-Host "MongoDB'ye bağlanılıyor..." -ForegroundColor Yellow
    
    # mongosh kontrolü
    $mongoshPath = Get-Command mongosh -ErrorAction SilentlyContinue
    
    if ($mongoshPath) {
        Write-Host "mongosh bulundu, database'ler listeleniyor..." -ForegroundColor Yellow
        
        # JavaScript script'i oluştur
        $scriptContent = @"
use admin
db.auth('admin', 'admin123')
var dbs = db.adminCommand('listDatabases').databases
var deleted = []
dbs.forEach(function(db) {
    if (db.name.startsWith('mng_')) {
        print('DELETING: ' + db.name)
        use(db.name)
        db.dropDatabase()
        deleted.push(db.name)
    }
})
print('DELETED_COUNT: ' + deleted.length)
deleted.forEach(function(db) {
    print('DELETED: ' + db)
})
"@
        
        # Script'i geçici dosyaya yaz
        $tempScript = [System.IO.Path]::GetTempFileName() + ".js"
        $scriptContent | Out-File -FilePath $tempScript -Encoding UTF8
        
        Write-Host "Database'ler listeleniyor ve siliniyor..." -ForegroundColor Yellow
        
        $output = & mongosh "$mongoConnectionString" --quiet --file $tempScript 2>&1
        
        $deletedCount = 0
        $deletedDbs = @()
        
        foreach ($line in $output) {
            if ($line -match '^DELETING:\s+(.+)$') {
                $dbName = $matches[1].Trim()
                Write-Host "  Database siliniyor: $dbName" -ForegroundColor Yellow
            } elseif ($line -match '^DELETED:\s+(.+)$') {
                $dbName = $matches[1].Trim()
                Write-Host "    ✓ $dbName silindi" -ForegroundColor Green
                $deletedDbs += $dbName
                $deletedCount++
            } elseif ($line -match '^DELETED_COUNT:\s+(\d+)$') {
                $deletedCount = [int]$matches[1]
            }
        }
        
        # Geçici dosyayı sil
        Remove-Item $tempScript -ErrorAction SilentlyContinue
        
        if ($deletedCount -gt 0) {
            Write-Host "✓ MongoDB: $deletedCount database silindi" -ForegroundColor Green
            $totalDeleted += $deletedCount
        } else {
            Write-Host "  ⚠️  'mng_*' ile başlayan database bulunamadı" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ⚠️  mongosh bulunamadı" -ForegroundColor Yellow
        Write-Host "  Manuel temizlik için MongoDB Compass kullanın: http://localhost:8081" -ForegroundColor Yellow
        Write-Host "  Veya mongosh ile:" -ForegroundColor Yellow
        Write-Host "    mongosh $mongoConnectionString" -ForegroundColor Gray
        Write-Host "    use admin" -ForegroundColor Gray
        Write-Host "    db.auth('admin', 'admin123')" -ForegroundColor Gray
        Write-Host "    db.adminCommand('listDatabases').databases.forEach(function(db) {" -ForegroundColor Gray
        Write-Host "        if (db.name.startsWith('mng_')) {" -ForegroundColor Gray
        Write-Host "            use(db.name); db.dropDatabase();" -ForegroundColor Gray
        Write-Host "        }" -ForegroundColor Gray
        Write-Host "    })" -ForegroundColor Gray
    }
    
} catch {
    Write-Host "✗ MongoDB temizliği başarısız: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Manuel temizlik için: http://localhost:8081 (MongoDB Compass)" -ForegroundColor Yellow
}
}

# ============================================
# 3. MINIO BUCKET'LARINI TEMİZLEME
# ============================================
Write-Host "`n=== 3. MINIO BUCKET'LARINI TEMİZLEME ===" -ForegroundColor Cyan

try {
    Write-Host "MinIO'ya bağlanılıyor..." -ForegroundColor Yellow
    
    # MC client path
    $mcExe = "$PSScriptRoot\mc.exe"
    
    # MC client yoksa indir
    if (-not (Test-Path $mcExe)) {
        Write-Host "MC client bulunamadı, indiriliyor..." -ForegroundColor Yellow
        
        $mcUrl = "https://dl.min.io/client/mc/release/windows-amd64/mc.exe"
        
        try {
            Invoke-WebRequest -Uri $mcUrl -OutFile $mcExe -ErrorAction Stop
            Write-Host "✓ MC client indirildi: $mcExe" -ForegroundColor Green
        } catch {
            Write-Host "✗ MC client indirilemedi: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "  Manuel indirme: https://dl.min.io/client/mc/release/windows-amd64/mc.exe" -ForegroundColor Yellow
            Write-Host "  Dosyayı $mcExe konumuna kaydedin" -ForegroundColor Yellow
            throw
        }
    }
    
    Write-Host "MC client kullanılıyor: $mcExe" -ForegroundColor Green
    
    # Alias ayarla
    & $mcExe alias set local "http://$minioEndpoint" $minioAccessKey $minioSecretKey 2>&1 | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ MinIO'ya bağlanılamadı (kod: $LASTEXITCODE)" -ForegroundColor Red
        Write-Host "  Endpoint kontrol edin: http://$minioEndpoint" -ForegroundColor Yellow
        throw
    }
    
    Write-Host "✓ Alias ayarlandı" -ForegroundColor Green
    
    Write-Host "`nBucket'lar listeleniyor..." -ForegroundColor Yellow
    $bucketsOutput = & $mcExe ls local 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Bucket'lar listelenemedi" -ForegroundColor Red
        Write-Host "  Çıktı: $bucketsOutput" -ForegroundColor Gray
        throw
    }
    
    # Bucket isimlerini parse et
    $buckets = @()
    foreach ($line in $bucketsOutput) {
        # MC ls çıktısı: [2025-12-16 19:45:23 UTC]     0B mng-bucket-name/
        if ($line -match '\[.*\]\s+\S+\s+(.+?)/?$') {
            $bucketName = $matches[1].Trim().TrimEnd('/')
            if (-not [string]::IsNullOrWhiteSpace($bucketName) -and $bucketName -notmatch '^\d+') {
                $buckets += $bucketName
            }
        }
    }
    
    if ($buckets.Count -eq 0) {
        Write-Host "  ⚠️  Bucket bulunamadı" -ForegroundColor Yellow
    } else {
        Write-Host "  Bulunan bucket'lar: $($buckets.Count)" -ForegroundColor Cyan
        foreach ($bucket in $buckets) {
            Write-Host "    - $bucket" -ForegroundColor Gray
        }
        Write-Host ""
        
        $deletedCount = 0
        foreach ($bucket in $buckets) {
            try {
                Write-Host "  Bucket siliniyor: $bucket" -ForegroundColor Yellow
                
                # Önce bucket içindeki tüm objeleri sil
                & $mcExe rm --recursive --force "local/$bucket" 2>&1 | Out-Null
                
                # Sonra bucket'ı sil (rb = remove bucket)
                & $mcExe rb --force "local/$bucket" 2>&1 | Out-Null
                
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "    ✓ $bucket silindi" -ForegroundColor Green
                    $deletedCount++
                } else {
                    Write-Host "    ✗ $bucket silinemedi (kod: $LASTEXITCODE)" -ForegroundColor Red
                }
            } catch {
                Write-Host "    ✗ $bucket silinemedi: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        
        Write-Host "✓ MinIO: $deletedCount bucket silindi" -ForegroundColor Green
        $totalDeleted += $deletedCount
    }
    
} catch {
    Write-Host "✗ MinIO temizliği başarısız: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Manuel temizlik için MinIO Console: http://localhost:9091" -ForegroundColor Yellow
    Write-Host "  Veya MC client yükleyin: winget install MinIO.MinIO" -ForegroundColor Yellow
}

# ============================================
# ÖZET
# ============================================
Write-Host "`n=== TEMİZLİK TAMAMLANDI ===" -ForegroundColor Green
Write-Host "Toplam silinen kayıt: $totalDeleted" -ForegroundColor Cyan

if ($totalDeleted -eq 0) {
    Write-Host "`n⚠️  Hiçbir kayıt silinmedi. Manuel temizlik gerekebilir:" -ForegroundColor Yellow
    Write-Host "  - Keycloak: http://localhost:8080/admin" -ForegroundColor Gray
    Write-Host "  - MongoDB: http://localhost:8081 (MongoDB Compass)" -ForegroundColor Gray
    Write-Host "  - MinIO: http://localhost:9091 (MinIO Console)" -ForegroundColor Gray
}

