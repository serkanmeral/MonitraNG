import type { FieldDefinition } from '@/stores/apps/dataset';
import type { AfFilterColumn } from '@/utils/afListFilters';
import { resolveAfFilterKind } from '@/utils/afListFilters';
import type { OdakHubListColumnConfig, OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import type { OdakHubListHeader } from '@/utils/odakSiparisHubListConfig';

const DEFAULT_VISIBLE_COUNT = 8;

/** Dataset şema alanından rapor listConfig üretir. */
export function defaultReportingListConfigFromFields(fields: FieldDefinition[]): OdakHubListConfig {
  const reportable = fields.filter((f) => Boolean(f.name?.trim()));
  const columns: OdakHubListColumnConfig[] = reportable.map((f, idx) => {
    const order = idx + 1;
    const isDateOrNumber = f.fieldType === 'datetime' || f.fieldType === 'number' || f.fieldType === 'incremental';
    const isTextLike = f.fieldType === 'text' || f.fieldType === 'select';
    return {
      fieldName: f.name,
      visible: order <= DEFAULT_VISIBLE_COUNT,
      order,
      sortable: isDateOrNumber || isTextLike || f.fieldType === 'bool',
      filterable: f.fieldType !== 'file' && f.fieldType !== 'object',
    };
  });

  const defaultSortField =
    reportable.find((f) => f.name === '__createdAt')?.name ??
    reportable.find((f) => f.fieldType === 'datetime')?.name ??
    reportable[0]?.name ??
    '';

  return {
    enableSearch: false,
    defaultSortBy: defaultSortField,
    defaultSortOrder: 'desc',
    columns,
  };
}

export function reportingFieldLabel(field: FieldDefinition | undefined, fieldName: string): string {
  if (field?.title?.trim()) return field.title.trim();
  return fieldName;
}

/** Eski birimId.ad → fieldName + relationDisplayField dönüşümü (yerinde). */
export function normalizeReportingListColumn(col: OdakHubListColumnConfig): OdakHubListColumnConfig {
  if (col.relationDisplayField?.trim()) return col;
  if (!col.fieldName.includes('.')) return col;
  const [root, ...rest] = col.fieldName.split('.');
  const sub = rest.join('.').trim();
  if (!root || !sub) return col;
  col.fieldName = root;
  col.relationDisplayField = sub;
  return col;
}

export function normalizeReportingListConfig(listConfig: OdakHubListConfig): void {
  for (const col of listConfig.columns) {
    normalizeReportingListColumn(col);
  }
}

/** Tablo slot / header key — relation için birimId.ad gibi. */
export function reportingColumnListKey(col: OdakHubListColumnConfig): string {
  normalizeReportingListColumn(col);
  const display = col.relationDisplayField?.trim();
  if (display) return `${col.fieldName}.${display}`;
  return col.fieldName;
}

/** DG fields + expand için kök alan adı. */
export function reportingColumnSourceField(col: OdakHubListColumnConfig): string {
  normalizeReportingListColumn(col);
  return col.fieldName.includes('.') ? col.fieldName.split('.')[0] : col.fieldName;
}

export function columnConfigByListKey(
  listConfig: OdakHubListConfig,
  listKey: string
): OdakHubListColumnConfig | undefined {
  return listConfig.columns.find((c) => reportingColumnListKey(c) === listKey);
}

/** VDataTable slot item may be raw row or `{ raw: row }` depending on table variant. */
export function reportingDataTableRow(
  item: Record<string, unknown> | { raw?: Record<string, unknown> } | null | undefined
): Record<string, unknown> {
  if (item != null && typeof item === 'object' && 'raw' in item && item.raw && typeof item.raw === 'object') {
    return item.raw as Record<string, unknown>;
  }
  return (item ?? {}) as Record<string, unknown>;
}

export function reportingQueryFieldNamesFromColumns(columns: OdakHubListColumnConfig[]): string[] {
  const set = new Set<string>();
  for (const col of columns) {
    if (col.virtual) continue;
    set.add(reportingColumnSourceField(col));
  }
  return [...set];
}

export function buildReportingListHeaders(
  listConfig: OdakHubListConfig,
  fields: FieldDefinition[],
  canViewColumn?: (fieldName: string) => boolean
): OdakHubListHeader[] {
  const fieldMap = new Map(fields.map((f) => [f.name, f]));
  const headers: OdakHubListHeader[] = [];

  for (const col of [...listConfig.columns].sort((a, b) => a.order - b.order)) {
    normalizeReportingListColumn(col);
    if (!col.visible) continue;
    const listKey = reportingColumnListKey(col);
    if (canViewColumn && !canViewColumn(listKey)) continue;

    let title = col.title?.trim();
    if (!title) {
      if (col.relationDisplayField?.trim()) {
        const root = fieldMap.get(col.fieldName);
        const rootLabel = reportingFieldLabel(root, col.fieldName);
        title = `${rootLabel} (${col.relationDisplayField})`;
      } else {
        const root = listKey.includes('.') ? listKey.split('.')[0]! : col.fieldName;
        title = reportingFieldLabel(fieldMap.get(root), listKey);
      }
    }

    headers.push({
      title,
      key: listKey,
      sortable: col.sortable,
      ...(col.width != null ? { width: col.width } : {}),
    });
  }

  return headers;
}

export function reportingEnumFieldOptions(
  fields: FieldDefinition[]
): Record<string, { value: string; title: string }[]> {
  const map: Record<string, { value: string; title: string }[]> = {};
  for (const f of fields) {
    const items = reportingSelectItemsFromField(f);
    if (items.length) map[f.name] = items;
  }
  return map;
}

export function reportingSelectItemsFromField(
  field: FieldDefinition | undefined
): { value: string; title: string }[] {
  if (!field?.options || typeof field.options !== 'object') return [];
  const items: { value: string; title: string }[] = [];
  const opts = field.options as Record<string, unknown>;
  if (Array.isArray(opts.items)) {
    for (const item of opts.items) {
      if (typeof item === 'string') items.push({ value: item, title: item });
      else if (item && typeof item === 'object') {
        const o = item as Record<string, unknown>;
        const value = String(o.value ?? o.id ?? o.key ?? '');
        const title = String(o.title ?? o.label ?? o.name ?? value);
        if (value) items.push({ value, title });
      }
    }
  } else if (Array.isArray(opts.values)) {
    for (const v of opts.values) {
      const s = String(v);
      items.push({ value: s, title: s });
    }
  }
  return items;
}

/** listConfig.filterable + şema → AfListFilters sütunları */
export function buildReportingFilterColumns(
  listConfig: OdakHubListConfig,
  fields: FieldDefinition[],
  canViewColumn?: (fieldName: string) => boolean
): AfFilterColumn[] {
  const fieldMap = new Map(fields.map((f) => [f.name, f]));
  return [...listConfig.columns]
    .filter((c) => c.filterable)
    .filter((c) => !canViewColumn || canViewColumn(reportingColumnListKey(c)))
    .sort((a, b) => a.order - b.order)
    .map((c) => {
      const sourceField = reportingColumnSourceField(c);
      const field = fieldMap.get(sourceField);
      const kind = resolveAfFilterKind(field?.fieldType ?? 'text');
      const col: AfFilterColumn = {
        key: sourceField,
        label: reportingFieldLabel(field, sourceField),
        kind,
      };
      if (kind === 'select') {
        col.selectItems = reportingSelectItemsFromField(field);
      }
      return col;
    });
}

export function visibleReportingColumnKeys(
  listConfig: OdakHubListConfig,
  canViewColumn?: (fieldName: string) => boolean
): string[] {
  return [...listConfig.columns]
    .filter((c) => c.visible)
    .map((c) => normalizeReportingListColumn({ ...c }))
    .filter((c) => !canViewColumn || canViewColumn(reportingColumnListKey(c)))
    .sort((a, b) => a.order - b.order)
    .map((c) => reportingColumnListKey(c));
}

/** @deprecated Use visibleReportingColumnKeys */
export function visibleReportingFieldNames(
  listConfig: OdakHubListConfig,
  canViewColumn?: (fieldName: string) => boolean
): string[] {
  return visibleReportingColumnKeys(listConfig, canViewColumn);
}

function readReportingCellValue(row: Record<string, unknown>, fieldName: string): unknown {
  if (!fieldName.includes('.')) return row[fieldName];
  let cur: unknown = row;
  for (const part of fieldName.split('.')) {
    if (cur == null || typeof cur !== 'object') return undefined;
    cur = (cur as Record<string, unknown>)[part];
  }
  return cur;
}

/** Relation expand sonrası hücre ham değeri. */
export function readReportingColumnValue(
  row: Record<string, unknown>,
  col: OdakHubListColumnConfig
): unknown {
  normalizeReportingListColumn(col);
  const display = col.relationDisplayField?.trim();
  if (display) {
    const rel = row[col.fieldName];
    if (rel == null || rel === '') return undefined;
    if (typeof rel === 'string' || typeof rel === 'number' || typeof rel === 'boolean') return rel;
    if (typeof rel === 'object') {
      const o = rel as Record<string, unknown>;
      const direct = o[display];
      if (direct != null && direct !== '') return direct;
    }
    return undefined;
  }
  return readReportingCellValue(row, reportingColumnListKey(col));
}

export function reportingCellRaw(row: Record<string, unknown>, fieldName: string): string {
  const val = readReportingCellValue(row, fieldName);
  return formatReportingCellScalar(val);
}

export function reportingCellRawForColumn(row: Record<string, unknown>, col: OdakHubListColumnConfig): string {
  return formatReportingCellScalar(readReportingColumnValue(row, col));
}

function formatReportingCellScalar(val: unknown): string {
  if (val == null) return '';
  if (typeof val === 'object') {
    const o = val as Record<string, unknown>;
    const label = o.ad ?? o.displayName ?? o.name ?? o.title ?? o.label ?? o.kod;
    if (label != null && label !== '') return String(label);
    const id = o.__dataId ?? o.dataId ?? o.id;
    if (id != null && id !== '') return String(id);
    try {
      return JSON.stringify(val);
    } catch {
      return String(val);
    }
  }
  return String(val);
}

export function columnConfigByField(
  listConfig: OdakHubListConfig,
  listKey: string
): OdakHubListColumnConfig | undefined {
  return columnConfigByListKey(listConfig, listKey) ?? listConfig.columns.find((c) => c.fieldName === listKey);
}

export function isReportingBoolField(fields: FieldDefinition[], fieldName: string): boolean {
  const root = fieldName.includes('.') ? fieldName.split('.')[0] : fieldName;
  return fields.find((f) => f.name === root)?.fieldType === 'bool';
}

/** DG fields sorgusu — relation sütunlarında kök alan. */
export function reportingQueryFieldNames(fieldNames: string[]): string[] {
  const set = new Set<string>();
  for (const name of fieldNames) {
    set.add(name.includes('.') ? name.split('.')[0] : name);
  }
  return [...set];
}

export function reportingQueryFieldNamesForListConfig(listConfig: OdakHubListConfig): string[] {
  const visible = listConfig.columns.filter((c) => c.visible);
  return reportingQueryFieldNamesFromColumns(visible);
}

/** true | false | null (unknown / empty) */
export function parseReportingBoolValue(raw: unknown): boolean | null {
  if (raw == null || raw === '') return null;
  if (typeof raw === 'boolean') return raw;
  if (typeof raw === 'number') return raw !== 0;
  const s = String(raw).trim().toLowerCase();
  if (s === 'true' || s === '1' || s === 'yes' || s === 'evet') return true;
  if (s === 'false' || s === '0' || s === 'no' || s === 'hayır' || s === 'hayir') return false;
  return null;
}
