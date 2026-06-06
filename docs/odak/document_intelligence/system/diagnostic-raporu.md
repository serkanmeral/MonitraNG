# Diagnostic Raporu

> **Kitle:** `MonitraNG Users` (IT / geliştirici ekibi)  
> **Konum:** Dokümanlar → **System** → bu doküman  
> **Amaç:** Üretim ortamında Operasyon Merkezi ve Dokümanlar modüllerinin **backend API yanıt sürelerini** periyodik olarak raporlamak.

Bu doküman **canlı** tutulur: her yeni ölçüm en üste eklenir; eski koşular aşağıda kalır.

---

## Ölçüm metodolojisi (IT ekibi için)

### Ne ölçülür?

| Ölçüm türü | Açıklama |
| --- | --- |
| **Sayfa API paketi** | Kullanıcı bir UI sayfasını açtığında tarayıcının tetiklediği backend çağrılarının **gruplu wall-clock süresi** (ms) |
| **Backend only** | Vue/Nuxt bileşen render süresi veya tarayıcı E2E **dahil değildir** |
| **Gateway üzerinden** | Tüm istekler müşteri ortamındaki gateway (`:5040`) üzerinden gider — gerçek yol |

### Metrikler

| Metrik | Tanım |
| --- | --- |
| **Session cold** | Oturumda ilgili senaryonun **ilk** çalıştırılması (önbellek ısınması öncesi) |
| **Warm P95** | Aynı senaryonun 3 tekrarından **95. persentil** wall-clock — ana SLA göstergesi |
| **Hedef (ms)** | Senaryo bazlı iç hedef; aşılırsa **WARN** |

**OK / WARN:** Warm P95 ≤ hedef ve son koşu başarılı → **OK**. Aksi → **WARN**.

### Araçlar ve script'ler

| Script | Kapsam |
| --- | --- |
| `docs/odak/diagnostic/scripts/diagnostic-operation-pages.ps1` | Operasyon Merkezi — 10 sayfa senaryosu |
| `docs/odak/diagnostic/scripts/diagnostic-document-intelligence-pages.ps1` | Dokümanlar — 8 sayfa senaryosu |

**Üretim koşusu (örnek):**

```powershell
# Repo kökünden — gateway prod IP otomatik prod token kullanır
.\docs\odak\diagnostic\scripts\diagnostic-operation-pages.ps1 `
  -GatewayBaseUrl "http://192.168.20.8:5040" `
  -OutputJson .\docs\odak\diagnostic\reports\oc_pages_prod.json

.\docs\odak\diagnostic\scripts\diagnostic-document-intelligence-pages.ps1 `
  -GatewayBaseUrl "http://192.168.20.8:5040" `
  -OutputJson .\docs\odak\diagnostic\reports\di_pages_prod.json
```

Ham JSON çıktılar `docs/odak/diagnostic/reports/` altında saklanır. Bu dokümanın markdown içeriği ölçüm sonrası güncellenir ve `seed-system-diagnostic-report.ps1` ile System klasörüne yüklenir.

### Senaryo listesi

**Operasyon Merkezi**

| Senaryo | UI karşılığı |
| --- | --- |
| `explorer_open` | Workspace gezgini — ilk açılış |
| `explorer_select_board` | Workspace seçimi + board listesi |
| `board_list_open` | Board — liste görünümü |
| `board_kanban_open` | Board — kanban görünümü |
| `profile_open` | İş profili (profile-view) |
| `dashboard_view` | Özet pano |
| `work_item_new` | Yeni iş formu |
| `notifications_inbox` | Bildirimler |
| `admin_scheduled_jobs` | Admin — zamanlanmış işler |
| `admin_ws_defs_shell` | Admin — workspace tanımları kabuk |

**Dokümanlar (Document Intelligence)**

| Senaryo | UI karşılığı |
| --- | --- |
| `di_bootstrap_open` | İlk açılış — bootstrap (önerilen) |
| `di_initial_open` | Eski model — tree + children paralel |
| `di_browse_root` | Kök klasör gezinme |
| `di_browse_folder` | Alt klasör gezinme |
| `di_select_folder` | Klasör seçimi (eski 3 API paralel) |
| `di_permissions_dialog` | İzinler diyalogu |
| `di_open_markdown` | Markdown doküman açma |
| `di_search` | Arama |

### Sınırlamalar

- **Tek kullanıcı** ölçümü; eşzamanlı yük testi değildir.
- **Admin token** (`odak_admin`) ile koşulur; grup bazlı filtreleme farklı sonuç verebilir.
- Script süresi **sunucu + ağ** gecikmesini içerir; müşteri istemcisinden farklı olabilir.
- Tarayıcı doğrulaması için DevTools → Network waterfall önerilir.

**Detaylı plan:** `docs/odak/diagnostic/DIAGNOSTIC_PLAN.md` · `docs/odak/diagnostic/README.md`

---

## Son koşu — 6 Haziran 2026 (üretim, OC performans paketi sonrası)

| Alan | Değer |
| --- | --- |
| **Ortam** | Üretim — `http://192.168.20.8:5040` |
| **Ölçüm zamanı (UTC)** | 2026-06-06 ~10:34 |
| **Paket** | OC-PERF-F2b (PV-PERF-4 + dashboard query dedup + UI profil cache) |
| **OC workspace** | (`383e85b7-…`) |
| **OC board** | (`60eb6bef-…`) |
| **Örnek iş** | (`3ace0262-…`) |
| **DI örnek klasör** | `MonitraNG` (`aaf3ab2b-…`) — *DI bu koşuda yeniden ölçülmedi (5 Haz)* |
| **DI örnek doküman** | Kullanıcı Rehberi (`b1566eb7-…`) |
| **Ham JSON** | `oc_pages_prod_post_perf_20260606.json` · DI: `di_pages_prod_20260605_final.json` |

### Özet

| Modül | Senaryo | OK | WARN |
| --- | ---: | ---: | ---: |
| Operasyon Merkezi | 10 | **8** | **2** |
| Dokümanlar | 8 | 5 | 3 *(5 Haz — değişmedi)* |

### Operasyon Merkezi

| Sayfa | Cold (ms) | Warm P95 (ms) | Hedef (ms) | Durum |
| --- | ---: | ---: | ---: | --- |
| Workspace explorer — ilk açılış | 442 | 375 | 1200 | ✅ OK |
| Explorer — workspace + board | 670 | 709 | 900 | ✅ OK |
| Board — liste görünümü | 602 | 697 | 1200 | ✅ OK |
| Board — kanban | 2035 | 2057 | 3500 | ✅ OK |
| **İş profili** | 8841 | **1694** | 1800 | ✅ OK |
| **Özet pano** | 2416 | **2047** | 1200 | ⚠️ WARN |
| Yeni iş formu | 615 | 696 | 2000 | ✅ OK |
| Bildirimler | 1224 | 1660 | 1500 | ⚠️ WARN |
| Admin — zamanlanmış işler | 1297 | 1294 | 2500 | ✅ OK |
| Admin — workspace tanımları | 317 | 310 | 800 | ✅ OK |

**Yorum:** **İş profili warm hedefin altına indi** (5 Haz 2083 ms → 6 Haz **1694 ms**). Günlük board↔profil geçişinde UI önbelleği ek hız sağlar. Pano warm ~2 sn — widget sorguları ağırlıklı; sonraki planlama kapsamı. Profil **session cold** ~8,8 sn — servis restart sonrası ilk istek; günlük warm kullanımda sorun değil.

### Dokümanlar

| Senaryo | Cold (ms) | Warm P95 (ms) | Hedef (ms) | Durum |
| --- | ---: | ---: | ---: | --- |
| Bootstrap açılış | 941 | 958 | 1200 | ✅ OK |
| Eski ilk açılış (tree+children) | 1619 | 1985 | 2000 | ✅ OK |
| Kök browse | 1282 | 963 | 800 | ⚠️ WARN |
| Klasör browse | 1293 | 1301 | 1200 | ⚠️ WARN |
| Klasör seçimi (3 API) | 3167 | 3500 | 2500 | ⚠️ WARN |
| İzinler diyalogu | 927 | 959 | 2000 | ✅ OK |
| Markdown açma | 936 | 977 | 2000 | ✅ OK |
| Arama | 1004 | 1041 | 2500 | ✅ OK |

**Yorum:** `bootstrap` / `browse` tek istek modeli hedef bandında. Eski **3 paralel API** klasör seçimi ~3,5 sn — UI'da `browse` kullanımı önerilir (PERF paketi). DG referans sorguları (dm_resources, permissions, versions) ~330–360 ms.

### Tekil endpoint özeti (Dokümanlar, 1 warm)

| Endpoint | ms |
| --- | ---: |
| bootstrap | 944 |
| browse (kök) | 951 |
| tree | 664 |
| markdown content | 1224 |
| permissions | 1094 |
| search | 937 |

---

## Önceki koşular

### 5 Haziran 2026 (üretim — performans paketi öncesi)

Ham JSON: `oc_pages_prod_20260605_final.json`, `di_pages_prod_20260605_final.json`

| Modül | OK | WARN |
| --- | ---: | ---: |
| Operasyon Merkezi | 8 | 2 (profil, pano) |
| Dokümanlar | 5 | 3 |

Öne çıkan: profil warm **2083 ms** ⚠️ · pano warm **2625 ms** ⚠️

---

## Güncelleme rehberi (ekip içi)

1. Prod gateway ile her iki diagnostic script'i çalıştırın; JSON'u `reports/` altına kaydedin.
2. Bu dosyada **Son koşu** bölümünü güncelleyin; önceki koşuyu **Önceki koşular** altına taşıyın.
3. `seed-system-diagnostic-report.ps1` ile System klasörüne yükleyin.

Müşteriye rapor vermeden önce WARN satırları için kısa yorum eklemeyi unutmayın.
