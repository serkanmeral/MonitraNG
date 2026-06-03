# Odak diagnostic — Faz 2 MO + sayfa paketleri (2 Haziran 2026)

**Ortam:** `192.168.20.20` · demo workspace `f414462a-cd9e-427e-87e8-3cdff0502325`  
**Deploy:** `mngoperations` + `mngui` (Faz 2 MO: profil metadata paralel, dashboard widget paralel, summaryCard take=1, profile-view timeline 35)

---

## 1. Endpoint benchmark (`diagnostic-benchmark.ps1`)

Hedef: warm P95 ≤ **3000 ms**, session cold ≤ **4000 ms** (runtime).

| Endpoint | Cold (ms) | Warm P95 (ms) | OK |
|----------|-----------|---------------|-----|
| runtime_board_list | 375 | **331** | ✅ |
| runtime_profile | 3869 | **1299** | ✅ |
| runtime_profile_view | 3534 | **2305** | ✅ |
| runtime_dashboard | 1935 | **1657** | ✅ |
| runtime_timeline | 1404 | 914 | ✅ |
| runtime_form_edit | 313 | 320 | ✅ |

**Önceki referans (sabah):** profile_view warm P95 ~2,0 sn; cold restart ~4,1 sn.  
**Bu ölçüm:** warm profile **~1,3 sn**, profile-view warm **~2,3 sn** — Faz 2 MO ile hedef bandında.

Rapor: `reports/benchmark_faz2_20260602.json`

---

## 2. Sayfa API paketleri (`diagnostic-operation-pages.ps1`) — YENİ

UI route’larının tetiklediği çağrı grupları (wall-clock, warm P95):

| Sayfa | Cold | Warm P95 | Hedef | OK |
|-------|------|----------|-------|-----|
| explorer_open | 420 | **372** | 1200 | ✅ |
| explorer_select_board | 600 | 1282 | 900 | ⚠️ |
| board_list_open | 913 | **669** | 1200 | ✅ |
| board_kanban_open | 956 | **964** | 3500 | ✅ |
| profile_open | 1959 | **2026** | 1800 | ⚠️ (~%13 üstü) |
| dashboard_view | 1636 | **1708** | 1200 | ⚠️ |
| work_item_new | 607 | 673 | 2000 | ✅ |
| notifications_inbox | 654 | 647 | 1500 | ✅ |
| admin_scheduled_jobs | 630 | 1062 | 2500 | ✅ |
| admin_ws_defs_shell | 335 | **335** | 800 | ✅ |

Rapor: `reports/oc_pages_faz2_20260602.json`

**Yorum:**
- Explorer ve board list **hedef altında** — Faz 1B lazy yükleme etkili.
- Profil ve pano warm hâlâ ~2 sn bandında; endpoint benchmark ile uyumlu. SLA tablosunda pano hedefi **≤1 sn** için ek MO agregasyon/cache turu gerekebilir.
- `explorer_select_board` P95 spike: DG dashboard kaydı + boards paralel — tek seferlik cold etkisi olabilir; tekrar ölçüm önerilir.

---

## 3. Workspace tanımları (`diagnostic-workspace-definitions.ps1`)

Deploy sonrası scheduled tab ve lazy model doğrulaması için aynı script çalıştırılmalı.  
Referans (önceki deploy): scheduled tab paralel **~2,1 sn** ✅.

---

## 4. Script envanteri

| Script | Kapsam |
|--------|--------|
| `diagnostic-benchmark.ps1` | MO runtime endpoint (tek tek) |
| `diagnostic-operation-pages.ps1` | **10 OC sayfa** API paketi |
| `diagnostic-workspace-definitions.ps1` | Admin workspace tanımları + scheduled |

---

## 5. Sıradaki (performans)

1. **Profil:** warm ~2 sn kabul edilebilir; cold için `docker restart mngoperations` + hemen benchmark (gerçek cold).
2. **Pano:** widget başına query profiling (`OC_PERF`); gerekirse paylaşımlı query cache / aynı queryKey birleştirme.
3. **SLA hedef güncellemesi:** `OPERATIONAL_WORKSPACE_PERF.md` — pano warm hedefi 1200→**1800 ms** geçici (ölçüme dayalı) veya Faz 2b MO.
4. Tarayıcı **Network waterfall** — script wall-clock ile çapraz doğrulama.
