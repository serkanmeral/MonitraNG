/**
 * op_fields.options.lookup — statik enum ve dataset kaynaklı seçim alanları.
 * Spec: docs/odak/operationcore/ui/OC_UI_LOOKUP_FIELDS.md
 * Tablo seçici: docs/odak/operationcore/ui/OC_UI_DATASET_TABLE_PICKER.md
 */

export type OcLookupSource = 'static' | 'dataset';
export type OcLookupPresentation = 'dropdown' | 'autocomplete' | 'picker';
export type OcLookupColumnFormat = 'text' | 'date' | 'bool' | 'enum' | 'relationLabel';

export interface OcLookupStaticItem {
  value: string;
  label: string;
}

export interface OcLookupDependsOn {
  fieldKey: string;
  filterTemplate: string;
}

export interface OcLookupColumn {
  field: string;
  title?: string;
  sortable?: boolean;
  filterable?: boolean;
  width?: number | string;
  format?: OcLookupColumnFormat;
  enumMap?: Record<string, string>;
}

export interface OcLookupDefaultSort {
  field: string;
  dir: 'asc' | 'desc';
}

export interface OcLookupSelection {
  mode?: 'single' | 'multi';
  min?: number;
  max?: number;
  /**
   * Fields joined for chip / summary label (picker). Uses column formats (enum, relationLabel).
   * When empty, falls back to labelField.
   */
  displayFields?: string[];
  /** Joiner between displayFields; default " · ". */
  displaySeparator?: string;
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
  columns: OcLookupColumn[];
  defaultSort: OcLookupDefaultSort | null;
  selection: OcLookupSelection | null;
}

export const OC_LOOKUP_DEFAULT_VALUE_FIELD = '__dataId';
export const OC_LOOKUP_DEFAULT_LABEL_FIELD = 'name';
export const OC_LOOKUP_DEFAULT_PAGE_SIZE = 50;
export const OC_LOOKUP_DROPDOWN_MAX_ITEMS = 100;

const LOOKUP_PRESENTATIONS: OcLookupPresentation[] = ['dropdown', 'autocomplete', 'picker'];
const LOOKUP_COLUMN_FORMATS: OcLookupColumnFormat[] = [
  'text',
  'date',
  'bool',
  'enum',
  'relationLabel',
];

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

function parseEnumMap(raw: unknown): Record<string, string> | undefined {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return undefined;
  const out: Record<string, string> = {};
  for (const [k, v] of Object.entries(raw as Record<string, unknown>)) {
    const label = asNonEmptyString(v);
    if (label) out[k] = label;
  }
  return Object.keys(out).length ? out : undefined;
}

function parseColumns(raw: unknown): OcLookupColumn[] {
  if (!Array.isArray(raw)) return [];
  const out: OcLookupColumn[] = [];
  for (const row of raw) {
    if (!row || typeof row !== 'object') continue;
    const o = row as Record<string, unknown>;
    const field = asNonEmptyString(o.field ?? o.key ?? o.name);
    if (!field) continue;
    const formatRaw = asNonEmptyString(o.format)?.toLowerCase();
    const format = LOOKUP_COLUMN_FORMATS.includes(formatRaw as OcLookupColumnFormat)
      ? (formatRaw as OcLookupColumnFormat)
      : undefined;
    const col: OcLookupColumn = { field };
    const title = asNonEmptyString(o.title ?? o.label);
    if (title) col.title = title;
    if (typeof o.sortable === 'boolean') col.sortable = o.sortable;
    else if (typeof o.sortable === 'string') {
      const s = o.sortable.trim().toLowerCase();
      if (s === 'true' || s === '1') col.sortable = true;
      else if (s === 'false' || s === '0') col.sortable = false;
    }
    if (typeof o.filterable === 'boolean') col.filterable = o.filterable;
    else if (typeof o.filterable === 'string') {
      const s = o.filterable.trim().toLowerCase();
      if (s === 'true' || s === '1') col.filterable = true;
      else if (s === 'false' || s === '0') col.filterable = false;
    }
    if (o.width != null && o.width !== '') col.width = o.width as number | string;
    if (format) col.format = format;
    const enumMap = parseEnumMap(o.enumMap ?? o.enum_map);
    if (enumMap) col.enumMap = enumMap;
    out.push(col);
  }
  return out;
}

function parseDefaultSort(raw: unknown): OcLookupDefaultSort | null {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return null;
  const o = raw as Record<string, unknown>;
  const field = asNonEmptyString(o.field ?? o.key);
  if (!field) return null;
  const dirRaw = asNonEmptyString(o.dir ?? o.direction)?.toLowerCase();
  const dir = dirRaw === 'desc' ? 'desc' : 'asc';
  return { field, dir };
}

function parseStringList(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((x) => String(x ?? '').trim()).filter(Boolean);
}

function parseSelection(raw: unknown): OcLookupSelection | null {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return null;
  const o = raw as Record<string, unknown>;
  const modeRaw = asNonEmptyString(o.mode)?.toLowerCase();
  const mode = modeRaw === 'multi' || modeRaw === 'single' ? modeRaw : undefined;
  const min =
    typeof o.min === 'number' && Number.isFinite(o.min) && o.min >= 0 ? Math.round(o.min) : undefined;
  const max =
    typeof o.max === 'number' && Number.isFinite(o.max) && o.max > 0 ? Math.round(o.max) : undefined;
  const displayFields = parseStringList(
    o.displayFields ?? o.display_fields ?? o.chipLabelFields ?? o.chip_label_fields
  );
  const displaySeparator =
    asNonEmptyString(o.displaySeparator ?? o.display_separator) ?? undefined;
  if (!mode && min == null && max == null && !displayFields.length && !displaySeparator) {
    return null;
  }
  const sel: OcLookupSelection = {};
  if (mode) sel.mode = mode;
  if (min != null) sel.min = min;
  if (max != null) sel.max = max;
  if (displayFields.length) sel.displayFields = displayFields;
  if (displaySeparator) sel.displaySeparator = displaySeparator;
  return sel;
}

function finalizeLookupConfig(
  partial: Partial<OcLookupConfig> | null | undefined,
  legacyStatic: OcLookupStaticItem[]
): OcLookupConfig {
  return {
    source: partial?.source ?? 'dataset',
    presentation: partial?.presentation ?? 'autocomplete',
    valueField: partial?.valueField ?? OC_LOOKUP_DEFAULT_VALUE_FIELD,
    labelField: partial?.labelField ?? OC_LOOKUP_DEFAULT_LABEL_FIELD,
    staticItems: partial?.staticItems ?? legacyStatic,
    searchFields: partial?.searchFields ?? [],
    pageSize: partial?.pageSize ?? OC_LOOKUP_DEFAULT_PAGE_SIZE,
    filter: partial?.filter ?? null,
    dependsOn: partial?.dependsOn ?? null,
    columns: partial?.columns ?? [],
    defaultSort: partial?.defaultSort ?? null,
    selection: partial?.selection ?? null,
  };
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
  const columns = parseColumns(o.columns);
  const defaultSort = parseDefaultSort(o.defaultSort ?? o.default_sort);
  const selection = parseSelection(o.selection);

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
  if (columns.length) partial.columns = columns;
  if (defaultSort) partial.defaultSort = defaultSort;
  if (selection) partial.selection = selection;
  return Object.keys(partial).length ? partial : null;
}

/** options.lookup veya legacy choices[] okur. */
export function parseOcLookupFromFieldOptions(
  optionsRaw: unknown,
  fieldType?: string | null
): OcLookupConfig | null {
  const ft = (typeof fieldType === 'string' ? fieldType : '').toLowerCase();
  const obj =
    optionsRaw && typeof optionsRaw === 'object' && !Array.isArray(optionsRaw)
      ? (optionsRaw as Record<string, unknown>)
      : null;

  const lookupPartial = parseLookupBlock(obj?.lookup);

  let legacyStatic: OcLookupStaticItem[] = [];
  if (obj?.choices) {
    const choices = obj.choices;
    if (Array.isArray(choices)) {
      legacyStatic = choices
        .map((c) => {
          if (c && typeof c === 'object') {
            const o = c as Record<string, unknown>;
            const value = asNonEmptyString(o.value ?? o.id) ?? '';
            const label = asNonEmptyString(o.label ?? o.title ?? o.name) ?? value;
            return value ? { value, label } : null;
          }
          const s = asNonEmptyString(c);
          return s ? { value: s, label: s } : null;
        })
        .filter((x): x is OcLookupStaticItem => x != null);
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
      columns: lookupPartial?.columns ?? [],
      defaultSort: lookupPartial?.defaultSort ?? null,
      selection: lookupPartial?.selection ?? null,
    };
  }

  if (ft === 'relation') {
    if (!lookupPartial && !legacyStatic.length) {
      return finalizeLookupConfig(
        {
          source: 'dataset',
          presentation: 'autocomplete',
          valueField: OC_LOOKUP_DEFAULT_VALUE_FIELD,
          labelField: OC_LOOKUP_DEFAULT_LABEL_FIELD,
        },
        []
      );
    }
    return finalizeLookupConfig(lookupPartial, legacyStatic);
  }

  if (lookupPartial) {
    return finalizeLookupConfig(lookupPartial, legacyStatic);
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
  columns?: OcLookupColumn[];
  defaultSort?: OcLookupDefaultSort | null;
  selection?: OcLookupSelection | null;
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
    const columns = (config.columns ?? []).filter((c) => c.field?.trim());
    if (columns.length) lookup.columns = columns;
    if (config.defaultSort?.field) {
      lookup.defaultSort = {
        field: config.defaultSort.field,
        dir: config.defaultSort.dir === 'desc' ? 'desc' : 'asc',
      };
    }
    if (config.selection) {
      const sel: Record<string, unknown> = {};
      if (config.selection.mode) sel.mode = config.selection.mode;
      if (config.selection.min != null) sel.min = config.selection.min;
      if (config.selection.max != null) sel.max = config.selection.max;
      const displayFields = (config.selection.displayFields ?? [])
        .map((f) => f.trim())
        .filter(Boolean);
      if (displayFields.length) sel.displayFields = displayFields;
      const sep = config.selection.displaySeparator?.trim();
      if (sep) sel.displaySeparator = sep;
      if (Object.keys(sel).length) lookup.selection = sel;
    }
  }

  return { lookup };
}

export function resolveEffectiveLookupPresentation(
  config: OcLookupConfig | null | undefined
): 'dropdown' | 'autocomplete' | 'picker' {
  const p = config?.presentation ?? 'autocomplete';
  if (p === 'dropdown') return 'dropdown';
  if (p === 'picker') return 'picker';
  return 'autocomplete';
}

/** Effective table columns: explicit columns, else label + up to 2 searchFields (L4 fallback). */
export function resolveLookupPickerColumns(config: OcLookupConfig | null | undefined): OcLookupColumn[] {
  if (config?.columns?.length) return config.columns;
  const cols: OcLookupColumn[] = [
    {
      field: config?.labelField || OC_LOOKUP_DEFAULT_LABEL_FIELD,
      title: config?.labelField || 'Label',
      sortable: true,
    },
  ];
  for (const key of (config?.searchFields ?? []).slice(0, 2)) {
    if (key === cols[0].field) continue;
    cols.push({ field: key, title: key, sortable: false });
  }
  return cols;
}

/** Prefer human labels on expanded relation objects over raw __dataId. */
function resolveExpandedRelationLabel(raw: Record<string, unknown>): string | null {
  return (
    asNonEmptyString(
      raw.ad
        ?? raw.name
        ?? raw.title
        ?? raw.label
        ?? raw.demirbasNo
        ?? raw.kod
        ?? raw.unvan
        ?? raw.model
        ?? raw.marka
    ) ?? null
  );
}

/** Format a raw cell value for picker table display. */
export function formatLookupPickerCell(
  raw: unknown,
  column: OcLookupColumn,
  empty = '—'
): string {
  if (raw == null || raw === '') return empty;
  const format = column.format ?? 'text';

  if (format === 'bool') {
    if (raw === true || raw === 'true' || raw === 1 || raw === '1') return 'Evet';
    if (raw === false || raw === 'false' || raw === 0 || raw === '0') return 'Hayır';
    return String(raw);
  }

  if (format === 'date') {
    const d = new Date(String(raw));
    if (!Number.isNaN(d.getTime())) return d.toLocaleDateString('tr-TR');
    return String(raw);
  }

  if (format === 'enum') {
    const key = String(raw);
    return column.enumMap?.[key] ?? key;
  }

  if (format === 'relationLabel' || (typeof raw === 'object' && raw && !Array.isArray(raw))) {
    if (typeof raw === 'object' && raw && !Array.isArray(raw)) {
      const o = raw as Record<string, unknown>;
      return resolveExpandedRelationLabel(o) ?? asNonEmptyString(o.__dataId ?? o.id) ?? empty;
    }
    return String(raw);
  }

  return String(raw);
}

/**
 * Chip / summary label for a selected dataset row.
 * Uses selection.displayFields (joined) when set; otherwise labelField.
 * Column formats (relationLabel, enum, …) apply when columns are provided.
 */
export function formatLookupSelectionLabel(
  raw: Record<string, unknown>,
  options: {
    labelField: string;
    columns?: OcLookupColumn[] | null;
    displayFields?: string[] | null;
    displaySeparator?: string | null;
    fallbackId?: string;
  }
): string {
  const colByField = new Map((options.columns ?? []).map((c) => [c.field, c]));
  const sep = options.displaySeparator?.trim() || ' · ';
  const fields = (options.displayFields ?? []).map((f) => f.trim()).filter(Boolean);

  const formatField = (field: string): string => {
    const col = colByField.get(field) ?? { field };
    return formatLookupPickerCell(raw[field], col, '').trim();
  };

  if (fields.length) {
    const parts = fields.map(formatField).filter(Boolean);
    if (parts.length) return parts.join(sep);
  }

  const fromLabel = formatField(options.labelField);
  if (fromLabel) return fromLabel;

  const legacy =
    asNonEmptyString(raw.label) ??
    asNonEmptyString(raw.title) ??
    asNonEmptyString(raw.name) ??
    asNonEmptyString(raw.ad);
  if (legacy) return legacy;

  return options.fallbackId?.trim() || '';
}

/** Build DG filter fragment for a column filter value. */
export function buildLookupColumnFilterClause(
  column: OcLookupColumn,
  value: string
): string | null {
  const v = value.trim();
  if (!v || !column.field) return null;
  const field = column.field;
  if (column.format === 'enum') {
    return `${field}:eq:${v}`;
  }
  // Text / default — case-insensitive contains
  return `${field}:contains:${v}`;
}

/** Tek veya çoklu lookup/relation değerinden id listesi. */
export function collectLookupIdsFromValue(value: unknown): string[] {
  if (value === null || value === undefined || value === '') return [];
  if (Array.isArray(value)) {
    const ids: string[] = [];
    for (const entry of value) {
      const id = extractLookupStoredValue(entry);
      if (id) ids.push(id);
    }
    return ids;
  }
  const single = extractLookupStoredValue(value);
  return single ? [single] : [];
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
