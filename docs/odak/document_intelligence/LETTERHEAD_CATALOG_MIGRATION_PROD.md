# Document Intelligence — Production Kurulum ve Migration

**Modül:** MngDocument + Mng.Ui (Document Intelligence)  
**Son güncelleme:** 7 Temmuz 2026 (prod migration uygulandı + logo fix `b11fccd4`)  
**İlişkili:** [DI_PRODUCT_ROADMAP.md](./DI_PRODUCT_ROADMAP.md) · [PROD_OPERATIONS_AND_MIGRATION.md](./PROD_OPERATIONS_AND_MIGRATION.md) · [current_status.md](../../MngDocument/current_status.md)

Bu doküman, production ortamına (`192.168.20.8`) **tek seferde uygulanacak** DI release paketinin kurulum ve migration rehberidir.

---

## 0. Bu release paketi (6 Tem 2026)

| Dilim | Bileşen | Prod'da gerekli |
|-------|---------|-----------------|
| **D-BR1 Sprint A** | Paylaşımlı antet kataloğu (`dm_letterheads`, Collabora tasarım, tablo footer) | `mngdocument` + dataset seed |
| **Faz P — Sayfa** | Markdown editör, keşif, etiket, changeNote, backlink, sürüm geçmişi, alan giriş sayfası | `mngdocument` + **`mngui`** |
| **Faz P — bilinçli hariç** | WYSIWYG / «Zengin» editör | Kaldırıldı — yalnızca Markdown editör |
| **Faz P — erteli** | Sayfa yorumu, izle/bildirim | Bu release'e dahil değil |

**Önemli:** Faz P yalnızca UI deploy ile **tam çalışmaz** — `changeNote`, backlink, `recent`/`drafts`, aramada taslak filtresi için **MngDocument** de deploy edilmelidir.

---

## 1. Özet — D-BR1 antet (ne değişti?)

| Alan | Eski | Yeni |
|------|------|------|
| Antet tanımı | Şablona gömülü `letterhead` + `footer` | **`dm_letterheads`** katalog kaydı |
| Footer modeli | Odak boolean toggle + programmatic inject | **Tablo boyutu** (`tableRows`×`tableColumns`) + Collabora'da düzenleme |
| Header | Programmatic (üretim) | Skeleton + **Collabora'da düzenlenebilir** tasarım DOCX |
| Üretim | `FooterInjector` / şablon ayarları | **Design DOCX merge** (`LetterheadDesignMerger`) |
| Legacy Odak | Tek yol | `LegacyOdakFooterEnabled` + `legacyOdakFooter` (migrasyon yedeği) |

---

## 2. Ön koşullar

- [ ] `mng_common` (MongoDB, MinIO, RabbitMQ, …) ayakta
- [ ] MngKeeper, MngDataGateway, MngGateway, **Collabora**, **Gotenberg** çalışıyor
- [ ] Prod token: `docs/odak/operationcore/scripts/load-operationcore-token-prod.ps1`
- [ ] Git `main` — logo fix commit `b11fccd4` (veya sonrası) deploy edilmiş olmalı

**Collabora / WOPI (prod compose):**

- `MngDocumentSettings__Wopi__HostBaseUrl=http://mngdocument:5095`
- Collabora `aliasgroup` içinde `http://mngdocument:5095`
- WOPI dışarıdan gateway üzerinden değil; Collabora → internal `mngdocument:5095`

---

## 3. Deploy sırası (prod)

```powershell
# Repo kökünden
$Prod = "192.168.20.8"

# 1) Kaynak senkron (D-BR1 + Faz P)
.\scripts\odak\sync-odak-prod.ps1 -Paths @(
  'MngDocument',
  'Mng.Ui',
  'ApplicationResources/mng_apps',
  'docs/odak/document_intelligence'
)

# 2) Backend + UI (+ Collabora — antet tasarımı için)
.\scripts\odak\deploy-odak-prod.ps1 -Services mngdocument,mngui,collabora -NoCache

# 3) Sağlık
curl -s -o /dev/null -w "%{http_code}" "http://${Prod}:5040/health"
curl -s -o /dev/null -w "%{http_code}" "http://${Prod}:5040/documents/api/v1/rendering/status"
```

**Yalnızca UI güncellemesi** (backend zaten güncelse):

```powershell
.\scripts\odak\sync-odak-prod.ps1 -Paths @('Mng.Ui','ApplicationResources/mng_apps')
.\scripts\odak\deploy-odak-prod.ps1 -Services mngui -NoCache
```

**UI erişim:** `http://192.168.20.8:3000` — deploy sonrası tarayıcı önbelleğini temizleyin veya hard refresh.

**appsettings (prod) — kontrol edin:**

```json
"MngDocumentSettings": {
  "LegacyOdakFooterEnabled": true,
  "Collabora": { "Enabled": true, ... },
  "Wopi": { "HostBaseUrl": "http://mngdocument:5095", ... }
}
```

`LegacyOdakFooterEnabled=true` → Eski şablon/ antet kayıtlarında `legacyOdakFooter` varsa üretim yedeği çalışır. Yeni antetler tablo modelini kullanır.

---

## 4. Dataset migration — `dm_letterheads`

### 4.1 Dataset tanımı

Kaynak: `docs/odak/document_intelligence/datasets/documentintelligence_datasets_phase1.json`  
(`dm_letterheads` bölümü — Sprint A ile eklendi)

```powershell
$env:DI_GATEWAY = "http://192.168.20.8:5040"
$token = & .\docs\odak\operationcore\scripts\load-operationcore-token-prod.ps1
$env:DI_TOKEN = $token.Trim()

# Tam DI dataset kurulumu (idempotent — mevcut dataset'leri günceller)
.\docs\odak\document_intelligence\scripts\setup-document-intelligence-datasets.ps1 `
  -BaseUrl "http://192.168.20.8:5040"
```

**Doğrulama:**

```powershell
Invoke-RestMethod -Uri "http://192.168.20.8:5040/documents/api/v1/letterheads" `
  -Headers @{ Authorization = "Bearer $env:DI_TOKEN" }
# Beklenen: 200, items: [] (seed öncesi)
```

### 4.2 Antet seed (Odak kurumsal — opsiyonel tenant içeriği)

```powershell
.\docs\odak\document_intelligence\scripts\seed-letterheads-odak.ps1 `
  -BaseUrl "http://192.168.20.8:5040"
```

Oluşturur/günceller:

| Kod | Açıklama |
|-----|----------|
| `ODK-STD` | Varsayılan antet (logo + header alanları + 2×2 footer tablosu) |
| `ODK-MINIMAL` | Logo kapalı |

Şablonlar `COC-STANDARD` → `defaultLetterheadId` bağlantısı yapılır. **Published şablonlar** güncellenemezse seed bu adımı atlar (normal).

### 4.3 Tasarım DOCX ilk oluşturma

Seed sonrası her antet için **bir kez** tasarım oturumu açılmalı (skeleton yazılır):

```powershell
# Örnek: ODK-STD
$list = Invoke-RestMethod -Uri "http://192.168.20.8:5040/documents/api/v1/letterheads" `
  -Headers @{ Authorization = "Bearer $env:DI_TOKEN" }
$id = ($list.items | Where-Object code -eq 'ODK-STD').id
Invoke-RestMethod -Uri "http://192.168.20.8:5040/documents/api/v1/letterheads/$id/design-session" `
  -Headers @{ Authorization = "Bearer $env:DI_TOKEN" }
```

UI: **Belge Tasarımcısı → Antetler → Tasarım** ile Collabora açıp kurumsal footer içeriğini doldurun (Odak metinleri).

---

## 5. Mevcut şablon migrasyonu

### 5.1 Şablonda gömülü antet → katalog varsayılanı

Şablon `modelJson` içindeki `defaultLetterheadId` alanı artık birincil kaynak. Gömülü `letterhead` / `footer` yalnızca **legacy fallback**.

**COC-STANDARD / LINE-ACTIVITY-STD** için seed JSON'ları `defaultLetterheadId` kullanacak şekilde güncellenmiş olmalı. Prod'da manuel kontrol:

```powershell
$tpl = Invoke-RestMethod -Uri "http://192.168.20.8:5040/documents/api/v1/templates?code=COC-STANDARD" `
  -Headers @{ Authorization = "Bearer $env:DI_TOKEN" }
# defaultLetterheadId dolu mu?
```

Eksikse API PUT veya mevcut patch script'leri ile `defaultLetterheadId` atayın.

### 5.2 Eski footer boolean ayarları

Antet `settingsJson` içinde eski format:

```json
"footer": { "enabled": true, "showFormRevision": true, ... }
```

Serializer otomatik map eder:

- `footer` → `{ enabled, tableRows: 2, tableColumns: 2 }`
- `legacyOdakFooter` → eski boolean alanları

**Yeni kayıtlar** generic varsayılan kullanır: footer kapalı veya 1×1 tablo, docNo `{yyyy}-{0:D4}`.

### 5.3 Bozuk / eski tasarım DOCX onarımı

Footer programmatic enjeksiyondan kalan veya header'sız dosyalar için:

```powershell
.\docs\odak\document_intelligence\scripts\regenerate-letterhead-design.ps1 `
  -BaseUrl "http://192.168.20.8:5040" `
  -Code "ODK_TST_1"
```

Bu script tasarım dosyasını siler ve `design-session` ile skeleton yeniden oluşturur. **Kullanıcı Collabora içeriği silinir** — prod'da dikkatli kullanın.

### 5.4 Header logo / embedded media (7 Tem 2026)

Antet tasarım DOCX'inde logo çoğu zaman `header2` (default) + `word/media/image1.jpeg` içindedir. Şablona header merge edildiğinde yalnızca XML kopyalanıp medya atlanırsa Collabora «**LetterheadLogo**» gösterir; WOPI «Başlatılıyor...» takılabilir.

**Backend (commit `b11fccd4`):**

| Bileşen | Davranış |
|---------|----------|
| `LetterheadDesignMerger` | `EnsureHeaderWithMediaFromDesign`, `HasBrokenHeaderImages`, `RepairHeaderMediaFromDesign` |
| `TemplateEditorService` | WOPI GetFile — antet design indirme **session token** ile (Bearer yok) |
| `TemplateLetterheadApplier` | Branding merge: header + tüm design medya |
| `DocumentGenerationService` | Şablon yükleme + branding sonrası medya onarımı |

**Doğrulama:** WOPI GetFile bytes > 25 KB ve `word/media/image1.jpeg` var; yeni üretilmiş CoC/Activity belgesinde logo görünür.

**Not:** Migration öncesi üretilmiş belgeler otomatik düzelmez — yeniden generate gerekir.

---

## 6. Üretim akışı doğrulama

### 6.1 Checklist

- [x] Antet listesi UI açılıyor (`/apps/document-intelligence/designer/letterheads`)
- [x] Tasarım: Collabora'da header + footer tablosu (ODK-STD / ODK_KRMSL_ANT)
- [x] Kaydet sonrası WOPI tekrar açıldığında içerik yükleniyor (401 / takılma giderildi)
- [x] CoC/Activity üretimi: design header merge + logo (`b11fccd4` sonrası yeniden generate)
- [ ] Yeni antet: footer satır/sütun seçimi (Odak toggle'ları yok) — tam UI smoke isteğe bağlı
- [ ] `GET /letterheads/{id}/design-session` → `designFooterSource: design` — isteğe bağlı smoke

### 6.2 Smoke (PowerShell)

```powershell
$token = (Get-Content "$env:TEMP\operationcore_dg_token_prod.txt" -Raw).Trim()
$h = @{ Authorization = "Bearer $token" }
$lh = (Invoke-RestMethod -Uri "http://192.168.20.8:5040/documents/api/v1/letterheads" -Headers $h).items[0]
$session = Invoke-RestMethod -Uri "http://192.168.20.8:5040/documents/api/v1/letterheads/$($lh.id)/design-session" -Headers $h
Write-Host "source=$($session.designFooterSource)"

# WOPI içerik (sunucudan veya internal curl)
# header1.xml + footer1.xml mevcut olmalı
```

### 6.3 Bilinen sınırlamalar (Sprint A sonrası)

| Konu | Durum |
|------|--------|
| Üretim dialogunda antet seçimi | Henüz yok — şablon `defaultLetterheadId` kullanılır |
| Footer tablo boyutu değişince mevcut design regen | Manuel `regenerate-letterhead-design.ps1` |
| Kapak sayfası | D-BR2 — planlı |
| Context catalog dataset | Sprint B — planlı |

---

## 7. Geri alma (rollback)

1. `mngdocument` + `mngui` önceki image tag / commit deploy
2. `dm_letterheads` dataset silmek **zorunlu değil** — eski şablon gömülü antet fallback çalışır
3. `LegacyOdakFooterEnabled=true` bırakın — eski üretim yolu korunur
4. Şablon `defaultLetterheadId` boşaltılırsa legacy `model.letterhead` / `model.footer` devreye girer

---

## 8. Prod vs test farkları

| | Test (`192.168.20.20`) | Prod (`192.168.20.8`) |
|--|------------------------|------------------------|
| Sync script | `sync-odak-source.ps1` | `sync-odak-prod.ps1` |
| Deploy script | `deploy-odak-apps.ps1` | `deploy-odak-prod.ps1` |
| Token | `load-operationcore-token.ps1` | `load-operationcore-token-prod.ps1` |
| MongoDB | Ayrı volume | Ayrı volume |

Test'te doğrulanmış adımlar prod'da aynı script parametreleriyle tekrarlanır.

---

## 9. İlgili dosyalar

| Dosya | Amaç |
|-------|------|
| `MngDocument/.../LetterheadsController.cs` | REST API |
| `LetterheadDesignMerger.cs` | Design header/footer merge + medya onarımı |
| `TemplateEditorService.cs` | Şablon WOPI + header medya onarımı |
| `DocumentGenerationService.cs` | Üretim yolunda medya onarımı |
| `LetterheadDesignSkeletonBuilder.cs` | Header + boş footer tablosu |
| `seed-letterheads-odak.ps1` | Katalog seed |
| `regenerate-letterhead-design.ps1` | Tasarım DOCX onarım |
| `Mng.Ui/.../designer/letterheads/` | Antet UI |

---

## 10. Sonraki prod adımları (D-BR1 devam)

1. ~~CoC + Activity üretim smoke (header + logo)~~ — ✅ (7 Tem 2026, `b11fccd4` deploy)
2. Kurumsal footer metinlerini Collabora'da ODK-STD / ODK-MINIMAL tasarımlarına yazın (devam ediyor)
3. ~~Üretim dialog antet seçimi~~ — ➖ **iptal** (antet Belge Tasarımcısı'nda `defaultLetterheadId`)
4. `LegacyOdakFooterEnabled=false` kararı — tüm antetler tablo modeline geçince
5. CoC/Activity uçtan uca smoke — `odak_siparis_kalemleri` prod verisi varsa
6. ~~Faz P Sayfa~~ — ✅ (commit `1441ac90`); prod checklist §11.4

---

## 11. Faz P — Sayfa (UI + API)

### 11.1 Backend (MngDocument)

| Özellik | API / davranış |
|---------|----------------|
| Kayıt notu | `PUT .../markdown/{id}` → `changeNote` (opsiyonel) |
| Backlink | `GET .../markdown/{id}/backlinks` |
| Son sayfalar | `GET .../recent?limit=` (yalnızca **yayınlanmış**) |
| Taslaklarım | `GET .../drafts?limit=` |
| Arama | `GET .../search?q=` — markdown **taslakları hariç** |

Dataset: `dm_resource_versions` alanı `changeNote` — `setup-document-intelligence-datasets.ps1` idempotent günceller.

**Doğrulama (prod token):**

```powershell
$token = (Get-Content "$env:TEMP\operationcore_dg_token_prod.txt" -Raw).Trim()
$h = @{ Authorization = "Bearer $token" }
Invoke-RestMethod "http://192.168.20.8:5040/documents/api/v1/resources/recent?limit=5" -Headers $h
Invoke-RestMethod "http://192.168.20.8:5040/documents/api/v1/resources/drafts?limit=5" -Headers $h
```

### 11.2 UI (Mng.Ui)

| Özellik | Konum |
|---------|--------|
| Keşif ana ekranı | `/apps/document-intelligence` — son sayfalar, taslaklar, alan kısayolları, arama |
| Alan giriş sayfası | **Sayfalar** / **Dökümanlar** klasöründe `Giriş` banner |
| Markdown editör | Split önizleme, şablon, tablo, görsel, iç link picker |
| changeNote | Kaydet / yayınla diyaloğu |
| Backlink paneli | Sayfa görünümü — «Bu sayfaya link verenler» |
| Sürüm geçmişi | Ana ekran + deep link (`/r/{id}`) |
| Etiketler | Sayfa meta + klasör etiket filtresi |
| Terminoloji | Kullanıcıya «Sayfa»; markdown ikonu kaldırıldı |
| **Kaldırıldı** | «Zengin» / WYSIWYG editör modu |

### 11.3 İçerik seed (opsiyonel — yeni tenant / eksik kök yapı)

Kök alan klasörleri ve giriş sayfaları yoksa:

```powershell
$env:DI_GATEWAY = "http://192.168.20.8:5040"
$env:DI_TOKEN = (& .\docs\odak\operationcore\scripts\load-operationcore-token-prod.ps1).Trim()
.\docs\odak\document_intelligence\scripts\seed-resource-root-folders.ps1
```

Oluşturur: `Sayfalar/`, `Dökümanlar/` + `sayfalar-giris.md`, `dokumanlar-giris.md`.

Mevcut prod ağacında zaten varsa script atlanır (idempotent).

### 11.4 Faz P prod checklist

- [ ] `mngdocument` deploy — recent / drafts / backlinks / changeNote API
- [ ] `mngui` deploy — keşif, editör, backlink, sürüm geçmişi UI
- [ ] Dokümanlar → Keşfet → arama (taslak gelmemeli)
- [ ] Sayfa kaydet → «Ne değişti?» → geçmişte görünür
- [ ] Sayfalar klasörü → «Giriş» banner → giriş sayfası açılır
- [ ] Editörde «Markdown | Zengin» toggle **yok** (beklenen)

### 11.5 Geri alma (Faz P)

1. Önceki `mngui` + `mngdocument` image deploy
2. Dataset geri alma gerekmez; `changeNote` alanı boş kalabilir
3. Eski UI'da backlink/keşif yok — işlev kaybı, veri kaybı yok
