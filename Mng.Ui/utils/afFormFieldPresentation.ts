/** Automated Forms — form designer alan sunumu (widget) tipleri */

export type AfTextWidget = 'text' | 'textarea' | 'richtext';
export type AfChoiceWidget = 'select' | 'autocomplete';
export type AfDirectoryEntity = 'group' | 'user';
export type AfDirectoryValueKey = 'id' | 'name';

export interface AfFieldVisibleWhen {
  field: string;
  equals?: string | number | boolean;
}

export interface AfFieldLayoutEntry {
  columnSpan?: number;
  group?: string;
  /** Alanın altında görünen kısa açıklama. */
  hint?: string;
  textWidget?: AfTextWidget;
  choiceWidget?: AfChoiceWidget;
  /** Metin alanında Keeper kişi/grup seçici. Kayıt hâlâ metin olarak durur. */
  directory?: AfDirectoryEntity;
  /** Grup seçicisinde saklanan değer. Ad, izin ayarı gibi isim karşılaştırmaları içindir. */
  directoryValueKey?: AfDirectoryValueKey;
  visibleWhen?: AfFieldVisibleWhen;
}

export interface AfStaticSelectItem {
  value: string;
  label: string;
}

export function supportsTextWidget(fieldType: string): boolean {
  return fieldType === 'text';
}

export function supportsChoiceWidget(fieldType: string): boolean {
  return ['relation', 'persons', 'personGroups', 'select'].includes(fieldType);
}

export function defaultTextWidget(): AfTextWidget {
  return 'text';
}

export function defaultChoiceWidget(fieldType: string): AfChoiceWidget {
  return fieldType === 'select' ? 'select' : 'autocomplete';
}

export function resolveTextWidget(
  layout?: AfFieldLayoutEntry | null
): AfTextWidget {
  const w = layout?.textWidget;
  if (w === 'textarea' || w === 'richtext') return w;
  return 'text';
}

export function resolveChoiceWidget(
  fieldType: string,
  layout?: AfFieldLayoutEntry | null
): AfChoiceWidget {
  const w = layout?.choiceWidget;
  if (w === 'select' || w === 'autocomplete') return w;
  return defaultChoiceWidget(fieldType);
}

export function resolveDirectoryPicker(
  fieldType: string,
  layout?: AfFieldLayoutEntry | null
): { entity: AfDirectoryEntity; valueKey: AfDirectoryValueKey } | null {
  const valueKey: AfDirectoryValueKey = layout?.directoryValueKey === 'name' ? 'name' : 'id';
  if (fieldType === 'personGroups') {
    return { entity: 'group', valueKey };
  }
  if (fieldType === 'persons') {
    return { entity: 'user', valueKey: 'id' };
  }
  if (fieldType === 'text' && (layout?.directory === 'group' || layout?.directory === 'user')) {
    return { entity: layout.directory, valueKey };
  }
  return null;
}

export function isFieldVisibleForValues(
  layout: AfFieldLayoutEntry | null | undefined,
  values: Record<string, unknown> | null | undefined
): boolean {
  const when = layout?.visibleWhen;
  if (!when?.field) return true;
  const current = values?.[when.field];
  if (when.equals === undefined) return true;
  return String(current ?? '') === String(when.equals);
}

/** DG dataset field.options → statik select maddeleri */
export function parseAfStaticSelectItems(field: Record<string, unknown> | null | undefined): AfStaticSelectItem[] {
  if (!field) return [];
  const options = field.options as Record<string, unknown> | undefined;
  if (!options) return [];

  const lookup = options.lookup as Record<string, unknown> | undefined;
  const raw =
    lookup?.staticItems ??
    options.staticItems ??
    options.items ??
    options.choices;

  if (!Array.isArray(raw)) return [];

  const items: AfStaticSelectItem[] = [];
  for (const row of raw) {
    if (!row || typeof row !== 'object') continue;
    const o = row as Record<string, unknown>;
    const value = String(o.value ?? o.id ?? '').trim();
    const label = String(o.label ?? o.title ?? o.name ?? value).trim();
    if (value && label) items.push({ value, label });
  }
  return items;
}
