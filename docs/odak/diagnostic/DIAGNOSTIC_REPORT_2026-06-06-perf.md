# Odak diagnostic — Operasyon Merkezi performans paketi (6 Haziran 2026)

**Paket:** OC-PERF-F2b (MO) + UI profil cache + metadata TTL  
**Deploy:** Test `192.168.20.20` ve Prod `192.168.20.8` — `mngoperations` + `mngui` (`--no-cache`)  
**Smoke:** `gateway=200 ui=200 oc_live=200` (her iki ortam)

---

## 1. Yapılan iyileştirmeler

### 1.1 Backend (MngOperations)

| Kod | İyileştirme | Etki |
|-----|-------------|------|
| **PV-PERF-4** | `op_links` giden+gelen → tek DG `$or` sorgusu (`LoadProfileLinksAsync`) | −1 DG çağrısı |
| **PV-PERF-4** | `op_tags` ve katalog dataset'leri → `GetCatalogListAsync` ( `$in` yerine cache) | −1 DG çağrısı (warm) |
| **PV-PERF-4** | Timeline `changes` çözümü → paralel `form`+`pool` task'larını yeniden kullanır | Gereksiz form/pool yüklemesi yok |
| **Faz 2b** | Dashboard widget **query dedup** — aynı `queryKey`+parametre tek `ExecuteQueryCardsAsync` | Tekrarlayan widget'larda ~%30–50 |
| **TTL** | `MetadataCache.TtlSeconds` 120→**600**, `CatalogTtlSeconds` **600** | Soğuk metadata vuruşları seyrek |

**Dosyalar:** `RuntimeContextService.cs`, `RuntimeContextService.ProfileView.cs`, `RuntimeContextService.Timeline.cs`, `RuntimeContextService.Dashboard.cs`, `MngOperations.Api/appsettings.json`

### 1.2 UI (Mng.Ui)

| Kod | İyileştirme | Etki |
|-----|-------------|------|
| **UI-PERF-2** | `ocGetWorkItemProfileView` — **45 sn** client cache; mutation sonrası `force` | Board↔profil geçişi anında (cache hit) |
| **UI-PERF-2** | `ocInvalidateWorkItemProfileView` — `ocUpdateWorkItem` sonrası | Tutarsız cache yok |

**Dosyalar:** `services/operationCoreService.ts`, `pages/.../work-items/[id]/profile/index.vue`

### 1.3 Diagnostic araçları

- `diagnostic-benchmark.ps1` — prod gateway (`192.168.20.8`) için otomatik prod token

---

## 2. Sayfa ölçümü — deploy sonrası (`diagnostic-operation-pages.ps1`)

### Production (`192.168.20.8`)

Rapor: `reports/oc_pages_prod_post_perf_20260606.json`

| Sayfa | Önce (5 Haz) warm P95 | Sonra warm P95 | Hedef | OK |
|-------|----------------------|----------------|-------|-----|
| **profile_open** | **2377 ms** | **1694 ms** | 1800 | ✅ |
| dashboard_view | 3554 ms | 2047 ms | 1200 | ⚠️ |
| board_list_open | 753 ms | 697 ms | 1200 | ✅ |
| explorer_open | 354 ms | 375 ms | 1200 | ✅ |

Profil warm P95 **hedefin altına indi** (prod müşteri SLA).

### Test (`192.168.20.20`)

Rapor: `reports/oc_pages_test_post_perf_20260606.json`

| Sayfa | warm P95 | Hedef | OK |
|-------|----------|-------|-----|
| profile_open | 2963 ms | 1800 | ⚠️ |
| dashboard_view | 1424 ms | 1200 | ⚠️ |
| board_list_open | 738 ms | 1200 | ✅ |

Test demo workspace veri yükü / cold cache farkı nedeniyle profil hâlâ hedef üstü; prod'da iyileşme net.

---

## 3. Deploy komutları (referans)

```powershell
# Test
pwsh -File .\scripts\odak\sync-odak-source.ps1 -Paths MngOperations -Server 192.168.20.20
pwsh -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngoperations -NoCache -Server 192.168.20.20
pwsh -File .\scripts\odak\sync-odak-source.ps1 -Paths Mng.Ui -Server 192.168.20.20
pwsh -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngui -NoCache -Server 192.168.20.20

# Prod
pwsh -File .\scripts\odak\sync-odak-prod.ps1 -PathsCsv MngOperations
pwsh -File .\scripts\odak\deploy-odak-prod.ps1 -Services mngoperations -NoCache
pwsh -File .\scripts\odak\sync-odak-prod.ps1 -PathsCsv Mng.Ui
pwsh -File .\scripts\odak\deploy-odak-prod.ps1 -Services mngui -NoCache
```

---

## 4. Trade-off'lar

| Konu | Karar |
|------|--------|
| Metadata TTL 600 sn | Admin kural/SLA değişiklikleri MO'da en fazla ~10 dk gecikmeyle yansır (önceden ~2 dk) |
| UI profil cache 45 sn | Kaydet/geçiş sonrası `force` ile tazelenir; board↔profil gezinmede hız |
| Session cold (~8–9 sn) | `mngoperations` restart sonrası ilk profil isteği hâlâ yüksek; warm kullanımda sorun değil |

---

## 5. Açık kalanlar (sonraki planlama)

| Öncelik | Konu |
|---------|------|
| P2 | **Pano** warm ≤ 1200 ms (prod ~2 sn) — ek agregasyon veya widget sayısı azaltma |
| P2 | **Profil cold** ≤ 4 sn — DG read-through cache (Faz 3) veya MO warm-up |
| P3 | **Faz 3 DG** — global katalog cache (`op_states`, `op_priorities`, …) |
| P3 | Test ortamında profil warm hedefe indirme (veri/seed farkı araştırması) |
| Ops | Tarayıcı Network waterfall doğrulaması |
| Ops | `OC_PERF` regresyon kapısı deploy checklist |

---

## 6. İlgili dokümanlar

- [PERFORMANCE_ROADMAP.md](./PERFORMANCE_ROADMAP.md)
- [OPERATIONAL_WORKSPACE_PERF.md](./OPERATIONAL_WORKSPACE_PERF.md)
- [DEVAM.md](./DEVAM.md)
- [../operationcore/mngoperations/DEVAM.md](../operationcore/mngoperations/DEVAM.md) § OC-PERF-F2b
