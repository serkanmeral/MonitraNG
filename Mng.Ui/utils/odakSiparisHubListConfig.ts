import type { AfListColumnFormat } from '@/utils/afListColumnFormat';

export interface OdakHubListColumnConfig {
  fieldName: string;
  visible: boolean;
  order: number;
  sortable: boolean;
  filterable: boolean;
  width?: number;
  /** İsteğe bağlı sütun başlığı (relation alt alanları için). */
  title?: string;
  /** Relation kaynak alanı (fieldName) expand sonrası gösterilecek hedef alan — örn. birimId + ad. */
  relationDisplayField?: string;
  /** Sanal sütun — DG fields sorgusuna dahil edilmez (ör. katılımcı sayısı). */
  virtual?: boolean;
  format?: AfListColumnFormat;
}

export interface OdakHubListConfig {
  enableSearch?: boolean;
  defaultSortBy?: string;
  defaultSortOrder?: 'asc' | 'desc';
  columns: OdakHubListColumnConfig[];
}

export interface OdakHubListFieldDef {
  fieldName: string;
  listKey: string;
  defaultVisible: boolean;
  defaultSortable: boolean;
  defaultFilterable: boolean;
  defaultOrder: number;
  width?: number;
}

export function catalogColumnDefaults(catalog: OdakHubListFieldDef[]): OdakHubListColumnConfig[] {
  return catalog.map((d) => ({
    fieldName: d.fieldName,
    visible: d.defaultVisible,
    order: d.defaultOrder,
    sortable: d.defaultSortable,
    filterable: d.defaultFilterable,
    ...(d.width != null ? { width: d.width } : {}),
  }));
}

export function defaultHubListConfigFromCatalog(
  catalog: OdakHubListFieldDef[],
  options?: {
    enableSearch?: boolean;
    defaultSortBy?: string;
    defaultSortOrder?: 'asc' | 'desc';
  }
): OdakHubListConfig {
  return {
    enableSearch: options?.enableSearch ?? false,
    defaultSortBy: options?.defaultSortBy,
    defaultSortOrder: options?.defaultSortOrder ?? 'asc',
    columns: catalogColumnDefaults(catalog),
  };
}

function normalizeHubListColumn(raw: unknown, fallbackOrder: number): OdakHubListColumnConfig | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const fieldName = String(o.fieldName ?? o.FieldName ?? '').trim();
  if (!fieldName) return null;
  const relationDisplayField = String(o.relationDisplayField ?? o.RelationDisplayField ?? '').trim() || undefined;
  const title = String(o.title ?? o.Title ?? '').trim() || undefined;
  const virtual = o.virtual === true || o.Virtual === true;
  return {
    fieldName,
    visible: o.visible !== false && o.Visible !== false,
    order: Number(o.order ?? o.Order ?? fallbackOrder) || fallbackOrder,
    sortable: o.sortable === true || o.Sortable === true,
    filterable: o.filterable === true || o.Filterable === true,
    width: o.width != null ? Number(o.width) : o.Width != null ? Number(o.Width) : undefined,
    ...(title ? { title } : {}),
    ...(relationDisplayField ? { relationDisplayField } : {}),
    ...(virtual ? { virtual: true } : {}),
    format: (o.format ?? o.Format) as AfListColumnFormat | undefined,
  };
}

export function parseHubListConfig(raw: unknown, defaults: OdakHubListConfig): OdakHubListConfig {
  if (!raw || typeof raw !== 'object') return { ...defaults, columns: [...defaults.columns] };

  const root = raw as Record<string, unknown>;
  const lc = root.listConfig ?? root.ListConfig ?? root;
  if (!lc || typeof lc !== 'object') return { ...defaults, columns: [...defaults.columns] };

  const obj = lc as Record<string, unknown>;
  const columnsRaw = obj.columns ?? obj.Columns;
  const parsedColumns: OdakHubListColumnConfig[] = [];
  if (Array.isArray(columnsRaw)) {
    columnsRaw.forEach((item, idx) => {
      const col = normalizeHubListColumn(item, idx + 1);
      if (col) parsedColumns.push(col);
    });
  }

  const byField = new Map(defaults.columns.map((c) => [c.fieldName, { ...c }]));
  for (const col of parsedColumns) {
    byField.set(col.fieldName, col);
  }

  return {
    enableSearch: obj.enableSearch !== false && obj.EnableSearch !== false,
    defaultSortBy: String(obj.defaultSortBy ?? obj.DefaultSortBy ?? defaults.defaultSortBy ?? ''),
    defaultSortOrder:
      String(obj.defaultSortOrder ?? obj.DefaultSortOrder ?? defaults.defaultSortOrder ?? 'asc') === 'asc'
        ? 'asc'
        : 'desc',
    columns: [...byField.values()].sort((a, b) => a.order - b.order),
  };
}

export function mergeHubListConfig(saved: unknown, defaults: OdakHubListConfig): OdakHubListConfig {
  const parsed = parseHubListConfig(saved, defaults);
  const fieldSet = new Set(parsed.columns.map((c) => c.fieldName));
  const merged = [...parsed.columns];
  for (const col of defaults.columns) {
    if (!fieldSet.has(col.fieldName)) merged.push({ ...col });
  }
  merged.sort((a, b) => a.order - b.order);
  return { ...parsed, columns: merged };
}

/** Hub kaydında katalog dışı alanları (ör. yanlış scope birleşmesi) filtreler. */
export function filterHubListConfigToCatalog(
  config: OdakHubListConfig,
  catalog: OdakHubListFieldDef[]
): OdakHubListConfig {
  const allowed = new Set(catalog.map((d) => d.fieldName));
  return {
    ...config,
    columns: config.columns.filter((c) => allowed.has(c.fieldName)),
  };
}

export interface OdakHubListHeader {
  title: string;
  key: string;
  sortable: boolean;
  width?: number;
  minWidth?: number;
  align?: 'end';
}

export function buildFieldToListKeyMap(catalog: OdakHubListFieldDef[]): Record<string, string> {
  return Object.fromEntries(catalog.map((d) => [d.fieldName, d.listKey]));
}

export function buildListKeyToFieldMap(catalog: OdakHubListFieldDef[]): Record<string, string> {
  return Object.fromEntries(catalog.map((d) => [d.listKey, d.fieldName]));
}

export function buildHubListHeaders(
  listConfig: OdakHubListConfig,
  fieldToListKey: Record<string, string>,
  columnTitle: (fieldName: string, listKey: string) => string,
  canViewColumn?: (listKey: string) => boolean
): OdakHubListHeader[] {
  const headers: OdakHubListHeader[] = [];
  for (const col of [...listConfig.columns].sort((a, b) => a.order - b.order)) {
    if (!col.visible) continue;
    const listKey = fieldToListKey[col.fieldName] ?? col.fieldName;
    if (canViewColumn && !canViewColumn(listKey)) continue;
    headers.push({
      title: columnTitle(col.fieldName, listKey),
      key: listKey,
      sortable: col.sortable,
      ...(col.width != null ? { width: col.width } : {}),
    });
  }
  return headers;
}

export function hubListSortKeyFromField(fieldName: string, fieldToListKey: Record<string, string>): string {
  return fieldToListKey[fieldName] ?? fieldName;
}

export function hubFieldNameFromListSortKey(sortKey: string, listKeyToField: Record<string, string>): string {
  return listKeyToField[sortKey] ?? sortKey;
}
