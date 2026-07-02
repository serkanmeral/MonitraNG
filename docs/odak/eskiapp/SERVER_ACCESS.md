# Eski Kalite sunucusu — erişim bilgileri

**Sunucu:** `192.168.20.30`  
**Son güncelleme:** 2 Temmuz 2026

---

## SSH

| Alan | Değer |
|------|--------|
| Host | `192.168.20.30` |
| Kullanıcı | `odak` |
| Parola | `Odak333221` |
| Port | `22` (varsayılan) |

### Manuel bağlantı

```bash
ssh odak@192.168.20.30
```

Windows PowerShell:

```powershell
ssh odak@192.168.20.30
```

### Script ile (Posh-SSH)

Repo kökünden:

```powershell
. .\scripts\odak\OdakSshCommon.ps1
$sec = ConvertTo-SecureString 'Odak333221' -AsPlainText -Force
$cred = Get-OdakSshCredential -User odak -Server 192.168.20.30 -Password $sec
$session = New-SSHSession -ComputerName 192.168.20.30 -Credential $cred -AcceptKey
Invoke-SSHCommand -SessionId $session.SessionId -Command 'hostname; whoami; ls -la /home/odak/html/kalite/ | head -5'
Remove-SSHSession -SessionId $session.SessionId
```

**Not:** `192.168.20.30` MonitraNG test/prod sunucularından (`20.20` / `20.8`) farklıdır. `Initialize-OdakSshEnvironment` bu host için `.env.odak.local` kullanmaz; parolayı `-Password` veya ortam değişkeni ile verin.

---

## Uygulama yolları

| Yol | Açıklama |
|-----|----------|
| `/home/odak/html/kalite/` | CakePHP uygulama kökü |
| `.../kalite/webroot/` | Apache DocumentRoot altı |
| `http://192.168.20.30/kalite/` | Web arayüzü |

---

## Veritabanı

| Alan | Değer |
|------|--------|
| Motor | MariaDB 10.11 |
| Schema | `kalite` |
| Dinleme | `127.0.0.1:3306` (dışarı kapalı) |

### Kullanıcılar (2 Temmuz 2026 — salt okunur mod)

| Kullanıcı | Yetki | Kullanım |
|-----------|--------|----------|
| **`kalite_ro`** | `SELECT` only | CakePHP uygulaması (`app_local.php`) |
| **`kalite`** | `SELECT` only (yazma revoke) | Eski scriptler / acil okuma |
| **`root`** | Tam yetki (socket/sudo) | Bakım, dump import |

| Alan | `kalite_ro` |
|------|-------------|
| Parola | `KaliteRo333221` |
| Host | `localhost` |

Uygulama bağlantısı: `/home/odak/html/kalite/config/app_local.php`

### Salt okunur kurulum

```powershell
.\docs\odak\eskiapp\scripts\enable-legacy-kalite-db-readonly.ps1
```

Doğrulama (SSH sonrası):

```bash
# Okuma OK
mysql -u kalite_ro -pKaliteRo333221 kalite -e "SELECT COUNT(*) FROM packages;"

# Yazma engelli (hata beklenir)
mysql -u kalite_ro -pKaliteRo333221 kalite -e "INSERT INTO packages (package_no) VALUES ('x');"
```

**Not:** İş verisi yazımı DB seviyesinde engellenir. Oturum/cache dosya tabanlıdır; login ve listeleme çalışır. Kayıt ekleme/güncelleme denemeleri SQL hatası verir.

Acil yazma (dump import vb.) için `sudo mysql` ile `root` kullanın; ardından readonly scriptini tekrar çalıştırın.

---

## Ağ erişimi kontrolü

Yerel makineden:

```powershell
Test-NetConnection 192.168.20.30 -Port 22
```

---

## SSH bağlantı testi kaydı

Test sonuçları aşağıdaki bölüme oturum bazlı eklenir.

### 2 Temmuz 2026 — uygulama ayağa kaldırma

| Kontrol | Sonuç |
|---------|--------|
| Apache + MariaDB | Aktif |
| `kalite` DB verisi | 825 paket, 2769 kalem |
| Login sayfası | Temiz HTML (PHP deprecation gizlendi) |
| `CAKEPHP_UPLOAD_ROOT` | `/home/odak/html/` |

Yapılan düzeltmeler: `config/app_local.php`, `bootstrap.php` (app_local), `debug=false`, Apache `SetEnv`.

Tekrar kurulum:
```powershell
.\docs\odak\eskiapp\scripts\setup-legacy-kalite-server.ps1
```
