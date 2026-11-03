import { fetchFromDataGateway } from '@/services/apiService';
import { diGenerateFromTemplate, diListTemplates } from '@/services/documentIntelligenceService';
import { ocListStateFlowsForWorkspace } from '@/services/operationCore/flows';
import type { OcTransitionDocumentAction } from '@/types/apps/operationCore';
import { normalizeReportingDocumentBindings } from '@/utils/reportingDocumentBindings';
import { linkResourceToMatchingChildRow } from '@/utils/reportingChildRowLink';
import { formatTrDisplayDate } from '@/utils/trDisplayDate';

function rowsOf(raw: unknown): Record<string, unknown>[] {
  if (Array.isArray(raw)) {
    return raw.filter((row) => row && typeof row === 'object') as Record<string, unknown>[];
  }
  if (!raw || typeof raw !== 'object') return [];
  const record = raw as Record<string, unknown>;
  const items = record.items ?? record.data ?? record.results;
  if (Array.isArray(items)) {
    return items.filter((row) => row && typeof row === 'object') as Record<string, unknown>[];
  }
  if (record.__dataId || record.dataId) return [record];
  return [];
}

function textOf(value: unknown): string {
  if (value == null) return '';
  if (typeof value === 'string') return value.trim();
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  if (Array.isArray(value)) return value.map((item) => textOf(item)).filter(Boolean).join(', ');
  if (typeof value === 'object') {
    const record = value as Record<string, unknown>;
    const person = [record.firstName, record.lastName]
      .filter((part) => typeof part === 'string' && part.trim())
      .join(' ');
    const label =
      record.DisplayName ??
      record.displayName ??
      record.ad ??
      record.name ??
      record.title ??
      person ??
      record.key ??
      record.kod ??
      record.code;
    if (typeof label === 'string' && label.trim()) return label.trim();
    const id = record.__dataId ?? record.dataId ?? record.id ?? record.value;
    if (typeof id === 'string' && id.trim()) return id.trim();
  }
  return '';
}

function idList(value: unknown): string[] {
  if (Array.isArray(value)) return value.flatMap(idList);
  if (typeof value === 'string' && value.trim()) return [value.trim()];
  if (value && typeof value === 'object') {
    const record = value as Record<string, unknown>;
    const id = record.__dataId ?? record.dataId ?? record.id ?? record.value;
    if (typeof id === 'string' && id.trim()) return [id.trim()];
  }
  return [];
}

function overridesFromRow(row: Record<string, unknown>, action: OcTransitionDocumentAction): Record<string, string> {
  const overrides: Record<string, string> = {};
  for (const [field, value] of Object.entries(row)) {
    if (field.startsWith('_')) continue;
    const dated = formatTrDisplayDate(value);
    const text = dated || textOf(value);
    if (!text) continue;
    const labeled = action.displayMaps?.[field]?.[text] ?? action.displayMaps?.[field]?.[text.toLowerCase()];
    overrides[field] = labeled || text;
  }
  for (const [source, target] of Object.entries(action.fieldAliases ?? {})) {
    const alias = target.trim();
    const from = source.trim();
    if (!alias || !from || !overrides[from]) continue;
    if (!overrides[alias]) overrides[alias] = overrides[from];
  }
  const when = formatTrDisplayDate(new Date());
  if (when) overrides.generatedAt = when;
  return overrides;
}

async function templateIdByCode(code: string): Promise<string | null> {
  const listed = await diListTemplates();
  return listed.items.find((item) => item.code === code)?.id ?? null;
}

async function outputFolderId(action: OcTransitionDocumentAction): Promise<string | null> {
  const explicit = action.outputFolderId?.trim();
  if (explicit) return explicit;
  const reportId = action.reportId.trim();
  if (!reportId) return null;
  const raw = await fetchFromDataGateway(
    `/api/v1/data/@reporting_reports?filter=${encodeURIComponent(`id:eq:${reportId}`)}&limit=20`
  );
  const report = rowsOf(raw).find((row) => String(row.id ?? '') === reportId);
  if (!report) return null;
  const binding = normalizeReportingDocumentBindings(report.documentBindings).find(
    (item) => item.templateCode === action.templateCode || item.templateId === action.templateCode
  );
  return binding?.outputFolderId?.trim() || null;
}

async function loadSource(datasetName: string, id: string): Promise<Record<string, unknown> | null> {
  const raw = await fetchFromDataGateway(
    `/api/v1/data/${encodeURIComponent(datasetName)}/${encodeURIComponent(id)}?expand=true`
  );
  return rowsOf(raw)[0] ?? null;
}

export async function findTransitionDocumentAction(
  workspaceId: string,
  transitionKey: string
): Promise<OcTransitionDocumentAction | null> {
  const key = transitionKey.trim();
  const workspace = workspaceId.trim();
  if (!key || !workspace) return null;
  const flows = await ocListStateFlowsForWorkspace(workspace);
  for (const flow of flows) {
    const match = flow.transitions.find(
      (transition) => transition.transitionKey === key && transition.documentAction?.templateCode?.trim()
    );
    if (match?.documentAction) return match.documentAction;
  }
  return null;
}

/** Geçiş tanımındaki belge eylemini çalıştırır. Eylem yoksa çağıran hiç girmez. */
export async function writeTransitionDocuments(options: {
  action: OcTransitionDocumentAction;
  workItemKey: string;
  fields: Record<string, unknown>;
}): Promise<{ written: number; note: string }> {
  const action = options.action;
  const templateCode = action.templateCode.trim();
  const workItemKey = options.workItemKey.trim();
  if (!templateCode || !workItemKey) return { written: 0, note: '' };

  const itemFields = action.itemsField.split(',').map((field) => field.trim()).filter(Boolean);
  const sourceIds = [...new Set(itemFields.flatMap((field) => idList(options.fields[field])))];
  if (!sourceIds.length) return { written: 0, note: 'Belge için kayıt seçilmemiş.' };

  const templateId = await templateIdByCode(templateCode);
  if (!templateId) return { written: 0, note: 'Belge şablonu bulunamadı.' };

  const folderId = await outputFolderId(action);
  if (!folderId) return { written: 0, note: 'Çıktı klasörü tanımlı değil.' };

  const sourceDataset = action.sourceDataset.trim();
  let written = 0;
  for (const sourceId of sourceIds) {
    const row = await loadSource(sourceDataset, sourceId);
    if (!row) continue;
    const values = overridesFromRow(row, action);
    if (action.parentRefField?.trim()) values[action.parentRefField.trim()] = workItemKey;
    const labelField = action.labelField?.trim();
    const label =
      (labelField ? values[labelField] : '') ||
      values.demirbasNo ||
      values.kod ||
      values.name ||
      values.ad ||
      sourceId;
    const result = await diGenerateFromTemplate(templateId, {
      parentFolderId: folderId,
      documentName: `${templateCode} ${workItemKey} ${label}`.trim(),
      preserveMissingPlaceholders: false,
      overrides: values,
    });
    if (!result.resourceId) continue;
    if (
      action.linkDataset?.trim() &&
      action.linkParentField?.trim() &&
      action.linkMatchField?.trim() &&
      action.linkMatchValue?.trim()
    ) {
      await linkResourceToMatchingChildRow({
        resourceId: result.resourceId,
        datasetName: action.linkDataset.trim(),
        linkField: action.linkParentField.trim(),
        parentId: sourceId,
        matchField: action.linkMatchField.trim(),
        matchValue: action.linkMatchValue?.trim() || '',
        refField: action.linkRefField?.trim() || undefined,
        refValue: action.linkRefField?.trim() ? workItemKey : undefined,
        relationType: 'output',
        sortField: 'tarih',
      });
    }
    written += 1;
  }

  return {
    written,
    note: written ? `${written} belge çıktı klasörüne yazıldı.` : 'Belge yazılamadı.',
  };
}
