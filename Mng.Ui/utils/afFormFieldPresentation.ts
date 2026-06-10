/** Automated Forms — form designer alan sunumu (widget) tipleri */

export type AfTextWidget = 'text' | 'textarea' | 'richtext';
export type AfChoiceWidget = 'select' | 'autocomplete';

export interface AfFieldLayoutEntry {
  columnSpan?: number;
  group?: string;
  textWidget?: AfTextWidget;
  choiceWidget?: AfChoiceWidget;
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
