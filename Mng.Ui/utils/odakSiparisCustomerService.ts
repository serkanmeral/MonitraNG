import { ocCreate, ocListDatasetPage, ocUpdate } from '@/services/operationCoreService';
import { afListFiltersToQueryString, type AfListFilter } from '@/utils/afListFilters';
import { ODAK_CUSTOMER_SEKTOR_OPTIONS, ODAK_SIPARIS_CONFIG, type OdakCustomerRow } from '@/utils/odakSiparisConfig';
import { invalidateOdakSiparisCustomerCache, packageDataId } from '@/utils/odakSiparisService';

export interface OdakCustomerFormModel {
  kod: string;
  unvan: string;
  sektor: string | null;
  ulke: string;
  aktif: boolean;
  notlar: string;
}

export interface OdakCustomerListSort {
  key: string;
  order: 'asc' | 'desc';
}

export const CUSTOMER_LIST_SORT_FIELD_MAP: Record<string, string> = {
  kod: 'kod',
  unvan: 'unvan',
  sektor: 'sektor',
  ulke: 'ulke',
  aktif: 'aktif',
};

export type OdakCustomerDialogMode = 'create' | 'edit';

export function packagesByCustomerRoute(customerId: string): string {
  return `/apps/odak-siparis/packages?customerId=${encodeURIComponent(customerId)}`;
}

export function customerSektorLabel(value: unknown): string {
  if (value == null || value === '') return '—';
  const hit = ODAK_CUSTOMER_SEKTOR_OPTIONS.find((o) => o.value === value);
  return hit?.title ?? String(value);
}

export function emptyCustomerFormModel(partial?: Partial<OdakCustomerFormModel>): OdakCustomerFormModel {
  return {
    kod: partial?.kod ?? '',
    unvan: partial?.unvan ?? '',
    sektor: partial?.sektor ?? null,
    ulke: partial?.ulke ?? '',
    aktif: partial?.aktif ?? true,
    notlar: partial?.notlar ?? '',
  };
}

export function customerRowToFormModel(row: OdakCustomerRow): OdakCustomerFormModel {
  return emptyCustomerFormModel({
    kod: row.kod ?? '',
    unvan: row.unvan ?? '',
    sektor: row.sektor ?? null,
    ulke: row.ulke ?? '',
    aktif: row.aktif !== false,
    notlar: row.notlar ?? '',
  });
}

export function formModelToCustomerPayload(form: OdakCustomerFormModel): Record<string, unknown> {
  return {
    kod: form.kod.trim(),
    unvan: form.unvan.trim(),
    sektor: form.sektor || null,
    ulke: form.ulke.trim() || null,
    aktif: form.aktif,
    notlar: form.notlar.trim() || null,
  };
}

export function buildCustomerListSort(sortBy?: OdakCustomerListSort[]): string {
  const primary = sortBy?.[0];
  if (!primary) return 'unvan';
  const field = CUSTOMER_LIST_SORT_FIELD_MAP[primary.key];
  if (!field) return 'unvan';
  return primary.order === 'desc' ? `-${field}` : field;
}

export interface OdakCustomerListQuery {
  skip?: number;
  limit?: number;
  search?: string;
  sortBy?: OdakCustomerListSort[];
  advancedFilters?: AfListFilter[];
  aktifTab?: 'active' | 'inactive' | 'all';
}

export function buildCustomerListFilter(query: OdakCustomerListQuery): string | undefined {
  const parts: string[] = [];
  if (query.aktifTab === 'active') parts.push('aktif:eq:true');
  else if (query.aktifTab === 'inactive') parts.push('aktif:eq:false');

  const advanced = afListFiltersToQueryString(query.advancedFilters ?? []);
  if (advanced) parts.push(advanced);

  return parts.length ? parts.join(',') : undefined;
}

export async function fetchOdakCustomersPage(
  query: OdakCustomerListQuery
): Promise<{ items: OdakCustomerRow[]; total: number }> {
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.customersDataset, {
    skip: query.skip ?? 0,
    limit: query.limit ?? 20,
    sort: buildCustomerListSort(query.sortBy),
    filter: buildCustomerListFilter(query),
    search: query.search?.trim() || undefined,
  });
  return {
    items: (resp.items ?? []) as OdakCustomerRow[],
    total: resp.total ?? resp.items?.length ?? 0,
  };
}

export async function fetchOdakCustomerById(customerId: string): Promise<OdakCustomerRow | null> {
  if (!customerId) return null;
  const { fetchFromDataGateway } = await import('@/services/apiService');
  const url = `/api/v1/data/${encodeURIComponent(ODAK_SIPARIS_CONFIG.customersDataset)}/${encodeURIComponent(customerId)}`;
  const raw = await fetchFromDataGateway(url, 'GET');
  const item = Array.isArray(raw) ? raw[0] : raw;
  if (!item || typeof item !== 'object') return null;
  return item as OdakCustomerRow;
}

export async function createOdakCustomer(form: OdakCustomerFormModel): Promise<string | null> {
  const body = formModelToCustomerPayload(form);
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.customersDataset, body);
  invalidateOdakSiparisCustomerCache();
  return packageDataId(created as Record<string, unknown>) || null;
}

export async function updateOdakCustomer(customerId: string, form: OdakCustomerFormModel): Promise<void> {
  const body = formModelToCustomerPayload(form);
  await ocUpdate(ODAK_SIPARIS_CONFIG.customersDataset, customerId, body);
  invalidateOdakSiparisCustomerCache();
}
