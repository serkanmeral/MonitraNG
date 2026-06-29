import type { OcFormFieldRuntimeDto } from '@/types/apps/operationCore';
import { resolveOcFormFieldType } from '@/utils/ocFormFieldLabels';

/** Core alanlar — DG `op_work_items` şemasında çoklu `persons`. */
export const OC_CORE_PERSONS_MULTI_KEYS = new Set(['watchers']);

export type OcDynamicFieldWidget =
  | 'typeSelect'
  | 'prioritySelect'
  | 'boardSelect'
  | 'stateSelect'
  | 'staticSelect'
  | 'staticSelectMulti'
  | 'relationSelect'
  | 'relationSelectMulti'
  | 'tags'
  | 'text'
  | 'textarea'
  | 'richtext'
  | 'number'
  | 'bool'
  | 'date'
  | 'datetime'
  | 'persons'
  | 'personsMulti'
  | 'groupSelect'
  | 'groupSelectMulti'
  | 'file'
  | 'password';

/** Core alan key → op_* dataset (relation select). */
export const OC_CORE_RELATION_DATASET: Record<string, string> = {
  priorityId: 'op_priorities',
  boardId: 'op_boards',
  stateId: 'op_states',
  stateFlowId: 'op_state_flows',
  labels: 'op_tags',
  parentItemId: 'op_work_items',
};

export function isMultiCardinality(fieldKey: string, meta?: OcFormFieldRuntimeDto | null): boolean {
  if (OC_CORE_PERSONS_MULTI_KEYS.has(fieldKey)) return true;
  const c = (meta?.cardinality ?? 'single').toLowerCase();
  return c === 'multi' || c === 'multiple';
}

/** Keeper kullanıcı grubu seçici (personGroups / group pool alanları). */
export function isOcGroupPickerField(
  fieldKey: string,
  meta?: OcFormFieldRuntimeDto | null
): boolean {
  const ft = resolveOcFormFieldType(fieldKey, meta).toLowerCase();
  return ft === 'persongroups' || ft === 'persongroup' || ft === 'group';
}

/** Keeper kullanıcı seçici (personGroups hariç). */
export function isOcPersonsUserPickerField(
  fieldKey: string,
  meta?: OcFormFieldRuntimeDto | null
): boolean {
  const ft = resolveOcFormFieldType(fieldKey, meta).toLowerCase();
  if (isOcGroupPickerField(fieldKey, meta)) return false;
  return ft === 'persons' || ft === 'person';
}

export function resolveRelationDataset(fieldKey: string, meta?: OcFormFieldRuntimeDto | null): string | null {
  const fromMeta = meta?.relationDataset?.trim();
  if (fromMeta) return fromMeta;
  return OC_CORE_RELATION_DATASET[fieldKey] ?? null;
}

export function resolveOcDynamicFieldWidget(
  fieldKey: string,
  meta?: OcFormFieldRuntimeDto | null,
  options?: { masked?: boolean }
): OcDynamicFieldWidget {
  if (options?.masked) return 'password';

  if (fieldKey === 'typeId') return 'typeSelect';
  if (fieldKey === 'priorityId') return 'prioritySelect';
  if (fieldKey === 'boardId') return 'boardSelect';
  if (fieldKey === 'stateId') return 'stateSelect';
  // Çekirdek "Etiketler" alanı workspace tag kataloğunu (op_tags) kullanır → tags widget.
  if (fieldKey === 'labels') return 'tags';

  const ft = resolveOcFormFieldType(fieldKey, meta).toLowerCase();

  if (ft === 'richtext' || ft === 'rich_text' || ft === 'rich-text') return 'richtext';

  if (ft === 'number') return 'number';
  if (ft === 'bool' || ft === 'boolean') return 'bool';
  if (ft === 'date') return 'date';
  if (ft === 'datetime') return 'datetime';
  if (ft === 'file') return 'file';
  if (ft === 'persongroups' || ft === 'persongroup' || ft === 'group') {
    return isMultiCardinality(fieldKey, meta) ? 'groupSelectMulti' : 'groupSelect';
  }
  if (ft === 'persons' || ft === 'person') {
    return isMultiCardinality(fieldKey, meta) ? 'personsMulti' : 'persons';
  }
  // 'tags' → workspace etiket kataloğu (op_tags) destekli, inline oluşturmalı seçici.
  if (ft === 'tags') return 'tags';
  if (ft === 'select') {
    return isMultiCardinality(fieldKey, meta) ? 'staticSelectMulti' : 'staticSelect';
  }
  if (ft === 'relation' || ft === 'array') {
    return isMultiCardinality(fieldKey, meta) ? 'relationSelectMulti' : 'relationSelect';
  }
  if (ft === 'text') return 'text';

  return 'text';
}

export function coerceBoolValue(value: unknown): boolean {
  if (value === true || value === 'true' || value === 1 || value === '1') return true;
  return false;
}

export function coerceNumberValue(value: unknown): number | null {
  if (value === null || value === undefined || value === '') return null;
  const n = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(n) ? n : null;
}

/** v-select menü — overflow’lu kart/dialog içinde kesilmemesi için body overlay + z-index. */
export type OcSelectMenuContext = 'default' | 'dialog';

export function buildOcSelectMenuProps(context: OcSelectMenuContext = 'default') {
  const inDialog = context === 'dialog';
  return {
    zIndex: inDialog ? 3100 : 2600,
    maxHeight: 320,
    scrollStrategy: 'reposition' as const,
    ...(inDialog ? { contentClass: 'oc-select-menu-overlay' } : {}),
  };
}

export function recordToDatasetItems(
  rows: unknown[],
  options?: { idKey?: string; labelKey?: string }
): { title: string; value: string }[] {
  const idKey = options?.idKey ?? '__dataId';
  const labelKey = options?.labelKey ?? 'name';
  const items: { title: string; value: string }[] = [];
  for (const row of rows) {
    if (!row || typeof row !== 'object') continue;
    const o = row as Record<string, unknown>;
    const id = String(o[idKey] ?? o.dataId ?? o.id ?? '').trim();
    if (!id) continue;
    const title = String(o[labelKey] ?? o.label ?? o.title ?? o.key ?? id).trim() || id;
    items.push({ title, value: id });
  }
  return items;
}
