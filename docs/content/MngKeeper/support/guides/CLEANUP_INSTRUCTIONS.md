# Test Verilerini Temizleme Talimatları

## ✅ Tamamlanan: Keycloak
- **13 realm silindi** (master korundu)
- Script: `cleanup-all-test-data-v2.ps1` başarıyla çalıştı

---

## 🔧 Manuel Temizlik Gerekiyor

### 1. MongoDB Database'leri (mng_*)

#### Yöntem A: MongoDB Compass (Önerilen - En Kolay)
1. MongoDB Compass'ı açın: http://localhost:8081
2. Bağlantı: `mongodb://admin:admin123@localhost:27017`
3. Sol panelde `mng_*` ile başlayan tüm database'leri bulun
4. Her database'i **tıklayın** → **Settings** (⚙️) → **Drop Database** butonuna tıklayın
5. Onaylayın

**Not:** Eğer Drop Database butonu görünmüyorsa:
- Database'i seçin
- Üst menüden **"..."** (üç nokta) → **"Drop Database"** seçin

#### Yöntem B: mongosh Komutu
```bash
mongosh mongodb://admin:admin123@localhost:27017

use admin
db.auth('admin', 'admin123')

# Tüm mng_* database'lerini sil
db.adminCommand('listDatabases').databases.forEach(function(db) {
    if (db.name.startsWith('mng_')) {
        print('Deleting: ' + db.name)
        use(db.name)
        db.dropDatabase()
        print('Deleted: ' + db.name)
    }
})
```

**Temizlenecek Database'ler:**
- mng_testdomain*
- mng_seven
- mng_ebebek
- mng_proline
- mng_test13
- mng_test12
- mng_test10
- mng_test11

---

### 2. MinIO Bucket'ları

#### Yöntem A: MinIO Console (Önerilen)
1. MinIO Console'u açın: http://localhost:9091
2. Giriş: `admin` / `admin123`
3. Sol panelde tüm bucket'ları görün
4. Her bucket'ı seçip **"Delete Bucket"** butonuna tıklayın

#### Yöntem B: MC Client
```bash
# MC client yükleyin (eğer yoksa)
winget install MinIO.MinIO

# Alias ayarlayın
mc alias set local http://localhost:9090 admin admin123

# Bucket'ları listeleyin
mc ls local

# Tüm bucket'ları silin
mc rm --recursive --force local/*
```

**Temizlenecek Bucket'lar:**
- mng-testdomain*
- mng-seven
- mng-ebebek
- mng-proline
- mng-test13
- mng-test12
- mng-test10
- mng-test11

---

## 📊 Temizlik Durumu

| Servis | Durum | Silinen |
|--------|-------|---------|
| Keycloak | ✅ Tamamlandı | 13 realm |
| MongoDB | ⏳ Manuel | mng_* database'leri |
| MinIO | ⏳ Manuel | Tüm bucket'lar |

---

## 🚀 Hızlı Temizlik (Tümünü Birden)

Eğer **mongosh** ve **MC client** yüklüyse:

```powershell
# MongoDB
mongosh mongodb://admin:admin123@localhost:27017 --eval "use admin; db.auth('admin', 'admin123'); db.adminCommand('listDatabases').databases.forEach(function(db) { if (db.name.startsWith('mng_')) { use(db.name); db.dropDatabase(); } })"

# MinIO
mc alias set local http://localhost:9090 admin admin123
mc rm --recursive --force local/*
```

---

**Not:** Keycloak temizliği otomatik olarak tamamlandı. MongoDB ve MinIO için yukarıdaki yöntemlerden birini kullanabilirsiniz.

