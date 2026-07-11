# Zimmet — Plan ve Mimari Kararlar

**Son güncelleme:** 8 Temmuz 2026

---

## 1. Senaryo özeti

Bir fabrika personeline laptop, monitör, çanta gibi malzemeleri zimmetler. Süreç şu katmanlardan oluşur:

```text
TANIMLAR (AF)          STOK GİRİŞİ (OC)         DEMİRBAŞ (AF)           ZİMMET (OC)
─────────────          ───────────────          ───────────────         ──────────
Ürün grubu             Depo girişi WI           Tekil kayıt             Zimmet verme WI
Ürün kataloğu    →     Mal kabul / stokla   →   Seri no, durum     →   Personele ata
Depo / lokasyon        (F4: satınalma sonra)    Demirbaş no             İade WI
Tedarikçi (mevcut)
```

**Kritik ayrım:** Katalog ürünü (ör. *Dell Latitude 5520*) ile fiziksel varlık (SN: `ABC123` olan bu laptop) farklıdır. Zimmet **fiziksel demirbaş** kaydına yapılır.

---

## 2. Kilitli kararlar

| Konu | Karar |
|------|--------|
| Başlangıç kapsamı | **F0 → F1 → F2 → F3** tamamlandı; **F4 (satınalma) ertelendi** |
| Takip modeli | **Hibrit:** laptop/monitör → seri no ile tekil; çanta/aksesuar → miktar (isteğe bağlı seri) |
| Workspace sayısı | Başlangıçta **2 WS:** Depo Girişi (`GIR`) + Personel Zimmet (`ZIM`); satınalma sonra 3. WS (`SAT`) |
| Master veri | Yeni `zimmet_*` dataset'leri; **`tedarikciler` AF yeniden kullanılır** |
| Demirbaş kaydı | AF dataset `zimmet_demirbaslar`; OC zimmet WI ile durum **manuel/script senkronu** (otomasyon backlog'ta) |
| Geliştirme | Data/seed oturumlarında kod geliştirmesi yok; platform ihtiyaçları → **Geliştirme Backlog** |

---

## 3. Faz planı

### F0 — Tanımlar (AF) ✅

| Dataset | Açıklama |
|---------|----------|
| `zimmet_urun_gruplari` | Laptop, monitör, çanta, sarf; `trackBySerial`, `isFixedAsset` |
| `zimmet_urunler` | Katalog: marka, model, garanti; ürün grubuna relation |
| `zimmet_depolar` | Depo tanımları |
| `zimmet_depo_lokasyonlari` | Depo içi raf/bölme; depoya bağlı |
| `zimmet_demirbaslar` | Tekil fiziksel kayıt (F1 ile birlikte) |

Her dataset için eşleşen **Automated Form** (`zimmet-*-form`).

### F1 — Demirbaş envanteri (AF) ✅

- `zimmet_demirbaslar`: incremental `DMB-{0:D5}`, seri no, durum (`depoda` / `zimmetli` / …), depo/lokasyon, `persons` alanı
- Master seed: 4 grup, 5 katalog ürün, 1 depo, 3 lokasyon, 5 demirbaş

### F2 — Depo girişi (OC) ✅

**Workspace:** `Zimmet Depo` — prefix `GIR`

| Öğe | Değer |
|-----|-------|
| Tip | Depo girişi |
| Akış | Taslak → Mal kabul → Stoklandı → Kapalı |
| Pool alanlar | `katalogUrunId`, `miktar`, `depoId`, `lokasyonId`, `tedarikciId`, `faturaNo`, `girisTarihi`, `kaynak`, `seriNoListesi` |
| `kaynak` seçenekleri | `manuel`, `satinalma` (F4 için hazır) |

### F3 — Personel zimmet (OC) ✅

**Workspace:** `Personel Zimmet` — prefix `ZIM`

| Tip | Akış |
|-----|------|
| Zimmet verme | Talep → Onay bekliyor → Teslim edildi → Aktif → Kapalı |
| Zimmet iade | İade açık → İade tamam → Kapalı |

Pool alanlar: `demirbasId`, `personelId`, `departman`, `teslimTarihi`, `planliIadeTarihi`, `teslimDurumu`, `zimmetNotu`, `iadeDurumu`, `hasarAciklamasi`

### F4 — Satınalma (OC) ⏳ Planlandı

**Workspace:** `Zimmet Satınalma` — prefix `SAT` (henüz seed yok)

Önerilen akış taslağı:

```text
Talep → Onay → Sipariş → Teslim alındı → GIR'e bağla → Kapalı
```

- `GIR` WI `kaynak=satinalma` ile SAT WI'dan türetilebilir
- GIR kapanınca N adet demirbaş otomatik oluşturma → **AF-1 backlog**

---

## 4. Veri modeli — dataset özeti

| Dataset | formCode | Not |
|---------|----------|-----|
| `zimmet_urun_gruplari` | `zimmet-urun-gruplari-form` | `trackBySerial` hibrit takip anahtarı |
| `zimmet_urunler` | `zimmet-urunler-form` | `urunGrubuId` relation |
| `zimmet_depolar` | `zimmet-depolar-form` | |
| `zimmet_depo_lokasyonlari` | `zimmet-depo-lokasyonlari-form` | `depoId` relation |
| `zimmet_demirbaslar` | `zimmet-demirbaslar-form` | `DMB-{0:D5}`, unique `seriNo` |
| `tedarikciler` | `tedarikciler-form` | Mevcut AF — yeniden kullanım |

---

## 5. Operation Core — workspace özeti

### WS1 — Zimmet Depo (`GIR`)

| Bileşen | Odak test ID |
|---------|----------------|
| Workspace | `1f641a1e-da0b-40db-abc0-dec8dd063502` |
| Board (kuyruk) | `87eef393-debc-4ae7-94ea-40f70a8926c9` |
| Tip — Depo girişi | `f44d1357-b1c2-4f69-8cb9-47cc91b284be` |
| Flow | `54572c79-2f1c-4c7c-9423-2c6d0dfd22ca` |

### WS2 — Personel Zimmet (`ZIM`)

| Bileşen | Odak test ID |
|---------|----------------|
| Workspace | `08c0e3d9-15b2-4048-bf0d-1d4ee0011aeb` |
| Board (kuyruk) | `61f7b1fe-7ec0-4658-bb68-46bf78b83eaf` |
| Board (iade) | `c0f697cf-2671-4963-9718-86b6e0c543bb` |
| Tip — Zimmet verme | `a7ea7c55-7a2b-452e-a966-c49680d4bc03` |
| Tip — Zimmet iade | `5a280880-4d1d-4167-afcd-df23451ad6b2` |
| Flow verme | `a943f283-f33f-4c93-b1a1-bfad3108065c` |
| Flow iade | `40c1425c-2a84-4dfa-9876-adb1f47ea465` |

Tam ID listesi: [seed/zimmet-oc-seed.json](./seed/zimmet-oc-seed.json)

---

## 6. Side menü yapısı

AF formları `@side_menu` üzerinden **Dinamik Formlar** header'ı altında gruplanır:

```text
Dinamik Formlar (header — pageCode: dynamicForms.menuHeader)
  └── Zimmet Yönetimi (parent — pageCode: zimmet.menuParent)
        ├── Demirbaşlar       → /apps/automated-forms/view/zimmet-demirbaslar-form
        ├── Ürün Katalogu     → zimmet-urunler-form
        ├── Ürün Grupları     → zimmet-urun-gruplari-form
        ├── Depolar           → zimmet-depolar-form
        └── Depo Lokasyonları → zimmet-depo-lokasyonlari-form
```

Script: `scripts/patch-zimmet-side-menu.ps1`  
OC workspace'ler ayrı menü girişi gerektirmez; Operasyon Merkezi ağacında görünür.

---

## 7. Demo senaryo (seed)

Keeper personel (Odak domain):

| Kullanıcı | Person ID |
|-----------|-----------|
| `odak_admin` | `6a0f8fd13d6ba5d774ee37c7` |
| `test.user1` | `6a0f987f3d6ba5d774ee37cb` |

Örnek demirbaş durumu:

| Kod | Durum | Not |
|-----|-------|-----|
| DMB-LAP-001 | zimmetli | ZIM-0005 kapalı — odak_admin |
| DMB-MON-001 | zimmetli | ZIM-0006 aktif — test.user1 |
| DMB-LAP-002, DMB-LAP-003, DMB-BAG-001 | depoda | |

---

## 8. Geliştirme backlog

| ID | Konu | Durum |
|----|------|--------|
| **AF-1** | GIR kapanınca demirbaş üretimi (`createDatasetRows`) | ✅ |
| **OC-1** | Teslimde demirbaş zimmetle (`updateDatasetRows` / `deliver`) | ✅ |
| **OC-2** | İadede demirbaş depoya (`receive_return`) | ✅ |
| **OC-4** | Dataset tablo seçici + çoklu demirbaş | ✅ |
| **RPT** | Zimmet reporting katalog (6 rapor) | ✅ |
| **DI** | Personel dökümü + teslim/iade tutanakları | ✅ |
| **OC-3** | Demirbaş lookup runtime doğrulama | 🔲 (seed filter var) |
| **AF-2** | Ürün görseli | 🔲 |
| **DI-OC** | Geçiş sonrası otomatik tutanak | 🔲 sıradaki aday |
| **RPT-HIST** | Expand zimmet WI geçmişi | 🔲 sıradaki aday |
| **GEN-5** | MO seed yavaşlığı | 🔲 |
| **GEN-6** | Token refresh seed içinde | 🔲 |
| **GEN-7** | Metadata cache reload seed sonunda | ✅ (kısmen seed’de) |

---

## 9. Bilinen kısıtlar

1. ~~Demirbaş ↔ OC senkronu yalnızca manuel~~ → OC-1/OC-2 kuralları ile otomatik (Odak).
2. **Duplicate demo:** `setup-zimmet-all.ps1 -SeedDemo` iki kez çalıştırıldığında GIR/ZIM çiftleri oluşabilir.
3. **Prod ortam** henüz kurulmadı; tüm ID'ler Odak test'e özgü.
4. **`indexList` formatı:** DG'de `fields: { "kod": 1 }` (object); array formatı 400 verir.
5. Tutanaklar şu an **rapor satırından**; OC transition tetikli belge yok.

---

## 10. Gelecek genişlemeler (planlama)

- F4 Satınalma workspace + SAT→GIR bağlantısı
- Amortisman / garanti takibi (AF alanları) — garanti raporu + DI kısmen hazır
- Barkod/QR ile depo girişi
- OC’den tutanak / demirbaş zimmet geçmişi expand
- Prod deploy: `docs/odak/proddeploy/` sürecine entegrasyon
