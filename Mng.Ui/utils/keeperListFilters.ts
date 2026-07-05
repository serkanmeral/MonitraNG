import type { AfFilterColumn, AfListFilter } from '@/utils/afListFilters';

export interface KeeperListFetchParams {
  search?: string;
  isActive?: boolean;
  includeInApplication?: boolean;
  provisioningSource?: number;
  groupId?: string;
  groupIds?: string[];
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
}

const TEXT_SEARCH_FIELDS = new Set([
  'username',
  'email',
  'firstName',
  'lastName',
  'fullName',
  'name',
  'description',
  'department',
  'title',
]);

function parseBoolFilter(value: string): boolean | undefined {
  if (value === 'true' || value === '1') return true;
  if (value === 'false' || value === '0') return false;
  return undefined;
}

function parseProvisioningSource(value: string): number | undefined {
  const v = value.trim().toLowerCase();
  if (v === '0' || v === 'local') return 0;
  if (v === '1' || v === 'directory') return 1;
  const n = Number(v);
  if (n === 0 || n === 1) return n;
  return undefined;
}

function splitCsvIds(value: string): string[] {
  return [...new Set(value.split(',').map((s) => s.trim()).filter(Boolean))];
}

/** AfListFilters + hızlı arama → Keeper list API parametreleri. */
export function keeperListFiltersToFetchParams(
  filters: AfListFilter[],
  quickSearch?: string
): KeeperListFetchParams {
  const params: KeeperListFetchParams = {};
  const searchParts: string[] = [];

  const q = String(quickSearch ?? '').trim();
  if (q) searchParts.push(q);

  for (const f of filters) {
    const field = String(f.field ?? '').trim();
    const value = String(f.value ?? '').trim();
    if (!field || !value) continue;
    const op = f.operator || 'eq';

    if (field === 'isActive') {
      const b = parseBoolFilter(value);
      if (b !== undefined && (op === 'eq' || !f.operator)) params.isActive = b;
      continue;
    }
    if (field === 'includeInApplication') {
      const b = parseBoolFilter(value);
      if (b !== undefined && (op === 'eq' || !f.operator)) params.includeInApplication = b;
      continue;
    }
    if (field === 'provisioningSource') {
      const rawValues =
        op === 'in' || op === 'nin' ? splitCsvIds(value) : [value];
      const src = parseProvisioningSource(rawValues[0] ?? '');
      if (src !== undefined && (op === 'eq' || op === 'in' || !f.operator)) {
        params.provisioningSource = src;
      }
      continue;
    }
    if (field === 'groups') {
      if (op === 'in' || op === 'nin') {
        const ids = splitCsvIds(value);
        if (ids.length) params.groupIds = ids;
      } else if (op === 'eq' || !f.operator) {
        params.groupId = value;
      }
      continue;
    }
    if (TEXT_SEARCH_FIELDS.has(field) && (op === 'contains' || op === 'eq' || !f.operator)) {
      searchParts.push(value);
    }
  }

  const merged = [...new Set(searchParts.map((s) => s.trim()).filter(Boolean))];
  if (merged.length) params.search = merged.join(' ');
  return params;
}

export function buildKeeperFilterColumns(
  listConfig: { columns: Array<{ fieldName: string; filterable?: boolean }> },
  labelForField: (fieldName: string) => string,
  kindForField: (fieldName: string) => AfFilterColumn['kind'],
  extraForField?: (fieldName: string) => Partial<AfFilterColumn>
): AfFilterColumn[] {
  const out: AfFilterColumn[] = [];
  for (const col of listConfig.columns) {
    if (!col.filterable) continue;
    out.push({
      key: col.fieldName,
      label: labelForField(col.fieldName),
      kind: kindForField(col.fieldName),
      ...extraForField?.(col.fieldName),
    });
  }
  return out;
}
