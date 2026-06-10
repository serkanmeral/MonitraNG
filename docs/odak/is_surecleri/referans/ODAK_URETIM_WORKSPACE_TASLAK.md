# Odak Üretim — Workspace Taslağı (v0.1)

**Durum:** v0.1 — Odak test sunucusunda kuruldu (10 Haziran 2026)  
**Son güncelleme:** 10 Haziran 2026  
**Ortam:** Odak test · `http://192.168.20.20:5040`  
**Platform:** Operation Core (MngOperations + MngDataGateway)

**İlgili kaynaklar:**

| Kaynak | Rol |
|--------|-----|
| [P16 Depolama ve Sevkiyat](../uretim/P16%20DEPOLAMA%20VE%20SEVKİYAT%20PROSEDÜRÜ%20Rev08.pdf) | Depo / sevkiyat alan ve roller |
| [T37 Ölçü Aleti Doğrulama](../uretim/T37%20ÖLÇÜ%20ALETİ%20DOĞRULAMA%20TALİMATI%20Rev04.pdf) | Kalite altyapısı referansı |
| [AS9100 müşteri özeti](../../compliance/AS9100_MUSTERI_OZET.md) §5 | NCR/CAPA alan dili |
| [IT Help Desk seed kalıbı](../../operationcore/scripts/seed-operation-core-helpdesk-reference.ps1) | OC kurulum script referansı |

---

## 1. Amaç

**Odak Kompozit Teknolojileri** için siparişten sevkiyata kadar uçtan uca **tek workspace** üzerinde çalışan, kullanımı kolay bir operasyon modeli sunmak.

Bu belge **taslaktır**. Müşteri geri bildirimi sonrası alanlar, durumlar ve zorunluluklar güncellenecektir. Amaç: “Böyle bir sistem kurulabilir” demosu ve tartışma zeminidir.

**Kapsam (v0.1):**

- Tek workspace: **Odak Üretim**
- Ana kayıt: **Üretim emri** (`ODF-00001` …)
- Kalite sapması: **NCR** + **CAPA** (ana emre bağlı)
- Master veri: müşteri, ürün grubu, ürün (form lookup)
- Odak test üzerinde seed + demo kayıtları (sonraki adım)

**Bilinçli olarak v0.1 dışı:** ERP entegrasyonu, FAI (AS9102), tedarikçi onayı, cihaz doğrulama modülü, e-posta bildirimleri.

---

## 2. Workspace tanımı

| Alan | Değer |
|------|--------|
| **Ad** | Odak Üretim |
| **Açıklama** | Siparişten sevkiyata üretim operasyonları — kompozit havacılık tedarik |
| `workspaceType` | `operational` |
| `workItemKeyPrefix` | `ODF` |
| `workItemKeyFormat` | `{prefix}-{seq:D4}` |
| `workItemSequenceStart` | 1 |
| Örnek anahtar | `ODF-0001`, `NCR-0001`, `CAPA-0001` |

**NCR / CAPA prefix’leri:** Ayrı iş tipleri kendi prefix’ini taşır (`NCR`, `CAPA`). Workspace ana prefix’i yalnızca **Üretim emri** tipi için `ODF` kullanılır (OC tip bazlı key üretimi seed’de tanımlanır).

**Yetki (taslak — gruplar Odak Keycloak ile kesinleşecek):**

| Rol | Önerilen grup | Görüntüle | Düzenle |
|-----|---------------|-----------|---------|
| Herkes (operasyon) | `users` | Evet | Emir oluştur |
| Üretim | `uretim` | Evet | Üretim adımları |
| Kalite | `kalite` | Evet | Kalite + NCR/CAPA |
| Depo / sevkiyat | `depo` | Evet | Depo + sevkiyat adımları |
| Yönetim | `admins` | Tam | Tam |

---

## 3. Master veri dataset’leri

Formlarda **lookup (relation)** ile kullanılacak. Kategori: **BusinessDatasets** (tedarikçiler POC ile aynı kalıp).

### 3.1 `odak_musteriler`

| Alan | Tip | Zorunlu | Not |
|------|-----|---------|-----|
| `kod` | text | Evet | Unique · örn. `MUS-001` |
| `unvan` | text | Evet | Müşteri adı |
| `sektor` | select | Hayır | Havacılık · Savunma · Diğer |
| `ulke` | text | Hayır | |
| `aktif` | bool | Evet | Default `true` |
| `notlar` | text | Hayır | |

### 3.2 `odak_urun_gruplari`

| Alan | Tip | Zorunlu | Not |
|------|-----|---------|-----|
| `kod` | text | Evet | Unique · örn. `UG-KOM` |
| `ad` | text | Evet | örn. Kompozit yapısal parça |
| `aciklama` | text | Hayır | |
| `aktif` | bool | Evet | |

**Önerilen demo grupları:**

| Kod | Ad |
|-----|-----|
| `UG-KOM` | Kompozit yapısal parça |
| `UG-KAL` | Kalıp / fikstür / mastar |
| `UG-YM` | Yarı mamul |
| `UG-MNT` | Montaj seti |

### 3.3 `odak_urunler`

| Alan | Tip | Zorunlu | Not |
|------|-----|---------|-----|
| `partNumber` | text | Evet | Unique · parça numarası |
| `ad` | text | Evet | Parça adı |
| `urunGrubuId` | relation → `odak_urun_gruplari` | Evet | |
| `musteriId` | relation → `odak_musteriler` | Hayır | Müşteriye özel parça ise |
| `revizyon` | text | Hayır | Drawing rev |
| `birim` | select | Hayır | Adet · Takım · kg |
| `aktif` | bool | Evet | |

**Form davranışı:** Ürün grubu seçilince ürün listesi `dependsOn` ile filtrelenir (OC demo tedarikçi lookup kalıbı).

---

## 4. İş tipleri (`op_work_item_types`)

| Ad | `category` | Prefix | Varsayılan flow |
|----|------------|--------|-----------------|
| **Üretim emri** | `operational` | `ODF` | Odak Üretim — Ana Akış |
| **Uygunsuzluk (NCR)** | `incident` | `NCR` | Odak Üretim — NCR |
| **Düzeltici faaliyet (CAPA)** | `problem` | `CAPA` | Odak Üretim — CAPA |

Workspace `enabledTypeIds`: üç tip de aktif.

---

## 5. Öncelikler (`op_priorities`)

| Ad | `level` | Renk | Kullanım |
|----|---------|------|----------|
| Acil | 1 | `error` | Müşteri durdurma, kritik uygunsuzluk |
| Yüksek | 2 | `warning` | Yakın termin |
| Normal | 3 | `info` | **Varsayılan** |
| Düşük | 4 | `secondary` | Esnek plan |

---

## 6. Durumlar (`op_states`)

Global katalog; workspace `enabledStateIds` ile seçilir. Görünen adlarda **Odak Üretim -** prefix’i (IT Destek kalıbı).

### 6.1 Ana emir durumları

| Görünen ad | `category` | Bayraklar |
|------------|------------|-----------|
| Odak Üretim - Yeni | `open` | `isInitial`, `isStart` |
| Odak Üretim - Planlandı | `open` | — |
| Odak Üretim - Üretimde | `in_progress` | — |
| Odak Üretim - Kalite kontrol | `in_progress` | — |
| Odak Üretim - Kalite bekliyor | `on_hold` | — |
| Odak Üretim - Depoda | `in_progress` | — |
| Odak Üretim - Sevkiyat hazır | `in_progress` | — |
| Odak Üretim - Sevk edildi | `closed` | — |
| Odak Üretim - Kapandı | `closed` | `isClosed`, `isTerminal` |

### 6.2 NCR durumları

| Görünen ad | `category` | Bayraklar |
|------------|------------|-----------|
| Odak Üretim - NCR Açık | `open` | `isInitial` |
| Odak Üretim - Kontrol altında | `in_progress` | — |
| Odak Üretim - Değerlendirme | `in_progress` | — |
| Odak Üretim - Karar verildi | `closed` | `allowReopen` |
| Odak Üretim - NCR Kapandı | `closed` | `isClosed`, `isTerminal` |

### 6.3 CAPA durumları

| Görünen ad | `category` | Bayraklar |
|------------|------------|-----------|
| Odak Üretim - CAPA Açık | `open` | `isInitial` |
| Odak Üretim - Kök neden | `in_progress` | — |
| Odak Üretim - Aksiyon planı | `in_progress` | — |
| Odak Üretim - Uygulama | `in_progress` | — |
| Odak Üretim - Doğrulama | `in_progress` | — |
| Odak Üretim - CAPA Kapandı | `closed` | `isClosed`, `isTerminal` |

---

## 7. Durum akışları (`op_state_flows`)

### 7.1 Ana akış — `Odak Üretim — Ana Akış`

`initialStateId` = **Odak Üretim - Yeni**

| `transitionKey` | Geçiş | Etiket (UI) | Not |
|-----------------|--------|-------------|-----|
| `plan` | Yeni → Planlandı | Planla | |
| `start_production` | Planlandı → Üretimde | Üretime al | |
| `skip_to_production` | Yeni → Üretimde | Doğrudan üretime al | Acil / basit emirler |
| `send_to_quality` | Üretimde → Kalite kontrol | Kaliteye gönder | |
| `hold_quality` | Kalite kontrol → Kalite bekliyor | Uygunsuzluk — bekle | NCR açılmalı |
| `resume_from_hold` | Kalite bekliyor → Kalite kontrol | Tekrar kaliteye al | NCR kapatıldıktan |
| `approve_quality` | Kalite kontrol → Depoda | Kalite onayı | `qualityResult` = Uygun veya Şartlı |
| `move_to_ship_prep` | Depoda → Sevkiyat hazır | Sevkiyata hazırla | |
| `ship` | Sevkiyat hazır → Sevk edildi | Sevk et | |
| `close_order` | Sevk edildi → Kapandı | Kapat | |
| `cancel` | Yeni / Planlandı → Kapandı | İptal et | Erken kapanış (izin: yönetim) |

**Kanban kolonları (üretim panosu):** Planlandı · Üretimde · Kalite kontrol · Kalite bekliyor · Depoda · Sevkiyat hazır

### 7.2 NCR akışı — `Odak Üretim — NCR`

| `transitionKey` | Geçiş | Etiket |
|-----------------|--------|--------|
| `contain` | NCR Açık → Kontrol altında | Kontrol altına al |
| `review` | Kontrol altında → Değerlendirme | Değerlendir |
| `decide` | Değerlendirme → Karar verildi | Karar ver |
| `close_ncr` | Karar verildi → NCR Kapandı | Kapat |
| `reopen_ncr` | Karar verildi → Değerlendirme | Yeniden aç |

### 7.3 CAPA akışı — `Odak Üretim — CAPA`

| `transitionKey` | Geçiş | Etiket |
|-----------------|--------|--------|
| `analyze_root` | CAPA Açık → Kök neden | Kök neden analizi |
| `plan_action` | Kök neden → Aksiyon planı | Aksiyon planla |
| `implement` | Aksiyon planı → Uygulama | Uygula |
| `verify` | Uygulama → Doğrulama | Doğrula |
| `close_capa` | Doğrulama → CAPA Kapandı | Kapat |

---

## 8. Pool alanları (`op_fields`)

Değerler `op_work_items.extraFields` içinde. Workspace `enabledFieldIds` ile aktif edilir.

### 8.1 Üretim emri — ortak

| `key` | Etiket | `fieldType` | Not |
|-------|--------|---------------|-----|
| `customerId` | Müşteri | relation → `odak_musteriler` | |
| `productGroupId` | Ürün grubu | relation → `odak_urun_gruplari` | |
| `productId` | Ürün / parça | relation → `odak_urunler` | dependsOn: productGroupId |
| `customerOrderRef` | Müşteri sipariş no | text | |
| `quantity` | Miktar | number | |
| `plannedDate` | Planlanan bitiş | datetime | |
| `workCenter` | İş istasyonu / hat | text | örn. Otomatik layup, Pres |
| `lotSerial` | Lot / seri | text | AS9100 izlenebilirlik |
| `qualityResult` | Kalite sonucu | select | Uygun · Uygunsuz · Şartlı |
| `qualityNotes` | Kalite notu | text | |
| `storageLocation` | Depo lokasyonu | text | P16 |
| `packagingOk` | Paketleme OK | bool | P16 — KKS kontrolü |
| `waybillNo` | İrsaliye no | text | P16 |
| `shipmentNotes` | Sevkiyat notu | text | |

### 8.2 NCR — ek pool alanları

| `key` | Etiket | `fieldType` |
|-------|--------|-------------|
| `ncrSource` | Tespit aşaması | select: Girdi · Proses · Final · Müşteri iadesi · Denetim |
| `defectDescription` | Uygunsuzluk tanımı | text |
| `affectedQty` | Etkilenen adet | number |
| `containmentAction` | Acil kontrol (containment) | text |
| `disposition` | Disposition | select: Kullan · Yeniden işle · Tamir · Hurda · İade |
| `dispositionReason` | Disposition gerekçesi | text |
| `parentOrderId` | Bağlı üretim emri | relation → `op_work_items` | UI’da parent link ile de |

### 8.3 CAPA — ek pool alanları

| `key` | Etiket | `fieldType` |
|-------|--------|-------------|
| `rootCause` | Kök neden analizi | text |
| `correctiveAction` | Düzeltici faaliyet | text |
| `preventiveAction` | Önleyici faaliyet | text |
| `effectivenessCheck` | Etkinlik doğrulaması | text |
| `targetDate` | Hedef tarih | datetime |

---

## 9. Zorunlu alanlar (taslak — müşteri revize edebilir)

**İlke:** Oluştururken **az**, geçişte **aşama gerektirdiğinde** zorunlu. Kullanıcıyı boğmamak için minimum set.

### 9.1 Üretim emri — oluşturma

| Alan | Zorunlu | Not |
|------|---------|-----|
| `title` | Evet | Kısa özet |
| `typeId` | Evet | Default: Üretim emri |
| `priorityId` | Evet | Default: Normal |
| `customerId` | Hayır* | *Demo’da doldurulması önerilir |
| `productId` | Hayır | `plan` geçişinde zorunlu yapılır |
| `quantity` | Hayır | `plan` geçişinde zorunlu |

### 9.2 Geçiş bazlı zorunluluklar (`op_rules` veya transition `requiredFields`)

| Geçiş | Zorunlu alanlar |
|--------|-----------------|
| `plan` | `productId`, `quantity`, `plannedDate` |
| `start_production` | `workCenter` |
| `send_to_quality` | `lotSerial` |
| `approve_quality` | `qualityResult`, `qualityNotes` (Uygunsuz ise geçiş **engellenir** — `hold_quality` kullanılır) |
| `hold_quality` | `qualityResult` = Uygunsuz, kısa `qualityNotes` |
| `move_to_ship_prep` | `storageLocation` |
| `ship` | `waybillNo`, `packagingOk` = true |
| `close_ncr` | `disposition`, `dispositionReason` |
| `close_capa` | `effectivenessCheck` |

### 9.3 NCR oluşturma

| Alan | Zorunlu |
|------|---------|
| `title` | Evet |
| `defectDescription` | Evet |
| `ncrSource` | Evet |
| Parent link (üretim emri) | Evet (operasyonel) |

NCR açıldığında ana emir **`hold_quality`** durumuna alınır (`op_rules`: `WorkItemCreated` → parent emir güncelle — Faz 1.5 veya manuel süreç).

---

## 10. Formlar ve board’lar (özet)

### 10.1 Formlar

| Form | Tip | Bölümler |
|------|-----|----------|
| **ODF — Yeni emir** | Üretim emri create | Genel (başlık, müşteri, sipariş no) · Ürün (grup, parça, miktar) · Plan (termin, öncelik) |
| **ODF — Emir düzenle** | Üretim emri edit | Tüm pool alanları (aşamaya göre readonly politikaları) |
| **NCR — Kayıt** | NCR create | Uygunsuzluk · Etki · Containment |
| **CAPA — Kayıt** | CAPA create | Problem · Kök neden · Aksiyon |

### 10.2 Board’lar

| Board | `viewType` | Filtre / kolon |
|-------|------------|----------------|
| **Üretim panosu** | kanban | Ana akış — Planlandı … Sevkiyat hazır |
| **Kalite kuyruğu** | list | Tip = NCR veya durum = Kalite kontrol / Kalite bekliyor |
| **Depo & sevkiyat** | list | Durum = Depoda, Sevkiyat hazır |
| **Tüm açık emirler** | list | Tip = Üretim emri, terminal olmayan |

### 10.3 Dashboard (demo)

| Widget | Sorgu özeti |
|--------|-------------|
| Açık üretim emirleri | Workspace + açık |
| Kalite bekleyen | Durum = Kalite bekliyor |
| Bu hafta sevk | Sevk edildi + son 7 gün |
| Açık NCR | Tip = NCR, kapalı değil |

---

## 11. Kayıt ilişkileri

```text
Üretim emri (ODF-0001)
  ├─ parent-child → NCR-0001
  │     └─ parent-child → CAPA-0001
  └─ (ileride) op_links: relates_to diğer emirler
```

- NCR, bağlı `ODF` emrinin `__dataId` ile `parentItemId` veya `op_links` üzerinden bağlanır.
- CAPA, NCR’ye parent-child.
- Ana emir **Kalite bekliyor** iken NCR açık kalabilir; NCR kapanınca `resume_from_quality` ile devam.

---

## 12. Demo senaryosu (sunum)

| # | Kayıt | Hikâye |
|---|-------|--------|
| 1 | `ODF-0001` | Müşteri X · Kompozit panel · 10 adet · Planlandı → Üretimde → Kalite |
| 2 | `NCR-0001` | Layup hatası · 2 adet etkilendi · parent ODF-0001 · emir Kalite bekliyor |
| 3 | `CAPA-0001` | Proses parametresi düzeltmesi · parent NCR-0001 |
| 4 | `ODF-0001` devam | NCR kapat → Kalite onayı → Depoda → Sevkiyat hazır → Sevk → Kapandı |
| 5 | `ODF-0002` | Sorunsuz ikinci emir (kısa yol demo) |

Master seed: 2 müşteri, 4 ürün grubu, 6–8 ürün kaydı.

---

## 13. Uygulama sırası (Odak test)

| Sıra | İş | Çıktı |
|------|-----|-------|
| 1 | Master dataset JSON + setup script | `odak_musteriler`, `odak_urun_gruplari`, `odak_urunler` |
| 2 | OC metadata seed script | `seed-operation-core-odak-uretim.ps1` |
| 3 | Demo master + iş kayıtları | `odak-uretim-seed.json` |
| 4 | Smoke: MO transition + board | Test kullanıcısı ile |
| 5 | Müşteri sunumu | Bu belge + canlı demo |

**Script konumu (plan):** `docs/odak/is_surecleri/seed/`

---

## 14. Müşteriye sorulacak sorular (geri bildirim listesi)

1. Durum adları ve adım sayısı yeterli mi? Fazla / eksik adım var mı?
2. Zorunlu alanlar operasyonu yavaşlatır mı? Hangi alanlar kaldırılsın / eklensin?
3. Müşteri / ürün master verisi ERP’den mi gelecek, OC’de mi tutulacak?
4. NCR disposition seçenekleri (Kullan, Hurda, …) sizin prosedürünüzle uyumlu mu?
5. Rol grupları (`uretim`, `kalite`, `depo`) Keycloak’ta nasıl adlandırılmalı?
6. İrsaliye / stok etiketi (F146, F67) alanları ERP’de kalacak mı, OC’ye taşınacak mı?
7. Cihaz doğrulama (T37) ayrı iş tipi olarak eklensin mi (v0.2)?

---

## 15. Sürüm geçmişi

| Sürüm | Tarih | Değişiklik |
|-------|-------|------------|
| v0.1 | 10 Haz 2026 | İlk taslak — tek workspace, ODF prefix, master dataset’ler, 3 tip, akışlar |
| v0.1-deploy | 10 Haz 2026 | Odak test kurulumu + demo kayıtlar (ODF-0002…0005) |

---

*Operation Core genel spec: [operationcore_phase1.md](../../operationcore/operationcore_phase1.md)*
