# Lokal çalıştırma — Native stack (WSL/Docker olmadan)

**Durum:** ✅ Kuruldu · 13 Haziran 2026  
**Makine:** Windows Server · WSL kernel yok · Docker Desktop başlamıyor

---

## Neden Docker değil?

| Sorun | Durum |
|-------|--------|
| Docker Desktop | `WSL update required` — kernel dosyası yok |
| `wsl --update` | Windows Update politikası engelliyor |
| Uzak MySQL (`192.168.20.30:3306`) | Dış erişime kapalı |

**Çözüm:** PHP 7.4 + MySQL 8 zip — kullanıcı profili altında, admin gerektirmez.

---

## Konum

```
C:\Users\monitra\kalite-legacy-local\
├── php\              PHP 7.4.33
├── mysql\            MySQL 8.0.39 portable
├── mysql-data\       Veritabanı dosyaları
├── app\              Kalite CakePHP uygulaması
├── uploads\          PO PDF, Yonetim, Urunler...
├── my.ini
├── start-mysql.ps1
├── import-db.ps1     (tek seferlik — yapıldı)
└── start-web.ps1
```

Kaynak sync: `C:\Users\monitra\kalite-legacy-docker\` (SFTP ile sunucudan)

**Önemli:** `app/config/bootstrap.php` içinde `Configure::load('app_local', 'default');` satırı **açık** olmalı — aksi halde uygulama sunucu ayarlarıyla (3306 / kullanıcı `kalite`) bağlanmaya çalışır ve Database Error verir.

---

## Kullanım

### İlk açılış (oturum başında)

```powershell
cd $env:USERPROFILE\kalite-legacy-local
.\start-mysql.ps1
.\start-web.ps1
```

Tarayıcı: **http://localhost:8080**  
Giriş: `http://localhost:8080/users/login` — eski sistem kullanıcı adı/şifresi (DB dump'tan)

### Veritabanı yeniden import (gerekirse)

```powershell
.\start-mysql.ps1
.\import-db.ps1
```

### Durdurma

- Web: terminalde `Ctrl+C`
- MySQL: Görev Yöneticisi → `mysqld.exe` sonlandır veya `Stop-Process -Name mysqld`

---

## Kurulum scripti (sıfırdan)

```powershell
# 1) Sunucudan dosya sync (SFTP)
Import-Module Posh-SSH
& "$env:USERPROFILE\...\MonitraNG\docs\odak\siparis\scripts\sync-legacy-from-server.ps1"

# 2) Native stack
& "...\docs\odak\siparis\scripts\setup-native-stack.ps1"
```

---

## Doğrulama (13 Haziran 2026)

| Kontrol | Sonuç |
|---------|--------|
| MySQL :3307 | ✅ |
| `packages` count | 825 |
| HTTP login | 200 |
| CakePHP | 3.10.5 · PHP 7.4.33 |

---

## Docker (ileride)

WSL kernel güncellenirse: [DOCKER_LOCAL_PLAN.md](./DOCKER_LOCAL_PLAN.md) · `kalite-legacy-docker` klasörü hazır.

---

## Referans URL

| Ortam | URL |
|-------|-----|
| **Lokal (bu makine)** | http://localhost:8080 |
| Sunucu | http://192.168.20.30/kalite/ |
