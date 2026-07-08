import type { AfListFilter } from '@/utils/afListFilters';

export function cloneAfListFilters(filters: AfListFilter[]): AfListFilter[] {
  return filters.map((f) => ({ ...f }));
}

/** Dataset / filtrelenebilir sütun değişince geçersiz alanları düşürür. */
export function sanitizeReportingDefaultFilters(
  filters: AfListFilter[],
  allowedFieldNames: Iterable<string>
): AfListFilter[] {
  const allowed = new Set(allowedFieldNames);
  return filters.filter((f) => Boolean(f.field?.trim()) && allowed.has(f.field));
}
