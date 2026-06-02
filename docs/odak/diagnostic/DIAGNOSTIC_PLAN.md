# Backend Response Time Diagnostic Planı (Odak)

**Durum:** Faz 1 + 1B UI tamam (Odak deploy) — Faz 2 backend planlı
**Son güncelleme:** 2 Haziran 2026
**Rapor:** [DIAGNOSTIC_REPORT_2026-06-02.md](./DIAGNOSTIC_REPORT_2026-06-02.md)
**Yol haritası:** [PERFORMANCE_ROADMAP.md](./PERFORMANCE_ROADMAP.md) ← müşteri sunumu
**İlgili dokümanlar:**
- `docs/odak/operationcore/mngoperations/PERF_OPTIMIZATION.md`
- `docs/odak/operationcore/mngoperations/PERF_KONTROL_REHBERI.md`

---

## 1. Amaç

Backend servislerimizin — özellikle **MngOperations** — endpoint yanıt sürelerini **ölçmek**, yavaşlığın kaynağını **ayrıştırmak** (geliştirici makinesi mi, sunucu kaynakları mı, uygulama kodu mu) ve **aksiyon alınabilir bir rapor** üretmek.

### Hedef SLA (iş hedefi)

| Katman | Hedef | Not |
|--------|-------|-----|
| **Tek API çağrısı (P95, warm)** | ≤ **2–3 sn** | Kullanıcı tek ekran etkileşimi |
| **Tek API çağrısı (P95, cold)** | ≤ **4 sn** | İlk istek / cache miss kabul edilebilir |
| **Downstream tek hop (DG/Keeper)** | ≤ **500 ms** | MO içindeki tek DG sorgusu |
| **Gateway overhead** | ≤ **50 ms** | Doğrudan servis vs gateway farkı |

> Bu hedefler başlangıç çerçevesidir. Rapor sonrası endpoint bazında sıkılaştırılır (ör. `health` < 100 ms, `board_list` warm < 500 ms).

### Bilinen başlangıç noktası (MngOperations — Odak, May 2026)

Önceki perf oturumundan (`PERF_OPTIMIZATION.md`):

| Endpoint | Durum | totalMs | DG çağrı | Not |
|----------|-------|---------|----------|-----|
| `board_list` | warm | ~330 | 1 | Hedefin altında |
| `board_list` | cold | ~1747 | 5 | Metadata + person cold |
| `profile` | warm | ~1218 (opt. sonrası) | 4 | Hedefe yakın |
| `profile` | cold | ~3119 | 10 | Hedefin üzerinde |

**Çıkarım:** Sorun tek tip değil; bazı endpoint'ler iyi, bazıları (özellikle cold path ve profil) hâlâ yavaş. Platform geneli için sistematik ölçüm şart.

---

## 2. Temel soru: Yavaşlık nereden geliyor?

Her yavaş istek için süreyi **dört katmana** ayırırız. Raporun kalbi bu ayrıştırmadır.

```
┌─────────────────────────────────────────────────────────────────┐
│  A. İstemci / ağ (geliştirici makinesi → Odak sunucusu)         │
├─────────────────────────────────────────────────────────────────┤
│  B. Edge (Nginx / MngGateway routing, TLS, CORS)                │
├─────────────────────────────────────────────────────────────────┤
│  C. Uygulama servisi (MO, DG, Keeper, … — iş mantığı)          │
├─────────────────────────────────────────────────────────────────┤
│  D. Altyapı (CPU/RAM/IO, MongoDB, Redis, RabbitMQ, Keycloak)   │
└─────────────────────────────────────────────────────────────────┘
```

### Katman A — Geliştirici makinesi / istemci

**Belirti:** Tarayıcı Network sekmesi >> sunucu logundaki `totalMs`.

**Olası sebepler:**
- VPN / uzak ağ gecikmesi (Odak: `192.168.20.20`)
- Tarayıcı eklentileri, DevTools overhead
- UI'nin ardışık birden fazla API çağrısı (waterfall)
- `localStorage.OC_PERF` ile ölçülen süre gateway + TLS + serialization dahil

**Doğrulama:**
- Aynı endpoint'e **sunucu üzerinden** `curl` (container içi veya SSH)
- Geliştirici makinesinden `curl` vs sunucudan `curl` karşılaştırması
- UI waterfall: kaç istek, hangi sırayla, hangisi kritik yol

### Katman B — Gateway

**Belirti:** `curl localhost:5086/...` hızlı, `curl :5040/operations/...` yavaş.

**Olası sebepler:**
- Ocelot route / downstream timeout / retry
- JWT doğrulama (Keycloak introspection)
- Request/response body büyüklüğü (serialization)

**Doğrulama:**
- Doğrudan servis portu vs gateway portu (Odak portları: MO `5086`, Gateway `5040`)
- Gateway access log + downstream timing (varsa)

### Katman C — Uygulama kodu

**Belirti:** Sunucu içi ölçümde bile `totalMs` yüksek; downstream breakdown gösteriyor.

**Olası sebepler (MngOperations özelinde, kanıtlanmış / muhtemel):**

| Desen | Örnek | Etki |
|-------|-------|------|
| **N+1 downstream çağrı** | Profil cold: 10 DG çağrısı | Yüksek `dgCalls`, `dgMs` >> `totalMs` (paralel değilse) |
| **Sıralı bekleme** | Field-behavior bitene kadar links/timeline beklemesi (eski; kısmen düzeltildi) | Paralelleştirme fırsatı |
| **Overfetch** | Timeline `limit=200` (eski; `limit=5`'e indirildi) | Gereksiz IO |
| **Keeper N+1** | Board cold: 7 Keeper çağrısı (person directory) | `keeperCalls` yüksek |
| **Eksik cache** | Workspace/flow/fields her cold istekte yeniden | Cold >> warm farkı |
| **Ağır aggregation** | DG tarafında karmaşık pipeline | MO değil DG darboğazı |
| **Senkron IO** | RabbitMQ publish, dosya IO bloklayıcı | Sporadik spike |

**Doğrulama:**
- `MngOperationsSettings__PerfDiagnostics=true` → `OC_PERF` log satırları
- `OcCallStats`: `totalMs`, `dgCalls`, `dgMs`, `keeperCalls`, `ops=[...]`
- DG tarafında aynı sorgunun izole süresi

### Katman D — Sunucu kaynakları

**Belirti:** Tüm servisler aynı anda yavaş; CPU %100, MongoDB slow query log, disk IO wait yüksek.

**Olası sebepler:**
- Odak VM'de RAM yetersizliği (Ollama kapalı — bilinçli)
- MongoDB index eksikliği / collection scan
- Container CPU limiti
- Keycloak / RabbitMQ contention
- Disk doluluğu veya yavaş storage

**Doğrulama:**
- Ölçüm anında: `docker stats`, host CPU/RAM/disk
- MongoDB: `db.currentOp()`, slow query profiler (kısa pencere)
- Korelasyon: yavaşlık saat dilimi vs kaynak grafiği

---

## 3. Ölçüm metodolojisi

### 3.1 Altın kural: Tek değişken

Her koşuda **yalnızca bir katman** değişir. Örnek sıra:

1. **Sunucu-içi baseline** (ağ hariç) → Katman C+D
2. **Gateway üzerinden** → +Katman B
3. **Geliştirici makinesinden** → +Katman A
4. **Tarayıcı E2E** → UI waterfall

### 3.2 Cold vs warm

| | Cold | Warm |
|---|------|------|
| **Tanım** | Servis restart sonrası veya cache temiz | Aynı endpoint 2.–5. tekrar |
| **Neden önemli** | Kullanıcı ilk açılış deneyimi | Günlük kullanım hissi |
| **Ölçüm** | Her endpoint için 1 cold + 5 warm, medyan/P95 |

### 3.3 Metrik seti (her endpoint için)

| Metrik | Kaynak |
|--------|--------|
| `T_total` | Servis log / middleware / `OC_PERF` |
| `T_dg` | `OcCallStats.dgMs` |
| `T_keeper` | `OcCallStats.keeperMs` |
| `N_dg`, `N_keeper` | `OcCallStats` |
| `T_gateway_overhead` | Gateway − direct |
| `T_client` | Tarayıcı / `curl -w '%{time_total}'` |
| `Payload_bytes` | Response size |
| `HTTP_status` | 200/403/500 |

### 3.4 Ölçüm ortamı

| Ortam | Amaç |
|-------|------|
| **Odak sunucusu** (`192.168.20.20`) | Referans; prod-benzeri Docker stack |
| **Geliştirici makinesi (local dev)** | Karşılaştırma; “makinem mi yavaş?” sorusu |
| *(opsiyonel)* Production | Gerçek yük profili — ayrı onay |

**Karar (kilitlenecek):** Birincil rapor **Odak sunucu-içi** ölçümle üretilir; istemci katmanı ek tablo olarak eklenir.

---

## 4. Kapsam — servis ve endpoint envanteri

### 4.1 Öncelik sırası

| Öncelik | Servis | Port (Odak) | Gerekçe |
|---------|--------|-------------|---------|
| **P0** | MngOperations | 5086 | Kullanıcı şikayeti; en ağır runtime endpoint'leri |
| **P0** | MngDataGateway | 5010 | MO'nun ana downstream'i; DG yavaşsa MO yavaş |
| **P1** | MngKeeper | 5001 | Auth + person directory; Keeper N+1 |
| **P1** | MngGateway | 5040 | Tüm UI trafiği buradan |
| **P2** | MngHub, MngScheduler, MngNotifier, MngDocument, MngWorkflow | 5020, 5090, … | OC dışı akışlar |
| **P3** | MngAdmin, MngLLM (stub Odak'ta) | — | Odak'ta sınırlı |

### 4.2 MngOperations — P0 endpoint listesi

`RuntimeController` + diğer controller'lar:

| Endpoint | Method | Kritiklik | Bilinen baseline |
|----------|--------|-----------|------------------|
| `runtime/boards/{id}/list` | POST | Yüksek (ana liste) | warm ~330 ms |
| `runtime/work-items/{id}/profile` | GET | Yüksek (profil) | warm ~1.2 s |
| `runtime/work-items/{id}/profile-view` | GET | Yüksek | Ölçülecek |
| `runtime/boards/{id}` | GET | Orta | Ölçülecek |
| `runtime/work-items/{id}/form` | GET | Orta | Ölçülecek |
| `runtime/work-items/form` | GET | Orta | Ölçülecek |
| `runtime/work-items/{id}/timeline` | GET | Orta | Ölçülecek |
| `runtime/queries/{key}/execute` | POST | Orta | Ölçülecek |
| `work-items` CRUD | * | Orta | Ölçülecek |
| `catalogs/*` | * | Düşük–Orta | Ölçülecek |
| `health`, `version` | GET | Referans | < 100 ms beklenir |

### 4.3 MngDataGateway — P0 (MO tarafından tetiklenen)

MO `OC_PERF` logundaki `ops=[dg:...]` kırılımından türetilecek top-N sorgular; ayrıca:

- `query:op_work_items` (board list)
- `op_work_item_timelines`
- `op_links` (in/out)
- `op_fields`, workspace/flow metadata

---

## 5. Çalışma fazları

### Faz 0 — Planlama ve envanter *(şu an)*

- [x] Diagnostic plan dokümanı
- [ ] Endpoint envanterini UI ekranlarıyla eşleştir (hangi sayfa hangi API'yi çağırıyor)
- [ ] Odak erişim / token script'leri doğrula (`load-operationcore-token.ps1`)
- [ ] Rapor şablonu ve öncelik matrisini onayla

### Faz 1 — Altyapı snapshot (Katman D)

**Süre tahmini:** 0.5 gün

- Odak host: CPU, RAM, disk, Docker container stats (idle + yük altında)
- MongoDB: versiyon, WiredTiger cache, index sayıları (OC dataset'leri)
- Keycloak, RabbitMQ, Redis: ayakta mı, latency normal mi
- **Çıktı:** "Altyapı sağlıklı mı?" bölümü — evet/hayır + kanıt

### Faz 2 — Sunucu-içi endpoint baseline (Katman C, MO + DG)

**Süre tahmini:** 1–2 gün

1. `PerfDiagnostics=true` deploy (MO)
2. Mevcut smoke script genişlet veya yeni `diagnostic-benchmark.ps1`:
   - Token al
   - P0 endpoint'leri cold + 5× warm
   - Log'dan `OC_PERF` parse et
3. DG için aynı sorguları **doğrudan** DG API'den çağır (MO'suz)
4. **Çıktı:** Endpoint × (cold/warm) × (total, dg, keeper) tablosu

### Faz 3 — Gateway ve istemci katmanı (Katman A + B)

**Süre tahmini:** 0.5–1 gün

- Direct `:5086` vs Gateway `:5040/operations` karşılaştırması
- Geliştirici makinesi `curl` vs Odak localhost `curl`
- UI: `localStorage.OC_PERF='1'` + Network waterfall (board + profil)
- **Çıktı:** Katman katman gecikme payı (% stacked bar)

### Faz 4 — Kök neden analizi ve öneriler

**Süre tahmini:** 1 gün

- Yavaş endpoint'leri sınıflandır (aşağıdaki matris)
- Kod incelemesi: en yüksek `dgCalls` / `keeperCalls` olan path'ler
- DG slow query / explain (gerekirse)
- **Çıktı:** Öncelikli iyileştirme backlog'u

### Faz 5 — Rapor ve paydaş sunumu

- Tek PDF/Markdown rapor: `DIAGNOSTIC_REPORT_YYYY-MM-DD.md`
- Executive summary (1 sayfa) + teknik ek
- Sonraki sprint'e alınacak maddeler

---

## 6. Mevcut araçlar (yeniden kullanım)

| Araç | Konum | Kullanım |
|------|-------|----------|
| `OcCallStats` + `OC_PERF` log | MngOperations | İstek başına DG/Keeper breakdown |
| `PerfDiagnostics` bayrağı | `docker-compose.odak.yml` | Ölçüm modu aç/kapa |
| `localStorage.OC_PERF='1'` | `Mng.Ui/services/apiService.ts` | İstemci tarafı süre |
| `smoke-sla-faz1.ps1` | `docs/odak/operationcore/scripts/` | Token + profil smoke |
| `PERF_OPTIMIZATION.md` | mngoperations | Tarihsel baseline |

### Eksik / oluşturulacak

| Araç | Amaç | Öncelik |
|------|------|---------|
| `diagnostic-benchmark.ps1` | Tüm P0 endpoint'leri otomatik ölç, CSV/JSON üret | Yüksek |
| MO middleware request timing | Tüm controller'lar için genel `totalMs` (sadece PerfDiagnostics) | Orta |
| DG request timing | Downstream izolasyonu | Orta |
| Basit HTML/Markdown rapor generator | Ölçüm JSON → tablo | Düşük |

---

## 7. Sınıflandırma matrisi (rapor için)

Her yavaş endpoint için:

| Kod | Tanım | Tipik aksiyon |
|-----|-------|---------------|
| **INFRA** | CPU/RAM/Mongo darboğazı | Kaynak artır, index, shard |
| **NET** | İstemci–sunucu ağı | VPN/CDN değil; UI birleştirme |
| **GW** | Gateway overhead | Route optimizasyonu, JWT cache |
| **N+1-DG** | Çok sayıda sıralı DG çağrısı | Paralelleştir, batch API, cache |
| **N+1-KP** | Keeper person lookup | Toplu getUser, cache |
| **OVERFETCH** | Gereksiz büyük limit/alan | Query daralt |
| **COMPUTE** | MO CPU-bound (rule engine, mapping) | Algoritma / memoization |
| **DG-SLOW** | Tek DG sorgusu yavaş | Index, pipeline, DG optimizasyonu |
| **COLD-CACHE** | Warm iyi, cold kötü | Startup cache, metadata preload |

---

## 8. Çözüm önerileri (hipotez — ölçümle doğrulanacak)

### Hızlı kazanımlar (düşük risk)

1. **PerfDiagnostics açık kısa ölçüm turu** — tahmin değil veri
2. **Warm path SLA'ya yakın endpoint'leri dokunma** (board_list zaten ~330 ms)
3. **Profil cold path:** metadata (workspace/flow/fields) istek-içi veya distributed cache
4. **Keeper batch:** board list cold'daki 7 çağrı → tek bulk endpoint (Keeper tarafında var mı kontrol)
5. **UI request coalescing:** Aynı sayfadaki paralel çağrıları birleştiren BFF pattern (uzun vade)

### Orta vadeli

6. **DG sorgu optimizasyonu:** `op_work_items` pipeline explain; compound index
7. **MO genel timing middleware:** Sadece `RuntimeContextService` değil tüm controller'lar
8. **Gateway JWT cache:** Keycloak introspection tekrarını azalt

### Uzun vadeli / mimari

9. **Read model / projection:** Profil için önceden birleştirilmiş DTO (CQRS)
10. **Platform-wide APM:** OpenTelemetry + Seq/Grafana (sürekli izleme)
11. **Load test:** k6 ile eşzamanlı kullanıcı senaryosu (SLA regresyon kapısı)

---

## 9. Rapor çıktısı — şablon

```markdown
# Backend Response Time Diagnostic Raporu
Tarih: YYYY-MM-DD | Ortam: Odak | Ölçüm aracı: diagnostic-benchmark.ps1 v1

## Executive Summary
- P0 endpoint'lerin X/Y tanesi 2–3 sn hedefinin altında (warm P95)
- Ana darboğaz: [DG N+1 / Keeper / Mongo / Ağ / ...]
- Tahmini iyileştirme: [profil cold %40 ↓] öncelikli 3 madde

## Ortam
- Host: CPU/RAM/disk snapshot
- Stack: docker compose versiyonları, commit SHA

## Sonuç tablosu (warm P95)
| Servis | Endpoint | P95 ms | DG ms | Keeper ms | Sınıf | Öneri |
|--------|----------|--------|-------|-----------|-------|-------|

## Katman ayrıştırması (ör. profil)
- Sunucu direct: 1218 ms
- Gateway: +45 ms
- Dev machine curl: +120 ms
- Browser E2E: 1800 ms (3 paralel istek)

## Öncelikli aksiyon listesi
1. ...
2. ...

## Ek: Ham ölçüm JSON
```

---

## 10. Riskler ve sınırlamalar

- **Odak ≠ Production:** Yük profili farklı; rapor "Odak referans" olarak etiketlenmeli
- **PerfDiagnostics üretimde kapalı kalmalı** — yalnızca ölçüm penceresinde açılır
- **Stub servisler:** Odak'ta MngReactor/MngLLM stub; ilgili endpoint'ler kapsam dışı
- **Tek kullanıcı ölçümü:** Concurrent load yok; Faz 5+ için load test ayrı planlanır

---

## 11. Kilitli kararlar (2 Haziran 2026)

| Konu | Karar |
|------|-------|
| **Ortam** | Yalnızca **Odak sunucusu** — lokal makinede backend çalışmıyor (UI debug hariç) |
| **Kapsam** | **Backend only** — UI E2E sonraki fazlarda |
| **Metrik** | **Warm P95** (ana SLA göstergesi) + **session cold** (oturumda ilk istek) + medyan/min/max |
| **Otomasyon** | `docs/odak/diagnostic/scripts/diagnostic-benchmark.ps1` — token/seed için `operationcore/scripts` |
| **Açık** | DG Mongo explain / Odak host `docker stats` — Faz 1 altyapı snapshot'ta |

---

## 12. Araçlar

| Araç | Konum | Durum |
|------|-------|-------|
| Benchmark script | `docs/odak/diagnostic/scripts/diagnostic-benchmark.ps1` | ✅ Hazır |
| Token / seed | `docs/odak/operationcore/scripts/` | Mevcut |
| OC_PERF breakdown | `MngOperationsSettings__PerfDiagnostics` | Mevcut (manuel aç/kapa) |
| JSON rapor çıktısı | `docs/odak/diagnostic/reports/` | Script üretir |

### Çalıştırma

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\diagnostic\scripts\diagnostic-benchmark.ps1
.\docs\odak\diagnostic\scripts\diagnostic-benchmark.ps1 -CompareDirect
```

---

## Tetikleyici şikayet (2 Haziran 2026)

Sunucu UI'da sıradan işlemler yavaş; **workspace tanımlama → zamanlanmış görevler listesi 20–30 sn** sürüyor.

**İlk bulgu:** Scheduled tab backend yükü ~2 sn (DG). 20–30 sn büyük olasılıkla **eager tab storm** + tarayıcı bağlantı limiti. Detay: `DIAGNOSTIC_REPORT_2026-06-02.md` §2.

---

## 13. Sonraki adım (konuya dönüldüğünde)

- [x] Benchmark + workspace definitions ölçümü
- [x] İlk rapor (`DIAGNOSTIC_REPORT_2026-06-02.md`)
- [x] UI Faz 1 — workspace tanımlama lazy + `useOcWorkspaceCatalog` (Odak deploy)
- [x] UI Faz 1B — operasyon explorer lazy boards, dashboard/relation defer, kanban batch, workspace cache (Odak deploy)
- [ ] Deploy sonrası UI doğrulama (Network + `diagnostic-workspace-definitions.ps1`)
- [ ] `PerfDiagnostics=true` → OC_PERF cold profil analizi
- [ ] P0 backend aksiyonları (Faz 2: metadata cache, profil cold, dashboard)
