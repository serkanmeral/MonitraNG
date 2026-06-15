# Lokal Docker Desktop — Eski Kalite uygulaması

**Durum:** Plan taslağı · Docker bu makinede **WSL kernel eksik** nedeniyle çalışmıyor · **Native stack kuruldu** → [NATIVE_LOCAL_PLAN.md](./NATIVE_LOCAL_PLAN.md)  
**Amaç:** Eski uygulamayı **referans ortamı** olarak bu makinede çalıştırmak (ekran inceleme, alan doğrulama, migrasyon testi). Production veya MonitraNG yerine geçmez.

**Sonraki oturum:** Bu belge üzerinden adım adım kurulum yapılacak.

---

## 1. Ön koşullar

| Gereksinim | Not |
|------------|-----|
| Docker Desktop (Windows) | WSL2 backend önerilir |
| Disk | ~2 GB (kod + dump + MySQL volume) |
| Ağ | `192.168.20.30` SSH erişimi (dosya kopyası için) |
| Port | Host `8080` → web (çakışma yoksa) |

---

## 2. Kopyalanacak dosyalar (kaynak sunucu)

Kaynak: `odak@192.168.20.30:/home/odak/`

| Kaynak | Hedef (lokal proje dışı öneri) | Zorunlu |
|--------|--------------------------------|---------|
| `html/kalite/` | `./kalite-legacy/app/` | ✅ |
| `html/kalite_yedek.sql` veya `kalite_schema.sql` + data | `./kalite-legacy/db/` | ✅ |
| `html/Yonetim/MUSTERI_PO/` | `./kalite-legacy/files/MUSTERI_PO/` | PO PDF için |
| `html/file_storage/` | `./kalite-legacy/files/file_storage/` | Upload |
| `html/Urunler/` | `./kalite-legacy/files/Urunler/` | Opsiyonel |
| `html/empty.pdf` | `./kalite-legacy/files/` | PO fallback |

**Not:** Dump repoya **commit edilmemeli** (boyut + veri). `.gitignore` ile hariç tutun.

Örnek kopyalama (PowerShell + Posh-SSH veya scp):

```powershell
# Örnek hedef — MonitraNG dışında kullanıcı klasörü
$dest = "$env:USERPROFILE\kalite-legacy-docker"
New-Item -ItemType Directory -Force -Path $dest\app, $dest\db, $dest\files
# scp/rsync ile html/kalite ve sql dosyalarını çekin
```

---

## 3. Docker Compose taslağı

Dosya konumu (henüz oluşturulmadı — onay sonrası):

`docs/odak/siparis/docker/docker-compose.yml`  
veya kullanıcı home: `~/kalite-legacy-docker/docker-compose.yml`

```yaml
# TASLAK — uygulama öncesi gözden geçirilecek
services:
  mysql:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: ${MYSQL_ROOT_PASSWORD}
      MYSQL_DATABASE: kalite
      MYSQL_USER: kalite
      MYSQL_PASSWORD: ${MYSQL_PASSWORD}
    volumes:
      - ./db/init:/docker-entrypoint-initdb.d:ro
      - kalite_mysql_data:/var/lib/mysql
    ports:
      - "3307:3306"   # host 3307 — yerel MySQL çakışmasını önler
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost"]
      interval: 5s
      timeout: 5s
      retries: 10

  web:
    build:
      context: .
      dockerfile: Dockerfile
    depends_on:
      mysql:
        condition: service_healthy
    ports:
      - "8080:80"
    volumes:
      - ./app:/var/www/html
      - ./files:/var/www/uploads:ro
    environment:
      CAKEPHP_UPLOAD_ROOT: /var/www/uploads/
    env_file:
      - .env

volumes:
  kalite_mysql_data:
```

### Dockerfile taslağı (PHP 7.4 — CakePHP 3.10 uyumu)

```dockerfile
FROM php:7.4-apache
RUN docker-php-ext-install pdo pdo_mysql mysqli
RUN a2enmod rewrite
ENV APACHE_DOCUMENT_ROOT /var/www/html/webroot
RUN sed -ri -e 's!/var/www/html!${APACHE_DOCUMENT_ROOT}!g' /etc/apache2/sites-available/*.conf
RUN sed -ri -e 's!/var/www/!${APACHE_DOCUMENT_ROOT}!g' /etc/apache2/apache2.conf /etc/apache2/conf-available/*.conf
# Composer vendor zaten sunucudan kopyalandıysa ek kurulum gerekmez
WORKDIR /var/www/html
```

**Neden PHP 7.4:** Sunucuda PHP 8.3 çalışıyor; CakePHP 3.10 için 7.4 daha güvenli. Sorun olmazsa 8.1 denenebilir.

---

## 4. Uygulama konfigürasyonu

Sunucudaki `config/app.php` değerleri Docker’a uyarlanmalı:

| Ayar | Docker değeri |
|------|---------------|
| `Datasources.default.host` | `mysql` (compose servis adı) |
| `Datasources.default.database` | `kalite` |
| `Datasources.default.username` | `kalite` |
| `Datasources.default.password` | `.env` → `MYSQL_PASSWORD` |
| `CAKEPHP_UPLOAD_ROOT` | `/var/www/uploads/` |

**Yöntemler:**

1. `config/app_local.php` override (tercih — repo dışı mount)
2. Ortam değişkeni ile patch script

**Güvenlik:** SMTP şifresi eski `app.php` içinde — Docker `.env` kullanın; SMTP’yi lokal dev’de devre dışı bırakın.

### `.env` örneği (commit edilmez)

```env
MYSQL_ROOT_PASSWORD=changeme_root
MYSQL_PASSWORD=changeme_kalite
```

---

## 5. Veritabanı import

```text
db/init/
├── 01-schema.sql      # kalite_schema.sql (opsiyonel — dump zaten CREATE içeriyorsa tek dosya)
└── 02-data.sql        # kalite_yedek.sql — büyük; ilk init uzun sürebilir (~5–15 dk)
```

**Alternatif:** İlk çalıştırmada boş DB + manuel import:

```bash
docker compose exec mysql mysql -u kalite -p"$MYSQL_PASSWORD" kalite < /path/kalite_yedek.sql
```

---

## 6. Apache / CakePHP URL

| Ortam | URL |
|-------|-----|
| Sunucu | http://192.168.20.30/kalite/ |
| Docker (taslak) | http://localhost:8080/ |

`.htaccess` ve `App.base` gerekirse `config/app.php` içinde `'base' => ''` veya subdirectory ayarı — kurulumda doğrulanacak.

---

## 7. Kurulum adımları (checklist)

- [ ] Docker Desktop çalışıyor
- [ ] Dosyalar kopyalandı (`app`, `db`, `files`)
- [ ] `.env` oluşturuldu
- [ ] `docker compose build`
- [ ] `docker compose up -d`
- [ ] MySQL healthy · tablo sayısı doğrulandı (`packages` = 825)
- [ ] http://localhost:8080 açılıyor
- [ ] Login (eski `users` tablosundan — test kullanıcısı)
- [ ] Planlama → İş Paketleri listesi yükleniyor
- [ ] Örnek detay + PO PDF (dosya mount doğru mu)

---

## 8. Bilinen riskler

| Risk | Çözüm |
|------|--------|
| PHP 8.3 uyumsuzluk | Dockerfile → 7.4 |
| Dump init timeout | Manuel import · `innodb_buffer_pool_size` |
| PDF 404 | `CAKEPHP_UPLOAD_ROOT` + `polink` path eşlemesi |
| Türkçe karakter | UTF-8 dump import · `utf8_turkish_ci` |
| Vendor eksik | Sunucudan `vendor/` kopyala veya `composer install` (PHP 7.4) |
| Windows path / volume | WSL2 path veya kısa mount yolu |

---

## 9. MonitraNG repo ile ilişki

- Docker dosyaları isteğe bağlı `docs/odak/siparis/docker/` altına eklenebilir
- **Dump ve `.env` repoya girmez**
- Script: `docs/odak/siparis/scripts/sync-legacy-from-server.ps1` (ileride)

---

## 10. Alternatif: sunucuyu doğrudan kullan

Kurulum yapmadan referans için:

- http://192.168.20.30/kalite/ (ağ erişimi varsa)

Docker yalnızca **offline / yan yana karşılaştırma / migrasyon geliştirme** için gerekli.

---

## 11. Sonraki oturum gündemi

1. Lokal klasör yapısını birlikte seçmek (repo içi vs `%USERPROFILE%`)
2. Sunucudan dosya sync script
3. `docker-compose.yml` + `Dockerfile` oluşturma
4. İlk `docker compose up` ve login testi
5. UX haritası maddelerini canlı ekranda tick’leme

İlgili: [UX_UYUMLULUK_HARITASI.md](./UX_UYUMLULUK_HARITASI.md) · [DEVAM.md](./DEVAM.md)
