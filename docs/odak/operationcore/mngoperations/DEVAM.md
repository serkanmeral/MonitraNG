# MngOperations & Operation Core UI — Devam noktası (checkpoint)

**Son güncelleme:** 1 Haziran 2026 (**OC-EDIT** — profil ekranında **yerinde düzenleme** (Düzenle/Kaydet/İptal, diff'li patch), liste actions edit butonu kaldırıldı; **readonly etiketler artık renkli chip**; **profil açılış skeleton paneli**; **labels→op_tags expand bug fix** (DG `GetById` expand=false). **Tümü `main`'e commit+push edildi.** Backend (expand fix dahil) Odak'ta canlı; **UI deploy hâlâ bekliyor** — PROF-VIEW + OC-PREV + OC-ACT + OC-TAGS + OC-EDIT birlikte.)  
**Durum:** SW **SW-0…SW-6** ✅ · A1 R-Plus ✅ · **SLA-0/1/2** ✅ · **D1 Board admin** ✅ · **BL** ✅ · **BO (+BO-5/6)** ✅ · **BLF (+BLF-8/9/10)** ✅ · **BLC** ✅ · **FC** ✅ · **NP (+NP-7)** ✅ · **E1-P1/W-CREATE/E1-P2** ✅ · **CC** ✅ · **A** ✅ · **F (+F-T2/F-K)** ✅ · **BL-GRP (+BL-GRP-2)** ✅ · **PERF** ✅ · **PROF-VIEW** ✅ · **OC-ACT** ✅ · **OC-TAGS** ✅ · **OC-EDIT** ✅ (backend canlı, UI bekliyor) · **PV-PERF** ✅ (warm 14→8 DG çağrısı, ~4.5→~2.5sn; backend canlı, commit bekliyor) — diğerleri Odak'ta canlı

> **⭐ KALDIĞIMIZ YER (1 Haz ~22:15) — yeni chat buradan devam edecek:** Bu chat'te **Widgets & Dashboards D-A (viewer)** tamamlandı (aşağıdaki "DASH-DA" bölümü). Mimari onaylandı: **birleşik model** — `op_dashboards.widgets[]` query-tabanlı widget tanımları (MO server-side çalıştırır), `op_dashboards.layout` = template builder'ın **rows/cols responsive grid** yapısı (`col.widgetId` → widget `key`). **Şema değişikliği GEREKMEDİ** (op_dashboards zaten layout/widgets/scope/workspaceId/permissions/isActive içeriyor; DashboardRecord + `GET /runtime/dashboards/{id}` + 5 named query `wi_*` hazırdı). **Backend (MO, davranış+derleme OK 0/0):** `DashboardRuntimeContext`'e **Catalogs + People + Groups** eklendi; `RuntimeContextService.Dashboard.cs` widget item'larından tek seferde person/grup adı + workspace kataloğu çözüyor (board context deseni) → list/summary ham id yerine ad/renk gösterir. **UI (yeni):** tipler (`OcDashboard*`), servis `ocGetDashboard` + `ocListDashboardsForWorkspace` (+`OC_DATASETS.dashboards`), viewer sayfa `pages/apps/operation-core/dashboards/[id]/index.vue`, özyinelemeli `OcDashboardGrid.vue` (rows/cols+nested+responsive span; layout yoksa tip-bazlı varsayılan grid), `OcDashboardSummaryCard`/`OcDashboardListWidget`/`OcDashboardWidget` (wrapper; chart=placeholder, D-C), workspace hub'a **"Panolar"** bölümü, i18n (tr/en). Seed (`seed-operation-core-demo.ps1` [11]) zaten op_dashboards + 4 widget + smoke test yapıyordu; **layout'u rows/cols formatına güncelledim** (3 özet kart satırı + tam genişlik list). **Lokal dev'de pano açıldı ve doğrulandı** (tek `<template v-for>` key derleme hatası çıktı → `v-for` doğrudan `v-list-item`'a alınıp CSS border ile çözüldü). **COMMIT + PUSH + DEPLOY TAMAM:** D-A `0ce72fb` (OC), DI WIP `a5fb29b` (kullanıcı kararıyla dahil edildi: dosya önizleme + kaynak izinleri), `main`'e push'landı; **`mngui + mngoperations` Odak'a `--no-cache` deploy edildi (1 Haz ~22:57, healthy — `gateway=200 ui=200 oc_live=200`).** *(`appsettings.Development.json` (gizli) + alakasız docs commit edilmedi.)* **Not:** Odak DG'deki mevcut demo op_dashboards kaydı eski `{columns:2}` layout'unu taşıyor → viewer varsayılan grid'e düşerek yine düzgün render eder; yeni rows/cols layout'u için seed'i güncellemek (Find-OrCreate SKIP ettiği için PUT/elle) gerekir, kritik değil. **Sıradaki:** **D-B** (drag&drop admin editor — LayoutEditor uyarlama + yazma yolu kararı) / **D-C** (chart + MO agregasyon ucu). *(Aşağıdaki PV-PERF notu önceki durumdur.)*
>
> **KALDIĞIMIZ YER (1 Haz ~20:20):** Bu chat'te **profile-view perf optimizasyonu (PV-PERF)** yapıldı. Kullanıcı Postman'de `profile-view`'in ~10-14sn sürmesini bildirdi; `PerfDiagnostics` (`OC_PERF`) ile ölçüldü. **Kök neden:** her DG çağrısı sunucu-içi bile **~300ms** (DG'nin per-request tabanı) + DG eşzamanlı istekleri **fiilen seriye düşürüyor** (8 paralel ≈ 8 sıralı; DG CPU 1→4 yükseltmek düzeltmedi → CPU değil, muhtemel Mongo pool/lock). profile-view **warm'da 14 DG çağrısı** yapıyordu → ~4.5sn. **Yapılan (MO-tarafı, davranış birebir):** (1) **work item dedup** — `LoadWorkItemAsync` 5×→1× (yüklenen `workItem` `GetProfileAsync`/`GetFormEditAsync`/`GetTimelineAsync`/`ResolveActivityChangesAsync` private overload'larına geçiriliyor); (2) **policy cache** — `ResolveProfilePolicyAsync` artık `op_rules`/`op_sla_policies`'i doğrudan DG yerine cache'li `GetRulesForWorkspaceAsync` + yeni `GetSlaPoliciesForWorkspaceAsync`'ten okuyor (warm'da 2 DG çağrısı → 0). **Sonuç: warm DG çağrısı 14→8, süre ~4.5sn→~2.5sn.** Backend `--no-cache` deploy'lu, `PerfDiagnostics` geri **false**, CPU override'ları geri alındı. **UI'da gerçek kullanım kabul edilebilir gecikmeyle açılıyor** (kullanıcı doğruladı); Postman'deki ~9sn, çağrı arası 120sn TTL dolup her çağrının yeniden soğumasıydı (gerçek kullanımda sorun değil). **Açık kalanlar:** (a) **Soğuk cache** ilk çağrı hâlâ ~10sn (26-28 DG çağrısı tüm metadata'yı yükler) — metadata cache TTL'i (120sn) artırmak soğuk vuruşları seyrekleştirir (admin metadata bayatlık tradeoff'u, kullanıcıyla konuşulacak); (b) **DG per-request ~300ms + eşzamanlı serileştirme** asıl tavan — DG-tarafı (Mongo pool/concurrency) ayrı, geniş kapsamlı iş; (c) opsiyonel MO küçültme: `op_links` 2→1 ($or) + `op_tags` 2→1. **Bu PV-PERF değişiklikleri henüz commit/push EDİLMEDİ.** ⚠️ Ayrıca **UI deploy hâlâ bekliyor** (PROF-VIEW+OC-PREV+OC-ACT+OC-TAGS+OC-EDIT birlikte).
>
> **KALDIĞIMIZ YER (1 Haz ~10:15):** Bu chat'te **OC-EDIT** tamamlandı (aşağıdaki bölüm): **(1)** Profil ekranı artık **yerinde düzenlenebilir** — header'da **Düzenle** butonu (canEdit ise), tıklayınca form `readonly`→edit moduna geçer; **Kaydet** yalnızca değişen alanları diff'leyip (`collectFormChanges`) `ocUpdateWorkItem` ile patch'ler, **İptal** snapshot'a döner; düzenleme modunda transition butonları gizlenir. `useOcDynamicFormLookups` artık `readonly` değişimini izleyip select/person/relation listelerini edit'e geçince yükler. **(2)** Liste (board) actions sütunundaki **edit butonu kaldırıldı** (`openEditDialog` temizlendi). **(3)** **Readonly profilde etiketler renkli chip** — `OcDynamicFormField` tags'i readonly metinden çıkardı; `OcTagSelector` `disabled` iken combobox yerine renkli `v-chip` listesi basıyor (boşsa "—"). **(4)** **Profil açılış skeleton paneli** — `loading` sırasında boş ekran yerine form+yan panel yerleşimini taklit eden `v-skeleton-loader` + "Profil yükleniyor…". **(5)** **labels→op_tags expand bug fix** — DG `GetById` varsayılan `expand=true` olduğundan çekirdek `labels` alanını eski `op_labels`'a expand edip `op_tags` id'lerini düşürüyordu (readonly'de "—" + patch'te veri kaybı riski). `IMngDataGatewayClient.GetByIdAsync`'e `expand` param eklendi; iş kaydı okumaları (`RuntimeContextService.LoadWorkItemAsync` + `WorkItemCommandService.LoadWorkItemAsync`) **`expand:false`** ile çağrılıyor → ham id'ler MO'da `op_tags` ile çözülür. Backend `--no-cache` deploy edildi (`oc_live=200`), kullanıcı local dev (Odak backend'e proxy) ile renkli chip + skeleton'ı doğruladı. **Tüm birikmiş OC işi (OC-PREV+PROF-VIEW+OC-ACT+OC-TAGS+OC-EDIT) tek commit ile `main`'e push edildi.** ⚠️ **Sıradaki iş:** **UI deploy (5'i birlikte)** Odak'a (`deploy-odak-apps.ps1 -Services mngui -NoCache`) → tarayıcıda son kontrol; ardından **Widgets & Dashboards** planlama.
>
> **KALDIĞIMIZ YER (1 Haz ~08:35):** Bu chat'te **OC-TAGS** tamamlandı (aşağıdaki bölüm): **workspace'e ait** etiket kataloğu `op_tags` (her kayıt `workspaceId`, unique `(workspaceId,name)`). **Yönetim 2 yoldan:** (1) Workspace Tanımları → yeni **"Etiketler"** sekmesi (CRUD: ad+renk+açıklama); (2) iş kaydı formunda **`tags` alanına yazıp Enter → inline yeni etiket** o workspace'e kaydedilir. Backend profil+aktivite çözümünde `fieldType:'tags'` → `op_tags` id→ad çözer. **`op_tags` Odak DG'ye provizyonlandı** (`setup-operation-core-datasets.ps1` + generator/`$order`), **backend `--no-cache` deploy edildi** (`oc_live=200`), **OC Demo Workspace'e (`f414462a…`) 5 başlangıç etiketi eklendi** (Acil/Donanım/Yazılım/Ağ/Tekrarlayan). ⚠️ **UI deploy + commit/push HENÜZ YAPILMADI** — **PROF-VIEW + OC-PREV + OC-ACT + OC-TAGS UI değişiklikleri birlikte** deploy bekliyor. Lokal `dotnet build` + `nuxt build` temiz. **Sıradaki iş:** UI deploy (4'ü birlikte) + tarayıcıda kontrol (Etiketler sekmesi, form inline tag, profil/aktivitede etiket adı); ardından Widgets & Dashboards planlama.
>
> **KALDIĞIMIZ YER (1 Haz ~08:05):** Bu chat'te **OC-ACT** tamamlandı (aşağıdaki bölüm) + **OC-ACT-4 bug fix** (katalog alanları — state/priority/type/board — `$in` query yerine cache'li `GetCatalogListAsync` ile çözülür; transition `Durum: guid → guid` sorunu giderildi). Aktivite sekmesi artık alan bazlı değişiklikleri (`Etiket: eski → yeni`, kim/ne zaman) gösteriyor: `WorkItemCommandService` güncelleme/geçişte `op_activities.changes[]`'e **ham id/scalar diff** yazar; `RuntimeContextService.Timeline.cs` bunları **read-time** katalog(cache)/dizin/relation ile görünen metne çözer (changes yoksa metadata yüklenmez). **Backend Odak'a 2 kez `--no-cache` deploy edildi (1 Haz ~08:01, healthy — `oc_live=200`).** ⚠️ **UI deploy + commit/push HENÜZ YAPILMADI** — kullanıcı UI'yi sonra deploy edecek; **PROF-VIEW + OC-PREV + OC-ACT UI değişiklikleri birlikte** deploy bekliyor. Lokal `dotnet build` + `nuxt build` temiz. ⚠️ **Deploy dersi:** kritik backend fix sonrası **`docker compose build --no-cache <servis>`** (`deploy-odak-apps.ps1 -NoCache`). **Sıradaki iş (konuşulacak):** UI deploy (PROF-VIEW + OC-PREV + OC-ACT birlikte) + tarayıcıda kontrol; ardından Widgets & Dashboards planlama. *(PROF-VIEW/OC-PREV/OC-CMT bölümleri tarihsel.)*
>
> **KALDIĞIMIZ YER (1 Haz ~07:30):** **PROF-VIEW** tamamlandı: profil ekranının ~13sn / ~18 isteklik yükü **tek toplu `GET /runtime/work-items/{id}/profile-view`** ucuna indirgendi (≈4sn). MO; profile + edit form + katalog + pool alan + **alan görünen değerleri** + **çözülmüş politika** + ilk sayfa timeline'ı **paralel** üretip tek pakette döner. Readonly form seçim listesi yüklemez, `OcPolicyPanel` `resolvedPolicy` ile fetch yapmaz, `useOcBoardListLookups` `catalogsSource`'tan beslenir. **Backend `--no-cache` deploy edildi (healthy — `gateway=200 ui=200 oc_live=200`).** Mevcut `profile`/`form`/`timeline` uçları korundu. *(tarihsel)*
>
> **KALDIĞIMIZ YER (31 May ~23:20):** Bu chat'te yapılanlar Odak'ta canlı: **BL-KB** (Keeper by-ids + Redis), **Faz-4/B** (RuntimeContextService partial + operationCoreService barrel — davranış birebir), **Faz-4/A** (Kanban kolonlarına "daha fazla yükle"). Tüm değişiklikler `main`'e push edildi (`f0f64cc`, `5c1d7fb`, `f98889b`, `90374ce`). **Birleşik manuel kontrol rehberi** oluşturuldu: [KONTROL_REHBERI_2026-05-31.md](KONTROL_REHBERI_2026-05-31.md). Kullanıcı sunucudaki (Odak) web UI üzerinden kontrole başladı ve **kritik bir config bug** buldu+düzeltti: **UI nginx'te `/api/operations/` proxy bloğu hiç yokmuş** → tüm OC runtime çağrıları (board liste POST, profil, workspace genel, form katalog enrichment) `try_files index.html`'e düşüyordu (405 / boş tab / ham id). `Mng.Ui/nginx.conf`'a `/api/operations/` → `mngoperations:5086/api/` bloğu eklendi, `mngui` deploy edildi (~23:10), doğrulandı (`/api/operations/v1/health/live` → 200 JSON). **nginx.conf fix'i commit+push edildi (`000e624`).** **Sıradaki:** (1) ✅ `nginx.conf` fix'i commit+push edildi; (2) kontrol rehberindeki maddeleri (board liste B/C/D, profil, NP-7, gruplar, Faz-4) sunucu UI'sinde tek tek doğrula; kullanıcı yeni gözlemler/bug'lar yazacak → birlikte fix. *(Aşağıdaki uzun "Kaldığımız yer" paragrafı önceki ara durumdur, tarihsel.)*
>
> **Kaldığımız yer (31 May, önceki):** Biriken backlog (F-T2/F-K, BLF-10, NP-7, BL-GRP, BO-5/6, BL-GRP-2) **`mngoperations`+`mngui` Odak'a deploy edildi** (31 May ~02:28, healthy — `gateway=200 ui=200`, SLA-1 smoke yeşil OCD-0065). Ardından **grup alan filtresi (BL-GRP-3)** de yapıldı ve `mngui` deploy edildi (31 May ~02:41, `ui=200`). Tüm biriken işler Odak'ta canlı; değişiklikler `main`'e **commit + push** edildi. Ardından **Keeper `by-ids` toplu uç + Redis profil cache (BL-KB)** yapıldı (User/Group `POST by-ids`; MO dizin servisleri tek istekte çözer, N+1 giderildi; Keeper'da `IDirectoryCache` Redis cache + CRUD invalidation) ve **`mngkeeper`+`mngoperations` Odak'a deploy edildi** (31 May ~03:04, healthy). Ardından **Faz-4 / dosya bölme (B)** kısmen yapıldı (davranış birebir aynı): `RuntimeContextService.cs` 1549→1015 satır + 3 `partial` dosya (MO build 0/0); `operationCoreService.ts` 2324→2025 satır, leaf domain'ler (notifications/rules/sla/schedules) `services/operationCore/` altına barrel ile taşındı (`nuxt build` temiz). Henüz commit/deploy **yapılmadı**. Sıradaki: kalan TS domain'leri (opsiyonel) ve/veya tablo sanallaştırma (ihtiyaç doğunca).

**Ana plan:** [OC_UI_ADMIN_FAZ1_PLAN.md](../ui/OC_UI_ADMIN_FAZ1_PLAN.md) · **Perf detay:** [PERF_OPTIMIZATION.md](PERF_OPTIMIZATION.md) · **Bu oturum kontrol rehberi:** [PERF_KONTROL_REHBERI.md](PERF_KONTROL_REHBERI.md)

---

## PV-PERF — profile-view DG çağrı azaltma (work item dedup + policy cache) (1 Haz)

`GET /runtime/work-items/{id}/profile-view` ucu Postman'de ~10-14 sn (soğuk) / ~4.5 sn (sıcak) sürüyordu. `PerfDiagnostics` (`OC_PERF`) bayrağı açılıp ölçülerek darboğaz tespit edildi ve MO-tarafı DG çağrı sayısı düşürüldü. **UI'da gerçek kullanım kabul edilebilir gecikmeyle açılıyor** (UI tek toplu çağrı + cache sıcak); endpoint düzeyinde de iyileştirme yapıldı.

### Ölçümle tespit edilen kök neden
- **Her DG çağrısı sunucu-içi (MO→DG, tek tek) bile ~300ms** — DG'nin per-request işleme tabanı (ağ değil; `docker exec mngoperations curl …` ile doğrulandı: 0.30-0.33 sn).
- **DG eşzamanlı istekleri fiilen seriye düşürüyor** — 8 paralel `getById` ≈ 8 sıralı (wall ~2.7 sn). DG CPU 1.0→4.0 yükseltmek **düzeltmedi** → CPU-bound değil, muhtemel Mongo connection-pool/lock. *(Bu DG-tarafı darboğaz tüm servisleri etkiler; ayrı/geniş kapsamlı iş, dokunulmadı.)*
- profile-view **warm'da 14 DG çağrısı** yapıyordu (DG seri işlediği için çağrı sayısı ≈ süre). En büyük israf: aynı work item **5× `getById`**.
- **Postman'de "sonraki run'lar 9 sn"** gözlemi: çağrılar arası **metadata cache TTL'i (120 sn)** dolunca her çağrı yeniden soğuyor. Hızlı ardışık çağrıda kötüleşme yok (run 1 ~4.5sn ısınma, run 2-8 ~2.5sn). Bağlantı sızıntısı/birikme **yok**.

### Yapılanlar (MO-tarafı, davranış birebir korundu)

| Kod | Durum | Not |
|-----|--------|-----|
| **PV-PERF-1** | ✅ | **Work item dedup** — `LoadWorkItemAsync` profile-view içinde **5×→1×**. Yüklenen `workItem` artık `GetProfileAsync`/`GetFormEditAsync`/`GetTimelineAsync`/`ResolveActivityChangesAsync` için eklenen **private overload'lara** geçiriliyor (public metotlar standalone kullanımda kendi `LoadWorkItemAsync`'ını korur). → warm 14→10 çağrı. |
| **PV-PERF-2** | ✅ | **Policy cache** — `ResolveProfilePolicyAsync` `op_rules`+`op_sla_policies`'i doğrudan `_dg.GetAsync` yerine **cache'li** `IMetadataCache.GetRulesForWorkspaceAsync` + yeni **`GetSlaPoliciesForWorkspaceAsync`** (TTL'li, `ResolveSlaPolicyAsync` ile aynı `sla:policies:{ws}` cache anahtarını paylaşır) üzerinden tipli `RuleRecord`/`SlaPolicyRecord` olarak okur. Eşleşme mantığı (scopeMatches, snapshot→derived) birebir aynı. → warm'da 2 DG çağrısı **0**'a düşer (10→8). |
| **PV-PERF-3** | ✅ | **Önceki retry fix korundu** — `DataGatewaySettings`'e `TimeoutSeconds=30`/`RetryCount=3`/`RetryBaseDelayMs=200`; Polly backoff sn→ms (200/400/800ms ≈ 1.4sn), HttpClient timeout 120→30sn. Tek başarısız çağrının profili 14sn bloke etmesi engellendi. |

### Sonuç (warm, `OC_PERF` ölçümü)

| Aşama | Warm DG çağrısı | Warm süre |
|-------|-----------------|-----------|
| Başlangıç | 14 | ~4.5 sn |
| + PV-PERF-1 (dedup) | 10 | ~3.5 sn |
| + PV-PERF-2 (policy cache) | **8** | **~2.5 sn** |

Kalan 8 warm çağrı: `op_tags` ×2, `op_links` ×2, `op_work_items` getById ×1, `op_comments` ×1, `op_work_item_timelines` ×1, `op_activities` ×1.

**MO değişen:** `Services/RuntimeContextService.cs` (`GetProfileAsync`/`GetTimelineAsync` load↔core ayrımı + private overload), `Services/RuntimeContextService.Form.cs` (`GetFormEditAsync` private overload), `Services/RuntimeContextService.Timeline.cs` (`ResolveActivityChangesAsync` `workItem` parametresi), `Services/RuntimeContextService.ProfileView.cs` (overload'lara `workItem` geçişi + `ResolveProfilePolicyAsync` cache'li tipli kayıtlar + `MapSlaPolicy(SlaPolicyRecord)`; kullanılmayan `ReadDouble` kaldırıldı), `Services/MetadataCacheService.cs` (+`GetSlaPoliciesForWorkspaceAsync`, `ResolveSlaPolicyAsync` onu kullanır), `Interfaces/IMetadataCache.cs` (+imza). *(Retry fix: `Configuration/MngOperationsSettings.cs`, `ServiceRegistration.cs`, `Clients/MngDataGatewayClient.cs`, `appsettings.json`.)*
**Deploy:** MO build temiz (0/0) · backend `--no-cache` deploy'lu ve canlı · `PerfDiagnostics` ölçüm sonrası geri **false** · geçici CPU override'ları (`docker-compose.odak.yml`) geri alındı.

⚠️ **Bu PV-PERF değişiklikleri henüz commit/push EDİLMEDİ.**

### Açık kalanlar (kullanıcı kararı)
- **(a) Soğuk cache** ilk çağrı ~10 sn (26-28 DG çağrısı tüm metadata'yı yükler). Metadata cache TTL'i (`MetadataCache.TtlSeconds=120`) artırmak (örn. 600-900sn) soğuk vuruşları seyrekleştirir → admin metadata bayatlık tradeoff'u. **UI'da gerçek kullanım kabul edilebilir olduğu için düşük öncelik.**
- **(b) DG per-request ~300ms + eşzamanlı serileştirme** asıl tavan — DG-tarafı (Mongo pool/concurrency), geniş kapsamlı/riskli ayrı iş.
- **(c) Opsiyonel ek MO küçültme:** `op_links` 2→1 ($or) + `op_tags` 2→1 → ~8'den ~6 çağrıya (~1.9sn). Relation-collapse davranışıyla etkileşim riski not edildi.

---

## OC-EDIT — Profil yerinde düzenleme + readonly etiket chip + skeleton + labels expand fix (1 Haz)

Profil ekranı salt-okunur olmaktan çıkıp **yerinde düzenlenebilir** hale geldi; readonly etiketler renkli chip'e, açılış da skeleton panele kavuştu; `labels→op_tags` görünürlük bug'ı kökten çözüldü.

| Kod | Durum | Not |
|-----|--------|-----|
| **OC-EDIT-1** | ✅ | **Yerinde düzenleme** — `profile/index.vue`: header'da **Düzenle** (canEdit), `editMode` ile `OcDynamicForm` `readonly` toggle. `startFormEdit`/`cancelFormEdit` (snapshot `initialModel`), `collectFormChanges` diff → `buildUpdateWorkItemRequest`+`ocUpdateWorkItem` ile **yalnızca değişen alanlar** patch'lenir; `collectOcFormValidationIssues` ile validasyon (özet + alan hataları). Edit modunda transition butonları gizli; Kaydet/İptal header + form altında. |
| **OC-EDIT-2** | ✅ | **Lookup reload** — `useOcDynamicFormLookups` `watch` bağımlılığına `options.readonly` eklendi: readonly profil edit'e geçince select/person/relation listeleri o anda yüklenir (readonly iken `reload()` erken dönüyordu). |
| **OC-EDIT-3** | ✅ | **Liste edit butonu kaldırıldı** — `boards/[boardId]/index.vue` actions sütunundaki `mdi-pencil-outline` butonu + `openEditDialog` temizlendi (düzenleme artık profilden). |
| **OC-EDIT-4** | ✅ | **Readonly etiket = renkli chip** — `OcDynamicFormField` `useReadonlyDisplay`'den tags çıkarıldı (artık düz metne düşmez); `OcTagSelector` `disabled` (profil readonly) iken combobox yerine **renkli `v-chip` listesi** render eder (`displayChips`=id→ad+renk; boşsa "—"). |
| **OC-EDIT-5** | ✅ | **Açılış skeleton** — `profile/index.vue` `loading` sırasında boş ekran yerine sol(form alanları+tab chip)+sağ(SLA/policy) yerleşimini taklit eden `v-skeleton-loader` paneli + "Profil yükleniyor…"; i18n `operationCore.profile.loadingHint`. |
| **OC-EDIT-6 (bug)** | ✅ | **labels→op_tags expand fix** — DG `GetById` varsayılanı `expand=true`; çekirdek `labels` (artık `op_tags` id tutuyor) eski `op_labels`'a expand edilince id'ler düşüyordu → readonly'de "—" + sonraki patch'te boş labels geri yazılıp **veri kaybı riski**. `IMngDataGatewayClient.GetByIdAsync` + `MngDataGatewayClient`'e `bool expand=true` param (`?expand=false` URL); `RuntimeContextService.LoadWorkItemAsync` + `WorkItemCommandService.LoadWorkItemAsync` **`expand:false`** ile çağrılır → ham id MO'da `op_tags` ile çözülür. Backend `--no-cache` deploy (`oc_live=200`); local dev (Odak backend proxy) ile doğrulandı. |

**MO değişen:** `Interfaces/IMngDataGatewayClient.cs` (+`expand`), `Clients/MngDataGatewayClient.cs` (`?expand=false`), `Services/RuntimeContextService.cs` + `Services/WorkItemCommandService.cs` (`LoadWorkItemAsync` → `expand:false`).
**UI değişen:** `pages/.../work-items/[id]/profile/index.vue` (editMode + skeleton), `composables/useOcDynamicFormLookups.ts` (readonly watch), `pages/.../boards/[boardId]/index.vue` (edit butonu kaldırıldı), `components/.../OcDynamicFormField.vue` (tags readonly çıkışı), `components/.../OcTagSelector.vue` (readonly chip), `utils/locales/{tr,en}.json` (`profile.edit.*` + `profile.loadingHint`).
**Deploy:** MO build temiz · Nuxt build temiz · backend `--no-cache` deploy (expand fix) canlı · **UI deploy bekliyor** (PROF-VIEW+OC-PREV+OC-ACT+OC-TAGS+OC-EDIT birlikte).

---

## OC-TAGS — Workspace-kapsamlı etiket kataloğu (op_tags) (1 Haz)

Pool `tags` alan tipi artık **workspace'e ait `op_tags` kataloğundan** beslenir. Kararlar: yeni `op_tags` dataset'i (op_labels değil) — pool `tags`'a adanmış, her kayıt **`workspaceId` zorunlu**; iş kaydı formunda **inline oluşturma** (yazıp Enter → o workspace'e kaydedilir); admin tarafında ayrı **"Etiketler"** sekmesi.

| Kod | Durum | Not |
|-----|--------|-----|
| **OC-TAGS-1** | ✅ | **DG dataset** — `op_tags` (`name` zorunlu, `workspaceId` zorunlu relation, `color`, `description`; index `idx_workspaceId` + unique `idx_workspaceId_name`). Generator `build-operationcore-datasets-draft.mjs`'e `buildOpTagsDataset()` + append; `setup-operation-core-datasets.ps1` `$order`'a `op_tags`. **Odak DG'ye provizyonlandı** (`op_tags OK`). |
| **OC-TAGS-2** | ✅ | **Backend çözüm** — `OcDatasets.Tags="op_tags"`; `RuntimeContextService.ProfileView.cs` + `.Timeline.cs` `fieldType=='tags'` → `op_tags` (relation gibi `$in` ile id→ad). UI ham id görmez; PROF-VIEW/OC-ACT ile aynı kanal. |
| **OC-TAGS-3** | ✅ | **UI servis/tip** — `services/operationCore/tags.ts` (`ocListTagsForWorkspace`/`ocCreateTag`/`ocUpdateTag`/`ocDeleteTag`), `OpTag` tipi, `OC_DATASETS.tags`, barrel export. |
| **OC-TAGS-4** | ✅ | **Form widget** — `ocDynamicFormField.ts` `tags` ayrı widget; `OcTagSelector.vue` (`v-combobox`, çoklu chip, ad↔id eşleme, **inline create** → `ocCreateTag`). `OcDynamicFormField.vue` tags dalı + readonly'de fieldDisplay metni (lookup yok), `OcDynamicForm.vue` `workspaceId` geçer. |
| **OC-TAGS-5** | ✅ | **Admin sekmesi** — `OcWorkspaceDefinitionsTagsTab.vue` (CRUD: ad+renk+açıklama), `useOcWorkspaceDefinitionTabs` keys + `workspace-definitions/index.vue` (ikon+render), tr/en i18n (`operationCore.tags.*`, `tabs.tags`). |
| **OC-TAGS-6** | ✅ | **Seed** — OC Demo Workspace (`f414462a…`): Acil(error)/Donanım(warning)/Yazılım(info)/Ağ(primary)/Tekrarlayan(secondary). |
| **OC-TAGS-7** | ✅ | **labels→op_tags birleştirme (bug fix)** — formdaki yerleşik "Etiketler" (çekirdek `labels`) alanı `op_labels`'ı okuyordu (boş) → kullanıcının `op_tags`'a tanımladığı etiketler görünmüyordu (i18n'de hem `labels` hem pool `tags` "Etiketler"). Çözüm: çekirdek `labels` artık **workspace `op_tags`** kullanır. Backend `CoreRelationDatasets["labels"]=op_tags` (ProfileView+Timeline). UI: `OC_CORE_RELATION_DATASET.labels='op_tags'`, `resolveOcDynamicFieldWidget` `labels`→`tags` widget (OcTagSelector, inline create), `useOcDynamicFormLookups` tags widget'larını relation yüklemesinden atlar. `op_labels` zaten boştu → veri kaybı yok. Backend `--no-cache` deploy (`oc_live=200`). **UI deploy bu fix için şart.** |

**MO değişen:** `Domain/Constants/OcDatasets.cs`, `Services/RuntimeContextService.ProfileView.cs`, `Services/RuntimeContextService.Timeline.cs`.
**UI değişen:** `services/operationCore/tags.ts` (yeni), `services/operationCoreService.ts` (`OC_DATASETS.tags`+export), `types/apps/operationCore.ts` (`OpTag`), `utils/ocDynamicFormField.ts`, `components/.../OcTagSelector.vue` (yeni), `components/.../OcDynamicFormField.vue`, `components/.../OcDynamicForm.vue`, `components/.../workspace-definitions/OcWorkspaceDefinitionsTagsTab.vue` (yeni), `composables/useOcWorkspaceDefinitionTabs.ts`, `pages/.../admin/workspace-definitions/index.vue`, `utils/locales/{tr,en}.json`.
**Provizyon/script:** `docs/odak/operationcore/scripts/build-operationcore-datasets-draft.mjs` + `…/setup-operation-core-datasets.ps1` + regenerated `operationcore_datasets_phase1_draft_2026-05-26.json`.

⚠️ **UI deploy bekliyor** (PROF-VIEW + OC-PREV + OC-ACT + OC-TAGS birlikte). Lokal build'ler temiz.

---

## OC-ACT — Aktivite alan değişiklik satırları (op_activities changes[]) (1 Haz)

Aktivite sekmesi artık her güncelleme/durum geçişinde **alan bazlı değişiklikleri** (ör. `Öncelik: Düşük → Yüksek`, `Atanan: Ali → Veli`) kim/ne zaman bilgisiyle gösteriyor. **Ham id/scalar yazılır; id→ad çözümü MO'da (read-time) yapılır** (UI ham veri işlemez — PROF-VIEW felsefesiyle aynı). Kararlar: tüm alanlar (çekirdek + pool) izlenir; çözüm read-time; durum geçişi de `Durum: X → Y` satırı.

| Kod | Durum | Not |
|-----|--------|-----|
| **OC-ACT-1** | ✅ | **Yazım — ham diff** — `WorkItemCommandService.PatchAsync` `existing` vs `updated` farkını `CollectPatchFieldKeys(request)` alanlarında hesaplar; değişen her alan için `changes:[{field,from,to}]` (ham id/scalar) aktiviteye yazılır (`BuildChangeActivityExtra`/`AppendFieldChanges`/`NormalizeChangeValue`; değişiklik yoksa eklenmez, davranış korunur). `ApplyTransitionAsync` aktivitesine `{field:"stateId",from,to}` + geçişte düzenlenen dinamik alanlar eklenir. |
| **OC-ACT-2** | ✅ | **Okuma — read-time çözüm** — `RuntimeContextService.Timeline.cs` `ResolveActivityChangesAsync`: changes içeren aktivite varsa form (etiket/tür) + pool (relationDatasetName) yüklenir; her alan kind'ine göre person/grup dizini ya da dataset (`ResolveRelationNamesAsync`, çekirdek katalog/relation + `typeId`→`op_work_item_types`) ile çözülür. `TimelineEntryDto.Changes` = `[{field,label,fieldType,fromDisplay,toDisplay}]`. **Perf:** changes yoksa metadata hiç yüklenmez (yorum yenilemede ek maliyet yok). |
| **OC-ACT-3** | ✅ | **UI** — `OcTimelineChange` tipi + `OcTimelineEntry.changes`; `operationCoreService.mapTimelineEntry` `changes`/`Changes` map'ler. `profile/index.vue` Aktivite satırı: `entry.changes?.length` ise her satır **`Etiket: eski → yeni`** (üstü çizili eski + `mdi-arrow-right-thin` + yeni; boş → "—"); yoksa eski `text`'e düşer. |
| **OC-ACT-4 (bug)** | ✅ | **Katalog alanları id gösteriyordu** — state/priority/type/board (`op_states`/`op_priorities`/`op_work_item_types`/`op_boards`) çözümü relation'larla aynı `ResolveRelationNamesAsync` (`$in` query ucu) üzerinden yapılıyordu; bu kanal **katalog dataset'leri için kayıt döndürmüyor** → ham id'ye düşüyordu (transition `Durum: guid → guid`). Düzeltme: katalog dataset'leri profil-view ile aynı **cache'li `GetCatalogListAsync`** yolundan çözülür (`ResolveChangeDatasetNamesAsync`/`ResolveCatalogNamesAsync`); relation alanlar eskisi gibi `$in`. Backend `--no-cache` redeploy (healthy, `oc_live=200`). |

**MO değişen:** `Contracts/Runtime/ProfileRuntimeContext.cs` (`TimelineChangeDto` + `TimelineEntryDto.Changes`), `Services/WorkItemCommandService.cs` (diff yazımı + helper'lar), `Services/RuntimeContextService.cs` (`GetTimelineAsync` changes entegrasyonu), **yeni** `Services/RuntimeContextService.Timeline.cs` (read-time çözüm).
**UI değişen:** `types/apps/operationCore.ts` (`OcTimelineChange`/`OcTimelineEntry.changes`), `services/operationCoreService.ts` (`mapTimelineChanges`), `pages/.../work-items/[id]/profile/index.vue` (aktivite satırı değişiklik render'ı).
**Deploy:** MO build temiz (0/0) · Nuxt build temiz · backend `--no-cache` deploy (bu adım) · **UI deploy + commit/push kullanıcı isteyince** (PROF-VIEW + OC-PREV ile birlikte bekliyor).

**Açık/ileriki:**
- ⬜ Rule motorunun dolaylı değiştirdiği (patch'te olmayan) alanlar şu an izlenmiyor; gerekiyorsa eklenecek.
- ⬜ Eski (changes'sız) aktiviteler eski `text` satırıyla görünür (geriye dönük dolum yok).
- ⬜ Uzun metin (title/description) eski→yeni değerleri kırpılabilir; tarih alanları biçimlenebilir.

---

## OC-CMT — Yorumlar/Aktivite tab ayrımı + zengin editör + yazar adı çözümü + düzenle/sil (1 Haz)

Profil ekranındaki tek "Aktivite & yorum" sekmesi **Detaylar | Yorumlar | Aktivite | Ekler** olarak ayrıldı. Yorumlar zengin (TipTap) editöre, emoji paletine, tek seviyeli yanıt thread'ine ve **yazarın kendi yorumunu düzenleme/silme** yetkisine kavuştu. Yazar/aktör adları timeline'da doğru çözülür.

| Kod | Durum | Not |
|-----|--------|-----|
| **OC-CMT-1** | ✅ | **Tab ayrımı** — `profile/index.vue` sekmeleri `Detaylar \| Yorumlar[N] \| Aktivite[N] \| Ekler`. `commentThreads` (kök + tek seviye yanıt) ve `activityEntries` (yorum dışı) computed'ları. i18n `operationCore.profile.tabs.*` + `comments.*` + `activity.empty`. |
| **OC-CMT-2** | ✅ | **Zengin editör + emoji + yanıt** — `OcCommentComposer` `v-textarea`→**TipTap** (StarterKit: kalın/italik/üstü-çizili/liste); hafif özel **emoji paleti**; `@`-mention TipTap'e uyarlandı. Yorum gövdesi **HTML** saklanır, render'da **DOMPurify** ile sanitize (client-only). Tek seviye thread (`parentCommentId`), "↳ {ad} kişisine yanıt" etiketi. `dompurify` + `@types/dompurify` eklendi. |
| **OC-CMT-3** | ✅ | **Yazar adı çözümü (2 ayrı bug)** — (a) yorum/aktivite `author`/`actor` artık `MngPersonId` yazılıyor (NP-4/6 ile aynı uzay); (b) **DG, person referans alanını okuma sırasında tam `@users` nesnesine genişletiyor** (`{__dataId, firstName, ...}`) → `GetString` tüm JSON'u string sanıyordu. **`WorkItemDataHelper.GetPersonRefId/GetPersonRefName`** eklendi: düz id veya genişletilmiş nesneden gerçek id'yi çıkarır; `GetTimelineAsync` önce dizinden (BL-KB) ada, olmazsa nesnedeki ad/soyada, en son ham id'ye düşer. Ayrıca `HttpRequestContext.MngPersonId` claim JSON gelirse `__dataId`'yi çıkaracak şekilde savunmacı yapıldı. |
| **OC-CMT-4** | ✅ | **Kendi yorumunu düzenle/sil** — `PUT/DELETE /work-items/{id}/comments/{commentId}`. Yetki **backend-enforced**: `LoadOwnCommentAsync` yorumun iş kaydına ait olduğunu + yazarın (`GetPersonRefId(author)`) geçerli `MngPersonId` ile eşleştiğini doğrular (aksi 404/403). Güncellemede DG PUT tam-değiştirme → mevcut alanlar korunur, `author` düz id'ye normalize edilir, `editedDate` yazılır. UI: kendi yorumunda **Düzenle** (inline TipTap, ek butonu kapalı) / **Sil** (onay dialogu); "(düzenlendi)" etiketi. Timeline'a `ActorId` + `EditedAt`, `OcTimelineEntry`'ye `actorId`/`editedAt`. |

**MO değişen:** `Utilities/WorkItemDataHelper.cs` (`GetPersonRefId`/`GetPersonRefName`), `Services/RuntimeContextService.cs` (`ResolveActor` nesne-farkında + `ActorId`/`EditedAt`), `Services/WorkItemCommandService.cs` (author=MngPersonId + `UpdateCommentAsync`/`DeleteCommentAsync`/`LoadOwnCommentAsync`), `Controllers/WorkItemsController.cs` (PUT/DELETE comment), `Contracts/WorkItems/AddCommentRequest.cs` (`UpdateCommentRequest`), `Interfaces/IWorkItemCommandService.cs`, `Contracts/Runtime/ProfileRuntimeContext.cs` (`ActorId`/`EditedAt`), `Presentation/.../Services/HttpRequestContext.cs` (`MngPersonId` JSON-savunma).
**UI değişen:** `OcCommentComposer.vue` (TipTap + emoji + `initialHtml`/`showCancel`/`allowAttachments`), `profile/index.vue` (tab ayrımı + thread + edit/delete + DOMPurify), `services/operationCoreService.ts` (`ocUpdateWorkItemComment`/`ocDeleteWorkItemComment` + `mapTimelineEntry` actorId/editedAt), `types/apps/operationCore.ts` (`OcTimelineEntry.actorId/editedAt`), locale `tr/en` (`comments.edit/delete/save/edited/deleteTitle/deleteConfirm`), `package.json` (`dompurify`).
**Deploy:** MO build temiz · Nuxt build temiz · **`mngoperations` Odak'a `--no-cache` deploy (healthy, `oc_live=200`)** · `mngui` deploy + commit/push bu adımda.

**Açık/ileriki:**
- ✅ **Ekler (Dosyalar) sekmesi — önizleme** (görsel/PDF/düz metin) → **OC-PREV** (aşağıda).
- ✅ **Aktivite sekmesinde alan değişiklik satırları** (`Öncelik: Düşük → Yüksek`, kim/ne zaman) — `op_activities`'e `changes[]` yazımı → **OC-ACT** (aşağıda).
- ⬜ Düzenlemede mention/ek değişikliği (şimdilik yalnız gövde güncelleniyor).

---

## OC-PREV — Ekler & yorum eklerinde inline önizleme + 2 bug fix (1 Haz)

İş kaydı **Ekler** sekmesindeki ve **yorum eklerindeki** dosyalar artık ortak bir modalda inline önizlenebiliyor (görsel / PDF / düz metin). Önizlenemeyen tipler eskisi gibi yalnız indirilir. **UI-only; backend değişmedi** — mevcut auth'lu `/files/download` blob akışı yeniden kullanıldı.

| Kod | Durum | Not |
|-----|--------|-----|
| **OC-PREV-1** | ✅ | **Önizleme altyapısı** — `utils/ocAttachmentPreview.ts`: `previewKind(att)` (image/pdf/text/none, uzantı `fileExt`→ad sonu), boyut tavanları (görsel/PDF **25MB**, metin **2MB** üstü → indir), `previewMime(att)` (uzantı→MIME), `isPreviewable`. `operationCoreService.ocFetchAttachmentBlob(att)` (blob çekme; `ocDownloadAttachment` bunu yeniden kullanır). |
| **OC-PREV-2** | ✅ | **Önizleme modalı** — `OcAttachmentPreviewDialog.vue` (`max-width 980`, scrollable, lazy blob). Görsel `<img>`, PDF `<iframe>`, metin `blob.text()`→`<pre>`. Üstte dosya adı + **İndir** + kapat. Object URL'ler kapanışta/yeni ekte `revokeObjectURL`. Profil: Ekler satırında **göz ikonu** + tıklanabilir ad; yorum eki chip'leri `previewOrDownload` (önizlenebilirse modal, değilse indir, ikon `mdi-file-eye-outline`). i18n `attachments.preview/previewUnavailable/previewError`. |
| **OC-PREV-3 (bug)** | ✅ | **Yorum composer ek butonu görünmüyordu** — OC-CMT'te eklenen `allowAttachments?: boolean` prop'u, ana composer'a hiç geçilmediğinden Vue **boolean-cast** ile `false` oluyordu (`undefined` değil) → `attachmentsEnabled=false` → buton gizli. Düzeltme: `withDefaults(defineProps<…>(), { allowAttachments: true })`. Düzenleme composer'ları (`:allow-attachments="false"`) kapalı kalır. |
| **OC-PREV-4 (bug)** | ✅ | **PDF/görsel önizleme boş + indiriyordu** — DG `/files/download` MinIO mime metadata yoksa `application/octet-stream` döndürüyor; iframe/img octet-stream blob URL'ini render etmeyip tarayıcı **indirme** olarak ele alıyor. Düzeltme: önizlemede blob, uzantı MIME'i ile yanlışsa `new Blob([blob], { type: mime })` ile yeniden sarılır (`application/pdf`, `image/*`). |

**UI yeni:** `utils/ocAttachmentPreview.ts`, `components/apps/operation-core/OcAttachmentPreviewDialog.vue`.
**UI değişen:** `services/operationCoreService.ts` (`ocFetchAttachmentBlob`), `pages/.../work-items/[id]/profile/index.vue` (Ekler göz ikonu + dialog + yorum chip `previewOrDownload`), `components/apps/operation-core/OcCommentComposer.vue` (`withDefaults` ek butonu fix), locale `tr/en` (`operationCore.profile.attachments.preview/previewUnavailable/previewError`).
**Deploy:** Lokal `nuxt build` temiz (Server + Nitro built). **`mngui` deploy + commit/push bekliyor** (kullanıcı yerelde görsel+PDF önizlemeyi doğruladı).

**Açık/ileriki:**
- ⬜ Office (docx/xlsx) inline render, video/audio oynatıcı, sayfalı PDF kontrolü — ayrı tur (kapsam dışı bırakıldı).
- ⬜ Çok büyük metin dosyalarında sanal kaydırma / kısmi yükleme (şimdilik 2MB tavanı → indir).

---

## PROF-VIEW — Profil tek toplu "profile-view" endpoint (performans) (1 Haz)

Profil ekranı tek yüklemede ~13sn / **~18 istek** üretiyordu: MO'ya `form?mode=edit` + `profile` + `timeline` (3), DG'ye doğrudan `op_fields`×2, `op_states`, `op_priorities`, `op_work_item_types`×2, `op_boards`, `op_rules`, `op_sla_policies`, relation kayıtları×4, Keeper `user?page`×2. Kök neden: readonly form yine de tüm seçim listelerini yüklüyor; katalog/politika ayrı fetch; relation/person/grup adları UI'da çözülüyordu. **Çözüm:** tek toplu uç — tüm veri MO'da (iç ağda, paralel, cache'li) çözülüp tek seferde döner; readonly form lookup yapmaz; "önce id sonra isim" titremesi kalkar. **Plan:** `.cursor/plans/profil_tek_endpoint_performans_*.plan.md`.

| Kod | Durum | Not |
|-----|--------|-----|
| **PROF-VIEW-1** | ✅ | **DTO** — `Contracts/Runtime/ProfileRuntimeContext.cs`: `ProfileViewContext { Profile, Form, Catalogs(BoardCatalogsDto), Boards(id→ad), PoolFields(ham op_fields), FieldDisplays(key→metin), Policy(ResolvedPolicyDto), Timeline }` + `ResolvedPolicyDto`/`ResolvedSlaPolicyDto`/`ResolvedRuleDto`. |
| **PROF-VIEW-2** | ✅ | **Servis** — `Services/RuntimeContextService.ProfileView.cs` (yeni partial): `GetProfileViewAsync` mevcut `GetProfileAsync` + `GetFormEditAsync` + `GetTimelineAsync(0,100)` + `BuildBoardCatalogsAsync` + politika + pool-alan çözümünü **`Task.WhenAll`** ile paralel çağırır. `boardId` adı metadata cache'ten. Davranış birebir; mevcut uçlar korundu. |
| **PROF-VIEW-3** | ✅ | **FieldDisplays** — `BuildProfileFieldDisplaysAsync`: her form alanının çözülmüş görünen metni — state/priority/type→katalog adı, boardId→ad, person (çekirdek + **pool**, eksik id'ler `_personDirectory`'den), grup, **relation pool→ilgili kaydın adı** (dataset bazında `QueryPageAsync` `__dataId $in` ile batched). Scalarlar (text/number/date/bool) UI'da ham gösterilir (display üretilmez). |
| **PROF-VIEW-4** | ✅ | **Politika çözümü MO'da** — `ResolveProfilePolicyAsync`: `op_rules`/`op_sla_policies` workspace filtresiyle MO'da çekilip filtrelenir (UI'ya tümü gitmez). `OcPolicyPanel`'daki `scopeMatches`/`matchedPolicy`/`applicableRules` ile **birebir** (snapshot id→direct/derived; type/priority kapsamı; rules board/type/state kapsamı + priority asc sıralama). |
| **PROF-VIEW-5** | ✅ | **Controller** — `Controllers/RuntimeController.cs`: `GET work-items/{id}/profile-view` → `GetProfileViewAsync`. |
| **PROF-VIEW-6** | ✅ | **UI servis** — `services/operationCoreService.ts`: `ocGetWorkItemProfileView(id)` → `OcWorkItemProfileView` (yeni tip + `OcResolvedPolicy`/`OcResolvedSlaPolicy`/`OcResolvedRule`); mevcut `mapWorkItemProfile`/`mapFormRuntimeContext`/`parseBoardCatalogs`/`mapOpField`/`mapTimelineEntry` yeniden kullanıldı. |
| **PROF-VIEW-7** | ✅ | **Profil sayfası tek çağrı** — `profile/index.vue` `loadProfile` artık **tek** `ocGetWorkItemProfileView(id)`. Ayrı form/profile/poolFields/timeline fetch'leri kaldırıldı (timeline payload'dan; `loadTimeline` yalnız yorum CRUD sonrası yenileme). `catalogsRef` → `useOcBoardListLookups` (katalog fetch yok), `fieldDisplays` → `OcDynamicForm`, `resolvedPolicy` → `OcPolicyPanel`. |
| **PROF-VIEW-8** | ✅ | **Readonly form lookup yapmaz** — `useOcDynamicFormLookups`'a `{ readonly: Ref }`; readonly'de `reload()` + person-picker senkronu kısa devre. `OcDynamicForm`/`OcDynamicFormField`: readonly select/person/grup alanları `fieldDisplays[key]` metniyle gösterilir (`fieldDisplay` prop'u); scalar alanlar değişmez. Yazılabilir (create/edit) mod aynı. |
| **PROF-VIEW-9** | ✅ | **OcPolicyPanel fetch'siz** — yeni `resolvedPolicy` prop'u verilince hiç fetch yapmaz; şablon birleşik `PolicyView`/`RuleView` üzerinden render eder (workspace fetch yolu create modal/diğer kullanımlar için geride kalır). |

**MO yeni:** `Infrastructure/.../Services/RuntimeContextService.ProfileView.cs`. **Değişen:** `Contracts/Runtime/ProfileRuntimeContext.cs` (ProfileViewContext + policy DTO'ları), `Interfaces/IRuntimeContextService.cs` (`GetProfileViewAsync`), `Presentation/.../Controllers/RuntimeController.cs` (uç).
**UI değişen:** `services/operationCoreService.ts` (`ocGetWorkItemProfileView` + mapping), `types/apps/operationCore.ts` (`OcWorkItemProfileView`/`OcResolvedPolicy`/…), `pages/.../work-items/[id]/profile/index.vue` (tek çağrı + prop geçişleri), `composables/useOcDynamicFormLookups.ts` (readonly switch), `components/.../OcDynamicForm.vue` + `OcDynamicFormField.vue` (`fieldDisplays`/`fieldDisplay` + readonly metin dalı), `components/.../OcPolicyPanel.vue` (`resolvedPolicy`). **Script:** `scripts/odak/deploy-odak-apps.ps1` (`-NoCache` switch + `oc_live` sağlık curl'ü).
**Deploy:** Lokal `dotnet build` (MO) + `nuxt build` (UI) temiz. **`mngoperations` Odak'a `--no-cache` deploy edildi (1 Haz ~07:28, healthy — `gateway=200 ui=200 oc_live=200`).** ⚠️ **`mngui` deploy + commit/push BEKLİYOR** (kullanıcı UI'yi sonra deploy edecek; OC-PREV ile birlikte alınacak).

**Açık/ileriki:**
- ⬜ UI deploy sonrası tarayıcıda profil davranış-koruyan kontrol + **network istek sayısı ölçümü** (öncesi/sonrası; `localStorage.OC_PERF`).
- ⬜ `fieldDisplays` ilk sürümde düz metin; gerekiyorsa katalog renk/ikon (sidebar zaten `catalogs` ile alıyor).

---

## NP — In-app bildirim paneli + mention görünürlüğü (bu oturum, 30 May)

Header bildirim paneli (`NotificationDD.vue`) **mock veriyi bırakıp** geçerli kullanıcının `op_notifications` kayıtlarını gösterir. FC-4 mention bildirimleri (`CommentMention`) ve tüm event/kural bildirimleri artık kullanıcıya görünür.

| Kod | Durum | Not |
|-----|--------|-----|
| **NP-1** | ✅ | **MO bildirim ucu** — `GET /notifications` (`unreadOnly`, skip/take, en yeni önce) + `POST /notifications/{id}/read` + `POST /notifications/read-all`. `INotificationQueryService`/`NotificationQueryService`: kapsam daima `IRequestContext.UserId`; match user'a sabit, mark işlemleri sahiplik doğrular (404). `NotificationDto`/`NotificationListResponse` (+`unreadCount`). |
| **NP-2** | ✅ | **createdAt damgası** — `NotificationOrchestratorService` (mention + in-app) artık `createdAt` (ISO) yazar; DG otomatik damgalamadığı için sıralama (`-createdAt`) buna dayanır. Forward-only (eski kayıtlarda yok → en alta düşer). |
| **NP-3** | ✅ | **Panel UI** — `NotificationDD.vue`: rozet (okunmamış sayısı), tip ikon/renk (`CommentMention`=@), göreli zaman (`Intl.RelativeTimeFormat`), okunmamış vurgusu, tıklayınca okundu+iş kaydı profiline git, "Tümünü okundu işaretle", 60sn poll. Admin kapısı kaldırıldı (mention edilen kullanıcı admin olmayabilir). i18n `header.notifications.*` (en/tr). |

| **NP-4** | ✅ | **Kimlik uzayı düzeltmesi (bug)** — Bildirimler person picker kimliğiyle (`mng_person_id` = Keeper `@users` id) yazılıyor; panel ise `sub` (Keycloak id) ile sorguluyordu → **asla eşleşmiyordu** (mention + atama bildirimleri görünmez). `IRequestContext.MngPersonId` (claim `mng_person_id`, `sub` yedek); `NotificationQueryService` artık `userId $in {mng_person_id, sub}` ile eşler. Ayrıca mention/event **actor** = `MngPersonId` (self-exclude doğru çalışır). |
| **NP-5** | ✅ | **Yerleşik atama bildirimi** — `INotificationOrchestrator.DispatchAssignmentAsync`: politikadan bağımsız, atama yapılınca her zaman atanan kişiye in-app `WorkItemAssigned` bildirimi (best-effort). Create (assignee varsa) + Patch (assignee **değiştiyse**). Atayan kişi kendine atadıysa veya değişmediyse atlanır. UI: `WorkItemAssigned` ikon/etiket (`mdi-account-arrow-right`, "Atama"). |

**MO yeni:** `Contracts/Notifications/NotificationDto.cs`, `Interfaces/INotificationQueryService.cs`, `Services/NotificationQueryService.cs`, `Controllers/NotificationsController.cs`. **Değişen:** `ServiceRegistration.cs` (DI), `Services/NotificationOrchestratorService.cs` (`createdAt`), `Interfaces/IRequestContext.cs` + `Services/HttpRequestContext.cs` (`MngPersonId`), `Services/WorkItemCommandService.cs` (mention/event actor = `MngPersonId`).
**UI yeni/değişen:** `components/lc/Full/vertical-header/NotificationDD.vue` (yeniden yazıldı), `services/operationCoreService.ts` (`ocGetNotifications`/`ocMarkNotificationRead`/`ocMarkAllNotificationsRead`), `types/apps/operationCore.ts` (`OcNotification`/`OcNotificationListResponse`), `vertical-header/index.vue` (admin gate kaldırıldı), locale `en/tr`.
**Deploy:** MO build temiz (0 hata) · UI `npm run generate` temiz (169 route). **`mngoperations` Odak'a deploy edildi (30 May, healthy — NP-1…5).** **`mngui` Odak'a deploy edildi (30 May 11:03, healthy — `ui=200`; NP paneli + BLF-8 canlı).**

**Açık/ileriki:**
- ✅ **Atama bildirimi** — NP-5 ile yerleşik (politikadan bağımsız) hale getirildi. Diğer event'ler (`WorkItemUpdated`/`Transitioned` vb.) hâlâ `op_notification_policies` gerektirir.
- ✅ **`createdBy` kimlik uzayı düzeltmesi (NP-6)** — `WorkItemCommandService` create damgası artık `_requestContext.MngPersonId` (claim `mng_person_id`, yoksa `sub` yedek) yazıyor; eskiden `sub` yazıyordu ve person/ad çözümü (`mng_person_id`/@users id bekleyen Keeper) eşleşmiyordu (NP-4 ile aynı sorun). assignee/watchers + bildirim aktörüyle aynı kimlik uzayı. **Forward-only** (eski kayıtlar `sub` taşır, ham görünür). **Backend-only; `mngoperations` Odak'a deploy edildi (31 May, healthy).**
- ⬜ Bildirim okundu durumunu farklı sekmeler/cihazlar arası canlı senkron (şu an 60sn poll).
- ✅ **"Tümünü gör" bildirim sayfası (NP-7)** — yeni route `pages/apps/operation-core/notifications/index.vue`: server-side sayfalama (`skip/take`, `v-pagination` + sayfa boyutu), `Tümü | Yalnızca okunmamış` filtresi, tekil + toplu okundu, kayda tıklayınca profile git, tip ikon/renk + göreli zaman. Dropdown footer'a "Tümünü gör" linki (`header.notifications.viewAll`). i18n `operationCore.notifications.*` (tr/en). **Backend hazır (NP-1 `skip/take/total/unreadCount/unreadOnly`), UI-only.** UI generate temiz (170 route). **`mngui` Odak'a deploy edildi (31 May, healthy).**

---

## BLC — Board liste audit/SLA sütunları + sabit actions (bu oturum, 30 May)

Liste tablosuna **sistem sütunları** (oluşturan/oluşturma zamanı/geçen süre/SLA durumu) + paylaşılan format katmanı; actions sütunu yatay scroll'da **sağda sabit**. Plan: [BOARD_LIST_FORM_CHROME_PLAN.md](./BOARD_LIST_FORM_CHROME_PLAN.md).

| Kod | Durum | Not |
|-----|--------|-----|
| **BLC-1** | ✅ | **Audit/SLA alanları** — `WorkItemCardDto` + `createdAt/createdBy/updatedAt/lastStateChangeAt/closedAt/sla`. Create'te **forward-only `createdBy`** damgası (`IRequestContext.UserId`); `createdBy` person çözümüne eklendi. |
| **BLC-2** | ✅ | **Sistem sütunları admin** — `OcWorkspaceBoardListScopeEditor` sistem sütunlarını (state/priority/type/assignee + createdAt/createdBy/age/sla) seçtirir; per-sütun **Biçim** (`text/number/money/date/relativeTime`). `BoardListColumnDto.Format`. |
| **BLC-3** | ✅ | **Format katmanı + SLA chip** — `utils/ocColumnFormat.ts` (locale-aware date/number/money/relativeTime), `OcSlaStatusChip.vue` (akıllı faz: initial state'ten çıkış = response karşılandı proxy; canlı sayaç). Server-side sort: `age`→`createdAt` (ters), `sla`→`sla.resolveDueAt`. |
| **BLC-4** | ✅ | **Sabit actions sütunu** — son `th/td` `position: sticky; right:0` (scoped CSS, `boards/[boardId]/index.vue`). |

---

## FC — Form chrome (profil + create/edit modal; form tasarımından bağımsız) (bu oturum, 30 May)

Hibrit profil (ana kolonda sekmeler + sağ sidebar özetler) ve sade modal. Plan: [BOARD_LIST_FORM_CHROME_PLAN.md](./BOARD_LIST_FORM_CHROME_PLAN.md).

| Kod | Durum | Not |
|-----|--------|-----|
| **FC-profil** | ✅ | Profil sayfası yeniden yazıldı: ana kolon sekmeler `Detay | Aktivite & yorum[N] | Ekler[N]`; sağ sidebar `SLA · Politikalar · Meta · İzleyenler · Bağlılar`. `ProfileRuntimeContext` + `People` map (assignee/reporter/createdBy/watchers ad çözümü) + `WorkItemSummaryDto.CreatedBy`. |
| **FC-1 (yorum)** | ✅ | Aktivite & yorum sekmesi — `ocGetWorkItemTimeline` + `ocAddWorkItemComment`; timeline (yorum/state/transition). |
| **FC-2 (SLA)** | ✅ | Sidebar SLA paneli — `OcSlaStatusChip` + response/resolve due tarihleri. |
| **FC-3 (politika)** | ✅ | `OcPolicyPanel.vue` (client-side) — eşleşen SLA politikası (id veya type/priority türetme) hedef süreleri + uygulanan `op_rules` (board/type/state kapsamı). Profil sidebar **+ create/edit modalı** (form altı açılır panel, form modelinden canlı type/priority/state). |
| **FC-4 (mention)** | ✅ | `OcCommentComposer.vue` — `@` ile kişi autocomplete (`useOcPersonPicker`), token+id eşleme; `AddCommentRequest.Mentions` → `op_comments.mentions={personIds}` + **in-app bildirim** (`INotificationOrchestrator.DispatchMentionAsync`, `op_notifications` tip `CommentMention`, yazarı dışlar, best-effort). |
| **FC-5 (ekler)** | ✅ | **DG `file` (isArray) alanı** — yeni MinIO backend yok. `op_work_items.attachments` writable (`WorkItemCoreFields`); `ProfileRuntimeContext.Attachments` ham döner. UI Ekler sekmesi: yükle (base64 inline PATCH; mevcut ekler ham `raw` ile korunur, yeni `content` DG'ye yüklenir), indir (`/files/download`→blob), kaldır. |

**MO yeni/değişen:** `WorkItemCoreFields` (`attachments` writable), `Contracts/Runtime/BoardRuntimeContext.cs` (`WorkItemCardDto` audit/SLA + `BoardListColumnDto.Format` + `InitialStateId`), `Contracts/Runtime/ProfileRuntimeContext.cs` (`People`+`Attachments`), `Contracts/WorkItems/AddCommentRequest.cs` (`Mentions`), `Interfaces/INotificationOrchestrator.cs` (`DispatchMentionAsync`), `Services/NotificationOrchestratorService.cs`, `Services/RuntimeContextService.cs` (audit map + sort + people + attachments), `Services/WorkItemCommandService.cs` (`createdBy` damga + comment mentions), `Utilities/ProfileRuntimeBuilder.cs`/`WorkItemCoreFields.cs`.
**UI yeni:** `OcSlaStatusChip.vue`, `OcCommentComposer.vue`, `OcPolicyPanel.vue`, `utils/ocColumnFormat.ts`, `utils/ocBoardListColumns.ts`. **Değişen:** `pages/.../boards/[boardId]/index.vue`, `pages/.../work-items/[id]/profile/index.vue`, `OcWorkItemFormDialog.vue`, `OcWorkspaceBoardListScopeEditor.vue`, `services/operationCoreService.ts` (profil/timeline/comment/attachment fn'leri), `types/apps/operationCore.ts`, locale `en/tr`.
**Deploy:** `mngoperations` Odak'a deploy edildi (healthy, 0 hata) · **`mngui` Odak'a deploy edildi (30 May, healthy — `ui=200`)**. UI build temiz (Nitro built).

**Açık/ileriki:**
- ⬜ **Dinamik (computed) sütunlar** (`expr-eval`, display-only) — BLC kapsamından ertelendi.
- ✅ Mention bildirim paneli `CommentMention` tipini gösteriyor → **NP** bölümü (in-app bildirim paneli) ile çözüldü.
- ⬜ `op_comments.attachments` (yorum ekleri) — şu an sadece iş kaydı ekleri.

---

## BLF — Board liste server-side sıralama/filtre/arama + gelişmiş arama (bu oturum, 30 May)

Board liste görünümü **tam sunucu tarafı**na taşındı: sütun sırası + per-sütun yetkiler (admin), `v-data-table-server`, arama, filtre bileşeni ve **açılır gelişmiş arama** (operatör + çok satırlı AND).

| Kod | Durum | Not |
|-----|--------|-----|
| **BLF-1** | ✅ | **DG `POST /data/{ds}/query` iyileştirme** — `$facet` ile data+total tek sorguda; `search` query param; `X-Total-Count` header. MO `IMngDataGatewayClient.QueryPageAsync` (native Mongo `match` + sort/skip/limit/search → `DataGatewayPage{Items,Total}`). |
| **BLF-2** | ✅ | **MO `POST /runtime/boards/{boardId}/list`** — `GetBoardListAsync`; izin + board akış state kapsamı + filtre + sıralama + arama + person çözümleme → `QueryExecuteResponse`. Sözleşme: `BoardListRequest` + `BoardListColumnDto`/`BoardSortDto`/`BoardListFilterDto`. |
| **BLF-3** | ✅ | **Board admin: sütun sırası + sortable/filterable + defaultSort** — `config.listColumns[]` (sıralı, per-sütun yetki) + `config.defaultSort`. Eski board'larda `visibleFields`'tan geriye dönük türetme (`deriveBoardListColumns`). UI: `OcWorkspaceBoardListScopeEditor` (sıralama + toggle'lar + varsayılan sıralama). |
| **BLF-4** | ✅ | **Liste UI server-side** — `v-data-table-server` (page/itemsPerPage/sortBy), debounce'lu arama, `OcBoardListFilters` (katalog multi-select / person picker / metin). Store `loadBoardListPage` + `listItems/listTotal/listLoading`. |
| **BLF-5** | ✅ | **Katalog filtre seçenekleri workspace kapsamı** — `BuildBoardCatalogsAsync` artık state = board akış kapsamı ∪ `enabledStateIds`; priority/type = `enabled*Ids` (yoksa workspace tipleri / tüm katalog). Enabled ID alandan, yoksa `settings` yedeğinden. *(Önceki: tüm katalog geliyordu.)* |
| **BLF-6** | ✅ | **Gelişmiş arama + AND** — MO filtreleri `$and` ile birleşir (aynı alana çoklu koşul ezilmez; kullanıcı `stateId` filtresi kapsamla kesişir). UI: açılır panel, çok satırlı `[Alan][Operatör][Değer]`. Operatörler: `eq/ne/in/nin/contains/startsWith/endsWith` (+ backend `gt/gte/lt/lte`). |
| **BLF-7** | ✅ | **Search temizleme fix + Clear butonu** — `clearable` `null` → `(searchInput ?? '').trim()` (eski: `null.trim()` patlıyordu, liste yenilenmiyordu); belirgin "Aramayı temizle" butonu + `@click:clear`. |
| **BLF-8** | ✅ | **Sayısal/tarih operatörleri UI'a açıldı** — gelişmiş aramada `number` ve `date` alan tipleri: `gt/gte/lt/lte` (+`eq/ne`). Alan tipi tespiti: `filterKind()` artık `columnFormat`(`number`/`money`/`date`) + pool `fieldType`(`number`/`date`/`datetime`) ile karar veriyor (core date: createdAt/lastStateChangeAt/closedAt). Değer girişi: number=`type=number`, date=`type=datetime-local` → `toISOString()` UTC ile gönderilir (saklama ISO string ile sözlüksel uyumlu). Bu alanlar hızlı filtreden hariç (operatör gerektirir). **Backend değişikliği yok** (`CoerceScalar` long/double; ISO string lexicographic). |
| **BLF-9** | ✅ | **Relation alanlarda option/relation etiketi** — pool `relation` alanları (`relationDatasetName`): liste hücresinde ham `__dataId` yerine ilgili kaydın **adı**; filtrede (hızlı + gelişmiş) ham metin yerine **v-select** (option=ad, value=id, `in/nin/eq/ne`). Board sayfası relation dataset'lerini `ocListDataset`+`recordToDatasetItems` ile (dataset bazında tek sefer) yükler → `relationOptionsByKey`/`relationLabelByKey`. Filtre bileşeni `OcBoardFilterKind`'a `relation` eklendi; katalog mantığı "select" (katalog ∪ relation) olarak genelleştirildi. *(Statik `select`/`enum` pool tipi yok; OC tipleri text/number/bool/datetime/relation/persons/personGroups/tags/file.)* **Backend değişikliği yok.** |
| **BLF-10** | ✅ | **Tags (çoklu serbest etiket) alanlarda filtre** — pool `tags` alanları: hücre zaten birleşik metin (relation gibi), dokunulmadı. Filtrede yeni `tags` kind: hızlı + gelişmiş aramada **`v-combobox`** (serbest giriş + chip) ile çoklu değer, operatörler `in/nin/eq/ne` (dizi üyeliği; Mongo `$in`/`$nin` etiket dizisinde herhangi bir eşleşme). Öneriler `tagOptionsByKey` ile **yüklü liste satırlarından** toplanır (ek sorgu yok; serbest giriş açık olduğundan kısmi öneri yeterli). Board sayfası: `tagsPoolKeySet` + `filterKind`→`tags`. **Backend değişikliği yok** (BLF-9 gibi UI-only). |

**Düzeltmeler (aynı oturum grubu):**
- ✅ **assignee bozulması** — edit'te `assignee` object olarak persist ediliyordu; `MngDataGatewayClient.CollapseRelationValue` `assignee` (tekil) + `watchers` (çoklu) relation'ı id'ye indirger.
- ✅ **Çoklu person combobox** — form içinde 2+ person alanında ikinci picker liste getirmiyordu; `useOcDynamicFormLookups` artık alan başına izole `useOcPersonPicker()` (`pickerForField`).

**MO yeni/değişen:** `IMngDataGatewayClient.QueryPageAsync` + `DataGatewayPage`, `Clients/MngDataGatewayClient.cs`, `Contracts/Runtime/BoardRuntimeContext.cs` (`ListColumns`/`DefaultSort` + yeni DTO'lar), `Services/RuntimeContextService.cs` (`GetBoardListAsync`, `ParseListColumns`/`ParseDefaultSort`, `BuildBoardCatalogsAsync` scope, `$and` filtre, `BuildMatchCondition`), `Controllers/RuntimeController.cs` (`[HttpPost] boards/{id}/list`).
**DG değişen:** `Services/DataService.cs` `QueryWithMatchAsync` (`$facet` total + search), `Controllers/DataController.cs` (`search` param + `X-Total-Count`).
**UI yeni/değişen:** `components/.../OcBoardListFilters.vue` (yeni — hızlı filtre + gelişmiş arama paneli), `OcWorkspaceBoardListScopeEditor.vue`, `OcWorkspaceBoardDialog.vue`, `pages/.../boards/[boardId]/index.vue`, `services/operationCoreService.ts` (`ocGetBoardListPage`), `stores/apps/operationCore.ts`, `types/apps/operationCore.ts`, `utils/ocBoardListColumns.ts`, locale `en/tr`.

**Deploy:** `mngdatagateway` + `mngoperations` (30 May) + **`mngui` Odak'a deploy edildi (31 May, healthy — BLF-9/BLF-10 dahil tüm board liste UI canlı).** MO/DG build 0 hata; UI generate 170 route.

**Belgeler:** [API_SURFACE §3.1/3.1.2](./API_SURFACE.md) (board context + liste ucu) · [RUNTIME_CONTEXT §5.2](./RUNTIME_CONTEXT.md) (katalog scope).

**Açık/ileriki:**
- ✅ Gelişmiş aramada sayısal/tarih `gt/gte/lt/lte` → **BLF-8** (UI-only; Odak'a deploy edildi 30 May 11:03, healthy).
- ✅ Pool relation alanlarda filtre/hücre option-relation etiketi → **BLF-9** (UI-only, 31 May deploy edildi).
- ✅ `tags` (çoklu serbest etiket) alanlarda filtre (combobox + `in/nin/eq/ne`, yüklü satırlardan öneri) → **BLF-10** (UI-only, 31 May deploy edildi).
- ✅ **`mngui` Odak deploy** (30 May, healthy — BLF+BLC+FC+NP+BLF-8 canlı). *Not: BLF-9 yeni UI değişikliği, henüz deploy edilmedi.*

## BO — Board liste operasyonel aksiyonlar (bu oturum, 30 May)

Board liste görünümünde **yeni iş modalı + satır aksiyonları** (profil/düzenle/sil).

| Kod | Durum | Not |
|-----|--------|-----|
| **BO-1** | ✅ | **Yeni iş → modal** — `OcWorkItemFormDialog` (create/edit), genişlik form design `layout.dialogMaxWidth`'ten; create board `defaultFormId`'sini çözer. Eski `/work-items/new` sayfası duruyor (geri uyum). |
| **BO-2** | ✅ | **Actions sütunu** — View Profile (ayrı sayfa), Edit (modal, edit context + diff PATCH), Delete (onay modalı). Edit/Delete `permissions.canEdit` gate'li. |
| **BO-3** | ✅ | **MO `DELETE /work-items/{id}`** — `IWorkItemCommandService.DeleteAsync` (DG delete + activity `WorkItemDeleted` + `oc.workitem.deleted` event); yetki = `EnsureWorkItemUpdate`; 204. |
| **BO-4** | ✅ | **Profil sayfası geçici** — seçili formu **salt-okunur** render (`ocGetFormEditContext` + `OcDynamicForm readonly`); gerçek profil tasarımı (Epic F) bekliyor. |
| **BO-5** | ✅ | **Silmede ilişki guard'ı (31 May)** — `DELETE /work-items/{id}?force=` + `IWorkItemCommandService.DeleteAsync(id, force)`. `!force`: bağlı link (`op_links` source/target) veya alt kayıt (`parentItemId`) varsa **409 `WORK_ITEM_HAS_RELATIONS`** (details: `links`, `children`). `force=true`: siler + ilgili linkleri best-effort temizler (`DeleteRelatedLinksAsync`). UI: board silme dialog'u guard'ı yakalar → uyarı + **"Yine de sil"** (force) butonu; `ocDeleteWorkItem(id, force)`. i18n `board.actions.deleteHasRelations`/`deleteForce` (tr/en). |
| **BO-6** | ✅ | **Edit'te alan temizleme (31 May)** — `PatchWorkItemRequest` core nullable scalar alanlar (`Description`/`Assignee`/`PriorityId`/`BoardId`) `JsonElement?` oldu → **absent (değişmedi) vs explicit null (temizle)** ayrımı. `TryReadPatchScalar` tri-state okur; eski `if (x != null)` mantığı null'ı "yok" sayıp temizlemeyi engelliyordu. Pool alanları zaten `WorkItemFieldWriter` ile doğru temizleniyordu. UI tarafı (diff PATCH zaten explicit null gönderiyor) değişmedi. |

**UI yeni/değişen:** `components/.../OcWorkItemFormDialog.vue` (yeni), `pages/.../boards/[boardId]/index.vue` (silme guard + force), `pages/.../work-items/[id]/profile/index.vue`, `services/operationCoreService.ts` (`ocGetFormEditContext`, `ocUpdateWorkItem`, `ocDeleteWorkItem(force)`, `buildUpdateWorkItemRequest`, `ocErrorCode`, `ocExtractOperationsMessage`), `services/apiService.ts` (`fetchFromOperations` reject'inde `error.data`/`statusCode` korunur).
**MO değişen:** `IWorkItemCommandService` + `WorkItemCommandService.DeleteAsync(force)` + guard helper'ları (`EnsureNoBlockingRelationsAsync`/`DeleteRelatedLinksAsync`), `PatchAsync` + `CollectPatchFieldKeys` + `TryReadPatchScalar`, `PatchWorkItemRequest` (JsonElement?), `WorkItemsController` `[HttpDelete]` `?force`.
**Deploy:** BO-1…4 (30 May) + **BO-5/BO-6 `mngoperations`+`mngui` Odak'a deploy edildi (31 May ~02:28, healthy — `gateway=200 ui=200`, SLA-1 smoke yeşil OCD-0065).** MO build 0/0; UI generate 170 route.

**Açık/ileriki:**
- ✅ Edit modunda alan **temizleme** — **BO-6** (tri-state PATCH; core nullable alanlar artık temizlenebilir).
- ✅ Silmede kullanım/ilişki guard'ı — **BO-5** (link + alt kayıt; 409 + force). *(Açık: yorum/activity orphan temizliği — silinen kaydın yorumları/aktiviteleri DG'de kalır; ayrı temizlik/arşiv işi.)*
- ⬜ Profil gerçek tasarımı (header/sidebar/timeline) — Epic F.

---

## BL — Board liste enrichment (bu oturum, 29 May gece)

Board liste/kanban kartlarında id yerine **isim + ikon + renk**; UI client-side join yapmaz. Üç parça:

| Kod | Durum | Not |
|-----|--------|-----|
| **BL-1** | ✅ | **Katalog enrichment** — board context `catalogs` map'leri (state/priority/type → id/name/color/icon); `OcBoardCatalogLabel` Tabler/MDI ikon + renk. Katalog CRUD MO `/api/v1/catalogs/{source}` (write-through cache, `CatalogService`). DG `$lookup`'ta `__history` hariç. |
| **BL-2** | ✅ | **Pool alan sütunları** — board `visibleFields` artık çekirdek + pool alan key'lerini kabul eder; sütun seçici (scope editor) pool alanları listeler; `WorkItemCardDto.Fields` (`extraFields`) → liste hücresi (`listTablePoolCellValue`). |
| **BL-3** | ✅ | **Person çözümleme MO-side** — `assignee`/`watchers` + person tipi pool alanlar id→ad; `IKeeperDirectoryClient` (`GET api/User/{id}`) + `IPersonDirectory` in-memory cache (`PersonTtlSeconds`); `QueryExecuteResponse.People`; UI `boardPeople` map. |

**MO yeni dosyalar:** `Interfaces/IKeeperDirectoryClient.cs`, `Interfaces/IPersonDirectory.cs`, `Clients/MngKeeperClient.cs`, `Services/PersonDirectoryService.cs`, `Catalogs/OcCatalogRegistry.cs`, `Interfaces/ICatalogService.cs`, `Services/CatalogService.cs`, `Controllers/CatalogsController.cs`.

**Deploy:** `mngoperations` Odak'a deploy edildi (healthy). **`mngui` Odak'a deploy edildi (30 May 2026, healthy — `ui=200`)**; lokal `npm run generate` build temiz (169 route, 0 hata).

**Belgeler:** [RUNTIME_CONTEXT §5.2/5.3](./RUNTIME_CONTEXT.md) · [INTEGRATIONS §1.1](./INTEGRATIONS.md) (person + Redis ileriki faz notu).

**Açık/ileriki:**
- ✅ **mngui** Odak deploy (30 May 2026, healthy).
- ✅ **Person grup alanları ad çözümü (BL-GRP)** — `personGroups`/`group` pool alanları + çekirdek `assignmentGroups`: liste hücresinde ham grup id yerine **grup adı**. MO: `IKeeperDirectoryClient.GetGroupAsync` (`GET Group/{id}`, `[ManagerAuthorization]` — User ucuyla aynı yetki) + `GroupDirectoryService`/`IGroupDirectory` (PersonDirectory deseni, in-memory cache + TTL) + DI. `QueryExecuteResponse.Groups` ve `ProfileRuntimeContext.Groups` (PersonDisplayDto: id→ad). `RuntimeContextService`: `GetGroupPoolFieldKeysAsync` (fieldType ∈ personGroups/personGroup/group) + `ResolveGroupsForCardsAsync` (board liste + kanban kolon) + profil `assignmentGroups`+grup pool çözümü. UI: `OcQueryExecuteResponse.groups`/`OcWorkItemProfile.groups`, store `boardGroups`, board sayfası `groupPoolKeySet` + `resolveGroupValue` (hücre). MO+UI build temiz. **`mngoperations`+`mngui` Odak'a deploy edildi (31 May, healthy).** *(Açık: Keeper `by-ids` toplu uç + Redis — ayrı/daha büyük. Grup alan filtresi → BL-GRP-3, profil grup adı → BL-GRP-2 tamam.)*
- ✅ **Profil alan render'ında grup adı (BL-GRP-2, 31 May)** — readonly profil formunda grup alanları (`personGroups`/`group` + çekirdek `assignmentGroups`) artık ham id yerine **grup adı** gösteriyor. `OcDynamicFormField`: `isGroupField` + `collectGroupIds` + `groupReadonlyText`; `fieldDisabled && isGroupField` dalı (readonly text-field, ad/adlar). `OcDynamicForm` `groupNames` prop'unu geçirir; profil sayfası `groupNames`'i `profile.groups`'tan (id→ad) kurar. UI-only, generate temiz (170 route). **`mngui` Odak'a deploy edildi (31 May, healthy).**
- ✅ **Grup alan filtresi (BL-GRP-3, 31 May)** — board liste filtresinde grup alanları (`personGroups`/`group` pool + çekirdek `assignmentGroups`) artık `text` yerine **`group` kind** (select, operatörler `in/nin/eq/ne`; relation deseni gibi). `OcBoardListFilters`: yeni `'group'` kind, `groupOptionsByKey` prop'u, `isSelectKind`'e dahil → quick/advanced v-select otomatik. Board sayfası: `filterKind`→`group`; `groupOptionsByKey` = `boardGroups`'tan (id→ad, yüklü satırlardan; tags gibi) her grup key'ine aynı liste. Backend tags/relation ile aynı `$in`/`$nin` (dizi üyeliği) — MO değişikliği yok, UI-only. Generate temiz (170 route). **`mngui` Odak'a deploy edildi (31 May ~02:41, healthy — `ui=200`).**
- ✅ Pool **relation** hücrelerinde relation adı (ham id yerine) → **BLF-9** (`tags` hariç).
- ✅ **Keeper `by-ids` toplu uç + Redis profil cache (BL-KB, 31 May)** — `POST api/Group/by-ids` + `POST api/User/by-ids` (User: id'ler `__dataId` **veya** Keycloak `sub`; tek Mongo `$or/$in`). Repo `GetByIdsAsync` (User/Group), MediatR `GetUsersByIds`/`GetGroupsByIds` query+handler, controller uçları (`[ManagerAuthorization]` — tekil GET ile aynı yetki). MO: `IKeeperDirectoryClient.GetUsersAsync/GetGroupsAsync` (POST by-ids, istenen id→ad eşlemesi), `MngKeeperClient` uygular; `PersonDirectoryService`/`GroupDirectoryService` artık eksik id'leri **tek istekte** çözer (N+1 giderildi; negatif sonuç da MO in-memory'de cache). **Redis profil cache:** `IDirectoryCache`/`DirectoryCacheService` (Keeper, `IRedisService` üstünde, fail-open; anahtar `oc:dir:user|group:{domain}:{id}`, TTL 10dk, kullanıcı hem __dataId hem sub anahtarıyla yazılır); by-ids önce Redis'ten okur, eksikleri Mongo'dan çekip cache'ler. **Invalidation:** UpdateUser (2 yol) + DeleteUser → `InvalidateUserAsync` (her iki kimlik); UpdateGroup + DeleteGroup → `InvalidateGroupAsync`. MO+Keeper build temiz (0 hata). **`mngkeeper`+`mngoperations` Odak'a deploy edildi (31 May ~03:04, healthy — `gateway=200 ui=200`).**

---

## D1 Board admin (önceki oturum)

| Kod | Durum | Not |
|-----|--------|-----|
| **D1-B1** | ✅ | Kolon `defaultTransitionKey` — akış geçiş seçici |
| **D1-B2** | ✅ | Board varsayılanları: profile/type/priority/state |
| **D1-B3** | ✅ | `visibleFields` + MO `cardFieldKeys` etiketleri |
| **D1-B4** | ✅ | `viewGroups` / `editGroups` (Keeper grupları) |
| **D1-B5** | ✅ | Kolon state ↔ `enabledStateIds` uyarı |
| **W-CREATE** | ⬜ | Yeni workspace UI — backlog |

**UI:** `/apps/operation-core/admin/workspace-definitions?tab=boards`

---

## SLA Faz 1

| Kod | Durum | Not |
|-----|--------|-----|
| **SLA-0** | ✅ | `op_sla_policies` Odak'ta mevcut; demo policy `OC Demo Default SLA` |
| **SLA-1** | ✅ | MO create → `profile.sla` + `op_work_items.sla` snapshot (Odak smoke) |
| **SLA-2** | ✅ | Workspace tanımları → **SLA** sekmesi, CRUD dialog |
| **SLA-3** | ✅ | Profil + board liste SLA chip (`OcSlaStatusChip`, akıllı faz) — FC/BLC ile tamamlandı |

**Scriptler:** `setup-op-sla-policies-dataset.ps1` · **`smoke-sla-faz1.ps1`** (SLA-1 DoD)

**UI URL:** `/apps/operation-core/admin/workspace-definitions?workspaceId=...&tab=sla`

---

## E1-P1 + W-CREATE — Workspace yetki grupları + Yeni workspace UI (bu oturum, 30 May)

| Kod | Durum | Not |
|-----|--------|-----|
| **E1-P1** | ✅ | **Genel sekmede yetki grupları** — `OcWorkspaceDefinitionsGeneralTab.vue`'ye `viewGroups`/`editGroups`/`adminGroups` Keeper grup seçicileri (board dialog pattern: `useGroupStore` + multi-select chips). Load `ocGetWorkspace` → form; save `ocUpdateWorkspace` payload'una eklenir. Tip: `OpWorkspaceDetail`'e `viewGroups/editGroups/adminGroups/ownerGroups`; `mapWorkspaceDetail` `resolveRelationIds` ile parse (string id veya relation obje). |
| **W-CREATE** | ✅ | **Yeni workspace oluşturma UI** — `OcWorkspaceCreateDialog.vue` (name zorunlu + workspaceType + prefix + açıklama; `key` DG incremental). Servis `ocCreateWorkspace` = `ocCreateRecordId('op_workspaces', …)`. Hub (`admin/workspace-definitions/index.vue`): v-select yanına "Yeni workspace" butonu + boş-durum CTA; create sonrası `loadWorkspaces` + yeni id seçilir + Genel sekme. |

**UI yeni:** `OcWorkspaceCreateDialog.vue`. **Değişen:** `OcWorkspaceDefinitionsGeneralTab.vue`, `admin/workspace-definitions/index.vue`, `services/operationCoreService.ts` (`ocCreateWorkspace` + `mapWorkspaceDetail` grupları), `types/apps/operationCore.ts`, locale `en/tr`.
**Backend değişikliği yok** — `WorkspaceRecord` zaten `ViewGroups/EditGroups/AdminGroups/OwnerGroups` içeriyor; `op_workspaces` create DG `key` incremental üretir.
**Deploy:** UI-only, **Odak'ta canlı** (30 May, commit `cd6848a`).

---

## E1-P2 — Akış geçişlerinde requiredFields + yetki grupları (bu oturum, 30 May)

| Kod | Durum | Not |
|-----|--------|-----|
| **E1-P2** | ✅ | **Flows sekmesi geçiş editörü** — her transition kartına `requiredFields` (core form-layout key'leri + pool alanları, etiketli) ve `permissions.groups` (aktif Keeper grupları) çoklu seçimi. `OcWorkspaceDefinitionsFlowsTab.vue`: `ocListPoolFieldsForWorkspace` + `useGroupStore` yüklenir; `fieldKeyItems` `resolveOcFieldDisplayLabel` ile etiket; `buildPayload` her geçişe `requiredFields:[]` + `permissions:{groups:[]}` yazar. Tip `OpStateFlowTransition.permissionGroups`; `mapOpStateFlowTransition` `permissions.groups` parse eder. |

**Backend değişikliği yok** — MO zaten zorunlu kılıyor: `StateFlowCatalog.EnsureRequiredFields` (WI transition akışında) + `PermissionEvaluator.EnsureTransition`/`CanApplyTransition` (`permissions.groups`). Boş grup = kısıtlama yok (`GroupListParser.Intersects` `requiredGroups.Count==0 → true`).
**Değişen:** `OcWorkspaceDefinitionsFlowsTab.vue`, `services/operationCoreService.ts` (`mapOpStateFlowTransition`), `types/apps/operationCore.ts`, locale `en/tr`.
**Deploy:** UI-only, **`mngui` Odak'a deploy edildi (31 May, healthy).**

**Açık/ileriki:**
- ⬜ Create dialog'da opsiyonel: ilk board/akış seed'i (şimdilik boş; Değerler/Akış sekmelerinden kurulur).
- ⬜ Workspace silme/devre dışı bırakma (Genel sekme) — guard + ilişki kontrolü.

---

## CC — Dinamik (computed) sütunlar, faz-1 (bu oturum, 30 May)

| Kod | Durum | Not |
|-----|--------|-----|
| **CC** | ✅ | **Hesaplanan liste sütunu (display-only, client-eval)** — board liste sütun editöründe "Hesaplanan sütun" girişi (`key`/`label`/`expr`/`format`); `expr-eval` ile güvenli ifade (eval yok). Satır bağlamı = core alanlar + pool `fields`; değer board sayfasında `evaluateComputedExpr` ile hesaplanıp `formatCellValue`'dan geçer. Sunucu sort/filter yok (computed → kapalı). |

**Yaklaşım A (MO passthrough):** `BoardListColumnDto`'ya `Computed`/`Expr`/`Label`; `ParseListColumns` bunları okur, computed sütunları `cardFieldKeys`'ten (DG alan seçimi) ve sortable/filterable'dan hariç tutar. UI runtime context'ten okuyup hesaplar.
**Yeni:** `utils/ocComputedColumns.ts` (expr-eval değerlendirici, parse cache, güvenli değişken çözümü), bağımlılık `expr-eval`.
**Değişen (MO):** `BoardRuntimeContext.cs` (`BoardListColumnDto`), `RuntimeContextService.cs` (`ParseListColumns`, cardFieldKeys). **Değişen (UI):** `OcWorkspaceBoardListScopeEditor.vue` (computed sütun ekleme + expr satırı), `OcWorkspaceBoardDialog.vue` (visibleFields'tan computed hariç), `boards/[boardId]/index.vue` (computed render), `ocBoardListColumns.ts` (`deriveBoardListColumns` computed korur), `operationCoreService.ts` (iki mapper), `types/apps/operationCore.ts`, locale `en/tr`.
**Deploy:** MO build temiz (0/0). MO + mngui deploy gerekir.

**Açık/ileriki (CC faz-2+):**
- 🚫 **Computed sütunlarda sıralama/filtre — YAPILMAYACAK** (31 May kararı). Computed değer DG'de saklanmadığından (ifadeyle türetiliyor) server-side sort/filter mümkün değil; client-side ise projenin **server-side sayfalama/sıralama/filtre** ilkesiyle çelişir. Computed sütunlar display-only kalır.
- ⬜ `tags`/relation çoklu değerlerini ifade içinde etiketle kullanma (şu an dizi → eleman sayısı).
- ⬜ Tarih farkı/iş günü fonksiyonları (özel expr fonksiyonları).
- ⬜ Profil/kart görünümünde de computed alan gösterimi.

---

## A — Yorum ekleri (op_comments.attachments) (bu oturum, 30 May)

| Kod | Durum | Not |
|-----|--------|-----|
| **A** | ✅ | **Yorumlara dosya eki** — `op_comments.attachments` (file isArray, şemada zaten mevcut). Composer'da dosya seç/kaldır; gönderimde base64 `content` ile MO'ya gider, DG MinIO'ya yükler. Timeline yorum girdilerinde ekler indirilebilir chip (mevcut `ocDownloadAttachment`). İş kaydı ekleri pattern'i birebir. |

**Backend (MO):** `AddCommentRequest.Attachments` (+ `CommentAttachmentInput`); `AddCommentInternalAsync` payload'a `attachments` yazar; `TimelineEntryDto.Attachments` + timeline yorum döngüsü ham `attachments`'ı geçirir. Build 0/0.
**UI:** `OcCommentComposer.vue` (dosya seçici + bekleyen chip'ler + emit `files`), `operationCoreService.ts` (`ocAddWorkItemComment` files→base64, `mapTimelineEntry` attachments parse), `OcTimelineEntry.attachments`, profil timeline ek chip'leri, locale `en/tr`.
**Backend şema değişikliği yok** — `op_comments.attachments` (file isArray) phase-1 şemasında zaten tanımlı.
**Deploy:** MO + mngui deploy gerekir.

---

## F — Operasyonel runtime: durum geçişleri (bu oturum, 30 May)

| Kod | Durum | Not |
|-----|--------|-----|
| **F-T** | ✅ (lokal) | **Profilde durum geçişi (transition) uygulama** — profil header'ında geçerli durumdan uygulanabilir geçiş butonları; tıklayınca opsiyonel yorumlu onay dialog'u; uygulanınca güncel profil + timeline yeniden yüklenir. MO yetki + koşul + `requiredFields` doğrulamasını yapar (UI eklemeli, ham). |
| **F-T2** | ✅ (lokal, 31 May) | **Geçiş `requiredFields` ön-toplama** — `ProfileActionDto.RequiredFields` (akış `transition.requiredFields`) profil context'e eklendi. Geçiş onay dialog'unda zorunlu alanlar `OcDynamicFormField` ile (lazy alt-bileşen `OcTransitionRequiredFields.vue`, sadece dialog açılınca lookup yükler) toplanır; mevcut değerlerle ön-doldurulur; hepsi dolmadan **Uygula kapalı**. Değerler `ocApplyTransition({ fields })` → `TransitionWorkItemRequest.Fields` ile gider; MO merge edip persist eder, sonra `EnsureRequiredFields` doğrular (artık 400 yerine inline toplama). |

**Backend (F-T):** değişiklik yoktu — MO profil context `Actions` (`ProfileActionDto`) ve `POST /work-items/{id}/transitions/{key}` (`TransitionWorkItemRequest`: `comment?`/`fields?`) zaten hazırdı; UI bunları hiç okumuyordu.
**Backend (F-T2):** `ProfileActionDto.RequiredFields` (yeni alan) — `RuntimeContextService` `StateFlowCatalog.GetRequiredFields(transition)` ile doldurur, `ProfileActionBuilder.Build` korur. Transition ucu zaten `Fields`'i merge+persist edip doğruluyordu (mantık değişmedi). MO build 0/0.
**UI (F-T):** `types/apps/operationCore.ts` (`OcProfileAction` + `OcWorkItemProfile.actions`), `operationCoreService.ts` (`mapProfileAction`, `ocApplyTransition`), profil sayfası `work-items/[id]/profile/index.vue` (header geçiş butonları + onay/yorum dialog), locale `en/tr` (`profile.transitions.*`).
**UI (F-T2):** yeni `OcTransitionRequiredFields.vue`; `OcProfileAction.requiredFields` + `mapProfileAction` parse; `ocApplyTransition({ fields })`; profil dialog'unda zorunlu alan toplama + `Uygula` gate + başarı sonrası `loadProfile()` (form + state tazelenir); locale `profile.transitions.requiredTitle`.
**Deploy:** **`mngoperations`+`mngui` Odak'a deploy edildi (31 May, healthy — F-T2 `RequiredFields`/F-K `IncomingTransitions` + tüm F UI canlı; SLA-1 smoke yeşil).** Remote build 0/0; UI generate 170 route.

| **F-K** | ✅ (lokal, 31 May) | **Kanban'da DnD ile durum geçişi** — kart bir kolondan diğerine sürüklenince ilgili transition uygulanır. Backend `BoardColumnDto.IncomingTransitions` (`{transitionKey, fromStateId, requiredFields}`) eklendi → UI kaynak state'e göre **doğru** geçişi seçer (çok-girişli kolon desteği). `OcBoardKanban` `vue-draggable-next` ile sürükle-bırak (yalnız `canEdit`; `dropEligible=false` kolona bırakma reddedilir; yerel kopya + transition sonrası `refreshBoard` ile optimistic taşıma düzeltilir/geri alınır). Drop akışı: geçersiz from→to → uyarı + geri al; `requiredFields` varsa → kartı geri al + **profile yönlendir** (snackbar "Profilde aç", profil F-T2 ile zorunlu alan toplar); aksi halde `ocApplyTransition` + kolon yenile. **Liste görünümünde transition yok** (kapsam kararı: profil/düzenle yeterli). |

**Backend (F-K):** `BoardColumnDto.IncomingTransitions` + `BoardColumnTransitionDto` (yeni); `BoardColumnBuilder.FindTransitionsToState` artık her giriş geçişi için `{key, from, requiredFields}` döner (`DefaultTransitionKey`/`AlternativeTransitionKeys` geri uyumlu korundu). MO build 0/0.
**UI (F-K):** `OcBoardColumn.incomingTransitions` (+ `OcBoardColumnTransition` tip) + `mapBoardColumn` parse; `OcBoardKanban.vue` DnD'ye çevrildi (`vue-draggable-next`, global kayıtlı); board sayfası `onKanbanTransition` (çözümleme + snackbar + `refreshBoard`), `:editable="canEdit"`; locale `board.transition.*` (success/error/invalid/requiredFields/openProfile).

**Açık/ileriki (F faz-2+):**
- ✅ Geçiş dialog'unda `requiredFields` ön-toplama → **F-T2** (lokal, mngoperations canlı; mngui bekliyor).
- ✅ Kanban'da DnD geçiş uygulama → **F-K** (lokal, mngoperations canlı; mngui bekliyor).
- ⬜ Kanban'da kart **sıralaması** kalıcılığı (şu an DnD yalnız state geçişi; aynı kolon içi sıra sunucuya yazılmıyor).

---

## PERF — Board liste + profil performans/kod optimizasyonu (bu oturum, 30 May)

Ölçüm-öncelikli, davranış-koruyan tur (`perf/oc-optimization` → `main`). Detay: `PERF_OPTIMIZATION.md`.

| Kod | Durum | Not |
|-----|--------|-----|
| **PERF-BE** | ✅ Odak'ta canlı | Profil DG: `op_links`/`timeline` erken paralel başlat (metadata+field-behavior ile örtüşür) + timeline `limit=200→sort=-enteredAt&limit=5`; FieldBehavior istek-başı tek `key→record` map (O(alan×enabledIds) tarama kalktı). **Ölçülen:** profil warm ~1575-1822ms → **~1218ms (~%30)**. Board liste warm zaten optimal (tek DG sorgusu, değişmedi). |
| **PERF-UI** | ✅ Odak'ta canlı | `Intl` formatter memoize, tek global "now" ticker (N timer→1), lookup Map dedup, `listRows` önceden çözüm (slot'lar her render'da `resolveX` çağırmıyor), `OcBoardKanban` lazy. Davranış birebir. |
| **PERF-DIAG** | ✅ (flag kapalı) | `OcCallStats` + `PerfDiagnostics` bayrağı (default kapalı) — istek başına DG/Keeper çağrı sayısı/süresi; UI `localStorage.OC_PERF`. Üretimde kapalı, gelecekte ölçüm için hazır. |

**Faz-4 / büyük dosya bölme refactor (B) — kısmen yapıldı (31 May, davranış birebir aynı):**
- ✅ **`RuntimeContextService.cs`** (MO) `partial class`'a bölündü: 1549 → 1015 satır + `.Dashboard.cs` / `.Directory.cs` / `.Form.cs`. MO build temiz (0/0).
- ✅ **`operationCoreService.ts`** (UI) barrel'a dönüştürüldü: 2324 → 1936 satır. Leaf domain'ler `services/operationCore/` altına taşındı — `notifications.ts`, `rules.ts`, `sla.ts`, `schedules.ts`, `flows.ts` (ana dosya `export *` ile re-export ediyor; paylaşılan yardımcılar `resolveRelationId`/`pickStr`/`ocCreateRecordId`/`parseSingleDgRecord` export'landı). `nuxt build` temiz; mevcut import yolları (`@/services/operationCoreService`) değişmedi.
- ⬜ Kalan TS domain'leri (catalogs/workspaces/forms/work-items/runtime-board) — bunlar paylaşılan parse yardımcıları ve çapraz çağrılarla **sıkı bağlı** (örn. `readEnabled*` hem catalog hem workspace'te); ayırmanın getirisi/risk oranı düşük, ihtiyaç doğunca yapılır.

**Faz-4/A (sanallaştırma) — yapılmadı, yerine "daha fazla yükle" eklendi (31 May):** İnceleme: Kanban kolonları tek sayfa (`take=suggestedPageSize`) yüklüyor, kartlar `vue-draggable-next` içinde (içeride sanallaştırma DnD'yi bozar); liste görünümü `v-data-table-server`, admin tabloları `v-data-table` (zaten sayfalı). Yani sanallaştırma için güvenli/faydalı hedef yok. Bunun yerine **Kanban kolonlarına "daha fazla yükle"** eklendi: `store.loadMoreColumn` (skip=yüklü kart sayısı, append + dedupe, people/groups merge, fail-soft) + `OcBoardKanban` kolon altı buton (`n/total`, `columnLoadingMore` spinner). Büyük backlog'lar artık erişilebilir. `nuxt build` temiz.

**Deploy:** Faz-4/B refactor (RuntimeContextService partial + operationCore barrel) `mngoperations`+`mngui` Odak'a **deploy edildi** (31 May ~22:12, healthy — `gateway=200 ui=200`, SLA smoke OCD-0066 yeşil). Kanban **"daha fazla yükle"** (commit `90374ce`) de `mngui` ile Odak'a **deploy edildi** (31 May ~22:35, `ui=200`). Tüm bu chat çıktıları artık canlı.

> **Manuel kontrol:** Bu chat'te yapılan ve kullanıcı tarafından henüz doğrulanmamış tüm işler için birleşik kontrol rehberi: [KONTROL_REHBERI_2026-05-31.md](KONTROL_REHBERI_2026-05-31.md).

---

## UI-NGINX — `/api/operations/` proxy fix (31 May gece, KRİTİK config bug)

**Belirti (sunucu UI'sinde 3 ayrı görünüm, tek kök neden):** (1) Board liste görünümü `405 Not Allowed` (nginx/1.31.1) verdi; (2) Workspace → **Genel** tab boş geldi; (3) Form düzenlemede varsayılan öncelik **adı yerine id** gösterdi.

**Kök neden:** Üretim UI'si statik SPA (`npm run generate` → `.output/public`) + **nginx** ile servis ediliyor (`Mng.Ui/Dockerfile` → `Mng.Ui/nginx.conf`). UI, OC çağrılarını same-origin `/api/operations/v1/...` yoluna yapıyor (`services/apiService.ts` `fetchFromOperations`). nginx.conf'ta `/api/auth`, `/api/keeper`, `/api/admin`, `/api/llm`, `/api/data`, `/api/v1` proxy blokları vardı ama **`/api/operations/` bloğu hiç yoktu** (`git log -S "api/operations" -- Mng.Ui/nginx.conf` boş). Dolayısıyla tüm OC runtime çağrıları `location /` → `try_files $uri $uri/ /index.html`'e düşüyordu: GET → index.html (HTML, JSON değil → boş/ham); POST → statik dosyaya POST olamaz → **405**. Dataset çağrıları `/api/data/` (DataGateway) proxy'li olduğundan çalışıyordu; sadece mngoperations'a giden runtime/profil/board-list/bildirim çağrıları kırıktı. *(Bu yüzden önceki deploylar health/smoke (gateway) ile yeşil görünüyordu ama tarayıcıdan OC runtime hiç çalışmamıştı.)*

**Fix:** `Mng.Ui/nginx.conf`'a eklendi (diğer bloklarla simetrik):
```nginx
location /api/operations/ {
    set $oc_auth $http_authorization;
    if ($arg_access_token != "") { set $oc_auth "Bearer $arg_access_token"; }
    client_max_body_size 50m;
    proxy_pass http://mngoperations:5086/api/;   # /api/operations/v1/... -> mngoperations /api/v1/...
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header Authorization $oc_auth;
    proxy_pass_header Access-Control-Allow-Origin;
    proxy_pass_header Access-Control-Allow-Methods;
    proxy_pass_header Access-Control-Allow-Headers;
}
```
(`mngui` + `mngoperations` aynı ağda `mng_common_mng_network`; mngoperations route'ları `/api/v1/...`.)

**Deploy/doğrulama:** `mngui` sync+build+up (31 May ~23:10, healthy). `http://192.168.20.20:3000/api/operations/v1/health/live` → **200 `application/json` `{"status":"alive"}`** (eskiden index.html/405). 3 belirtiyi de çözmesi beklenir; kullanıcı tarayıcıda (hard refresh sonrası) teyit edecek.

**✅ Commit edildi:** `Mng.Ui/nginx.conf` fix'i `main`'e commit+push edildi (`000e624` — `fix(ui-nginx): add /api/operations proxy to mngui`).

---

## Sıradaki işler

| # | Epic | Hedef |
|---|------|--------|
| ~~1~~ | ~~**E1-P1**~~ | ✅ Genel sekme yetki grupları (view/edit/admin) — Odak'ta canlı |
| ~~2~~ | ~~**W-CREATE**~~ | ✅ Yeni workspace oluşturma UI — Odak'ta canlı |
| ~~3~~ | ~~**E1-P2**~~ | ✅ Akışlar: geçiş `requiredFields` + `permissions.groups` — Odak'ta canlı |
| ~~4~~ | ~~**CC**~~ | ✅ Dinamik (computed) sütunlar (expr-eval, display-only) — Odak'ta canlı |
| ~~5~~ | ~~**A**~~ | ✅ `op_comments.attachments` (yorum ekleri) — Odak'ta canlı (31 May) |
| ~~6~~ | ~~**F**~~ | ✅ Operasyonel runtime: profilde durum geçişi (transition) uygulama — Odak'ta canlı (31 May); SLA-3 chip zaten ✅ (FC/BLC) |

---

## Görsel ilerleme

```text
[✓] Kurallar + Politikalar + Zamanlanmış işler + Admin jobs
[✓] SLA admin (politika CRUD)
[✓] Board admin (D1-B1…B5 UI)
[✓] Board liste enrichment (katalog ikon/renk + pool sütunları + person MO-side)
[✓] mngui Odak deploy (BL UI canlı)
[✓] Board liste aksiyonlar (yeni-iş modal + profil/düzenle/sil + MO DELETE)
[✓] Board liste server-side (sıralama/filtre/arama + gelişmiş arama + workspace scope)
[✓] Board liste audit/SLA sütunları + sabit actions sütunu
[✓] Form chrome: hibrit profil + yorum/timeline + SLA paneli + politika paneli + mention + ekler (DG file)
[✓] In-app bildirim paneli (op_notifications + mention görünür) — Odak'ta canlı
[✓] Gelişmiş arama gt/gte/lt/lte UI (BLF-8) — Odak'ta canlı
[✓] Relation alanlarda option/relation etiketi (BLF-9) — Odak'ta canlı
[✓] Tags alanlarda filtre combobox (BLF-10) — Odak'ta canlı (31 May)
[✓] createdBy kimlik düzeltmesi (NP-6) — Odak'ta canlı (31 May)
[✓] "Tümünü gör" bildirim sayfası (NP-7) — Odak'ta canlı (31 May)
[✓] Person grup alanları ad çözümü (BL-GRP + profil grup adı BL-GRP-2) — Odak'ta canlı (31 May)
[✓] Grup alan filtresi (BL-GRP-3, group kind + in/nin/eq/ne) — Odak'ta canlı (31 May)
[✓] Workspace yetki grupları (E1-P1) + Yeni workspace UI (W-CREATE) — Odak'ta canlı
[✓] Admin kapanış: akış geçiş requiredFields + yetki grupları (E1-P2) — Odak'ta canlı
[✓] Dinamik (computed) sütunlar (CC, expr-eval display-only) — Odak'ta canlı
[✓] Yorum ekleri (op_comments.attachments) — Odak'ta canlı (31 May)
[✓] Operasyonel runtime: profilde durum geçişi uygulama (F) — Odak'ta canlı (31 May); SLA-3 chip zaten ✓
[✓] F faz-2: geçiş requiredFields ön-toplama (F-T2) — Odak'ta canlı (31 May)
[✓] F faz-2: Kanban DnD ile durum geçişi (F-K) — Odak'ta canlı (31 May)
[✓] Board silme ilişki guard'ı (BO-5) + edit alan temizleme (BO-6) — Odak'ta canlı (31 May)
[✓] Performans optimizasyonu (PERF): profil ~%30 hızlanma + UI yapısal kazanımlar — Odak'ta canlı, main'e merge
[✓] Keeper by-ids toplu uç + Redis profil cache (BL-KB): person/grup dizin çözümü tek istekte (N+1 giderildi) + Keeper Redis cache (CRUD invalidation) — Odak'ta canlı (31 May, mngkeeper+mngoperations healthy)
[✓] OC-CMT: Yorumlar/Aktivite tab ayrımı + TipTap zengin editör + yazar adı çözümü + düzenle/sil — Odak'ta canlı (1 Haz)
[~] OC-PREV: Ekler & yorum eklerinde inline önizleme (görsel/PDF/metin) + composer ek butonu fix + PDF MIME fix — lokal build temiz, DEPLOY BEKLİYOR (1 Haz)
```
