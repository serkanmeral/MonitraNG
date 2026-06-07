# Widget Manifest Şeması

**Son güncelleme:** 7 Haziran 2026  
**Durum:** ✅ Planlama v1  
**İlişkili:** [ARCHITECTURE.md](./ARCHITECTURE.md) · [INTERACTIVITY_MODEL.md](./INTERACTIVITY_MODEL.md)

---

## 1. Genel bakış

Widget manifest, UI, dashboard layout ve (ileride) Reporting Servis arasındaki **tek sözleşmedir**. Üç seviye:

| Seviye | Kimlik | Depolama |
|--------|--------|----------|
| **Template** | `templateId` | `@widget_templates` veya seed JSON |
| **Definition** | `id` / `name` | `@widgets` |
| **Placement** | layout hücresi | `@dashboards.layout` |

Bu doküman primarily **Template** ve **Definition** manifest’ini tanımlar. Placement layout şeması mevcut `@dashboards` spec ile uyumludur.

---

## 2. Manifest versiyonu

```json
{
  "manifestVersion": "1.0"
}
```

Breaking değişikliklerde major artırılır. Definition kayıtları `templateVersion` ile hangi şablon sürümünden türediğini tutar.

---

## 3. Template manifest (Katman 1)

MonitraNG seed şablonu. Müşteri doğrudan düzenlemez; **Klonla** ile Definition oluşturulur.

### 3.1 TypeScript arayüzü

```typescript
interface WidgetTemplateManifest {
  manifestVersion: '1.0';
  templateId: string;           // "alarm.open-count-stat"
  templateVersion: string;      // "1.0.0"

  domain: 'alarm' | 'siem' | 'operation-core' | 'document-intelligence' | 'generic';
  /** İleride: 'monitoring' (metrik modülü — V1 plan dışı), 'compliance' */
  category: string;             // @widget_categories.name veya slug

  /** i18n key veya çok dilli obje */
  title: string | LocalizedString;
  description?: string | LocalizedString;
  tags?: string[];              // ["kpi", "realtime"]

  presentation: {
    kind: PresentationKind;
    defaultPreset: string;
    allowedPresets: string[];
  };

  dataBinding: DataBindingConfig;

  /** Kullanıcıya gösterilen parametre formu */
  parametersSchema: ParameterSchemaField[];

  interactions?: InteractionConfig;

  permissions?: {
    /** Bu template'i görmek için gereken DG dataset read izni */
    requiredDatasetRead?: string[];
    groups?: string[];
  };

  export?: ExportCapabilities;
}

type PresentationKind =
  | 'stat'
  | 'chart'
  | 'table'
  | 'list'
  | 'banner'
  | 'gauge'
  | 'map';

interface LocalizedString {
  tr?: string;
  en?: string;
  [locale: string]: string | undefined;
}
```

### 3.2 DataBindingConfig

```typescript
interface DataBindingConfig {
  /** Semantic referans — DG predefined query (alarm/siem/mo/oc) */
  queryRef?: string;

  /** Domain API referansı — Document Intelligence vb. */
  serviceRef?: string;          // "mngdocument:resources/search"

  /** queryRef veya serviceRef çözüldükten sonra giden parametreler */
  defaultParameters?: Record<string, ParameterValue>;

  /** Admin gelişmiş mod — seed içinde; UI'da gizli */
  advanced?: {
    getMethod: 'predefined' | 'default' | 'query' | 'aggregate';
    dataset?: string;
    predefined?: { queryName: string };
    default?: Record<string, unknown>;
    query?: { match: object };
    aggregate?: { pipeline: object[] };
    mapping?: Record<string, string>;
  };

  /** Yanıt → presentation field map (çoğu preset'te gömülü) */
  fieldMap?: {
    value?: string;
    label?: string;
    series?: string;
    x?: string;
    y?: string;
    rows?: string;
    total?: string;
  };
}

/** Parametre değeri: sabit, context ref veya kullanıcı girdisi */
type ParameterValue =
  | string
  | number
  | boolean
  | null
  | { $ref: string };            // "$timeRange.hours", "$variables.severity"
```

### 3.3 ParameterSchemaField

Designer wizard’da gösterilen form alanları.

```typescript
interface ParameterSchemaField {
  name: string;                   // "severity", "limit"
  type: 'string' | 'number' | 'boolean' | 'enum' | 'duration' | 'workspace' | 'asset';
  label: string | LocalizedString;
  description?: string | LocalizedString;
  required?: boolean;
  default?: ParameterValue;

  /** type=enum */
  enum?: Array<{ value: string | number; label: string | LocalizedString }>;

  /** type=duration — UI: "Son 24 saat" dropdown */
  durationPresets?: Array<'20m' | '1h' | '6h' | '24h' | '7d' | '30d'>;

  /** Context'ten geliyorsa designer'da gizle */
  hidden?: boolean;
  bindToContext?: string;         // "variables.severity"

  /** Gelişmiş modda göster */
  advanced?: boolean;
}
```

---

## 4. Definition manifest (Katman 2)

Müşteri kaydı. `@widgets` dataset alanlarına map edilir.

```typescript
interface WidgetDefinitionManifest extends Omit<WidgetTemplateManifest, 'templateId' | 'templateVersion'> {
  /** @widgets.name — unique */
  name: string;

  /** Kaynak şablon */
  templateId: string;
  templateVersion: string;

  /** Kullanıcının seçtiği preset */
  presentation: {
    kind: PresentationKind;
    preset: string;
    config?: Record<string, unknown>;  // ChartWidget / StatCard fine-tune
  };

  /** Kullanıcı parametre değerleri */
  parameters: Record<string, ParameterValue>;

  isActive: boolean;
  order?: number;
}
```

### 4.1 `@widgets` dataset alan eşlemesi

| Mevcut alan | Manifest karşılığı |
|-------------|-------------------|
| `name` | `name` |
| `title` | `title` |
| `description` | `description` |
| `category` | `category` (relation) |
| `type` | `presentation.kind` (legacy sync) |
| `dataSource` | `dataBinding.advanced` veya runtime `queryRef` çözümü |
| `config` | `presentation.config` + `parameters` |
| `permissions` | `permissions` |
| *(yeni)* `manifestVersion`, `templateId`, `templateVersion` | object field veya ayrı text alanları |

**Geçiş:** Eski kayıtlarda `templateId` yoksa `templateId: "legacy.custom"` kabul edilir; `dataSource` doğrudan kullanılır.

---

## 5. Parametre çözümleme

Runtime ve rapor snapshot’ında parametreler şu sırayla birleştirilir:

```
1. template.dataBinding.defaultParameters
2. definition.parameters
3. placement.overrides.parameters
4. surfaceContext.variables  ($variables.{key})
5. surfaceContext.timeRange  ($timeRange.{preset|from|to|hours})
```

### 5.1 `$ref` sözdizimi

| Ref | Kaynak |
|-----|--------|
| `$timeRange.preset` | `SurfaceContext.timeRange.preset` |
| `$timeRange.from` | ISO başlangıç |
| `$timeRange.to` | ISO bitiş |
| `$timeRange.hours` | preset’ten türetilmiş sayı (24h → 24) |
| `$variables.severity` | `SurfaceContext.variables.severity` |
| `$variables.workspaceId` | OC workspace bağlamı |
| `$locale` | `SurfaceContext.locale` |

### 5.2 Çözümleme örneği

Template default:
```json
{ "hours": { "$ref": "$timeRange.hours" }, "severity": { "$ref": "$variables.severity" } }
```

Context:
```json
{ "timeRange": { "preset": "24h" }, "variables": { "severity": "high" } }
```

DG predefined body:
```json
{ "hours": 24, "severity": "high" }
```

---

## 6. Presentation config

Preset seçildikten sonra bileşene giden `config` — mevcut `ChartWidget` / `StatCard` yapısıyla uyumlu.

### 6.1 Chart preset örneği

```json
{
  "kind": "chart",
  "preset": "chart-area-gradient",
  "config": {
    "type": "area",
    "height": 280,
    "xAxis": { "field": "bucket", "type": "datetime" },
    "yAxis": { "field": "count" },
    "chartOptions": {
      "stroke": { "curve": "smooth", "width": 2 },
      "fill": { "type": "gradient" }
    }
  }
}
```

### 6.2 Stat preset örneği

```json
{
  "kind": "stat",
  "preset": "stat-sparkline",
  "config": {
    "format": "number",
    "icon": "mdi-bell-alert",
    "color": "error",
    "sparkline": { "field": "trend", "type": "area", "height": 40 }
  }
}
```

---

## 7. Interaction config

```typescript
interface InteractionConfig {
  drillDown?: DrillDownConfig | DrillDownConfig[];
  rowClick?: DrillDownConfig;
  actions?: WidgetAction[];
}

interface DrillDownConfig {
  type: 'route' | 'external';
  label?: string | LocalizedString;
  path: string;                   // "/apps/alarm-center/alarms"
  paramMap: Record<string, string>;  // { "severity": "$row.severity", "status": "open" }
  openInNewTab?: boolean;
}

interface WidgetAction {
  id: string;
  label: string | LocalizedString;
  icon?: string;
  type: 'route' | 'workflow' | 'api';
  /** type=workflow */
  workflowId?: string;
  parameterMap?: Record<string, string>;
  /** Yetki — admin/manager grupları */
  requiredGroups?: string[];
}
```

---

## 8. Export capabilities (Reporting hook)

```typescript
interface ExportCapabilities {
  supportsPdf: boolean;
  supportsCsv: boolean;
  supportsPng: boolean;
  supportsSnapshot: boolean;
  snapshotTtlSeconds?: number;
}
```

Rapor motoru `supportsSnapshot: true` widget’ları manifest + çözülmüş parametrelerle kaydeder.

---

## 9. Placement (Katman 4)

Mevcut `@dashboards` layout col yapısı — değişiklik minimal.

```typescript
interface LayoutCol {
  span?: number;
  spanSm?: number;
  spanMd?: number;
  spanLg?: number;
  spanXl?: number;
  widgetId?: string;
  widgetOverrides?: {
    parameters?: Record<string, ParameterValue>;
    presentation?: Partial<WidgetDefinitionManifest['presentation']>;
    refreshSeconds?: number | null;
  };
  rows?: LayoutRow[];  // nested
}
```

---

## 10. Tam örnekler

### 10.1 Template — Açık alarm sayısı (Alarm)

```json
{
  "manifestVersion": "1.0",
  "templateId": "alarm.open-count-stat",
  "templateVersion": "1.0.0",
  "domain": "alarm",
  "category": "alarm-kpi",
  "title": { "tr": "Açık alarm sayısı", "en": "Open alarm count" },
  "description": { "tr": "Onaylanmamış açık alarmların anlık sayısı", "en": "Current count of open unacknowledged alarms" },
  "tags": ["kpi", "realtime"],
  "presentation": {
    "kind": "stat",
    "defaultPreset": "stat-simple",
    "allowedPresets": ["stat-simple", "stat-sparkline"]
  },
  "dataBinding": {
    "queryRef": "@alarms/queries/openCount",
    "defaultParameters": {
      "severity": { "$ref": "$variables.severity" }
    },
    "fieldMap": { "value": "count", "label": "label" }
  },
  "parametersSchema": [
    {
      "name": "severity",
      "type": "enum",
      "label": { "tr": "Önem derecesi", "en": "Severity" },
      "enum": [
        { "value": "", "label": { "tr": "Tümü", "en": "All" } },
        { "value": "critical", "label": { "tr": "Kritik", "en": "Critical" } },
        { "value": "high", "label": { "tr": "Yüksek", "en": "High" } }
      ],
      "bindToContext": "variables.severity"
    }
  ],
  "interactions": {
    "drillDown": {
      "type": "route",
      "path": "/apps/alarm-center/alarms",
      "paramMap": { "status": "open", "severity": "$variables.severity" }
    }
  },
  "export": {
    "supportsPdf": true,
    "supportsCsv": false,
    "supportsPng": true,
    "supportsSnapshot": true
  }
}
```

### 10.2 Template — SIEM saatlik olay trendi

```json
{
  "manifestVersion": "1.0",
  "templateId": "siem.events-hourly-trend",
  "templateVersion": "1.0.0",
  "domain": "siem",
  "category": "siem-charts",
  "title": { "tr": "Saatlik olay trendi", "en": "Hourly event trend" },
  "presentation": {
    "kind": "chart",
    "defaultPreset": "chart-area-gradient",
    "allowedPresets": ["chart-area-gradient", "chart-line-smooth", "chart-bar"]
  },
  "dataBinding": {
    "queryRef": "@siem_events/queries/eventsByHour",
    "defaultParameters": {
      "hours": { "$ref": "$timeRange.hours" }
    },
    "fieldMap": { "x": "bucket", "y": "count", "rows": "items" }
  },
  "parametersSchema": [
    {
      "name": "hours",
      "type": "duration",
      "label": { "tr": "Zaman aralığı", "en": "Time range" },
      "durationPresets": ["1h", "6h", "24h", "7d"],
      "default": { "$ref": "$timeRange.hours" },
      "bindToContext": "timeRange"
    }
  ],
  "interactions": {
    "drillDown": {
      "type": "route",
      "path": "/apps/siem-center/events",
      "paramMap": { "from": "$timeRange.from", "to": "$timeRange.to" }
    }
  },
  "export": {
    "supportsPdf": true,
    "supportsCsv": true,
    "supportsPng": true,
    "supportsSnapshot": true
  }
}
```

### 10.3 Definition — müşteri klonu

```json
{
  "manifestVersion": "1.0",
  "name": "noc-open-alarms-critical",
  "templateId": "alarm.open-count-stat",
  "templateVersion": "1.0.0",
  "domain": "alarm",
  "category": "alarm-kpi",
  "title": { "tr": "Kritik açık alarmlar", "en": "Critical open alarms" },
  "presentation": {
    "kind": "stat",
    "preset": "stat-sparkline",
    "config": { "color": "error", "icon": "mdi-alert-circle" }
  },
  "parameters": {
    "severity": "critical"
  },
  "dataBinding": {
    "queryRef": "@alarms/queries/openCount",
    "defaultParameters": {
      "severity": "critical"
    },
    "fieldMap": { "value": "count" }
  },
  "parametersSchema": [],
  "isActive": true,
  "permissions": { "groups": ["noc", "admin"] }
}
```

### 10.4 Placement — dashboard layout hücresi

```json
{
  "span": 3,
  "widgetId": "{{widgets.noc-open-alarms-critical.__dataId}}",
  "widgetOverrides": {
    "refreshSeconds": 60
  }
}
```

### 10.5 Template — DI klasör içeriği (serviceRef örneği)

```json
{
  "manifestVersion": "1.0",
  "templateId": "di.folder-children-table",
  "templateVersion": "1.0.0",
  "domain": "document-intelligence",
  "category": "di-lists",
  "title": { "tr": "Klasör içeriği", "en": "Folder contents" },
  "presentation": {
    "kind": "table",
    "defaultPreset": "table-compact",
    "allowedPresets": ["table-compact", "list-activity"]
  },
  "dataBinding": {
    "serviceRef": "mngdocument:resources/children",
    "defaultParameters": {
      "folderId": { "$ref": "$variables.folderId" },
      "limit": 10
    },
    "fieldMap": { "rows": "items" }
  },
  "parametersSchema": [
    {
      "name": "folderId",
      "type": "string",
      "label": { "tr": "Klasör", "en": "Folder" },
      "required": true,
      "description": { "tr": "Liste gösterilecek klasör", "en": "Folder to list" }
    }
  ],
  "interactions": {
    "rowClick": {
      "type": "route",
      "path": "/apps/document-intelligence",
      "paramMap": { "resourceId": "$row.id" }
    }
  },
  "export": {
    "supportsPdf": true,
    "supportsCsv": true,
    "supportsPng": false,
    "supportsSnapshot": true
  }
}
```

---

## 11. JSON Schema

Zorunlu alanlar:

**Template:** `manifestVersion`, `templateId`, `templateVersion`, `domain`, `title`, `presentation`, `dataBinding` (`queryRef` **veya** `serviceRef`)

**Definition:** `manifestVersion`, `name`, `templateId`, `title`, `presentation.preset`, `parameters`, `dataBinding`, `isActive`

Dosya: [schemas/widget-manifest-v1.schema.json](./schemas/widget-manifest-v1.schema.json)

---

## 12. queryRef / serviceRef → runtime çağrısı

**DG (queryRef):**
```
queryRef = "@alarms/queries/openCount"
  → POST /api/v1/data/@alarms/queries/openCount
  → body = resolvedParameters
```

**MngDocument (serviceRef):**
```
serviceRef = "mngdocument:resources/search"
  → GET /documents/api/v1/resources/search?q=...&folderId=...&limit=...
  → Mng.Ui server proxy; PermissionService filtreleri backend'de
```

`dataBinding` içinde **queryRef veya serviceRef** zorunlu (biri); ikisi birlikte değil.

Document Intelligence örnekleri: [DOMAIN_DOCUMENT_INTELLIGENCE.md](./DOMAIN_DOCUMENT_INTELLIGENCE.md)

`advanced.getMethod` tanımlıysa (legacy) doğrudan `widgetDataService` switch kullanılır.

---

## 13. Preset kayıt defteri (v1 seed)

| preset | kind | Açıklama |
|--------|------|----------|
| `stat-simple` | stat | Sayı + ikon |
| `stat-sparkline` | stat | Sayı + mini grafik |
| `chart-line-smooth` | chart | Düzgün çizgi |
| `chart-area-gradient` | chart | Alan + gradient |
| `chart-bar` | chart | Dikey bar |
| `chart-donut-breakup` | chart | Donut dağılım |
| `chart-combo-bar-line` | chart | Combo eksen |
| `chart-pie` | chart | Pasta |
| `table-compact` | table | Sıkışık tablo |
| `table-drilldown` | table | Satır tıklama |
| `list-activity` | list | Aktivite listesi |
| `banner-info` | banner | Bilgi bandı |
| `gauge-threshold` | gauge | Eşik göstergesi |
| `map-assets` | map | Asset haritası |

Preset → Vue map tablosu implementasyon detayı: `Mng.Ui` widget bileşenleri.
