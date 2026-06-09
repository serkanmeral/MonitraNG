/**
 * op_fields.options.lookup — statik enum ve dataset kaynaklı seçim alanları.
 * Spec: docs/odak/operationcore/ui/OC_UI_LOOKUP_FIELDS.md
 */

export type OcLookupSource = 'static' | 'dataset';
export type OcLookupPresentation = 'dropdown' | 'autocomplete' | 'picker';

export interface OcLookupStaticItem {
  value: string;
  label: string;
}

export interface OcLookupDependsOn {
  fieldKey: string;
  filterTemplate: string;
}

export interface OcLookupConfig {
  source: OcLookupSource;
  presentation: OcLookupPresentation;
  valueField: string;
  labelField: string;
  staticItems: OcLookupStaticItem[];
  searchFields: string[];
  pageSize: number;
  filter: string | null;
  dependsOn: OcLookupDependsOn | null;
}

export const OC_LOOKUP_DEFAULT_VALUE_FIELD = '__dataId';
export const OC_LOOKUP_DEFAULT_LABEL_FIELD = 'name';
export const OC_LOOKUP_DEFAULT_PAGE_SIZE = 50;
export const OC_LOOKUP_DROPDOWN_MAX_ITEMS = 100;

const LOOKUP_PRESENTATIONS: OcLookupPresentation[] = ['dropdown', 'autocomplete', 'picker'];

function asNonEmptyString(raw: unknown): string | null {
  if (raw == null) return null;
  const s = String(raw).trim();
  return s || null;
}

function parseStaticItems(raw: unknown): OcLookupStaticItem[] {
  if (!Array.isArray(raw)) return [];
  const out: OcLookupStaticItem[] = [];
  for (const row of raw) {
    if (!row || typeof row !== 'object') continue;
    const o = row as Record<string, unknown>;
    const value = asNonEmptyString(o.value ?? o.id);
    const label = asNonEmptyString(o.label ?? o.title ?? o.name);
    if (!value || !label) continue;
    out.push({ value, label });
  }
  return out;
}

function parseDependsOn(raw: unknown): OcLookupDependsOn | null {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return null;
  const o = raw as Record<string, unknown>;
  const fieldKey = asNonEmptyString(o.fieldKey);
  const filterTemplate = asNonEmptyString(o.filterTemplate ?? o.filter);
  if (!fieldKey || !filterTemplate) return null;
  return { fieldKey, filterTemplate };
}

function parseLookupBlock(raw: unknown): Partial<OcLookupConfig> | null {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return null;
  const o = raw as Record<string, unknown>;

  const sourceRaw = asNonEmptyString(o.source)?.toLowerCase();
  const source: OcLookupSource | undefined =
    sourceRaw === 'static' || sourceRaw === 'dataset' ? sourceRaw : undefined;

  const presRaw = asNonEmptyString(o.presentation)?.toLowerCase();
  const presentation = LOOKUP_PRESENTATIONS.includes(presRaw as OcLookupPresentation)
    ? (presRaw as OcLookupPresentation)
    : undefined;

  const valueField = asNonEmptyString(o.valueField ?? o.idField);
  const labelField = asNonEmptyString(o.labelField ?? o.displayField ?? o.textField);

  const staticItems = parseStaticItems(o.staticItems ?? o.items ?? o.choices);
  const searchFieldsRaw = o.searchFields ?? o.search_fields;
  const searchFields = Array.isArray(searchFieldsRaw)
    ? searchFieldsRaw.map((x) => String(x).trim()).filter(Boolean)
    : undefined;

  const pageSizeRaw = o.pageSize ?? o.page_size ?? o.limit;
  let pageSize: number | undefined;
  if (typeof pageSizeRaw === 'number' && Number.isFinite(pageSizeRaw) && pageSizeRaw > 0) {
    pageSize = Math.round(pageSizeRaw);
  }

  const filter = asNonEmptyString(o.filter);
  const dependsOn = parseDependsOn(o.dependsOn ?? o.depends_on);

  const partial: Partial<OcLookupConfig> = {};
  if (source) partial.source = source;
  if (presentation) partial.presentation = presentation;
  if (valueField) partial.valueField = valueField;
  if (labelField) partial.labelField = labelField;
  if (staticItems.length) partial.staticItems = staticItems;
  if (searchFields?.length) partial.searchFields = searchFields;
  if (pageSize) partial.pageSize = pageSize;
  if (filter) partial.filter = filter;
  if (dependsOn) partial.dependsOn = dependsOn;
  return Object.keys(partial).length ? partial : null;
}

/** options.lookup veya legacy choices[] okur. */
export function parseOcLookupFromFieldOptions(
  optionsRaw: unknown,
  fieldType?: string | null
): OcLookupConfig | null {
  const ft = (fieldType ?? '').toLowerCase();
  const obj =
    optionsRaw && typeof optionsRaw === 'object' && !Array.isArray(optionsRaw)
      ? (optionsRaw as Record<string, unknown>)
      : null;

  const lookupPartial = parseLookupBlock(obj?.lookup);

  // Legacy: { choices: ["A","B"] } veya { choices: [{value,label}] }
  let legacyStatic: OcLookupStaticItem[] = [];
  if (obj?.choices) {
    const choices = obj.choices;
    if (Array.isArray(choices)) {
      legacyStatic = choices.map((c) => {
        if (c && typeof c === 'object') {
          const o = c as Record<string, unknown>;
          const value = asNonEmptyString(o.value ?? o.id) ?? '';
          const label = asNonEmptyString(o.label ?? o.title ?? o.name) ?? value;
          return value ? { value, label } : null;
        }
        const s = asNonEmptyString(c);
        return s ? { value: s, label: s } : null;
      }).filter((x): x is OcLookupStaticItem => x != null);
    }
  }

  if (ft === 'select') {
    const staticItems = lookupPartial?.staticItems?.length
      ? lookupPartial.staticItems
      : legacyStatic;
    return {
      source: 'static',
      presentation: lookupPartial?.presentation ?? 'dropdown',
      valueField: lookupPartial?.valueField ?? 'value',
      labelField: lookupPartial?.labelField ?? 'label',
      staticItems,
      searchFields: lookupPartial?.searchFields ?? [],
      pageSize: lookupPartial?.pageSize ?? OC_LOOKUP_DEFAULT_PAGE_SIZE,
      filter: lookupPartial?.filter ?? null,
      dependsOn: lookupPartial?.dependsOn ?? null,
    };
  }

  if (ft === 'relation') {
    if (!lookupPartial && !legacyStatic.length) {
      return {
        source: 'dataset',
        presentation: 'autocomplete',
        valueField: OC_LOOKUP_DEFAULT_VALUE_FIELD,
        labelField: OC_LOOKUP_DEFAULT_LABEL_FIELD,
        staticItems: [],
        searchFields: [],
        pageSize: OC_LOOKUP_DEFAULT_PAGE_SIZE,
        filter: null,
        dependsOn: null,
      };
    }
    return {
      source: lookupPartial?.source ?? 'dataset',
      presentation: lookupPartial?.presentation ?? 'autocomplete',
      valueField: lookupPartial?.valueField ?? OC_LOOKUP_DEFAULT_VALUE_FIELD,
      labelField: lookupPartial?.labelField ?? OC_LOOKUP_DEFAULT_LABEL_FIELD,
      staticItems: lookupPartial?.staticItems ?? legacyStatic,
      searchFields: lookupPartial?.searchFields ?? [],
      pageSize: lookupPartial?.pageSize ?? OC_LOOKUP_DEFAULT_PAGE_SIZE,
      filter: lookupPartial?.filter ?? null,
      dependsOn: lookupPartial?.dependsOn ?? null,
    };
  }

  if (lookupPartial) {
    return {
      source: lookupPartial.source ?? 'dataset',
      presentation: lookupPartial.presentation ?? 'autocomplete',
      valueField: lookupPartial.valueField ?? OC_LOOKUP_DEFAULT_VALUE_FIELD,
      labelField: lookupPartial.labelField ?? OC_LOOKUP_DEFAULT_LABEL_FIELD,
      staticItems: lookupPartial.staticItems ?? legacyStatic,
      searchFields: lookupPartial.searchFields ?? [],
      pageSize: lookupPartial.pageSize ?? OC_LOOKUP_DEFAULT_PAGE_SIZE,
      filter: lookupPartial.filter ?? null,
      dependsOn: lookupPartial.dependsOn ?? null,
    };
  }

  return null;
}

export function buildOcLookupFieldOptionsPayload(config: {
  fieldType: string;
  source?: OcLookupSource;
  presentation?: OcLookupPresentation;
  valueField?: string;
  labelField?: string;
  staticItems?: OcLookupStaticItem[];
  searchFields?: string[];
  pageSize?: number;
  filter?: string | null;
  dependsOnFieldKey?: string;
  dependsOnFilterTemplate?: string;
}): Record<string, unknown> {
  const ft = config.fieldType.toLowerCase();
  const lookup: Record<string, unknown> = {};

  if (ft === 'select') {
    lookup.source = 'static';
    lookup.presentation = config.presentation ?? 'dropdown';
    const items = (config.staticItems ?? []).filter((i) => i.value.trim() && i.label.trim());
    if (items.length) lookup.staticItems = items;
  } else if (ft === 'relation') {
    lookup.source = 'dataset';
    lookup.presentation = config.presentation ?? 'autocomplete';
    const valueField = config.valueField?.trim() || OC_LOOKUP_DEFAULT_VALUE_FIELD;
    const labelField = config.labelField?.trim() || OC_LOOKUP_DEFAULT_LABEL_FIELD;
    lookup.valueField = valueField;
    lookup.labelField = labelField;
    const searchFields = (config.searchFields ?? []).map((s) => s.trim()).filter(Boolean);
    if (searchFields.length) lookup.searchFields = searchFields;
    const pageSize = config.pageSize ?? OC_LOOKUP_DEFAULT_PAGE_SIZE;
    if (pageSize !== OC_LOOKUP_DEFAULT_PAGE_SIZE) lookup.pageSize = pageSize;
    const filter = config.filter?.trim();
    if (filter) lookup.filter = filter;
    const parentKey = config.dependsOnFieldKey?.trim();
    const filterTemplate = config.dependsOnFilterTemplate?.trim();
    if (parentKey && filterTemplate) {
      lookup.dependsOn = { fieldKey: parentKey, filterTemplate };
    }
  }

  return { lookup };
}

/** Picker henüz yok — autocomplete ile render. */
export function resolveEffectiveLookupPresentation(
  config: OcLookupConfig | null | undefined
): 'dropdown' | 'autocomplete' {
  const p = config?.presentation ?? 'autocomplete';
  if (p === 'dropdown') return 'dropdown';
  return 'autocomplete';
}

/** {{parentValue}} yer tutucusunu üst alan değeri ile doldurur (DG filter dizesi). */
export function resolveLookupDependsOnFilter(
  template: string,
  parentValue: unknown
): string | null {
  const parentId = extractLookupStoredValue(parentValue);
  if (!parentId) return null;
  const resolved = template.split('{{parentValue}}').join(parentId);
  if (resolved.includes('{{')) return null;
  return resolved.trim() || null;
}

export function extractLookupStoredValue(value: unknown): string | null {
  if (value === null || value === undefined || value === '') return null;
  if (Array.isArray(value)) {
    const first = value[0];
    return extractLookupStoredValue(first);
  }
  if (typeof value === 'object') {
    const o = value as Record<string, unknown>;
    return asNonEmptyString(o.__dataId ?? o.id ?? o.value);
  }
  return asNonEmptyString(value);
}

export function lookupStaticItemsToSelectItems(
  items: OcLookupStaticItem[]
): { title: string; value: string }[] {
  return items.map((i) => ({ title: i.label, value: i.value }));
}

/** Dataset şema alanları — label/value combobox için. */
export function buildLookupFieldKeyItems(
  fields: Array<{ name: string; title?: string; fieldType?: string }> | undefined
): { title: string; value: string }[] {
  const base = [{ title: '__dataId (ID)', value: '__dataId' }];
  if (!fields?.length) return base;
  const extra = fields
    .filter((f) => f.name && f.fieldType !== 'object' && f.fieldType !== 'relation')
    .map((f) => ({
      title: `${f.title?.trim() || f.name} (${f.name})`,
      value: f.name,
    }));
  return [...base, ...extra];
}
