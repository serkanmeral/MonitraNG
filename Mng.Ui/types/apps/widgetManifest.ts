/**
 * Widget manifest v1 — docs/odak/widgets/MANIFEST_SCHEMA.md
 */

export type ManifestVersion = '1.0';

export type WidgetDomain =
  | 'alarm'
  | 'siem'
  | 'operation-core'
  | 'document-intelligence'
  | 'generic'
  | 'monitoring'
  | 'compliance';

export type PresentationKind =
  | 'stat'
  | 'chart'
  | 'table'
  | 'list'
  | 'banner'
  | 'gauge'
  | 'map'
  | 'embed';

export type LocalizedString = string | { tr?: string; en?: string; [locale: string]: string | undefined };

export type ParameterValue =
  | string
  | number
  | boolean
  | null
  | { $ref: string };

export interface DataBindingConfig {
  queryRef?: string;
  serviceRef?: string;
  defaultParameters?: Record<string, ParameterValue>;
  fieldMap?: Record<string, string>;
  advanced?: Record<string, unknown>;
}

export interface PresentationConfig {
  kind: PresentationKind;
  preset?: string;
  defaultPreset?: string;
  allowedPresets?: string[];
  config?: Record<string, unknown>;
}

export interface ParameterSchemaField {
  name: string;
  type: string;
  label?: LocalizedString;
  description?: LocalizedString;
  required?: boolean;
  default?: ParameterValue;
  enum?: Array<{ value: string | number; label?: LocalizedString }>;
  durationPresets?: string[];
  hidden?: boolean;
  bindToContext?: string;
  advanced?: boolean;
}

export interface ExportCapabilities {
  supportsPdf: boolean;
  supportsCsv: boolean;
  supportsPng: boolean;
  supportsSnapshot: boolean;
  snapshotTtlSeconds?: number;
}

export interface WidgetTemplateManifest {
  manifestVersion: ManifestVersion;
  templateId: string;
  templateVersion: string;
  domain: WidgetDomain;
  category: string;
  title: LocalizedString;
  description?: LocalizedString;
  tags?: string[];
  presentation: PresentationConfig;
  dataBinding: DataBindingConfig;
  parametersSchema?: ParameterSchemaField[];
  interactions?: Record<string, unknown>;
  permissions?: { requiredDatasetRead?: string[]; groups?: string[] };
  export?: ExportCapabilities;
}

export interface WidgetDefinitionManifest extends Omit<WidgetTemplateManifest, 'templateId' | 'templateVersion'> {
  name: string;
  templateId: string;
  templateVersion: string;
  parameters?: Record<string, ParameterValue>;
  isActive: boolean;
  order?: number;
}

/** @widget_templates DG kaydı (üst düzey alanlar + manifest) */
export interface WidgetTemplateRecord {
  __dataId?: string;
  dataId?: string;
  templateId: string;
  templateVersion: string;
  domain: WidgetDomain;
  category: string;
  title: string;
  description?: string;
  tags?: string[];
  manifest: WidgetTemplateManifest;
  isSystem: boolean;
  isActive: boolean;
  order?: number;
}

export interface SurfaceContext {
  locale?: string;
  timeRange?: {
    preset?: string;
    from?: string;
    to?: string;
    hours?: number;
  };
  variables?: Record<string, string | number | boolean | null | undefined>;
}

/** Runtime fetch — Faz 1 WidgetHost */
export interface ManifestDataBinding {
  kind: 'queryRef' | 'serviceRef' | 'static';
  queryRef?: string;
  serviceRef?: string;
  parameters: Record<string, unknown>;
  fieldMap?: Record<string, string>;
  mapping?: Record<string, string>;
  /** stat → tek değer; rows → dizi (chart/table) */
  responseShape?: 'stat' | 'rows';
}
