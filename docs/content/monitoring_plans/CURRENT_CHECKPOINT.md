# Monitoring / UI — Devam Noktası

**Son güncelleme:** 2025-02-10 — Monitoring birleşik sayfa iyileştirmeleri (açıklamalar, cron builder, i18n TR/EN, Engine validasyonu) tamamlandı.

---

## Tamamlananlar

### Veri katmanı (Faz 0)
- Tüm monitoring dataset'leri oluşturuluyor: `scripts/tests/MngDataGateway/dataset/setup-monitoring-datasets.ps1`
- **mon_collectible_templates** seed: PDU (MngSim) ve Router (MngSim) SNMP şablonları script ile ekleniyor
- Token/SSL: `get-token.ps1` ve dataset script'i HTTPS için `curl -k` kullanıyor

### Mng.Ui — Asset Type Tanımları
- Sayfa: `/apps/asset-type-definitions` — sekmeler: **Aileler | Tipler | Şablonlar**
- Aile CRUD (altında tip varsa silme devre dışı), Tip CRUD, Şablon CRUD
- **CollectiblesEditor** bileşeni: expansion panel, metoda göre alan vurgusu (SNMP→OID, REST→Path, SSH/WMI→Metrik anahtarı), boş durum, "Metrik ekle"
- Tip formunda **Şablon uygula** dropdown'ı (metoda göre filtrelenmiş şablonlar)
- Store: `stores/apps/assetTypeDefinitions.ts` — families, types, templates, loadAll, templateOptionsByMethod

### Mng.Ui — Organizasyon
- Sayfa: `/apps/organization` — tree + sağ panel form
- **Tree:** Item hiyerarşisi + her item altında Asset'ler; relation alanları **string'e normalize** edildi (store + form):
  - `itemId`, `type`, `parentId` — API obje döndürse bile `normalizeRelationId` / `normalizeTypeId` ile string
- Asset'ler item altında görünüyor; Asset tipi dropdown'da "[object Object]" sorunu giderildi
- Item/Asset formları: connection_info, collectible_config; silme onay modal; canEdit (is_manager)

### Mng.Ui — Monitoring ekranları (birleşik sayfa)
- **Birleşik sayfa:** `/apps/monitoring` — dört sekme: **Toplama periyotları** | **İzleme aralıkları** | **Engine'ler** | **Agent'lar**
- Her sekmede: liste (arama), Yeni/Ekle, Yenile, CRUD modal, silme onay dialog'u
- Periyotlar: mon_collection_periods — ad, cron ifadesi, açıklama
- Schedules: mon_schedules — ad, tip (sürekli/zamanlanmış), config (weekdays, startTime, endTime)
- Engine'ler: mon_engines — ad, durum, username, password, sendSchedule, configSyncPeriodMinutes
- Agent'lar: mon_agents — ad, engineId, defaultPeriodId, defaultScheduleId, asset_configs
- Store: `stores/apps/monitoring.ts` — loadAll, periodOptions, scheduleOptions, engineOptions, CRUD per entity
- Tipler: `types/apps/monitoring.ts` — MonCollectionPeriod, MonSchedule, MonEngine, MonAgent, MonAgentAssetConfig
- **Eski ayrı sayfalar** (`/apps/collection-periods`, `/apps/schedules`, `/apps/engines`, `/apps/agents`) hâlâ mevcut; menüde tek link için `/apps/monitoring` kullanılabilir
- **İyileştirmeler:** Her sekmede açıklama metni (info alert); Toplama periyotları ve Engine’de cron ifadesi için **Cron oluştur** modal’ı; tüm metinler için **i18n (TR/EN)** — `monitoring.common`, `collectionPeriods`, `schedules`, `engines`, `agents`, `cronBuilder`; Engine formunda **v-form doğrulama** (zorunlu alan uyarısı); İzleme aralıklarında Zamanlanmış gün seçimi reaktivite düzeltmesi

---

## Önemli dosya yolları

| Ne | Yol |
|----|-----|
| Dataset script | `scripts/tests/MngDataGateway/dataset/setup-monitoring-datasets.ps1` |
| Token | `scripts/tests/MngDataGateway/auth/get-token.ps1`, `load-token.ps1` |
| Organizasyon sayfası | `Mng.Ui/pages/apps/organization/index.vue` |
| Organizasyon store | `Mng.Ui/stores/apps/organization.ts` |
| Asset/Item formları | `Mng.Ui/components/apps/organization/OrganizationAssetForm.vue`, `OrganizationItemForm.vue` |
| Asset Type Tanımları sayfası | `Mng.Ui/pages/apps/asset-type-definitions/index.vue` |
| CollectiblesEditor | `Mng.Ui/components/apps/asset-type-definitions/CollectiblesEditor.vue` |
| Spec'ler | `docs/content/Mng.Ui/support/specs/ORGANIZATION_PAGE_SPEC.md`, `ASSET_TYPE_DEFINITIONS_SPEC.md` |
| Implementasyon planı | `docs/content/monitoring_plans/MONITORING_IMPLEMENTATION_PLAN.md` |
| Monitoring tipleri | `Mng.Ui/types/apps/monitoring.ts` |
| Monitoring store | `Mng.Ui/stores/apps/monitoring.ts` |
| Monitoring (birleşik) sayfa | `Mng.Ui/pages/apps/monitoring/index.vue` |
| Reactor API proxy | `Mng.Ui/server/api/reactor/[...path].ts` |
| Eski sayfalar (yönlendirme) | `Mng.Ui/pages/apps/collection-periods|schedules|engines|agents/index.vue` |

---

## Sonraki adımlar — UI tarafında önerilen sıra

1. **Temizlik (kısa):** Eski dört monitoring sayfasını (`/apps/collection-periods`, `/apps/schedules`, `/apps/engines`, `/apps/agents`) kaldırmak veya `/apps/monitoring`’e yönlendirmek; Side Menu’da “Monitoring tanımları” linkini `/apps/monitoring` yapmak.
2. **Workflow UI (Faz 3.8):** `mon_workflows` için CRUD sayfası — liste, ekleme/düzenleme (scope, collectibleCode, condition, actions), silme. Backend’den bağımsız, dataset üzerinden yapılabilir.
3. **Monitoring Dashboard (Faz 5.6):** Metrik görselleştirme — widget/grafik ile `mon_metrics` (veya Reactor’dan gelen veri) gösterme. Reactor ingest hazır olduktan sonra anlamlı.
4. **Diğer UI iyileştirmeleri:** Agent formunda zorunlu alan doğrulaması (Engine’deki gibi); Monitoring sayfasında hata mesajlarının i18n’i; istenirse diğer dillere (fr, ar, zh) monitoring anahtarlarının eklenmesi.

**Backend odaklı sonraki adımlar (plan):** MngReactor (Faz 1) → MngEngine (Faz 2) → MngWorkflow (Faz 3) → MngSim (Faz 4) → Tamamlama (Faz 5).
