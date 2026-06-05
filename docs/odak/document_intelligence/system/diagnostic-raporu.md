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

## Son koşu — 5 Haziran 2026 (üretim)

| Alan | Değer |
| --- | --- |
| **Ortam** | Üretim — `http://192.168.20.8:5040` |
| **Ölçüm zamanı (UTC)** | 2026-06-05 ~08:13 |
| **OC workspace** | MonitraNG Geri Bildirim (`d2526f5d-…`) |
| **OC board** | Geri bildirim gönder (`2971e225-…`) |
| **Örnek iş** | `MNG-0001` (`6efb63c3-…`) |
| **DI örnek klasör** | `MonitraNG` (`aaf3ab2b-…`) |
| **DI örnek doküman** | Kullanıcı Rehberi (`b1566eb7-…`) |
| **Ham JSON** | `oc_pages_prod_20260605_final.json`, `di_pages_prod_20260605_final.json` |

### Özet

| Modül | Senaryo | OK | WARN |
| --- | ---: | ---: | ---: |
| Operasyon Merkezi | 10 | 8 | 2 |
| Dokümanlar | 8 | 5 | 3 |

### Operasyon Merkezi

| Sayfa | Cold (ms) | Warm P95 (ms) | Hedef (ms) | Durum |
| --- | ---: | ---: | ---: | --- |
| Workspace explorer — ilk açılış | 456 | 359 | 1200 | ✅ OK |
| Explorer — workspace + board | 638 | 668 | 900 | ✅ OK |
| Board — liste görünümü | 1024 | 674 | 1200 | ✅ OK |
| Board — kanban | 672 | 687 | 3500 | ✅ OK |
| **İş profili** | 12001 | **2083** | 1800 | ⚠️ WARN |
| **Özet pano** | 3312 | **2625** | 1200 | ⚠️ WARN |
| Yeni iş formu | 682 | 636 | 2000 | ✅ OK |
| Bildirimler | 620 | 990 | 1500 | ✅ OK |
| Admin — zamanlanmış işler | 983 | 682 | 2500 | ✅ OK |
| Admin — workspace tanımları | 323 | 334 | 800 | ✅ OK |

**Yorum:** Explorer ve board akışları hedef altında. İş profili warm ~2,1 sn (hedef 1,8 sn — sınırda aşım). Pano warm ~2,6 sn — widget sorguları ağırlıklı; MO tarafında agregasyon/cache iyileştirmesi değerlendirilebilir. Profil **session cold** 12 sn — ilk açılışta JIT/önbellek ısınması; warm ölçüm müşteri deneyimine daha yakındır.

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

*(İlk yayın — önceki kayıt yok.)*

---

## Güncelleme rehberi (ekip içi)

1. Prod gateway ile her iki diagnostic script'i çalıştırın; JSON'u `reports/` altına kaydedin.
2. Bu dosyada **Son koşu** bölümünü güncelleyin; önceki koşuyu **Önceki koşular** altına taşıyın.
3. `seed-system-diagnostic-report.ps1` ile System klasörüne yükleyin.

Müşteriye rapor vermeden önce WARN satırları için kısa yorum eklemeyi unutmayın.
