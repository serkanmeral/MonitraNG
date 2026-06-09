/** Operation Core — alan havuzu yardımcıları (op_fields + core katalog) */

export type OcFieldCategory =
  | 'classification'
  | 'assignment'
  | 'technical'
  | 'resolution'
  | 'operational';

export const OC_FIELD_CATEGORIES: OcFieldCategory[] = [
  'classification',
  'assignment',
  'technical',
  'resolution',
  'operational',
];

export type OcCoreFieldGroup = 'identity' | 'classification' | 'assignment' | 'sla' | 'relation' | 'context';

export interface OcCoreFieldCatalogEntry {
  key: string;
  fieldType: string;
  group: OcCoreFieldGroup;
}

/** op_work_items şeması — sistem (core) alanları; op_fields kaydı yok, UI salt okunur */
export const OC_CORE_WORK_ITEM_FIELDS: OcCoreFieldCatalogEntry[] = [
  { key: 'key', fieldType: 'text', group: 'identity' },
  { key: 'title', fieldType: 'text', group: 'identity' },
  { key: 'description', fieldType: 'text', group: 'identity' },
  { key: 'typeId', fieldType: 'relation', group: 'classification' },
  { key: 'category', fieldType: 'text', group: 'classification' },
  { key: 'stateId', fieldType: 'relation', group: 'classification' },
  { key: 'stateFlowId', fieldType: 'relation', group: 'classification' },
  { key: 'priorityId', fieldType: 'relation', group: 'classification' },
  { key: 'impact', fieldType: 'text', group: 'classification' },
  { key: 'urgency', fieldType: 'text', group: 'classification' },
  { key: 'severity', fieldType: 'text', group: 'classification' },
  { key: 'assignee', fieldType: 'persons', group: 'assignment' },
  { key: 'assignmentGroups', fieldType: 'personGroups', group: 'assignment' },
  { key: 'watchers', fieldType: 'persons', group: 'assignment' },
  { key: 'reporter', fieldType: 'persons', group: 'assignment' },
  { key: 'slaPolicyId', fieldType: 'relation', group: 'sla' },
  { key: 'sla', fieldType: 'object', group: 'sla' },
  { key: 'dueDate', fieldType: 'datetime', group: 'sla' },
  { key: 'labels', fieldType: 'relation', group: 'relation' },
  { key: 'parentItemId', fieldType: 'relation', group: 'relation' },
  { key: 'workspaceId', fieldType: 'relation', group: 'context' },
  { key: 'boardId', fieldType: 'relation', group: 'context' },
  { key: 'origin', fieldType: 'object', group: 'context' },
];

export const OC_POOL_FIELD_TYPE_VALUES = [
  'text',
  'number',
  'bool',
  'datetime',
  'relation',
  'persons',
  'personGroups',
  'tags',
  'file',
] as const;

export type OcPoolFieldType = (typeof OC_POOL_FIELD_TYPE_VALUES)[number];

export const OC_FIELD_KEY_PATTERN = /^[a-zA-Z_][a-zA-Z0-9_]*$/;

/** Create form layout’a eklenmez (sistem / akış / bağlam). */
export const OC_FORM_LAYOUT_EXCLUDED_CORE_KEYS = new Set([
  'key',
  'stateId',
  'stateFlowId',
  'workspaceId',
  'origin',
  'sla',
]);

/**
 * op_forms yerleşim editöründe sürüklenebilir core alanlar.
 * Önceki MVP listesi yalnızca 6 alan içeriyordu; watchers/reporter vb. eksikti.
 */
export const OC_FORM_LAYOUT_CORE_FIELD_KEYS: readonly string[] = OC_CORE_WORK_ITEM_FIELDS.map(
  (f) => f.key
).filter((key) => !OC_FORM_LAYOUT_EXCLUDED_CORE_KEYS.has(key));

/**
 * Workspace politika şartlarında her zaman seçilebilir core alanlar.
 * Form yerleşiminde yoktur (stateId akıştan gelir) ama koşulda sık kullanılır.
 */
export const OC_POLICY_CONDITION_ALWAYS_CORE_KEYS: readonly string[] = ['stateId'];

export function resolveOcCoreFieldCardinality(key: string): 'single' | 'multi' {
  if (key === 'watchers' || key === 'assignmentGroups' || key === 'labels') return 'multi';
  return 'single';
}

export function parseOcFieldOptions(raw: unknown): Record<string, unknown> | null {
  if (raw == null) return null;
  if (typeof raw === 'object' && !Array.isArray(raw)) return raw as Record<string, unknown>;
  if (typeof raw === 'string' && raw.trim()) {
    try {
      const v = JSON.parse(raw) as unknown;
      if (v && typeof v === 'object' && !Array.isArray(v)) return v as Record<string, unknown>;
    } catch {
      return null;
    }
  }
  return null;
}

export function stringifyOcFieldOptions(value: Record<string, unknown> | null | undefined): string {
  if (!value || Object.keys(value).length === 0) return '';
  return JSON.stringify(value, null, 2);
}

/** i18n message compiler `{...}` JSON örneklerini parse edemez; hint doğrudan metin. */
export function resolveOcFieldOptionsHint(locale: string): string {
  return locale === 'en'
    ? 'E.g. {"choices":["Low","Medium","High"]} — optional.'
    : 'Örn. {"choices":["Düşük","Orta","Yüksek"]} — boş bırakılabilir.';
}
