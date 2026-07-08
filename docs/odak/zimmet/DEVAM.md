# Zimmet — Kaldığımız Yer

**Son güncelleme:** 8 Temmuz 2026  
**Durum:** ✅ F0–F3 + demo seed + side menü tamamlandı (Odak test)  
**Sıradaki:** F4 satınalma taslağı, duplicate temizlik, prod kurulum

**Plan:** [PLAN.md](./PLAN.md) · **Özet:** [README.md](./README.md)

---

## Son oturum özeti (8 Temmuz 2026)

1. **Side menü patch** çalıştırıldı — `Dinamik Formlar` → `Zimmet Yönetimi` altında 5 AF formu
2. `patch-zimmet-side-menu.ps1` iyileştirildi:
   - `maxOrder` hesaplama hatası düzeltildi
   - `@side_menu` listesi eksik döndüğünde `pageCode` filter ile ID bulma eklendi
3. `setup-zimmet-all.ps1` içinde `-PatchSideMenu` (varsayılan `$true`) entegrasyonu mevcut

---

## Tamamlanan işler

### F0 — Dataset + Automated Forms ✅

| Dosya | Dataset / formCode |
|-------|-------------------|
| `datasets/zimmet_urun_gruplari_dataset.json` | `zimmet_urun_gruplari` |
| `datasets/zimmet_urunler_dataset.json` | `zimmet_urunler` |
| `datasets/zimmet_depolar_dataset.json` | `zimmet_depolar` |
| `datasets/zimmet_depo_lokasyonlari_dataset.json` | `zimmet_depo_lokasyonlari` |
| `datasets/zimmet_demirbaslar_dataset.json` | `zimmet_demirbaslar` |
| `automated-forms/zimmet_*_automated_form.json` | 5 form |

Script: `scripts/setup-zimmet-datasets-and-forms.ps1`

### F1 — Master seed ✅

- 4 ürün grubu (LAP, MON, CNT, ACC)
- 5 katalog ürün (Dell 5520, HP 840, Dell U2722D, Lenovo çanta, Logitech MX)
- 1 depo (`DEP-ANA`), 3 lokasyon
- 5 demirbaş kaydı

Script: `scripts/seed-zimmet-master-data.ps1`  
Çıktı ID'ler: `seed/zimmet_master_ids.json` (seededAt: 2026-07-07)

### F2–F3 — Operation Core workspace ✅

| Workspace | Prefix | Tipler |
|-----------|--------|--------|
| Zimmet Depo | `GIR` | Depo girişi |
| Personel Zimmet | `ZIM` | Zimmet verme, Zimmet iade |

Script: `scripts/seed-operation-core-zimmet.ps1`  
Çıktı: `seed/zimmet-oc-seed.json` (seededAt: 2026-07-07)

### Demo seed ✅

Keeper personel ile örnek iş kayıtları:

| WI | Durum | Özet |
|----|-------|------|
| GIR-0001, GIM-0002 | Kapalı | Depo girişi (duplicate — iki çalıştırma) |
| ZIM-0005 | Kapalı | DMB-LAP-001 → odak_admin |
| ZIM-0006 | Aktif | DMB-MON-001 → test.user1 |
| ZIM-0001, ZIM-0004 | (eski çalıştırma) | Duplicate kayıtlar |

### Side menü ✅

```
Dinamik Formlar
  └── Zimmet Yönetimi
        ├── Demirbaşlar
        ├── Ürün Katalogu
        ├── Ürün Grupları
        ├── Depolar
        └── Depo Lokasyonları
```

Script: `scripts/patch-zimmet-side-menu.ps1`  
Menü ID'leri (8 Temmuz 2026 patch):

| Öğe | ID |
|-----|-----|
| Dinamik Formlar header | `c6385ed4-14fd-4814-82ab-8971f7df610f` |
| Zimmet Yönetimi parent | `c4b23d3f-c3ec-4848-a314-080775e4c876` |
| Demirbaşlar | `fd7223d4-6e9e-4238-a078-fb6b34427d83` |
| Ürün Katalogu | `a4e86e5b-cc0c-4550-8f14-c9371ddf6585` |
| Ürün Grupları | `32c2c68e-cc62-462f-ba8a-6ed9353d5fcb` |
| Depolar | `01838397-3b6a-4fa6-a701-dbf0061fc8c5` |
| Depo Lokasyonları | `e57d35b1-d6f3-46e4-a49c-343a6094973f` |

---

## Script envanteri

| Script | Amaç |
|--------|------|
| `setup-zimmet-datasets-and-forms.ps1` | F0: dataset şema + AF formlar |
| `seed-zimmet-master-data.ps1` | F1: master veri yükleme |
| `seed-operation-core-zimmet.ps1` | F2–F3: OC WS + `-SeedDemo` |
| `patch-zimmet-side-menu.ps1` | AF formlarını side menüye ekle |
| `setup-zimmet-all.ps1` | Yukarıdakilerin tamamı |
| `lib/ZimmetDgCommon.ps1` | DG/MO ortak yardımcılar |

### Tam kurulum

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\zimmet\scripts\setup-zimmet-all.ps1 -SeedDemo
```

### Kısmi çalıştırma

```powershell
# Yalnızca schema + formlar
.\docs\odak\zimmet\scripts\setup-zimmet-datasets-and-forms.ps1

# Yalnızca master seed (schema mevcut olmalı)
.\docs\odak\zimmet\scripts\seed-zimmet-master-data.ps1

# Yalnızca OC + demo
.\docs\odak\zimmet\scripts\seed-operation-core-zimmet.ps1 -SeedDemo -ReloadMetadataCache

# Yalnızca side menü
.\docs\odak\zimmet\scripts\patch-zimmet-side-menu.ps1

# Schema/seed atla, sadece menü
.\docs\odak\zimmet\scripts\setup-zimmet-all.ps1 -SkipSchema -SkipMasterSeed -SkipOcSeed -SeedDemo:$false
```

### `setup-zimmet-all.ps1` parametreleri

| Parametre | Varsayılan | Açıklama |
|-----------|------------|----------|
| `-SkipSchema` | false | Dataset şema atlama |
| `-SkipForms` | false | AF form atlama |
| `-SkipMasterSeed` | false | Master veri atlama |
| `-SkipOcSeed` | false | OC workspace atlama |
| `-SeedDemo` | true | Demo WI + zimmet örnekleri |
| `-ReloadMetadataCache` | true | OC metadata cache yenileme |
| `-PatchSideMenu` | true | Side menü patch |

---

## Karşılaşılan sorunlar ve çözümler

| Sorun | Çözüm |
|-------|--------|
| `RepoRoot` yanlış path (`docs/docs/...`) | Script'lerde `../../../..` (4 seviye) |
| `indexList` 400 hatası | `fields: ["kod"]` → `fields: { "kod": 1 }` |
| MO token expire (uzun seed) | Token yenile, demo tekrar çalıştır |
| MO yavaş (~12 sn/WI) | Beklenen; demo ~6–8 dk |
| Side menü listesi 1 kayıt | `pageCode` filter lookup fallback |
| Metadata cache reload 404 | Workspace bazlı reload endpoint kullan |

---

## Sıradaki işler

### Öncelik 1 — Data / doğrulama

- [ ] UI'da side menüyü doğrula (sayfa yenile / çıkış-giriş)
- [ ] AF formlarında CRUD smoke test (demirbaş ekle/düzenle)
- [ ] OC'de GIR ve ZIM akışlarını manuel test et
- [ ] **Duplicate demo temizliği** (isteğe bağlı): GIR-0001, çift ZIM kayıtları

### Öncelik 2 — F4 Satınalma (planlama + seed)

- [ ] `SAT` workspace taslağını netleştir (akış, alanlar, GIR bağlantısı)
- [ ] `seed-operation-core-zimmet.ps1` veya ayrı script ile F4 seed
- [ ] `kaynak=satinalma` GIR demo senaryosu

### Öncelik 3 — Prod

- [ ] Prod token + `setup-zimmet-all.ps1` (demo olmadan veya ayrı demo)
- [ ] Prod side menü patch
- [ ] `docs/odak/proddeploy/` checklist'e ekle

### Geliştirme (kod — ayrı oturum)

Backlog detayı: [PLAN.md §8](./PLAN.md#8-geliştirme-backlog-kod--henüz-yapılmadı)

- OC-1/2: ZIM state → demirbaş otomatik senkron
- AF-1: GIR kapanınca demirbaş üretimi
- GEN-5/6/7: MO performans, token refresh, metadata cache

---

## Doğrulama checklist

1. **Side menü:** `Dinamik Formlar` → `Zimmet Yönetimi` → 5 link görünüyor mu?
2. **Demirbaşlar AF:** `/apps/automated-forms/view/zimmet-demirbaslar-form` — 5 kayıt, filtreler
3. **OC GIR:** Yeni depo girişi WI oluştur, akışı ilerlet
4. **OC ZIM:** Depodaki demirbaş seç, personele zimmet ver
5. **Keeper personel:** Zimmet formunda `personelId` picker çalışıyor mu?

---

## Önemli notlar

- Tüm seed ID'leri **Odak test** ortamına özgüdür; prod'da yeniden üretilir.
- `zimmet_master_ids.json` ve `zimmet-oc-seed.json` script çalıştırma sonrası güncellenir; commit öncesi kontrol edin.
- Yeni AF formu eklendiğinde `patch-zimmet-side-menu.ps1` içine menü girişi eklenmeli (veya script genişletilmeli).
- Geliştirme yapılmadan devam: yalnızca JSON seed + PowerShell data script'leri.
