import type {
  ReportingDocumentBinding,
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
  const segsRaw = o.outputFolderSegments ?? o.OutputFolderSegments;
  const outputFolderSegments = Array.isArray(segsRaw)
    ? segsRaw.map((s) => String(s).trim()).filter(Boolean)
    : undefined;
  const childTabId = String(o.childTabId ?? o.ChildTabId ?? '').trim() || undefined;
  const documentNamePattern =
    String(o.documentNamePattern ?? o.DocumentNamePattern ?? '').trim() || undefined;
  const generatedAtPattern =
    String(o.generatedAtPattern ?? o.GeneratedAtPattern ?? '').trim() || undefined;
  return {
    id,
    templateId,
    templateCode,
    label,
    contextType,
    ...(outputFolderSegments?.length ? { outputFolderSegments } : {}),
    ...(childTabId ? { childTabId } : {}),
    ...(documentNamePattern ? { documentNamePattern } : {}),
    ...(generatedAtPattern ? { generatedAtPattern } : {}),
  };
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
