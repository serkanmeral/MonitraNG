# MngAdmin Backup Test Script

Bu script, MngAdmin servisinin backup endpoint'lerini test eder.

## Kullanım

```powershell
.\scripts\tests\MngAdmin\backup\test-backup.ps1 -BaseUrl "http://localhost:5080" -DomainName "meral"
```

## Parametreler

- `-BaseUrl`: MngAdmin API base URL (varsayılan: `https://localhost:5080`)
- `-Token`: JWT token (opsiyonel, otomatik alınır)
- `-DomainName`: Test edilecek domain adı (varsayılan: `meral`)
- `-KeeperBaseUrl`: MngKeeper base URL (varsayılan: `https://localhost:5001`)
- `-Username`: Kullanıcı adı (varsayılan: `serkan.meral`)
- `-Password`: Şifre (varsayılan: `Serkan123!`)

## Test Senaryoları

1. **Health Check**: Uygulama sağlık kontrolü
2. **System MongoDB Backup**: mngkeeper veritabanı yedekleme
3. **System PostgreSQL Backup**: keycloak veritabanı yedekleme
4. **Domain Backup**: Domain MongoDB veritabanı yedekleme
5. **Get System Backup List**: Sistem yedeklerini listeleme
6. **Get Domain Backup List**: Domain yedeklerini listeleme

## Notlar

- Script otomatik olarak token alır (MngKeeper'dan)
- HTTP ve HTTPS protokolleri desteklenir
- SSL sertifika doğrulaması bypass edilir (development için)
- Her backup işlemi için maksimum 60 saniye beklenir

## Sorun Giderme

### 401 Unauthorized Hatası

Eğer 401 hatası alıyorsanız:
1. Token'ın geçerli olduğundan emin olun
2. MngKeeper servisinin çalıştığından emin olun
3. Token'ı manuel olarak sağlayın: `-Token "your-token-here"`

### SSL/TLS Hataları

Script otomatik olarak SSL sertifika doğrulamasını bypass eder. Eğer hala sorun yaşıyorsanız:
- HTTP kullanın: `-BaseUrl "http://localhost:5080"`

### Connection Refused

Uygulamanın çalıştığından emin olun:
```powershell
netstat -ano | findstr ":5080"
```
