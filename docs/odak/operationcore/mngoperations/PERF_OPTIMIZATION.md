# Operation Core — Performans Optimizasyonu

**Mayıs 2026 (`perf/oc-optimization`):** Board liste + profil warm (~%30) — bu dosya §1–4.  
**Haziran 2026 (diagnostic program):** UI Faz 1 + 1B — [../../diagnostic/PERFORMANCE_ROADMAP.md](../../diagnostic/PERFORMANCE_ROADMAP.md), özet [DEVAM.md § UI-PERF-F1](./DEVAM.md#ui-perf-f1--diagnostic-program-ui-performans-faz-1--1b-2-haz--commit-84b296c--odak-deploy).

Ölçüm-öncelikli, davranış-koruyan optimizasyon. Plan: `.cursor/plans/oc_perf_optimization_*.plan.md`.

---

## 1. Davranış-koruma smoke checklist'i (her deploy sonrası)

Optimizasyonların **hiçbir çıktı/davranışı değiştirmediğini** doğrulamak için. Board liste + profil odaklı.

### Board liste (`/apps/operation-core/boards/{boardId}`)
- [ ] Liste açılıyor, satırlar geliyor (toplam sayı doğru).
- [ ] Sıralama (sortable sütun başlığına tıkla) çalışıyor; yön değişiyor.
- [ ] Hızlı filtre (durum/öncelik/tip) ve **gelişmiş arama** (gt/gte/lt/lte, in/nin) sonuçları doğru.
- [ ] Serbest metin arama (debounce) çalışıyor.
- [ ] Relation/select sütun ve filtrelerde **etiket** görünüyor (ham id değil).
- [ ] **Computed sütun** değeri doğru hesaplanıyor (expr).
- [ ] **SLA chip** doğru faz/renk; sayaç ilerliyor.
- [ ] Audit sütunları (createdAt/createdBy/age) ve sticky actions sütunu doğru.
- [ ] Actions: profil/düzenle/sil + yeni iş modalı açılıyor.
- [ ] Kanban moduna geçiş (varsa) çalışıyor.

### Work item profil (`/apps/operation-core/work-items/{id}/profile`)
- [ ] Profil açılıyor; başlık/key/durum doğru.
- [ ] Detay sekmesi: form salt-okunur, alanlar + label'lar doğru.
- [ ] Aktivite sekmesi: timeline + yorum gönder (+ mention) çalışıyor.
- [ ] Ekler sekmesi: yükle/indir/kaldır çalışıyor.
- [ ] **Durum geçişi (transition)** butonları görünüyor; uygulanınca durum + timeline yenileniyor.
- [ ] Sidebar: SLA paneli, politikalar, meta, izleyenler, bağlılar doğru.

---

## 2. Geçici ölçüm enstrümantasyonu

Hangi optimizasyonun gerçekten işe yaradığını kanıtlamak için. Kalıcı değil; bayrak arkasında.

### Backend (MngOperations)
- `OcCallStats` (scoped): istek başına DG + Keeper çağrı sayısı/süresi.
- Bayrak: `MngOperationsSettings:PerfDiagnostics` (default `false`).
- Açıkken `RuntimeContextService.GetProfileAsync` / `GetBoardListAsync` sonunda tek `OC_PERF` log satırı (Seq/console): toplam süre + DG çağrı sayısı/süresi + op kırılımı + Keeper sayısı.
- Açmak için: Odak `docker-compose.odak.yml` → `MngOperationsSettings__PerfDiagnostics: "true"` (veya appsettings).

### UI (Mng.Ui)
- `fetchFromOperations` içinde, tarayıcıda `localStorage.OC_PERF='1'` iken konsola `[OC_PERF] {method} {path} {ms}ms` log'u.

### Baseline (Odak, 30 May 2026 — OC Demo workspace, 50 satır)
Sunucu (OC_PERF) ölçümü:

| Endpoint | Durum | totalMs | DG çağrı | Keeper | Notlar |
|---|---|---|---|---|---|
| board_list | cold | 1747 | 5 | 7 | workspace+flow+fields+query+board; person cold |
| board_list | warm | ~330 | 1 | 0 | yalnız `query:op_work_items` (~315ms) |
| profile | cold | 3119 | 10 | 0 | fields×3, links×2, timeline, rules, board, profile, wi |
| profile | warm | ~1575-1822 | 4 | 0 | `links×2 + work_items + timeline` |

Bulgular:
- **Board liste warm zaten iyi** (tek DG sorgusu). Cold maliyeti metadata+person — cache'le çözülmüş.
- **Profil warm darboğazı**: 4 DG çağrısı. `links`/`timeline` yalnızca `workItemId`'ye bağlı ama
  field-behavior çözümlemesinden **sonra** başlıyor → baştan paralel başlatılırsa örtüşme kazancı.
- `op_work_item_timelines` `limit=200` overfetch ama yalnız son N (=DefaultStateSegmentCount) kullanılıyor.

---

## 3. Uygulanan optimizasyonlar

### Backend (Faz 2)
- **Field-behavior tek-tarama** (`FieldBehaviorResolverService`): enabled field metadata'sı istek başına
  tek `key→record` map'ine toplanıyor; kurallar bir kez çekiliyor. Eski `O(alan×enabledIds)` yeniden
  tarama kaldırıldı. Çözülen davranış (alan seçimi/kurallar) birebir korunur.
- **Profil erken paralel + timeline overfetch** (`RuntimeContextService.GetProfileAsync`):
  `op_links` (in/out) ve `op_work_item_timelines` çağrıları izin kontrolünden hemen sonra başlatılıp
  metadata + field-behavior çözümlemesiyle örtüştürülüyor. Timeline `limit=200` → `sort=-enteredAt&limit=5`
  (yalnız gösterilen son N). DG `sort=-enteredAt`'i onurlandırıyor (doğrulandı) → sonuç birebir aynı.
- **person-pool-key cache**: ölçüm board-liste warm'ın tek DG sorgusu olduğunu, `op_fields`'in zaten
  katalog cache'inde olduğunu gösterdi → darboğaz değil; riskli cache eklenmedi (plan ilkesi: ölç→fix).

**Profil warm sonuç:** totalMs ~1575-1822 → **~1218ms (~%30)**, dgMs ~3380-4263 → ~2460.
**Board liste:** warm zaten optimal (tek DG sorgusu ~315ms), değişmedi.

### UI (Faz 3) — yapısal kazanımlar (davranış-koruyan)
- **Intl formatter memoization** (`ocColumnFormat.ts`): `Intl.DateTimeFormat/NumberFormat` locale/currency
  anahtarıyla cache'leniyor. 50 satır × sütun = yüzlerce formatter kurulumu → birkaç tane.
- **Tek global "now" ticker** (`useSharedNow.ts` + `OcSlaStatusChip.vue`): satır başına `setInterval`
  yerine refcount'lu tek paylaşılan timer (N timer → 1).
- **Lookup map dedup** (`useOcBoardListLookups.ts`): context map'leri (states/priorities/types) yalnız
  kaynak değişince tek kez kuruluyor (eski: kaynak başına ~6 Map kurulumu).
- **listRows önceden çözüm** (`boards/[boardId]/index.vue`): state/priority/type/assignee/createdBy
  çözümü cache'li `listRows` computed'inde yapılıyor; şablon slotları her render'da `resolveX`
  çağırmıyor (davranış: eski slot çözümleriyle birebir, fallback dahil).
- **Kanban lazy** (`defineAsyncComponent`): `OcBoardKanban` yalnız kanban görünümünde yüklenir;
  list-only board'da bundle'a girmez.

### Kapsam dışı (riskli / ölçüm gerektirmiyor — plan ilkesi)
- `expr-eval` lazy: computed sütun değeri `listRows`'ta senkron hesaplanıyor; async'e çevirmek
  davranışı bozar, kütüphane küçük → bırakıldı.
- Profil `v-window` lazy + katalog-source yeniden kullanımı: gösterilen etiketleri değiştirme riski,
  kataloglar zaten paralel+cache'li → bırakıldı.
- **Faz 4 (tablo sanallaştırma + büyük dosya bölme)**: plan gereği ayrı onay kapısında; ölçüm
  gerektirmiyor (board warm zaten ~330ms). Ayrı onayla ele alınacak.

## 4. Durum (Mayıs turu)
- Backend + UI Odak'a deploy edildi; `mngoperations` + `mngui` healthy. API davranışı korunmuş
  (board 50 satır, profil 10 alan — birebir).
- Ölçüm bayrağı (`PerfDiagnostics`) Odak'ta **kapatıldı** (kod flag-gated, üretimde log yok).
- `main`'e merge edildi.

---

## 5. Haziran 2026 — UI performans Faz 1 + 1B (diagnostic program)

**Tetikleyici:** Workspace tanımlama 20–30 sn şikayeti; Odak backend ölçümü (`diagnostic-benchmark.ps1`, `diagnostic-workspace-definitions.ps1`).

| Faz | Odak | Uygulama | Commit |
|-----|------|----------|--------|
| **Faz 1** | Admin workspace tanımları | Lazy sekmeler, `useOcWorkspaceCatalog`, paralel person | `84b296c` |
| **Faz 1B** | Operasyon alanı | Lazy boards/dashboard/relation, kanban batch 4, ws cache 60s | `84b296c` |
| **Faz 2** | MO runtime | Profil cold, dashboard aggregation, metadata cache | ⬜ Bekliyor |

**Kök neden (özet):** Yönetim ekranı yavaşlığı çoğunlukla **UI-ARCH** (eager tab storm + tekrarlı katalog); scheduled tab saf backend ~2 sn. Explorer **UI-OVERFETCH** (`loadAllBoards` × N workspace). Profil/dashboard süreleri **backend** (Faz 2).

**Dokümantasyon:**

| Belge | İçerik |
|-------|--------|
| [../../diagnostic/DIAGNOSTIC_REPORT_2026-06-02.md](../../diagnostic/DIAGNOSTIC_REPORT_2026-06-02.md) | İlk Odak ölçümü, eager storm analizi |
| [../../diagnostic/PERFORMANCE_ROADMAP.md](../../diagnostic/PERFORMANCE_ROADMAP.md) | Müşteri yol haritası, SLA hedefleri |
| [../../diagnostic/OPERATIONAL_WORKSPACE_PERF.md](../../diagnostic/OPERATIONAL_WORKSPACE_PERF.md) | Sayfa bazlı API profili + Faz 1B |
| [../../diagnostic/README.md](../../diagnostic/README.md) | Scriptler, `OC_PERF` kullanımı |
| [DEVAM.md § UI-PERF-F1](./DEVAM.md#ui-perf-f1--diagnostic-program-ui-performans-faz-1--1b-2-haz--commit-84b296c--odak-deploy) | Checkpoint tabloları (Faz 1 / 1B kodları) |

**Doğrulama (açık):** Deploy sonrası aynı scriptler + tarayıcı Network — hedefler PERFORMANCE_ROADMAP §1 SLA tablosu.
