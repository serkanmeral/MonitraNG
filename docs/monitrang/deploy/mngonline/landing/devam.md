# MonitraNG Landing — Çalışma Özeti ve Devam Noktası

**Son güncelleme:** 16 Temmuz 2026  
**Canlı URL:** https://www.monitrang.com  
**Kaynak klasör:** `docs/monitrang/deploy/mngonline/landing/`

---

## Nerede kaldık?

Landing sayfası **hazır ve production'da yayında**. Eski test sayfasının yerini aldı. Logo görünürlüğü ve tipografi dengesi son turda iyileştirildi; kullanıcı onayı alındı (“şimdilik hazır”).

**Henüz yapılmayan / isteğe bağlı işler** aşağıdaki “Sonraki adımlar” bölümünde.

---

## Tamamlanan işler

### 1. Statik landing sayfası

`Mng.Ui` Homepage yapısı ve `docs/monitrang/pazarlama/` içerikleri temel alınarak saf HTML/CSS/JS landing üretildi.

| Dosya | Açıklama |
|-------|----------|
| `index.html` | Ana sayfa (hero, modüller, SSS, iletişim, footer) |
| `css/style.css` | Tüm stiller |
| `js/main.js` | Announce bar, mobil menü, accordion, marquee, yıl |

### 2. Sayfa yapısı (sıra)

1. Announce bar  
2. Header + navigasyon  
3. Hero (`#hero`)  
4. Yapay Zeka (`#yapay-zeka`)  
5. Neden MonitraNG (`#neden`)  
6. Platform yetenekleri ve modüller (`#moduller`)  
   - Dinamik Form & Dashboard (`#modul-veri-yuzeyleri`) — platform yeteneği, TOC'da ilk  
   - Döküman Zekası (`#modul-di`)  
   - Operasyon Merkezi (`#modul-oc`)  
   - Raporlama (`#modul-reporting`)  
   - Monitoring (`#modul-monitoring`)  
   - SIEM (`#modul-siem`)  
   - Workflow (`#modul-workflow`)  
7. Marquee  
8. On-Prem (`#on-prem`)  
9. Platform haritası (`#platform`)  
10. İletişim (`#iletisim`)  
11. SSS (`#sss`)  
12. CTA banner  
13. Footer  

### 3. İçerik güncellemeleri

- **Yapay zeka & on-prem** vurguları eklendi (hero, ayrı bölümler, SSS soruları).
- **Modül kataloğu** detaylandırıldı: sorun→çözüm, günlük deneyim, fonksiyon grupları, sınır notları; sticky `module-toc` navigasyonu.
- **Dinamik Form & Dashboard** ayrı modül değil, platform yeteneği olarak katalogun üstüne alındı.
- **`docs.monitrang.com` linkleri** HTML yorumuna alındı (`<!-- docs: restore when docs.monitrang.com is ready -->`).
- **GitLab linki** footer'dan kaldırıldı.

### 4. İletişim bölümü

`#iletisim` kart tabanlı bölüm:

| Kanal | Değer |
|-------|--------|
| E-posta | `info@monitrang.com` |
| Telefon | `0532 420 67 56` (`tel:+905324206756`) |
| Web | `www.monitrang.com` |

Nav, footer ve SSS alt metni de güncellendi. Eski mavi `contact-bar` kaldırıldı.

### 5. Logo çalışması

Kaynak: `docs/monitrang/pazarlama/Files/monitrang-logo-concept-light.png`

**Sorunlar ve çözümler:**

| Sorun | Çözüm |
|-------|--------|
| PNG'de büyük gri padding → header'da logo çok küçük | İkon ve yazı ayrı kırpılıp yeniden kompoze edildi |
| İkon ile yazı yükseklik farkı fazla | İkon ≈ yazı cap-height × 1.2 |
| Yazı Bold (700) kaba görünüyordu | `Segoe UI Semibold (600)`, negatif letter-spacing |

**Kullanılan asset'ler (landing):**

| Dosya | Kullanım |
|-------|----------|
| `assets/monitrang-logo-light.png` | Header (açık zemin) |
| `assets/monitrang-logo-dark.png` | Footer + CTA (koyu zemin) |
| `assets/monitrang-logo-icon.png` | Favicon |
| `assets/mng-icon.svg` | Favicon (SVG) |
| `assets/monitrang-modul-baglanti-haritasi.svg` | Platform haritası |

**Ara dosyalar (repoda var, sayfada kullanılmıyor):**  
`monitrang-icon-tile.png`, `monitrang-icon-trimmed.png`, `mng-logo-light.svg`, `mng-logo-dark.svg`

**CSS logo yüksekliği:** `42px` (`.brand-logo`, `.footer-logo`, `.cta-banner__logo img`)

### 6. Production deploy (16 Temmuz 2026)

| Alan | Değer |
|------|--------|
| Sunucu | `ssh root@monitrang-server` |
| Web root | `/var/www/www.monitrang.com/` |
| Nginx config | `/etc/nginx/sites-available/www.monitrang.conf` (enabled) |
| Eski test sayfası yedeği | `/var/www/www.monitrang.com.bak-testpage` |
| `monitrang.com` | 301 → `https://www.monitrang.com/` |

**Deploy komutu (özet):**

```powershell
# Yerel → sunucu
scp -r docs/monitrang/deploy/mngonline/landing/index.html `
         docs/monitrang/deploy/mngonline/landing/css `
         docs/monitrang/deploy/mngonline/landing/js `
         docs/monitrang/deploy/mngonline/landing/assets `
         root@monitrang-server:/var/www/www.monitrang.com/

# Sunucuda izinler + nginx reload
ssh root@monitrang-server "chown -R www-data:www-data /var/www/www.monitrang.com && nginx -t && systemctl reload nginx"
```

**Doğrulama (deploy sonrası):**

- `https://www.monitrang.com/` → HTTP 200, ~68 KB
- Logo asset → HTTP 200
- Statik dosyalar 7 gün cache; güncelleme sonrası gerekirse Ctrl+F5

---

## İlişkili dosyalar

```
docs/monitrang/deploy/mngonline/
├── ACCESS.md              # Sunucu SSH, URL'ler, dizin yapısı
└── landing/
    ├── devam.md           # Bu dosya
    ├── index.html
    ├── css/style.css
    ├── js/main.js
    └── assets/
        ├── monitrang-logo-light.png
        ├── monitrang-logo-dark.png
        ├── monitrang-logo-icon.png
        ├── monitrang-modul-baglanti-haritasi.svg
        └── ...
```

**Nginx repo referansı:** `ApplicationResources/mng_common/nginx/conf.d/www.monitrang.conf`  
(Sunucuda `/etc/nginx/sites-available/www.monitrang.conf` olarak aktif.)

**Pazarlama kaynakları:** `docs/monitrang/pazarlama/Docs/` ve `Files/`

---

## Sonraki adımlar (isteğe bağlı)

1. **Git commit/push** — Landing ve logo değişiklikleri henüz commit edilmedi (kullanıcı talebiyle).
2. **`docs.monitrang.com` linkleri** — Site hazır olunca HTML yorumlarından geri açılacak.
3. **Logo ince ayar** — Gerekirse ikon/yazı oranı veya font (Inter web font) tekrar gözden geçirilebilir; pazarlama klasöründeki `monitrang-logo-wordmark-*.svg` ve `monitrang-logo-horizontal-*.svg` alternatif kaynak olabilir.
4. **Favicon** — Yeni ikonla güncelleme istenirse `monitrang-logo-icon.png` veya kırpılmış tile kullanılabilir.
5. **`monitrang.com` ana domain** — Zaten `www`'ye yönlendiriliyor; ayrı içerik gerekmez.
6. **Deploy otomasyonu** — Tekrarlayan deploy için `scripts/` altında küçük bir PowerShell script eklenebilir.
7. **Eski yedek temizliği** — Sunucuda `/var/www/www.monitrang.com.bak-testpage` artık gerekmezse silinebilir.

---

## Hızlı düzenleme rehberi

| Ne değişecek? | Dosya |
|---------------|--------|
| Metin / bölüm içeriği | `index.html` |
| Görünüm / logo boyutu | `css/style.css` |
| İletişim bilgileri | `index.html` → `#iletisim`, footer, SSS |
| Logo görseli | `assets/monitrang-logo-light.png`, `monitrang-logo-dark.png` |
| Yeniden yayın | `scp` + `chown` + `nginx reload` (yukarıdaki deploy özeti) |

---

## Notlar

- `app.monitrang.com` landing'den bağımsız; MngUI uygulaması (`monitrang.conf`).
- Landing statik; build adımı yok — dosyaları düzenleyip sunucuya kopyalamak yeterli.
- Logo üretiminde pazarlama PNG'si değiştirilmedi; sadece `landing/assets/` altında optimize edilmiş versiyonlar var.
