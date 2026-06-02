# Backend Response Time Diagnostic Raporu

**Tarih:** 2 Haziran 2026  
**Ortam:** Odak (`192.168.20.20`)  
**Kapsam:** Backend only (UI E2E sonraki faz)  
**Tetikleyici şikayet:** Workspace tanımlama ekranında zamanlanmış görevler listesinin 20–30 sn sürmesi; genel UI yavaşlığı

**Güncelleme (2 Haziran 2026 — deploy sonrası):** UI performans paketi (Faz 1 + 1B) uygulandı ve Odak’ta `mngui` deploy edildi. Bu rapordaki **öncesi** ölçümler geçerlidir; deploy sonrası doğrulama henüz yapılmadı. Sıradaki iş: **Faz 2 backend** (profil cold, dashboard). Detay: [PERFORMANCE_ROADMAP.md](./PERFORMANCE_ROADMAP.md).

---

## Executive Summary

| Bulgu | Sonuç |
|-------|-------|
| **Altyapı (DG/Keeper health)** | Sağlıklı — referans endpoint'ler < 20 ms |
| **MngOperations runtime (warm)** | Board list ~320 ms ✅; profil ~1,3 sn ⚠️ |
| **Scheduled tab (DG only)** | ~**2 sn** ✅ — tek başına hedefin altında |
| **Workspace definitions sayfa yükü** | ~**35 eşzamanlı DG isteği** → ~**11 sn** (sınırsız paralel) |
| **20–30 sn hissinin kök nedeni** | Büyük olasılıkla **UI mimarisi** (eager tab + tekrarlı katalog çağrıları + tarayıcı bağlantı limiti) — saf backend tek endpoint sorunu değil |

**Öncelikli aksiyonlar (güncel durum):**
1. ~~UI eager tab + katalog paylaşımı~~ → **✅ Faz 1** (Odak deploy)
2. ~~Operasyon explorer lazy boards + ilgili UI~~ → **✅ Faz 1B** (Odak deploy)
3. MO profil cold path optimizasyonu — **Faz 2** (bekliyor)
4. DG global katalog cache — **Faz 3** (bekliyor)
5. Deploy sonrası benchmark / Network doğrulama — **açık** (konuya dönüldüğünde)

---

## Ortam

| Bileşen | Adres |
|---------|-------|
| API Gateway | `http://192.168.20.20:5040` |
| MngOperations | Gateway `/operations` · direct `:5086` |
| MngDataGateway | Gateway `/data/api/v1/data` · direct `:5010` |
| Demo workspace | `f414462a-cd9e-427e-87e8-3cdff0502325` |

**Ölçüm araçları:**
- `docs/odak/diagnostic/scripts/diagnostic-benchmark.ps1`
- `docs/odak/diagnostic/scripts/diagnostic-workspace-definitions.ps1`

**Metrik:** Warm P95 + session cold (benchmark); wall-clock paralel batch (workspace definitions)

---

## 1. MngOperations — P0 endpoint sonuçları

*(Kaynak: `reports/benchmark_20260602_100537.json`, warm N=3)*

| Endpoint | Session cold | Warm P95 | Warm medyan | Hedef (3 sn) |
|----------|-------------|----------|-------------|--------------|
| `runtime/board_list` | 338 ms | 322 ms | 318 ms | ✅ |
| `runtime/profile` | **3932 ms** | 1306 ms | 1306 ms | Cold ❌ / Warm ✅ |
| `runtime/profile_view` | **3942 ms** | 2896 ms | 2330 ms | Cold ❌ / Warm ⚠️ |
| `runtime/dashboard` | 1851 ms | 1655 ms | 1607 ms | ⚠️ |
| `runtime/timeline` | 1022 ms | 995 ms | 970 ms | ✅ |
| `runtime/board` (metadata) | 972 ms | 4 ms | 3 ms | Cold ⚠️ (cache) |
| `runtime/form_edit` | 306 ms | 334 ms | 312 ms | ✅ |
| `runtime/query_execute` | 329 ms | 327 ms | 327 ms | ✅ |
| `health/live`, `version` | < 20 ms | < 20 ms | — | ✅ |

**DG/Keeper downstream (direct):** health & version < 20 ms — altyapı darboğazı değil.

### Yorum

- **Board list** production-ready (warm ~330 ms).
- **Profil** cold path hâlâ ~4 sn; warm ~1,2–1,3 sn. Mayıs perf oturumu sonrası iyileşme korunuyor ama cold kabul edilemez.
- **Dashboard** warm ~1,6 sn — widget/query aggregation gözden geçirilmeli.

**Sınıflandırma:** `N+1-DG` + `COLD-CACHE` (profil); `DG-SLOW` / aggregation (dashboard) — OC_PERF ile doğrulanacak.

---

## 2. Workspace tanımlama — zamanlanmış görevler (kullanıcı şikayeti)

*(Kaynak: `reports/ws_definitions_20260602_100857.json`)*

### 2.1 Scheduled tab — backend çağrıları

UI kodu (`OcWorkspaceDefinitionsScheduledWorkItemsTab.loadAll`) 4 paralel grup + iç içe katalog çağrıları:

| DG isteği | Süre (sıralı) |
|-----------|---------------|
| `op_work_item_schedules` (workspace filter) | 350 ms |
| `op_boards` | 306 ms |
| `op_work_item_types` global (limit 500) | 312 ms |
| `op_work_item_types` scoped | 322 ms |
| `op_workspaces/{id}` | 300 ms |
| `op_priorities` global (limit 500) | 303 ms |

| Senaryo | Wall time |
|---------|-----------|
| Sıralı toplam | ~1,9 sn |
| Paralel (Promise.all) | **~2,0 sn** ✅ |

**Sonuç:** Zamanlanmış görevler sekmesinin **saf backend yükü ~2 sn** — 20–30 sn değil. Tek başına MO/DG sorunu olarak ağır değil.

Ek: Assignee etiketleri için `personPicker.ensureSelectedIds` → Keeper `GET /user/{id}` **N+1** (schedule sayısına bağlı; az assignee ile ihmal edilebilir).

### 2.2 Asıl sorun — sayfa açılışında “eager tab storm”

`workspace-definitions/index.vue` tüm ana sekmelerde **`eager`** kullanıyor:

```224:224:Mng.Ui/pages/apps/operation-core/admin/workspace-definitions/index.vue
            eager
```

`OcWorkspaceDefinitionsValuesTab` alt sekmeleri de **eager** — sayfa açılır açılmaz 11+ sekme + 4 alt sekme veri yüklemeye başlıyor.

Her sekme bağımsız olarak aynı global katalogları tekrar çekiyor:

| Tekrarlanan sorgu | ~Tekrar sayısı |
|-------------------|----------------|
| `op_states?limit=500` | ~6× |
| `op_priorities?limit=500` | ~8× |
| `op_work_item_types?limit=500` | ~9× |
| `ocGetWorkspace` (tek kayıt) | ~10× |

**Simülasyon:** 35 paralel DG isteği (sayfa storm'unun alt kümesi)

| Metrik | Değer |
|--------|-------|
| Paralel wall time (16 thread) | **10,9 sn** |
| En yavaş tek istek | **5,0 sn** |
| P95 tek istek | **5,0 sn** |
| Hata | 0 |

### 2.3 20–30 sn neden oluşuyor? (hipotez — yüksek güven)

Tarayıcı HTTP/1.1 **domain başına ~6 eşzamanlı bağlantı** limiti uygular. 35+ istek dalgalar halinde gider:

```
Dalga 1–6:  ~5 sn  (en yavaş istekler DG'de kuyruğa girer)
...
Toplam:     ~20–30 sn  ← kullanıcı gözlemiyle uyumlu
```

Backend script sınırsız paralelde ~11 sn ölçtü; **tarayıcı kısıtı** farkı açıklıyor. Ayrıca:
- Rules sekmesi: Keeper person N+1 (`resolvePersonTitles`)
- General sekmesi: `groupStore` yükleme
- Nuxt → Gateway proxy katmanı (sonraki UI E2E fazında ölçülecek)

**Sınıflandırma:** Ana sorun **`UI-ARCH`** (eager + duplicate fetch); ikincil **`NET-QUEUE`** (connection limit); DG tek sorgu başına ~300 ms normal.

---

## 3. Katman ayrıştırması

| Katman | Scheduled tab | MO profil (warm) | Workspace sayfa |
|--------|---------------|------------------|-----------------|
| **D — Altyapı** | OK | OK | DG contention under load |
| **C — Uygulama** | OK (~2 sn) | MO N+1 DG | UI duplicate calls |
| **B — Gateway** | ~+0–50 ms (tahmin) | Ölçülmedi | Aynı |
| **A — Tarayıcı kuyruk** | Sonraki faz | Sonraki faz | **Ana etken (tahmin)** |

---

## 4. Öncelikli aksiyon listesi

### P0 — Hızlı etki (backend, bu sprint)

| # | Aksiyon | Beklenen kazanım | Risk |
|---|---------|------------------|------|
| 1 | `PerfDiagnostics=true` → profil/dashboard `OC_PERF` log analizi | Cold path DG sayısı netleşir | Düşük (flag-gated) |
| 2 | MO metadata cache (workspace/flow/fields) — profil cold | Cold 4 sn → ~2 sn | Orta |
| 3 | DG: global katalog read cache (`op_states`, `op_priorities`, `op_work_item_types`) | Tek sorgu 300 ms → < 50 ms warm | Orta |

### P1 — Orta vade (backend)

| # | Aksiyon | Not |
|---|---------|-----|
| 4 | MO workspace-scoped catalog API (tek istek: enabled types/states/priorities) | UI tekrarını backend'den de azaltır |
| 5 | Dashboard widget aggregation profiling | ~1,6 sn → hedef 1 sn |
| 6 | Keeper bulk user lookup endpoint | Rules + scheduled assignee N+1 |

### P2 — UI (Faz 1 + 1B — tamamlandı, Odak deploy 2 Haz 2026)

| # | Aksiyon | Durum |
|---|---------|-------|
| 7 | **`eager` → lazy** tab yükleme | ✅ |
| 8 | Workspace catalog composable (`useOcWorkspaceCatalog`) | ✅ |
| 9 | Person lookup paralel (`ensureSelectedIds`, rules) | ✅ |
| 10 | Operasyon explorer lazy boards + dashboard/relation defer | ✅ Faz 1B |
| 11 | Tarayıcı waterfall / script doğrulama | ⏳ Konuya dönüldüğünde |

---

## 5. Sonraki adımlar (konuya dönüldüğünde)

- [ ] Deploy sonrası UI doğrulama — workspace definitions + explorer Network waterfall
- [ ] `diagnostic-workspace-definitions.ps1` — deploy öncesi/sonrası karşılaştırma
- [ ] Odak'ta `PerfDiagnostics=true` deploy → profil cold + dashboard için `OC_PERF` satırları
- [ ] `diagnostic-benchmark.ps1 -CompareDirect` → gateway overhead
- [ ] Faz 2 backend — metadata cache, profil cold, dashboard aggregation

---

## Ek: Ham rapor dosyaları

- `docs/odak/diagnostic/reports/benchmark_20260602_100537.json`
- `docs/odak/diagnostic/reports/ws_definitions_20260602_100857.json`
