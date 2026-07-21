# Pazarlama — Devam Noktası

Bu dosya, pazarlama içerikleri üzerinde yapılan işleri ve bir sonraki oturumda ele alınacak konuları özetler. Yeni bir chat veya oturumda **«pazarlama devam.md'ye bak»** demek yeterli olmalıdır.

**Son güncelleme:** 15 Temmuz 2026

---

## Klasör yapısı (hızlı referans)

| Konum | İçerik |
|-------|--------|
| `brosur/monitrang-platform-brosuru.md` | Ana platform broşürü (kaynak MD) |
| `brosur/moduller/*.md` | Modül alt sayfaları (DI'da ayrı sayfalar) |
| `Docs/*.md` | Detaylı modül / platform dokümantasyonu (pazarlama kaynağı) |
| `Files/` | Logo, modül haritası, üretilmiş DOCX/PDF |
| `scripts/` | Antet seed, referans DOCX, PDF export |
| `templates/reference-brosur-mng-std.docx` | Pandoc `--reference-doc` (tipografi + antet/footer) |
| `docs/odak/document_intelligence/scripts/seed-monitrang-pazarlama-brosur.ps1` | MD + görselleri DI'ya senkron |

**DI klasör yolu:** `MonitraNG > Pazarlama > Broşür` (markdown sayfalar + `MonitraNG Platform Broşürü.docx`)

---

## Tamamlanan işler

### 1. Ana platform broşürü (MD)

Kaynak: `brosur/monitrang-platform-brosuru.md`

- Müşteri odaklı v2.0 metin; hero satırı yapay zeka vurgulu.
- **Yapay zeka** bölümü eklendi (DI AI, Monitoring anomaly, güven/kontrol).
- **Dil çeviri** satırı «Tanıdık sorunlar» tablosuna ve DI modül açıklamasına eklendi.
- **Platform modülleri** tablosu zenginleştirildi: Dinamik Form, Widget & Dashboard, Scheduler ayrı satırlar; Raporlama veri katmanları ve çalıştırma modları; OC, DI, Monitoring detayları.
- Modül bağlantı haritası: SVG → PNG + `di-fp:` (DI seed script ile).

### 2. DI uygulama içi broşür senkronu

Script: `docs/odak/document_intelligence/scripts/seed-monitrang-pazarlama-brosur.ps1`

- Markdown içeriği DI'ya yüklenir; `../Files/*.svg` referansları PNG'ye çevrilip `di-fp:` ile değiştirilir.
- Ana broşür + modül alt sayfaları DI'da görüntülenir.

**Çalıştırma (repo kökünden):**
```powershell
.\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-brosur.ps1
```

### 3. DataGateway — SVG MIME tipi

- `MngDataGateway/.../FileFieldValidator.cs`: SVG dosyaları magic byte yerine içerik (`<svg`, `<?xml`) ile tanınıyor.
- DI'ya SVG yükleme seed sırasında çalışıyor (PNG yine tercih ediliyor).

### 4. MD → DOCX → PDF pipeline

| Script | Amaç |
|--------|------|
| `scripts/ensure-brosur-reference-docx.ps1` | Pandoc referans DOCX (tablo stilleri, margin, antet/footer) |
| `scripts/export-monitrang-brosur-pdf.ps1` | MD → DOCX → DI yükleme → PDF export |

**Referans DOCX özellikleri:**
- Pandoc varsayılan `Table` stili (tablolar düzgün render).
- Antet: sol = `MonitraNG — Kurumsal Operasyon Platformu`, sağ = `www.monitrang.com`
- Footer: sol = `Sayfa N`, sağ = `www.monitrang.com`
- Kompakt tipografi: gövde 10pt, H1 13pt, tablo 9pt → **PDF 4 sayfa**
- Export sırasında MD gövdesindeki hero `**www.monitrang.com**` satırı çıkarılır (kaynak MD değişmez).

**Çalıştırma:**
```powershell
# Sadece yerel DOCX
.\docs\monitrang\pazarlama\scripts\export-monitrang-brosur-pdf.ps1 -LocalOnly

# DOCX + DI + PDF (localhost:5040, Gotenberg gerekli)
.\docs\monitrang\pazarlama\scripts\export-monitrang-brosur-pdf.ps1

# Referans DOCX yenileme
.\docs\monitrang\pazarlama\scripts\ensure-brosur-reference-docx.ps1 -Force
```

**Çıktılar:**
- `Files/MonitraNG-Platform-Brosuru.docx`
- `Files/MonitraNG-Platform-Brosuru.pdf`
- DI: `MonitraNG Platform Broşürü.docx` (her export'ta eski silinip yenisi yüklenir)

### 5. Modül alt sayfaları (kısmi)

Aşağıdakiler ana broşür revizyonuyla uyumlu olacak şekilde güncellendi; DI'ya seed ile yansır:

| Dosya | Durum |
|-------|--------|
| `01-dokuman-zekasi.md` | AI çeviri, dil varyantı eklendi |
| `03-raporlama.md` | Veri katmanları, çalıştırma modları |
| `04-monitoring.md` | Anomaly detection vurgusu |

Diğer modül sayfaları (`00`, `02`, `05`, `06`, `07`) henüz aynı derinlikte zenginleştirilmedi.

### 6. Manuel düzenleme (kullanıcı)

- DI/Collabora içinde **tablo sütun genişlikleri** elle ayarlandı.
- PDF indirildi; görünüm onaylandı.
- **Not:** Bu sütun ayarları DI'daki DOCX'te; repo'daki `Files/MonitraNG-Platform-Brosuru.docx` otomatik export ile yeniden üretilirse manuel düzenlemeler kaybolabilir. Kalıcı yapmak için: DI'dan DOCX indirip `Files/` altına koymak veya referans/pandoc tarafında sütun genişliği iyileştirmesi yapmak gerekir.

---

## Bekleyen karar

Bir sonraki büyük iş için iki seçenek tartışıldı; henüz kesinleşmedi:

1. **Modül broşür alt sayfalarını zenginleştirmek** (`brosur/moduller/` — OC, SIEM, Workflow, platform omurgası vb.)
2. **`monitrang.com` landing page** (`MngLanding/` — şu an iskelet HTML/CSS/JS var)

---

## Sonraki adımlar (önerilen sıra)

### Kısa vadeli

- [ ] **Modül alt sayfaları:** Ana broşürdeki zenginlik seviyesine `02-operasyon-merkezi`, `05-guvenlik-merkezi`, `06-workflow`, `00-platform-omurgasi`, `07-veri-yuzeyleri` getirilsin.
- [ ] **DI seed:** Modül MD güncellemelerinden sonra `seed-monitrang-pazarlama-brosur.ps1` çalıştırılsın.
- [ ] **Tablo sütunları:** Manuel DI düzenlemesi repo ile senkron mu kontrol edilsin; gerekirse DI'dan nihai DOCX `Files/` altına alınsın.

### Orta vadeli

- [ ] **Modül bazlı PDF:** Ana broşür pipeline'ı modül sayfalarına uyarlanabilir (her modül için kısa PDF broşür).
- [ ] **Landing page:** `MngLanding/index.html` — broşür metninden türetilmiş hero, modül kartları, CTA; nginx `www.monitrang.conf` ile yayın.
- [ ] **Antet / marka:** Referans DOCX'e logo (MNG-STD letterhead görselleri) eklenebilir; şu an metin tabanlı antet/footer kullanılıyor.

### Uzun vadeli

- [ ] Pazarlama içeriği ↔ DI ↔ landing tek kaynak stratejisi (MD mi, DI mı, ikisi senkron mu?)
- [ ] İngilizce broşür varyantı
- [ ] Broşür görsellerinin baskı kalitesi (300 dpi PNG, modül haritası boyutu)

---

## Önemli notlar

1. **Kaynak sırası:** Broşür metni için birincil kaynak repo'daki `.md` dosyalarıdır. DI markdown sayfaları seed ile güncellenir; printable DOCX/PDF ayrı export pipeline'ından gelir.
2. **www.monitrang.com:** MD kaynağında hero satırı duruyor (DI markdown görünümü için). PDF/DOCX export'ta gövdeden çıkarılıp antet/footer'a yazılıyor. «Daha fazla bilgi» bölümündeki web linki bilinçli bırakıldı.
3. **PNG üretimi:** Modül haritası SVG'si için PNG gerekirse `resvg-js` veya benzeri ile `Files/monitrang-modul-baglanti-haritasi.png` üretilmeli (seed/export kontrol eder).
4. **Docker:** PDF export için DI + Gotenberg çalışır olmalı (`ApplicationResources/mng_apps/docker-compose.yml`).
5. **Commit:** Bu oturumdaki script ve template değişiklikleri henüz commit edilmemiş olabilir; push öncesi `git pull` kuralına uy.

---

## Hızlı komut özeti

```powershell
# DI markdown broşür sayfalarını güncelle
.\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-brosur.ps1

# Printable DOCX + PDF (DI'ya da yükler)
.\docs\monitrang\pazarlama\scripts\export-monitrang-brosur-pdf.ps1

# Sadece yerel DOCX (DI/Gotenberg olmadan)
.\docs\monitrang\pazarlama\scripts\export-monitrang-brosur-pdf.ps1 -LocalOnly
```

---

## İlgili dosyalar (değişen / yeni)

- `docs/monitrang/pazarlama/brosur/monitrang-platform-brosuru.md`
- `docs/monitrang/pazarlama/brosur/moduller/01-dokuman-zekasi.md`
- `docs/monitrang/pazarlama/brosur/moduller/03-raporlama.md`
- `docs/monitrang/pazarlama/brosur/moduller/04-monitoring.md`
- `docs/monitrang/pazarlama/scripts/ensure-brosur-reference-docx.ps1` *(yeniden yazıldı)*
- `docs/monitrang/pazarlama/scripts/export-monitrang-brosur-pdf.ps1`
- `docs/monitrang/pazarlama/templates/reference-brosur-mng-std.docx`
- `docs/odak/document_intelligence/scripts/seed-monitrang-pazarlama-brosur.ps1`
- `MngDataGateway/.../FileFieldValidator.cs` *(SVG MIME)*
