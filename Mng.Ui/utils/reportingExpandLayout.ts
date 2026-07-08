import type { FieldDefinition } from '@/stores/apps/dataset';
import type { OpFormLayoutSection } from '@/types/apps/operationCore';
import type { ReportingExpandChildListTab, ReportingExpandConfig } from '@/types/apps/reporting';
import {
  buildOcFormLayoutPayload,
  parseOpFormLayout,
  type ParsedOpFormLayout,
} from '@/utils/ocFormLayout';
import { reportingFieldLabel } from '@/utils/reportingListConfig';

export function reportingLayoutFieldItems(
  fields: FieldDefinition[]
): { value: string; title: string; fieldType?: string }[] {
  return fields
    .filter((f) => Boolean(f.name?.trim()))
    .map((f) => ({
      value: f.name,
      title: reportingFieldLabel(f, f.name),
      fieldType: f.fieldType,
    }));
}

/** Expand panel varsayılanı: tüm alanlar tek bölümde. */
export function defaultReportingExpandConfigFromFields(
  fields: FieldDefinition[]
): ReportingExpandConfig {
  const names = fields.map((f) => f.name).filter(Boolean);
  const fieldCols: Record<string, number> = {};
  for (const name of names) {
    const ft = fields.find((f) => f.name === name)?.fieldType;
    fieldCols[name] = ft === 'text' && name.toLowerCase().includes('desc') ? 12 : 6;
  }

  const sections: OpFormLayoutSection[] = names.length
    ? [{ key: 'detail', title: '', cols: 12, fields: names }]
    : [];

  return {
    enabled: false,
    hideEmptyFields: true,
    heading: '',
    intro: '',
    sections,
    fieldCols,
    actions: [],
    tabs: [],
    defaultTabId: 'fields',
  };
}

export function parsedReportingExpandLayout(config: ReportingExpandConfig): ParsedOpFormLayout {
  const payload = buildOcFormLayoutPayload({
    formHeading: config.heading,
    formIntro: config.intro,
    sections: config.sections,
    fieldCols: config.fieldCols,
  });
  return parseOpFormLayout(payload);
}

export function expandLayoutFieldNames(config: ReportingExpandConfig): string[] {
  const names = new Set<string>();
  for (const section of config.sections) {
    for (const field of section.fields) {
      if (field) names.add(field);
    }
  }
  return [...names];
}

export function reportingExpandChildTabs(config: ReportingExpandConfig): ReportingExpandChildListTab[] {
  return config.tabs ?? [];
}

export function reportingExpandHasChildTabs(config: ReportingExpandConfig): boolean {
  return reportingExpandChildTabs(config).length > 0;
}

export function reportingExpandFieldsTabTitle(config: ReportingExpandConfig): string {
  const titled = config.sections.find((s) => s.title?.trim());
  return titled?.title?.trim() || '';
}

export function resolveReportingExpandParentValue(
  row: Record<string, unknown>,
  parentField = '__dataId'
): string {
  if (parentField === '__dataId') return reportingRowId(row);
  const val = row[parentField];
  if (val == null || val === '') return '';
  if (typeof val === 'object') {
    const o = val as Record<string, unknown>;
    const id = o.__dataId ?? o.dataId ?? o.id;
    if (id != null && id !== '') return String(id);
  }
  return String(val);
}

export function reportingRowId(row: Record<string, unknown>): string {
  const id = row.__dataId ?? row.dataId ?? row.DataId ?? row.id;
  return id != null ? String(id) : '';
}

/** expand.actions navigate — `{__dataId}` ve `{alanAdi}` şablonları. */
export function resolveReportingExpandNavigatePath(
  template: string,
  row: Record<string, unknown>
): string {
  return template.replace(/\{([^}]+)\}/g, (_, key: string) => {
    const trimmed = key.trim();
    if (trimmed === '__dataId') return reportingRowId(row);
    const val = row[trimmed];
    return val != null ? String(val) : '';
  });
}
