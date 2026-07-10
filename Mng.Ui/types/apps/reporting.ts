/** Report catalog + designer types (local until DG R2). */

import type { AfListFilter } from '@/utils/afListFilters';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import type { OpFormLayoutSection } from '@/types/apps/operationCore';
import type { OdakFieldPoliciesBlob, OdakFieldVisibilityPolicy } from '@/utils/odakSiparisFieldPolicies';

/** DI dm_template_categories ile aynı şema (rapor kataloğu ağacı). */
export interface ReportingCategory {
  id: string;
  parentId: string | null;
  ancestorIds: string[];
  name: string;
  description: string | null;
  sortOrder: number;
  status: string;
  createdBy: string | null;
  createdAt: string | null;
  updatedAt: string | null;
}

export interface ReportingCreateCategoryRequest {
  name: string;
  description?: string;
  parentId?: string | null;
}

export interface ReportingRenameCategoryRequest {
  name: string;
}

/** Çalışma zamanı rapor parametreleri — gelişmiş filtre panelinden bağımsız. */
export type ReportingParameterType = 'person' | 'year' | 'statusTab' | 'search';

export type ReportingParameterWidget =
  | 'select'
  | 'buttonGroup'
  | 'personPicker'
  | 'search'
  | 'number'
  | 'date'
  | 'dateRange';

export interface ReportingParameterChoiceBinding {
  value: string;
  title: string;
  filters: AfListFilter[];
}

/** @deprecated Parametreler birbirinden bağımsız AND ile birleşir; yeni tanımlarda kullanmayın. */
export interface ReportingParameterFieldFromParameter {
  parameterId: string;
  map: Record<string, string>;
  defaultField?: string;
  skipChoiceValues?: string[];
}

export interface ReportingParameterBinding {
  kind: 'fieldEq' | 'choiceFilters' | 'datePartRange' | 'dateRange' | 'search';
  /** fieldEq | datePartRange | dateRange */
  field?: string;
  /** @deprecated */
  fieldFromParameter?: ReportingParameterFieldFromParameter;
  /**
   * datePartRange (year) — aynı yıl aralığı birden fazla tarih alanında OR ile uygulanır.
   * Durum vb. başka parametrelere bağlı değildir; tüm parametreler $and ile birleşir.
   */
  orDateFields?: string[];
  /** datePartRange */
  part?: 'year' | 'month' | 'quarter';
  emptyMeans?: 'noFilter';
  /** choiceFilters */
  choices?: ReportingParameterChoiceBinding[];
}

export type ReportingParameterOptions =
  | {
      kind: 'yearRange';
      min?: number;
      max?: number | 'currentYear';
      includeAll?: boolean;
    }
  | {
      kind: 'quarterRange';
      min?: number;
      max?: number | 'currentYear';
    }
  | {
      kind: 'static';
      items: { value: string; title: string }[];
      includeAll?: boolean;
    };

export interface ReportingStatusTabOption {
  value: string;
  title: string;
  /** null = filtre uygulanmaz (tümü). */
  filter: AfListFilter | null;
}

export interface ReportingReportParameter {
  id: string;
  /** Geriye dönük uyumluluk; yeni tanımlarda widget + binding tercih edilir. */
  type: ReportingParameterType;
  label: string;
  required: boolean;
  widget?: ReportingParameterWidget;
  binding?: ReportingParameterBinding;
  options?: ReportingParameterOptions;
  /** @deprecated binding.field (fieldEq) */
  field?: string;
  defaultValue?: string;
  /** @deprecated binding (datePartRange) */
  dateField?: string;
  /** @deprecated binding.choices */
  statusOptions?: ReportingStatusTabOption[];
}

/** Gelecek: expand toolbar aksiyonları (navigate, link, command). */
export interface ReportingExpandAction {
  id: string;
  label: string;
  type: 'navigate' | 'link' | 'command';
  config?: Record<string, unknown>;
}

/** Expand panel — parent satıra bağlı alt dataset tablosu. */
export interface ReportingExpandChildListConfig {
  datasetName: string;
  /** Child dataset alanı (örn. parentTrainingId). */
  linkField: string;
  /** Parent satır alanı; varsayılan __dataId. */
  parentField?: string;
  listConfig: OdakHubListConfig;
  sort?: string;
  limit?: number;
  expand?: boolean;
  emptyMessage?: string;
  /** Bağlı liste özet metrikleri (aggregate). */
  summary?: ReportingSummaryConfig;
}

/** Ek expand sekmeleri — varsayılan «Detay» sekmesi expand.sections ile gelir. */
export interface ReportingExpandChildListTab {
  id: string;
  title: string;
  childList: ReportingExpandChildListConfig;
  /**
   * Child liste sütun görünürlüğü (ana rapor fieldPolicies ile aynı model).
   * Yok / boş = tüm sütunlar herkese açık.
   */
  fieldPolicies?: OdakFieldPoliciesBlob;
  /**
   * Sekmenin kendisinin görünürlüğü (rapor visibilityPolicies ile aynı model).
   * Yok / boş = sekme herkese görünür.
   */
  visibilityPolicies?: OdakFieldVisibilityPolicy[];
}

export interface ReportingExpandConfig {
  enabled: boolean;
  hideEmptyFields: boolean;
  heading: string;
  intro: string;
  sections: OpFormLayoutSection[];
  fieldCols: Record<string, number>;
  actions: ReportingExpandAction[];
  /** Alan detayı dışındaki sekmeler (bağlı dataset listeleri). */
  tabs?: ReportingExpandChildListTab[];
  /** İlk açılan sekme: 'fields' veya tabs[].id */
  defaultTabId?: string;
}

/** Özet metrik — DG aggregate ($group). */
export type ReportingSummaryMetricKind = 'count' | 'sum';
export type ReportingSummaryPlacement = 'cards' | 'footer' | 'both' | 'none';
export type ReportingSummaryValueFormat = 'number' | 'integer';

export interface ReportingSummaryMetric {
  id: string;
  label: string;
  kind: ReportingSummaryMetricKind;
  /** sum için zorunlu. */
  field?: string;
  format?: ReportingSummaryValueFormat;
}

export interface ReportingSummaryConfig {
  placement: ReportingSummaryPlacement;
  metrics: ReportingSummaryMetric[];
}

/** Belge üretimi bağlamı — D3/D4 için parentRow/childRow rezerv. */
export type ReportingDocumentContextType = 'reportRun' | 'parentRow' | 'childRow';

/** Rapor ↔ DI şablon bağı (şablon exclusive: tek rapor). */
export interface ReportingDocumentBinding {
  id: string;
  templateId: string;
  templateCode?: string;
  label: string;
  contextType: ReportingDocumentContextType;
  /** Varsayılan: ['Reports', reportId] */
  outputFolderSegments?: string[];
}

export interface ReportingReportDefinition {
  id: string;
  title: string;
  description?: string;
  categoryId: string | null;
  datasetName: string;
  listConfig: OdakHubListConfig;
  expand: ReportingExpandConfig;
  fieldPolicies: OdakFieldPoliciesBlob;
  defaultFilters: AfListFilter[];
  /** Boş = herkese görünür (sütun yetkisi ile aynı model). */
  visibilityPolicies: OdakFieldVisibilityPolicy[];
  /** Çalıştırıcıda gösterilen parametreler (varsayılan filtrelerden ayrı). */
  parameters: ReportingReportParameter[];
  /** Tablo üstü kart / alt footer özet metrikleri. */
  summary?: ReportingSummaryConfig;
  /** DI belge şablon bağları. */
  documentBindings?: ReportingDocumentBinding[];
  createdAt: string;
  updatedAt: string;
}

export interface ReportingCatalog {
  reports: ReportingReportDefinition[];
}

export interface ReportingCatalogSnapshot {
  catalog: ReportingCatalog;
  updatedAt: string;
}

/** @deprecated Use ReportingReportDefinition — designer draft slice. */
export interface ReportingDraft {
  title: string;
  datasetName: string;
  listConfig: OdakHubListConfig;
  expand: ReportingExpandConfig;
  fieldPolicies: OdakFieldPoliciesBlob;
  defaultFilters: AfListFilter[];
  visibilityPolicies?: OdakFieldVisibilityPolicy[];
  parameters?: ReportingReportParameter[];
}

export interface ReportingPreviewResult {
  rows: Record<string, unknown>[];
  totalCount: number;
  /** Mongo aggregate pipeline when showQuery=true on DG request. */
  dgQuery?: unknown;
  /** Full DG list path including query string (for debugging). */
  requestUrl?: string;
}
