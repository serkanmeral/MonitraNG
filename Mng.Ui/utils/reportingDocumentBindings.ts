import type {
  ReportingDocumentBinding,
  ReportingDocumentChildLink,
  ReportingDocumentContextType,
  ReportingReportDefinition,
  ReportingReportParameter,
} from '@/types/apps/reporting';
import type { AfListFilter } from '@/utils/afListFilters';
import { reportingParameterRawValue } from '@/utils/reportingParameterValueKeys';
import { normalizeReportingParameters } from '@/utils/reportingParameterModel';

const CONTEXT_TYPES: ReportingDocumentContextType[] = ['reportRun', 'parentRow', 'childRow'];

export function normalizeReportingDocumentContextType(raw: unknown): ReportingDocumentContextType {
  const v = String(raw ?? '').trim();
  if ((CONTEXT_TYPES as string[]).includes(v)) return v as ReportingDocumentContextType;
  return 'reportRun';
}

export function normalizeReportingDocumentBinding(raw: unknown): ReportingDocumentBinding | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const id = String(o.id ?? o.Id ?? '').trim();
  const templateCode = String(o.templateCode ?? o.TemplateCode ?? '').trim() || undefined;
  let templateId = String(o.templateId ?? o.TemplateId ?? '').trim();
  if (!templateId && templateCode) templateId = templateCode;
  const label = String(o.label ?? o.Label ?? '').trim();
  if (!id || !templateId || !label) return null;
  const contextType = normalizeReportingDocumentContextType(o.contextType ?? o.ContextType);
  const outputFolderId = String(o.outputFolderId ?? o.OutputFolderId ?? '').trim() || undefined;
  const segsRaw = o.outputFolderSegments ?? o.OutputFolderSegments;
  const outputFolderSegments = Array.isArray(segsRaw)
    ? segsRaw.map((s) => String(s).trim()).filter(Boolean)
    : undefined;
  const childTabId = String(o.childTabId ?? o.ChildTabId ?? '').trim() || undefined;
  const documentNamePattern =
    String(o.documentNamePattern ?? o.DocumentNamePattern ?? '').trim() || undefined;
  const generatedAtPattern =
    String(o.generatedAtPattern ?? o.GeneratedAtPattern ?? '').trim() || undefined;
  const rowFilters = normalizeDocumentRowFilters(o.rowFilters ?? o.RowFilters);
  const linkToChild = normalizeDocumentChildLink(o.linkToChild ?? o.LinkToChild);
  return {
    id,
    templateId,
    templateCode,
    label,
    contextType,
    ...(outputFolderId ? { outputFolderId } : {}),
    ...(outputFolderSegments?.length ? { outputFolderSegments } : {}),
    ...(childTabId ? { childTabId } : {}),
    ...(documentNamePattern ? { documentNamePattern } : {}),
    ...(generatedAtPattern ? { generatedAtPattern } : {}),
    ...(rowFilters.length ? { rowFilters } : {}),
    ...(linkToChild ? { linkToChild } : {}),
  };
}

function normalizeDocumentChildLink(raw: unknown): ReportingDocumentChildLink | undefined {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return undefined;
  const o = raw as Record<string, unknown>;
  const childTabId = String(o.childTabId ?? o.ChildTabId ?? '').trim();
  const matchField = String(o.matchField ?? o.MatchField ?? '').trim();
  const matchValue = String(o.matchValue ?? o.MatchValue ?? '').trim();
  const relationType = String(o.relationType ?? o.RelationType ?? '').trim() || undefined;
  const refField = String(o.refField ?? o.RefField ?? '').trim() || undefined;
  const parentRefField = String(o.parentRefField ?? o.ParentRefField ?? '').trim() || undefined;
  if (!childTabId && !matchField && !matchValue && !relationType && !refField && !parentRefField) return undefined;
  return {
    childTabId,
    matchField,
    matchValue,
    ...(relationType ? { relationType } : {}),
    ...(refField ? { refField } : {}),
    ...(parentRefField ? { parentRefField } : {}),
  };
}

function normalizeDocumentRowFilters(raw: unknown): AfListFilter[] {
  if (!Array.isArray(raw)) return [];
  const filters: AfListFilter[] = [];
  for (const item of raw) {
    if (!item || typeof item !== 'object') continue;
    const filter = item as Record<string, unknown>;
    const field = String(filter.field ?? filter.Field ?? '').trim();
    const operator = String(filter.operator ?? filter.Operator ?? '').trim().toLowerCase();
    const value = String(filter.value ?? filter.Value ?? '').trim();
    if (!field || !operator) continue;
    filters.push({ field, operator, value });
  }
  return filters;
}

function documentRowScalar(value: unknown): string {
  if (value == null) return '';
  if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
    return String(value).trim();
  }
  if (Array.isArray(value)) {
    return value.map((item) => documentRowScalar(item)).filter(Boolean).join(',');
  }
  if (typeof value === 'object') {
    const record = value as Record<string, unknown>;
    const coded = record.value ?? record.code ?? record.key ?? record.id ?? record.__dataId ?? record.dataId;
    if (coded != null && String(coded).trim()) return String(coded).trim();
  }
  return '';
}

/** Bağlantıdaki satır koşullarının hepsi tutuyorsa düğme görünür. Koşul yoksa her satırda görünür. */
export function reportingDocumentBindingMatchesRow(
  binding: ReportingDocumentBinding,
  row: Record<string, unknown>
): boolean {
  const filters = binding.rowFilters ?? [];
  if (!filters.length) return true;
  return filters.every((filter) => {
    const field = filter.field.trim();
    const operator = filter.operator.trim().toLowerCase();
    if (!field || !operator) return true;
    const actual = documentRowScalar(row[field]).toLowerCase();
    const expected = filter.value.trim().toLowerCase();
    if (operator === 'eq') return actual === expected;
    if (operator === 'neq' || operator === 'ne') return actual !== expected;
    const listed = expected.split(',').map((item) => item.trim()).filter(Boolean);
    if (operator === 'in') return listed.includes(actual);
    if (operator === 'nin') return !listed.includes(actual);
    return true;
  });
}

export function normalizeReportingDocumentBindings(raw: unknown): ReportingDocumentBinding[] {
  if (!Array.isArray(raw)) return [];
  const out: ReportingDocumentBinding[] = [];
  for (const item of raw) {
    const b = normalizeReportingDocumentBinding(item);
    if (b) out.push(b);
  }
  return out;
}

export function defaultReportingDocumentFolderSegments(reportId: string): string[] {
  return ['Reports', reportId];
}

export function resolveReportingDocumentFolderSegments(
  reportId: string,
  binding: ReportingDocumentBinding
): string[] {
  if (binding.outputFolderSegments?.length) return [...binding.outputFolderSegments];
  return defaultReportingDocumentFolderSegments(reportId);
}

/** Aynı templateId / templateCode başka raporda bağlı mı? */
export function findReportingTemplateBindingConflict(
  reports: ReportingReportDefinition[],
  templateId: string,
  exceptReportId?: string | null,
  templateCode?: string | null
): { reportId: string; reportTitle: string; binding: ReportingDocumentBinding } | null {
  const tid = templateId.trim();
  const tcode = (templateCode ?? '').trim();
  if (!tid && !tcode) return null;
  for (const report of reports) {
    if (exceptReportId && report.id === exceptReportId) continue;
    for (const b of report.documentBindings ?? []) {
      const sameId = tid && b.templateId === tid;
      const sameCode =
        tcode &&
        ((b.templateCode ?? '').trim() === tcode || b.templateId === tcode);
      if (sameId || sameCode) {
        return { reportId: report.id, reportTitle: report.title, binding: b };
      }
    }
  }
  return null;
}

export function newReportingDocumentBindingId(): string {
  return `docbind_${Date.now()}_${Math.random().toString(36).slice(2, 7)}`;
}

/** Runner filtre/parametre özeti — belgede {{filtersSummary}}. */
export function buildReportingFiltersSummary(options: {
  parameters: ReportingReportParameter[];
  parameterValues: Record<string, string>;
  advancedFilters: AfListFilter[];
}): string {
  const parts: string[] = [];
  for (const param of normalizeReportingParameters(options.parameters)) {
    if (param.binding.kind === 'search') {
      const q = reportingParameterRawValue(options.parameterValues, param.id).trim();
      if (q) parts.push(`${param.label || 'Ara'}: ${q}`);
      continue;
    }
    if (param.binding.kind === 'choiceFilters') {
      const raw = reportingParameterRawValue(options.parameterValues, param.id);
      const choices = param.binding.choices ?? [];
      const selected = raw || param.defaultValue || choices[0]?.value || '';
      const choice = choices.find((c) => c.value === selected);
      if (choice?.title) parts.push(`${param.label || 'Durum'}: ${choice.title}`);
      continue;
    }
    if (param.binding.kind === 'datePartRange' && param.binding.part === 'year') {
      const y = reportingParameterRawValue(options.parameterValues, param.id).trim();
      if (y) parts.push(`${param.label || 'Yıl'}: ${y}`);
      continue;
    }
    if (param.binding.kind === 'fieldEq') {
      const v = reportingParameterRawValue(options.parameterValues, param.id).trim();
      if (v) parts.push(`${param.label || param.binding.field || param.id}: ${v}`);
    }
  }
  for (const f of options.advancedFilters) {
    if (!f.field || !f.operator || f.value === undefined || f.value === '') continue;
    parts.push(`${f.field} ${f.operator} ${f.value}`);
  }
  return parts.length ? parts.join(' · ') : 'Filtre yok';
}

export const REPORTING_DOCUMENT_ROW_SOFT_CAP = 2000;
