import type { FieldDefinition, FieldType } from '@/stores/apps/dataset';
import type {
  ReportingParameterBinding,
  ReportingParameterOptions,
  ReportingParameterType,
  ReportingReportParameter,
} from '@/types/apps/reporting';
import { reportingFieldLabel, reportingSelectItemsFromField } from '@/utils/reportingListConfig';

/** Alan seçilmeden global metin araması. */
export const REPORTING_PARAM_SEARCH_FIELD = '__search__';

export type ReportingParameterDatePart = 'year' | 'month' | 'quarter';

/** Alan tipine göre filtre / parametre biçimi (domain bağımsız). */
export type ReportingParameterBindingModeId =
  | 'search'
  | 'datePart'
  | 'dateRange'
  | 'fieldEq'
  | 'choiceGroup';

const DEFAULT_YEAR_MIN = 2017;

/** Parametre tanımında kullanılabilecek alan tipleri. */
export const REPORTING_PARAMETER_FIELD_TYPES: FieldType[] = [
  'text',
  'number',
  'bool',
  'datetime',
  'select',
  'relation',
  'persons',
  'personGroups',
  'incremental',
];

export function isReportingParameterSearchField(fieldName: string | null | undefined): boolean {
  return !fieldName?.trim() || fieldName === REPORTING_PARAM_SEARCH_FIELD;
}

export function reportableFieldsForParameters(fields: FieldDefinition[]): FieldDefinition[] {
  return fields.filter(
    (f) => f.name?.trim() && REPORTING_PARAMETER_FIELD_TYPES.includes(f.fieldType)
  );
}

/** Seçili alan tipine göre uygun filtre biçimleri. */
export function bindingModesForField(
  field: FieldDefinition | null | undefined
): ReportingParameterBindingModeId[] {
  if (!field) return ['search'];

  switch (field.fieldType) {
    case 'datetime':
      return ['datePart', 'dateRange', 'fieldEq'];
    case 'select':
      return reportingSelectItemsFromField(field).length
        ? ['choiceGroup', 'fieldEq']
        : ['fieldEq'];
    case 'persons':
    case 'personGroups':
    case 'text':
    case 'number':
    case 'bool':
    case 'relation':
    case 'incremental':
      return ['fieldEq'];
    default:
      return [];
  }
}

export function defaultBindingModeForField(
  field: FieldDefinition | null | undefined
): ReportingParameterBindingModeId {
  if (!field) return 'search';
  const modes = bindingModesForField(field);
  return modes[0] ?? 'fieldEq';
}

export function inferReportingParameterDatePart(
  param: ReportingReportParameter
): ReportingParameterDatePart {
  const binding = param.binding;
  if (binding?.kind === 'datePartRange' && binding.part) {
    return binding.part;
  }
  return 'year';
}

export function inferReportingParameterBindingMode(
  param: ReportingReportParameter
): ReportingParameterBindingModeId {
  const binding = param.binding;
  if (binding?.kind) {
    switch (binding.kind) {
      case 'search':
        return 'search';
      case 'datePartRange':
        return 'datePart';
      case 'dateRange':
        return 'dateRange';
      case 'choiceFilters':
        return 'choiceGroup';
      case 'fieldEq':
        return 'fieldEq';
      default:
        break;
    }
  }

  switch (param.type) {
    case 'search':
      return 'search';
    case 'year':
      return 'datePart';
    case 'statusTab':
      return 'choiceGroup';
    case 'person':
    default:
      return 'fieldEq';
  }
}

export function inferReportingParameterFieldName(param: ReportingReportParameter): string {
  const binding = param.binding;
  if (binding?.kind === 'search') return REPORTING_PARAM_SEARCH_FIELD;
  if (
    binding?.kind === 'fieldEq' ||
    binding?.kind === 'datePartRange' ||
    binding?.kind === 'dateRange'
  ) {
    return binding.field ?? param.field ?? param.dateField ?? '';
  }
  if (binding?.kind === 'choiceFilters') {
    return binding.choices?.find((c) => c.filters[0]?.field)?.filters[0]?.field ?? '';
  }
  if (param.type === 'search') return REPORTING_PARAM_SEARCH_FIELD;
  return param.field ?? param.dateField ?? '';
}

export function reportingParameterBindingDisplayLabel(
  param: ReportingReportParameter,
  t: (key: string, params?: Record<string, unknown>) => string
): string {
  const mode = inferReportingParameterBindingMode(param);
  if (mode === 'datePart') {
    const part = inferReportingParameterDatePart(param);
    return t('reporting.parameters.bindings.datePartWithPart', {
      part: t(`reporting.parameters.dateParts.${part}`),
    });
  }
  return t(`reporting.parameters.bindings.${mode}`);
}

export function uniqueReportingParameterId(base: string, existingIds: string[]): string {
  const slug = base
    .trim()
    .replace(/[^a-zA-Z0-9_]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .slice(0, 40);
  const root = slug || 'param';
  if (!existingIds.includes(root)) return root;
  let n = 2;
  while (existingIds.includes(`${root}_${n}`)) n += 1;
  return `${root}_${n}`;
}

function legacyTypeForBindingMode(mode: ReportingParameterBindingModeId): ReportingParameterType {
  switch (mode) {
    case 'search':
      return 'search';
    case 'datePart':
    case 'dateRange':
      return 'year';
    case 'choiceGroup':
      return 'statusTab';
    case 'fieldEq':
    default:
      return 'search';
  }
}

function boolSelectOptions(
  trueLabel: string,
  falseLabel: string
): { value: string; title: string }[] {
  return [
    { value: 'true', title: trueLabel },
    { value: 'false', title: falseLabel },
  ];
}

export interface BuildReportingParameterOptions {
  bindingMode: ReportingParameterBindingModeId;
  field?: FieldDefinition | null;
  fieldName?: string;
  label?: string;
  required?: boolean;
  defaultValue?: string;
  existingIds?: string[];
  parameterId?: string;
  datePart?: ReportingParameterDatePart;
  yearMin?: number;
  yearMax?: number | 'currentYear';
  yearIncludeAll?: boolean;
  includeAllOption?: boolean;
  allChoiceTitle?: string;
  boolTrueLabel?: string;
  boolFalseLabel?: string;
}

export function buildReportingParameter(
  opts: BuildReportingParameterOptions
): ReportingReportParameter {
  const existingIds = opts.existingIds ?? [];
  const rawFieldName = opts.field?.name ?? opts.fieldName?.trim() ?? '';
  const isSearch = opts.bindingMode === 'search' || isReportingParameterSearchField(rawFieldName);
  const fieldName = isSearch ? '' : rawFieldName;
  const field = isSearch ? null : opts.field ?? undefined;

  const label =
    opts.label?.trim() ||
    (field ? reportingFieldLabel(field, fieldName) : '') ||
    'Parametre';

  const id =
    opts.parameterId?.trim() ||
    uniqueReportingParameterId(fieldName || opts.bindingMode, existingIds);

  let widget: ReportingReportParameter['widget'];
  let binding: ReportingParameterBinding;
  let options: ReportingParameterOptions | undefined;

  switch (opts.bindingMode) {
    case 'search':
      widget = 'search';
      binding = { kind: 'search' };
      break;
    case 'datePart': {
      const part = opts.datePart ?? 'year';
      binding = {
        kind: 'datePartRange',
        field: fieldName,
        part,
        emptyMeans: 'noFilter',
      };
      if (part === 'year') {
        widget = 'number';
        options = {
          kind: 'yearRange',
          min: opts.yearMin ?? DEFAULT_YEAR_MIN,
          max: opts.yearMax ?? 'currentYear',
          includeAll: opts.yearIncludeAll !== false,
        };
      } else if (part === 'month') {
        widget = 'date';
      } else {
        widget = 'number';
        options = {
          kind: 'quarterRange',
          min: opts.yearMin ?? DEFAULT_YEAR_MIN,
          max: opts.yearMax ?? 'currentYear',
        };
      }
      break;
    }
    case 'dateRange':
      widget = 'dateRange';
      binding = {
        kind: 'dateRange',
        field: fieldName,
        emptyMeans: 'noFilter',
      };
      break;
    case 'choiceGroup': {
      const items = reportingSelectItemsFromField(field ?? undefined);
      const includeAll = opts.includeAllOption !== false;
      const allTitle = opts.allChoiceTitle?.trim() || 'Tümü';
      widget = 'buttonGroup';
      binding = {
        kind: 'choiceFilters',
        choices: [
          ...(includeAll ? [{ value: 'all', title: allTitle, filters: [] }] : []),
          ...items.map((item) => ({
            value: item.value,
            title: item.title,
            filters: fieldName
              ? [{ field: fieldName, operator: 'eq' as const, value: item.value }]
              : [],
          })),
        ],
      };
      break;
    }
    case 'fieldEq':
    default: {
      if (field?.fieldType === 'persons' || field?.fieldType === 'personGroups') {
        widget = 'personPicker';
      } else if (field?.fieldType === 'datetime') {
        widget = 'date';
      } else {
        widget = 'select';
      }
      binding = { kind: 'fieldEq', field: fieldName };

      const enumItems = reportingSelectItemsFromField(field ?? undefined);
      if (enumItems.length) {
        options = {
          kind: 'static',
          items: enumItems.map((i) => ({ value: i.value, title: i.title })),
          includeAll: opts.includeAllOption !== false,
        };
      } else if (field?.fieldType === 'bool') {
        options = {
          kind: 'static',
          items: boolSelectOptions(
            opts.boolTrueLabel ?? 'Evet',
            opts.boolFalseLabel ?? 'Hayır'
          ),
          includeAll: opts.includeAllOption !== false,
        };
      }
      break;
    }
  }

  return {
    id,
    type: legacyTypeForBindingMode(opts.bindingMode),
    label,
    required: opts.required ?? false,
    widget,
    binding,
    ...(options ? { options } : {}),
    ...(opts.defaultValue != null && opts.defaultValue !== '' ? { defaultValue: opts.defaultValue } : {}),
    ...(fieldName && widget === 'personPicker' ? { field: fieldName } : {}),
    ...(fieldName && (opts.bindingMode === 'datePart' || opts.bindingMode === 'dateRange')
      ? { dateField: fieldName }
      : {}),
  };
}

/** choiceGroup binding kullanan parametreler — gelişmiş tarih eşlemesi için. */
export function choiceGroupParameters(
  parameters: ReportingReportParameter[]
): ReportingReportParameter[] {
  return parameters.filter(
    (p) => inferReportingParameterBindingMode(p) === 'choiceGroup'
  );
}

/** @deprecated choiceGroupParameters kullanın. */
export function choiceFilterParameters(parameters: ReportingReportParameter[]): ReportingReportParameter[] {
  return choiceGroupParameters(parameters);
}

export function reportingFieldTypeLabel(
  fieldType: FieldType,
  t: (key: string) => string
): string {
  return t(`reporting.parameters.fieldTypes.${fieldType}`);
}

export function buildReportingParameterFromPreset(
  opts: BuildReportingParameterOptions & { presetId: string }
): ReportingReportParameter {
  const presetToMode: Record<string, ReportingParameterBindingModeId> = {
    search: 'search',
    dateYear: 'datePart',
    datePartYear: 'datePart',
    datePart: 'datePart',
    dateRange: 'dateRange',
    fieldEqSelect: 'fieldEq',
    fieldEqPerson: 'fieldEq',
    choiceFromEnum: 'choiceGroup',
    choiceGroup: 'choiceGroup',
    fieldEq: 'fieldEq',
  };
  return buildReportingParameter({
    ...opts,
    bindingMode: presetToMode[opts.presetId] ?? 'fieldEq',
    datePart: opts.presetId === 'datePartYear' ? 'year' : opts.datePart,
  });
}
