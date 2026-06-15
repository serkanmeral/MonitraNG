/** Automated Forms liste filtreleri — DG filter query dönüşümü */

export type AfListFilterKind =
  | 'text'
  | 'number'
  | 'bool'
  | 'date'
  | 'select'
  | 'relation'
  | 'person'
  | 'group';

export interface AfListFilter {
  field: string;
  operator: string;
  value: string;
}

export interface AfFilterColumn {
  key: string;
  label: string;
  kind: AfListFilterKind;
  selectItems?: { value: string; title: string }[];
}

export function resolveAfFilterKind(fieldType: string): AfListFilterKind {
  switch (fieldType) {
    case 'number':
      return 'number';
    case 'bool':
      return 'bool';
    case 'datetime':
      return 'date';
    case 'select':
      return 'select';
    case 'relation':
      return 'relation';
    case 'persons':
      return 'person';
    case 'personGroups':
      return 'group';
    default:
      return 'text';
  }
}

/** OcBoardListFilter ile aynı yapı — DG ?filter=field:op:value,... */
export function afListFiltersToQueryString(filters: AfListFilter[]): string {
  return filters
    .filter((f) => f.field && f.operator && f.value !== undefined && f.value !== '')
    .map((f) => {
      let value = f.value;
      if (f.operator === 'in' || f.operator === 'nin') {
        const items = value.split(',').map((v) => v.trim()).filter(Boolean);
        if (items.length > 1) value = JSON.stringify(items);
      }
      return `${f.field}:${f.operator}:${value}`;
    })
    .join(',');
}
