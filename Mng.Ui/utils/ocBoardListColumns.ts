import type {
  OcColumnFormat,
  OpBoardColumnConfig,
  OpBoardListColumnConfig,
  OpStateFlow,
  OcWorkItemCard,
} from '@/types/apps/operationCore';
import { formatCellValue, type OcFormatOptions } from '@/utils/ocColumnFormat';

/** Liste tablosunda gösterilebilecek sütunlar (MO WorkItemCard alanları). */
export const OC_BOARD_LIST_TABLE_COLUMN_KEYS = [
  'key',
  'title',
  'stateId',
  'assignee',
  'priorityId',
  'typeId',
] as const;

export type OcBoardListTableColumnKey = (typeof OC_BOARD_LIST_TABLE_COLUMN_KEYS)[number];

/** Sistem (audit/SLA) sütunları — form alanı değil, op_work_items üst seviye + sla. */
export const OC_BOARD_LIST_SYSTEM_COLUMN_KEYS = [
  'createdBy',
  'createdAt',
  'age',
  'updatedAt',
  'lastStateChangeAt',
  'closedAt',
  'sla',
] as const;

export type OcBoardListSystemColumnKey = (typeof OC_BOARD_LIST_SYSTEM_COLUMN_KEYS)[number];

const CORE_COLUMN_KEY_SET = new Set<string>(OC_BOARD_LIST_TABLE_COLUMN_KEYS);
const SYSTEM_COLUMN_KEY_SET = new Set<string>(OC_BOARD_LIST_SYSTEM_COLUMN_KEYS);

export function isCoreListColumn(key: string): boolean {
  return CORE_COLUMN_KEY_SET.has(key);
}

export function isSystemListColumn(key: string): boolean {
  return SYSTEM_COLUMN_KEY_SET.has(key);
}

/** Çekirdek veya sistem sütunu (i18n etiketi olan yerleşik sütun). */
export function isBuiltInListColumn(key: string): boolean {
  return CORE_COLUMN_KEY_SET.has(key) || SYSTEM_COLUMN_KEY_SET.has(key);
}

/** Sistem sütununun varsayılan format ipucu (admin override edebilir). */
export function defaultFormatForKey(key: string): OcColumnFormat | null {
  switch (key) {
    case 'createdAt':
    case 'updatedAt':
    case 'lastStateChangeAt':
    case 'closedAt':
      return 'date';
    case 'age':
      return 'relativeTime';
    default:
      return null;
  }
}

/** Sistem sütunu için kart ham değeri (date/age sütunlarında formatlanır). */
export function systemColumnRawValue(item: OcWorkItemCard, key: string): unknown {
  switch (key) {
    case 'createdAt':
    case 'age':
      return item.createdAt ?? null;
    case 'updatedAt':
      return item.updatedAt ?? null;
    case 'lastStateChangeAt':
      return item.lastStateChangeAt ?? null;
    case 'closedAt':
      return item.closedAt ?? null;
    default:
      return null;
  }
}

const DEFAULT_LIST_COLUMNS: OcBoardListTableColumnKey[] = [
  'key',
  'title',
  'stateId',
  'assignee',
  'priorityId',
];

/**
 * Geçerli sütun anahtarlarını süzer: çekirdek alanlar + (varsa) izin verilen pool alan key'leri.
 * Seçim boşsa varsayılan çekirdek sütunlar döner.
 */
export function normalizeListTableColumns(
  visibleFields: string[] | undefined | null,
  allowedFieldKeys?: string[]
): string[] {
  const allowed = new Set<string>([...OC_BOARD_LIST_TABLE_COLUMN_KEYS, ...OC_BOARD_LIST_SYSTEM_COLUMN_KEYS]);
  for (const key of allowedFieldKeys ?? []) {
    if (key?.trim()) allowed.add(key);
  }
  const picked = (visibleFields ?? []).filter((f) => allowed.has(f));
  return picked.length > 0 ? picked : [...DEFAULT_LIST_COLUMNS];
}

/** Pool alan tipinden liste sütunu format ipucu (listColumns.format yoksa). */
export function formatHintFromPoolFieldType(fieldType: string | null | undefined): OcColumnFormat | null {
  const ft = (fieldType ?? '').toLowerCase();
  if (ft === 'date' || ft === 'datetime') return 'date';
  if (ft === 'number') return 'number';
  return null;
}

/**
 * Liste sütunu için efektif format: board meta → sistem varsayılanı → pool fieldType.
 */
export function resolveListColumnFormat(
  key: string,
  explicitFormat: OcColumnFormat | null | undefined,
  poolFieldType?: string | null
): OcColumnFormat | null {
  if (explicitFormat) return explicitFormat;
  const fromDefault = defaultFormatForKey(key);
  if (fromDefault) return fromDefault;
  return formatHintFromPoolFieldType(poolFieldType);
}

/** Pool alan hücresi — format ipucu varsa formatCellValue, yoksa fieldDisplays / ham metin. */
export function listTablePoolCellDisplay(
  fields: Record<string, unknown> | undefined | null,
  key: string,
  format: OcColumnFormat | null | undefined,
  opts: OcFormatOptions = {},
  fieldDisplays?: Record<string, string>
): string {
  const fmt = format && format !== 'text' ? format : null;
  if (fmt) {
    return formatCellValue(fields?.[key], fmt, opts);
  }
  if (fieldDisplays?.[key]) return fieldDisplays[key];
  return listTablePoolCellValue(fields, key);
}

/** Pool alan değerini liste hücresi için okunabilir metne çevirir (best-effort). */
export function listTablePoolCellValue(
  fields: Record<string, unknown> | undefined | null,
  key: string
): string {
  if (!fields) return '—';
  const value = fields[key];
  return formatPoolValue(value);
}

function formatPoolValue(value: unknown): string {
  if (value === null || value === undefined || value === '') return '—';
  if (Array.isArray(value)) {
    const parts = value.map((v) => formatPoolScalar(v)).filter((s) => s && s !== '—');
    return parts.length ? parts.join(', ') : '—';
  }
  return formatPoolScalar(value);
}

function formatPoolScalar(value: unknown): string {
  if (value === null || value === undefined || value === '') return '—';
  if (typeof value === 'boolean') return value ? '✓' : '✗';
  if (typeof value === 'object') {
    const o = value as Record<string, unknown>;
    const label = o.name ?? o.title ?? o.label ?? o.__dataId ?? o.dataId;
    return label != null ? String(label) : '—';
  }
  return String(value);
}

/** Yeni sütun eklenince varsayılan olarak sıralanabilir kabul edilen alanlar. */
const DEFAULT_SORTABLE_KEYS = new Set<string>([
  'key',
  'title',
  'priorityId',
  'stateId',
  'createdAt',
  'age',
  'updatedAt',
  'lastStateChangeAt',
  'closedAt',
  'sla',
]);
/** Yeni sütun eklenince varsayılan olarak filtrelenebilir kabul edilen alanlar. */
const DEFAULT_FILTERABLE_KEYS = new Set<string>([
  'stateId',
  'priorityId',
  'typeId',
  'assignee',
  'createdBy',
]);

export function defaultSortableForKey(key: string): boolean {
  return DEFAULT_SORTABLE_KEYS.has(key);
}

export function defaultFilterableForKey(key: string): boolean {
  return DEFAULT_FILTERABLE_KEYS.has(key);
}

/**
 * Geçerli liste sütun tanımlarını (sıra + sortable/filterable) üretir.
 * Açık `listColumns` varsa onu (izin verilen key'lere göre) normalize eder;
 * yoksa eski `visibleFields`'tan varsayılan bayraklarla türetir.
 */
export function deriveBoardListColumns(
  listColumns: OpBoardListColumnConfig[] | undefined | null,
  visibleFields: string[] | undefined | null,
  allowedFieldKeys?: string[]
): OpBoardListColumnConfig[] {
  const allowed = new Set<string>([...OC_BOARD_LIST_TABLE_COLUMN_KEYS, ...OC_BOARD_LIST_SYSTEM_COLUMN_KEYS]);
  for (const k of allowedFieldKeys ?? []) {
    if (k?.trim()) allowed.add(k);
  }

  const source: OpBoardListColumnConfig[] =
    listColumns && listColumns.length
      ? listColumns
      : normalizeListTableColumns(visibleFields, allowedFieldKeys).map((key) => ({
          key,
          sortable: DEFAULT_SORTABLE_KEYS.has(key),
          filterable: DEFAULT_FILTERABLE_KEYS.has(key),
          format: defaultFormatForKey(key),
        }));

  const seen = new Set<string>();
  const out: OpBoardListColumnConfig[] = [];
  for (const c of source) {
    if (!c?.key || seen.has(c.key)) continue;
    // Computed sütunlar field değildir; `allowed` kontrolünden muaf, expr/label korunur.
    if (c.computed) {
      seen.add(c.key);
      out.push({
        key: c.key,
        sortable: false,
        filterable: false,
        format: c.format ?? null,
        computed: true,
        expr: c.expr ?? null,
        label: c.label ?? null,
      });
      continue;
    }
    if (!allowed.has(c.key)) continue;
    seen.add(c.key);
    out.push({
      key: c.key,
      sortable: !!c.sortable,
      filterable: !!c.filterable,
      format: c.format ?? defaultFormatForKey(c.key),
    });
  }

  if (out.length) return out;
  return normalizeListTableColumns(null).map((key) => ({
    key,
    sortable: DEFAULT_SORTABLE_KEYS.has(key),
    filterable: DEFAULT_FILTERABLE_KEYS.has(key),
    format: defaultFormatForKey(key),
  }));
}

export function boardListColumnKeys(cols: OpBoardListColumnConfig[]): string[] {
  return cols.map((c) => c.key);
}

/** Liste kapsamı — seçili durumlar → MO kolon sorguları (wi_board_column). */
export function buildListScopeColumns(
  stateIds: string[],
  stateTitleById: Map<string, string>
): OpBoardColumnConfig[] {
  return stateIds.map((stateId) => ({
    stateId,
    title: stateTitleById.get(stateId) ?? null,
    queryKey: 'wi_board_column',
    defaultTransitionKey: null,
  }));
}

export function listScopeStateIdsFromColumns(columns: OpBoardColumnConfig[]): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const col of columns) {
    if (!col.stateId || seen.has(col.stateId)) continue;
    seen.add(col.stateId);
    out.push(col.stateId);
  }
  return out;
}

/** Akıştaki tüm durumları liste kapsamına öner. */
export function suggestListScopeStateIdsFromFlow(flow: OpStateFlow): string[] {
  const order: string[] = [];
  const seen = new Set<string>();
  const initial = flow.initialStateId?.trim();
  if (initial && seen.add(initial)) order.push(initial);
  const transitions = [...flow.transitions].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  for (const tr of transitions) {
    const to = tr.toStateId?.trim();
    if (to && seen.add(to)) order.push(to);
  }
  return order;
}

export function listTableCellValue(
  item: OcWorkItemCard,
  field: string,
  context?: { stateLabel?: string }
): string {
  switch (field) {
    case 'key':
      return item.key || '—';
    case 'title':
      return item.title || '—';
    case 'stateId':
      return context?.stateLabel ?? item.stateId ?? '—';
    case 'assignee':
      return item.assignee ?? '—';
    case 'priorityId':
      return item.priorityId ?? '—';
    case 'typeId':
      return item.typeId ?? '—';
    default:
      return '—';
  }
}

export function isListLinkColumn(field: string): boolean {
  return field === 'key' || field === 'title';
}
