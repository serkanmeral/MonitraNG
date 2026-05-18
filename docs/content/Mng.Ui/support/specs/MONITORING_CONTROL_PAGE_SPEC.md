# Monitoring Kontrol Sayfası — Spec

**Amaç:** Monitoring bileşenlerinin durumunu izleme, Engine/Agent kontrolü, canlı metrik önizleme ve sistem sağlığı ekranı.

**Referanslar:**
- [ORGANIZATION_PAGE_SPEC](ORGANIZATION_PAGE_SPEC.md)
- [MONITORING_REACTOR_ARCHITECTURE](../../../monitoring_plans/MONITORING_REACTOR_ARCHITECTURE.md)
- [MONITORING_DATA_PRODUCTION](../../../monitoring_plans/MONITORING_DATA_PRODUCTION.md)
- [MONITORING_IMPLEMENTATION_PLAN](../../../monitoring_plans/MONITORING_IMPLEMENTATION_PLAN.md) Faz 5

---

## 1. Genel Bakış

Kontrol sayfası, tanım sayfalarından (Organizasyon, Monitoring tanımları) farklı olarak **okuma ve kontrol** odaklıdır. CRUD yerine durum gösterimi, sync tetikleme ve canlı veri önizleme sunar.

| Bölüm | Kapsam | API / Backend |
|-------|--------|----------------|
| **1. Engine/Agent durum** | Engine online/offline, lastSeenAt; Agent durum | DG `mon_engines`, `mon_agents` |
| **2. Toplama kontrolü** | Config sync tetikleme | Reactor `POST /api/v1/engine/{engineId}/sync` (planlanan) |
| **3. Canlı metrik önizleme** | Son metrik değerleri (asset/engine filtreli) | Reactor `GET /api/v1/metrics/latest` (planlanan) |
| **4. Sistem sağlığı** | Reactor, DG, Keeper, Gateway health | Mevcut health endpoint'leri |

---

## 2. Route ve Sayfa

- **Route:** `/apps/monitoring/control` veya `/apps/control` (Side Menu yapısına göre karar verilecek)
- **Sayfa:** `pages/apps/monitoring/control/index.vue` (veya `pages/apps/control/index.vue`)
- **Layout:** BaseBreadcrumb + sekmeli veya kart tabanlı bölümler

---

## 3. Layout Önerisi

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  BaseBreadcrumb: Dashboard > Monitoring > Kontrol                             │
├─────────────────────────────────────────────────────────────────────────────┤
│  [Yenile]  (Opsiyonel: Otomatik yenileme toggle — 30 sn)                     │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │  Sistem Sağlığı (Özet)                                                  │ │
│  │  [Reactor ✓] [DataGateway ✓] [Keeper ✓] [Gateway ✓]                     │ │
│  └─────────────────────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────────────────┤
│  v-tabs: [Engine/Agent Durum] [Canlı Metrikler] [Detaylı Sağlık]             │
├─────────────────────────────────────────────────────────────────────────────┤
│  Tab 1: Engine/Agent Durum                                                    │
│  ┌───────────────────────────────────────────────────────────────────────┐ │
│  │  Engine tablosu: Ad | Durum (online/offline) | Son görülme | Agent sayısı│ │
│  │  | Config Sync Tetikle | Config String                                  │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
│  ┌───────────────────────────────────────────────────────────────────────┐ │
│  │  Agent tablosu (seçili engine'e göre filtrelenebilir):                 │ │
│  │  Ad | Engine | Durum | Aktif asset sayısı | Son veri (opsiyonel)        │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
│  Tab 2: Canlı Metrik Önizleme                                                │
│  ┌───────────────────────────────────────────────────────────────────────┐ │
│  │  Filtre: Asset, Engine, Collectible Code | [Yenile]                      │ │
│  │  Son N metrik (limit 50-100): asset, collectibleCode, value, timestamp │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
│  Tab 3: Detaylı Sağlık (opsiyonel)                                          │
│  ┌───────────────────────────────────────────────────────────────────────┐ │
│  │  Her servis için health check detayı (checks, message)                 │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Bölüm Detayları

### 4.1 Engine/Agent Durum

**Veri kaynağı:**
- `mon_engines` — DG data API (`fetchFromDataGateway('/api/v1/data/mon_engines')`)
- `mon_agents` — DG data API

**Engine durum hesaplama:**
- `lastSeenAt`: Reactor ingest başarılı olduğunda güncellenir (mon_engines)
- **Online:** `lastSeenAt` son X dakika içindeyse (örn. 10 dakika) — config edilebilir
- **Offline:** `lastSeenAt` boş veya eski

**Engine tablosu kolonları:**
| Kolon | Açıklama |
|-------|----------|
| Ad | engine.name |
| Durum | Chip: online (yeşil) / offline (gri) |
| Son görülme | lastSeenAt formatlanmış (örn. `12.02.2026 14:35`) |
| Agent sayısı | Bu engine'e bağlı agent sayısı |
| İşlemler | Config Sync Tetikle, Config String (mevcut Monitoring sayfasındaki gibi) |

**Agent tablosu:**
- Tüm agent'lar veya seçili engine'e göre filtrelenebilir
- Kolonlar: Ad, Engine adı, Durum (status chip), Aktif asset sayısı

**Yetkilendirme:** Config Sync Tetikle ve Config String butonları sadece `canEdit` (is_manager / is_admin).

---

### 4.2 Toplama Kontrolü — Sync Tetikleme

**Mevcut durum:** Reactor CRUD (Engine/Agent/Asset create/update/delete) sonrası otomatik MQTT sync mesajı yayınlanıyor. Manuel tetikleme için UI'dan çağrılabilecek endpoint **şu an yok**.

**Planlanan backend:**
```http
POST /api/v1/engine/{engineId}/sync
Authorization: Bearer {token}
```
- Reactor EngineController'a yeni endpoint
- `IMqttSyncPublisher.PublishSyncAsync(domain, engineId)` çağrılır
- Yanıt: `{ "success": true, "message": "Sync signal sent" }`

**UI:** Engine satırında "Config Sync Tetikle" butonu → Reactor API proxy (`/api/reactor/v1/engine/{engineId}/sync`) üzerinden POST.

**Not:** MQTT Host boşsa Reactor sync'i atlar; UI yine de başarılı yanıt alabilir (Reactor duruma göre 200 döner).

---

### 4.3 Canlı Metrik Önizleme

**Veri kaynağı:** `mon_metrics` — MongoDB Time Series, Reactor tarafından doğrudan yazılıyor; **DG data API üzerinden erişilemiyor**.

**Planlanan backend:**
```http
GET /api/v1/metrics/latest?assetId=...&engineId=...&collectibleCode=...&limit=50
Authorization: Bearer {token}
```
- Reactor'da yeni `MetricsController` veya `MonitoringController`
- MongoDB `mon_metrics` koleksiyonundan son N kayıt; domain JWT'den alınır
- Filtre: assetId, engineId, collectibleCode (opsiyonel)
- Limit: varsayılan 50, max 200

**Yanıt örneği:**
```json
{
  "items": [
    {
      "timestamp": "2026-02-12T14:35:00.000Z",
      "meta": { "assetId": "...", "engineId": "...", "collectibleCode": "cpu_usage" },
      "value": 34.5,
      "unit": "%"
    }
  ]
}
```

**UI:**
- Filtre: Asset dropdown, Engine dropdown, Collectible code (opsiyonel metin)
- Tablo: timestamp, asset (ad), collectibleCode, value, unit
- Yenile butonu; opsiyonel otomatik yenileme (30 sn)

**İlk aşama:** Backend endpoint yoksa bu tab "Yakında" veya devre dışı gösterilebilir.

---

### 4.4 Sistem Sağlığı

**Mevcut endpoint'ler:**
| Servis | Health endpoint | Not |
|--------|-----------------|-----|
| Reactor | `GET /api/v1/health` | HealthCheckService, Status: healthy/unhealthy |
| DG | Olası `/health` veya `/ready` | Projede kontrol edilmeli |
| Keeper | Olası `/health` | Projede kontrol edilmeli |
| Gateway | Ocelot genelde downstream health kullanmaz | Nginx/Gateway kendi health'i |

**Reactor health:** `GET {gatewayUrl}/reactor/api/v1/health` — Gateway üzerinden erişim. `AllowAnonymous` olduğu için token gerekmeyebilir; UI’dan CORS/auth durumuna göre karar.

**UI özet alanı:**
- Ok ikonu + servis adı (yeşil: healthy, kırmızı: unhealthy)
- Tıklanınca detay (checks, message) gösterilebilir

**Detaylı sağlık tab'ı:**
- Her servis için ayrı kart; health yanıtı JSON veya formatlanmış gösterim

---

## 5. Store ve API

### 5.1 Store Önerisi

**controlStore** (veya mevcut `monitoringStore` genişletilebilir):

```typescript
interface ControlState {
  engines: MonEngine[];
  agents: MonAgent[];
  engineAgentCount: Map<string, number>;
  loading: boolean;
  error: string | null;
  lastRefreshAt: Date | null;
  // Metrik önizleme
  latestMetrics: LatestMetricItem[];
  metricsLoading: boolean;
  metricsError: string | null;
  // Sistem sağlığı
  healthStatus: Record<string, { status: string; checks?: object }>;
  healthLoading: boolean;
}

// Actions
loadEnginesAndAgents(): Promise<void>;
triggerSync(engineId: string): Promise<{ success: boolean; message?: string }>;
loadLatestMetrics(filters?: { assetId?: string; engineId?: string; limit?: number }): Promise<void>;
loadHealthStatus(): Promise<void>;
```

### 5.2 API Proxy

Mevcut `server/api/reactor/[...path].ts` proxy'si kullanılır:
- `GET /api/reactor/v1/engine/config-string?engineId=...` — mevcut
- `POST /api/reactor/v1/engine/{engineId}/sync` — planlanan (path parametresi desteği gerekebilir)
- `GET /api/reactor/v1/metrics/latest?...` — planlanan
- `GET /api/reactor/v1/health` — health (CORS/auth’a göre proxy veya doğrudan)

**Not:** Mevcut proxy `[...path]` kullanıyor; `v1/engine/xxx/sync` gibi path'ler `path` parametresine düşmeli. Route yapısı kontrol edilmeli.

---

## 6. Yetkilendirme ve Görünürlük

| Öğe | canEdit (is_manager / is_admin) | Tüm kullanıcılar |
|-----|--------------------------------|-------------------|
| Engine/Agent listesi | Görüntüleme | Görüntüleme |
| Config Sync Tetikle | Görünür + tıklanabilir | Görünmez veya disabled |
| Config String | Görünür + tıklanabilir | Görünmez veya disabled |
| Canlı metrikler | Görüntüleme | Görüntüleme |
| Sistem sağlığı | Görüntüleme | Görüntüleme |

---

## 7. Implementasyon Sırası

### Faz 1 — Mevcut API ile (backend değişikliği yok)
1. Sayfa ve layout oluştur (`pages/apps/monitoring/control/index.vue`)
2. Engine/Agent durum tablosu — DG `mon_engines`, `mon_agents` verisi
3. Config String butonu — mevcut Reactor proxy
4. Sistem sağlığı özeti — Reactor health (varsa DG, Keeper)
5. Side menüye "Kontrol" linki

### Faz 2 — Sync tetikleme (Reactor endpoint gerekli)
1. Reactor: `POST /api/v1/engine/{engineId}/sync` endpoint
2. UI: Config Sync Tetikle butonu
3. Nuxt proxy route güncellemesi (path parametresi)

### Faz 3 — Canlı metrikler
1. Reactor: `GET /api/v1/metrics/latest` endpoint
2. UI: Canlı metrik tab'ı, filtreler, tablo

### Faz 4 — Asset Explorer (Tree + Detay Paneli)

**Amaç:** Organizasyon yapısına benzer tree; Item'lar ve altında Asset'ler. Asset seçildiğinde detay panelinde metrik verileri.

**Layout:**
```
┌─────────────────────────────────────────────────────────────────────────────┐
│  [Stat kartları] [Sistem sağlığı] [Grafikler]                                │
├─────────────────────────────────────────────────────────────────────────────┤
│  Tab: [Overview] [Asset Explorer]                                            │
├──────────────────┬──────────────────────────────────────────────────────────┤
│  Tree (sol)       │  Detay paneli (sağ)                                       │
│  ▼ Istanbul       │  Asset seçildi: "sunucu1"                                 │
│    ▼ Çamlıca      │  ─────────────────────────────────────────────────────  │
│      • sunucu1 ←  │  Bilgi: Ad, Tip, Durum, Item (parent)                    │
│      • PDU-01     │  collectible_config (beklenen metrikler)                  │
│      • sunucu1-OS │  ─────────────────────────────────────────────────────  │
│                   │  Son metrikler: [Tablo veya grafik - API hazır olunca]    │
└──────────────────┴──────────────────────────────────────────────────────────┘
```

**Veri kaynağı:**
- `organizationStore` — `mon_items`, `mon_assets`, `treeNodes`, `filteredTreeNodes`
- Aynı `OrganizationTreeView` / `OrganizationTreeItem` bileşenleri (read-only kullanım)

**Asset detay paneli:**
- **Item seçildiğinde:** Item adı, açıklama, kind, alt item/asset sayısı (özet)
- **Asset seçildiğinde:** Asset adı, tip, durum, bağlı Item, collectible_config (beklenen metrikler); **Son metrikler** bölümü (Reactor `GET /api/v1/metrics/latest?assetId=...` hazır olunca gerçek veri; şimdilik placeholder)

**Bileşen:**
- `ControlAssetDetailPanel.vue` — Seçilen node tipine göre Item veya Asset özeti + metrik placeholder

---

## 8. i18n Anahtarları

```json
{
  "monitoring.control": {
    "pageTitle": "Kontrol",
    "engineStatus": "Engine durumu",
    "agentStatus": "Agent durumu",
    "liveMetrics": "Canlı metrikler",
    "systemHealth": "Sistem sağlığı",
    "online": "Çevrimiçi",
    "offline": "Çevrimdışı",
    "triggerSync": "Config sync tetikle",
    "configString": "Config string",
    "lastSeen": "Son görülme",
    "agentCount": "Agent sayısı",
    "assetCount": "Asset sayısı",
    "refresh": "Yenile",
    "autoRefresh": "Otomatik yenile",
    "noMetrics": "Henüz metrik verisi yok",
    "healthHealthy": "Sağlıklı",
    "healthUnhealthy": "Sağlıksız"
  }
}
```

---

## 9. Checklist

- [ ] `pages/apps/monitoring/control/index.vue` — sayfa, layout, breadcrumb
- [ ] Engine/Agent durum tabloları (DG verisi)
- [ ] Online/offline hesaplama (lastSeenAt eşiği)
- [ ] Config String butonu (mevcut Reactor proxy)
- [ ] Sistem sağlığı özet kartları
- [ ] Side menüye Kontrol linki
- [ ] (Faz 2) Reactor sync endpoint + Config Sync Tetikle butonu
- [ ] (Faz 3) Reactor metrics/latest endpoint + Canlı metrik tab'ı
- [ ] i18n anahtarları (tr, en)
- [ ] canEdit ile buton görünürlüğü

---

Bu spec, kontrol sayfasının kapsamını, veri kaynaklarını ve implementasyon sırasını tanımlar. Backend endpoint'leri (sync, metrics) eklendikçe UI buna göre tamamlanacaktır.
