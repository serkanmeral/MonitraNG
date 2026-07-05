# Antet Kataloğu (D-BR1) — Production Kurulum ve Migration

**Modül:** MngDocument (Document Intelligence)  
**Son güncelleme:** 6 Temmuz 2026  
**İlişkili:** [DI_PRODUCT_ROADMAP.md](./DI_PRODUCT_ROADMAP.md) §8 · [current_status.md](../../MngDocument/current_status.md) · [PROD_OPERATIONS_AND_MIGRATION.md](./PROD_OPERATIONS_AND_MIGRATION.md)

Bu doküman, **paylaşımlı antet kataloğu** (D-BR1 Sprint A) özelliğinin production ortamına (`192.168.20.8`) kurulması için adım adım migration rehberidir.

---

## 1. Özet — Ne değişti?

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
- [ ] Git `main` — bu migration commit'i deploy edilmiş olmalı

**Collabora / WOPI (prod compose):**

- `MngDocumentSettings__Wopi__HostBaseUrl=http://mngdocument:5095`
- Collabora `aliasgroup` içinde `http://mngdocument:5095`
- WOPI dışarıdan gateway üzerinden değil; Collabora → internal `mngdocument:5095`

---

## 3. Deploy sırası (prod)

```powershell
# Repo kökünden
$Prod = "192.168.20.8"

# 1) Kaynak senkron
.\scripts\odak\sync-odak-prod.ps1 -Paths @(
  'MngDocument',
  'Mng.Ui',
  'ApplicationResources/mng_apps',
  'docs/odak/document_intelligence'
)

# 2) Backend + UI + Collabora bağımlılıkları
.\scripts\odak\deploy-odak-prod.ps1 -Services mngdocument,mngui,collabora -NoCache

# 3) Sağlık
curl -s -o /dev/null -w "%{http_code}" "http://${Prod}:5040/health"
curl -s -o /dev/null -w "%{http_code}" "http://${Prod}:5040/documents/api/v1/rendering/status"
```

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

---

## 6. Üretim akışı doğrulama

### 6.1 Checklist

- [ ] Antet listesi UI açılıyor (`/apps/document-intelligence/designer/letterheads`)
- [ ] Yeni antet: footer satır/sütun seçimi görünüyor (Odak toggle'ları yok)
- [ ] Tasarım: Collabora'da header + boş footer tablosu
- [ ] Kaydet sonrası WOPI tekrar açıldığında aynı içerik
- [ ] CoC/Activity üretimi: design header/footer merge (logo, tablo)
- [ ] `GET /letterheads/{id}/design-session` → `designFooterSource: design`

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
| `LetterheadEditorService.cs` | WOPI + skeleton |
| `LetterheadDesignSkeletonBuilder.cs` | Header + boş footer tablosu |
| `seed-letterheads-odak.ps1` | Katalog seed |
| `regenerate-letterhead-design.ps1` | Tasarım DOCX onarım |
| `Mng.Ui/.../designer/letterheads/` | Antet UI |

---

## 10. Sonraki prod adımları (D-BR1 devam)

1. Kurumsal footer metinlerini Collabora'da ODK-STD / ODK-MINIMAL tasarımlarına yazın
2. CoC + Activity üretim smoke (header + footer görünürlük)
3. Üretim dialog antet seçimi (kod hazır olunca)
4. `LegacyOdakFooterEnabled=false` kararı — tüm antetler tablo modeline geçince
