import {
  OC_DATASETS,
  ocCreate,
  ocDelete,
  ocListDataset,
  ocUpdate,
  parseSingleDgRecord,
  resolveRelationId,
} from '@/services/operationCoreService';
import type { OcTransitionDocumentAction, OpStateFlow, OpStateFlowTransition } from '@/types/apps/operationCore';

function mapDocumentAction(raw: unknown): OcTransitionDocumentAction | undefined {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return undefined;
  const o = raw as Record<string, unknown>;
  const templateCode = String(o.templateCode ?? o.TemplateCode ?? '').trim();
  const itemsField = String(o.itemsField ?? o.ItemsField ?? '').trim();
  const sourceDataset = String(o.sourceDataset ?? o.SourceDataset ?? '').trim();
  const reportId = String(o.reportId ?? o.ReportId ?? '').trim();
  const outputFolderId = String(o.outputFolderId ?? o.OutputFolderId ?? '').trim();
  if (!templateCode || !itemsField || !sourceDataset || (!reportId && !outputFolderId)) return undefined;
  const text = (value: unknown) => {
    const s = String(value ?? '').trim();
    return s || undefined;
  };
  const aliases = o.fieldAliases ?? o.FieldAliases;
  const maps = o.displayMaps ?? o.DisplayMaps;
  return {
    templateCode,
    itemsField,
    sourceDataset,
    reportId,
    outputFolderId: text(o.outputFolderId ?? o.OutputFolderId),
    linkDataset: text(o.linkDataset ?? o.LinkDataset),
    linkParentField: text(o.linkParentField ?? o.LinkParentField),
    linkMatchField: text(o.linkMatchField ?? o.LinkMatchField),
    linkMatchValue: text(o.linkMatchValue ?? o.LinkMatchValue),
    linkRefField: text(o.linkRefField ?? o.LinkRefField),
    parentRefField: text(o.parentRefField ?? o.ParentRefField),
    labelField: text(o.labelField ?? o.LabelField),
    attachToWorkItem: Boolean(o.attachToWorkItem ?? o.AttachToWorkItem ?? false) || undefined,
    fieldAliases:
      aliases && typeof aliases === 'object' && !Array.isArray(aliases)
        ? (aliases as Record<string, string>)
        : undefined,
    displayMaps:
      maps && typeof maps === 'object' && !Array.isArray(maps)
        ? (maps as Record<string, Record<string, string>>)
        : undefined,
  };
}

function mapOpStateFlowTransition(raw: unknown): OpStateFlowTransition | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const fromStateId = resolveRelationId(o.fromStateId ?? o.FromStateId);
  const toStateId = resolveRelationId(o.toStateId ?? o.ToStateId);
  const transitionKey = String(o.transitionKey ?? o.TransitionKey ?? '').trim();
  if (!fromStateId || !toStateId || !transitionKey) return null;
  const orderRaw = o.order ?? o.Order;
  let order: number | null = null;
  if (orderRaw != null && orderRaw !== '') {
    const n = Number(orderRaw);
    order = Number.isFinite(n) ? n : null;
  }
  const requiredRaw = o.requiredFields ?? o.RequiredFields;
  const requiredFields = Array.isArray(requiredRaw)
    ? requiredRaw.map((x) => String(x).trim()).filter(Boolean)
    : undefined;

  const permsRaw = (o.permissions ?? o.Permissions) as Record<string, unknown> | undefined;
  const groupsRaw =
    permsRaw && typeof permsRaw === 'object' ? permsRaw.groups ?? permsRaw.Groups : undefined;
  const permissionGroups = Array.isArray(groupsRaw)
    ? groupsRaw.map((x) => String(x).trim()).filter(Boolean)
    : undefined;

  return {
    transitionKey,
    fromStateId,
    toStateId,
    label: o.label != null ? String(o.label) : o.Label != null ? String(o.Label) : null,
    order,
    requiredFields: requiredFields?.length ? requiredFields : undefined,
    requireComment: Boolean(o.requireComment ?? o.RequireComment ?? false),
    permissionGroups: permissionGroups?.length ? permissionGroups : undefined,
    documentAction: mapDocumentAction(o.documentAction ?? o.DocumentAction),
  };
}

export function mapOpStateFlow(raw: Record<string, unknown>): OpStateFlow {
  const transitionsRaw = raw.transitions ?? raw.Transitions;
  const transitions: OpStateFlowTransition[] = [];
  if (Array.isArray(transitionsRaw)) {
    for (const item of transitionsRaw) {
      const mapped = mapOpStateFlowTransition(item);
      if (mapped) transitions.push(mapped);
    }
  }
  transitions.sort((a, b) => (a.order ?? 0) - (b.order ?? 0));

  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? ''),
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId) ?? '',
    description: raw.description != null ? String(raw.description) : null,
    initialStateId: resolveRelationId(raw.initialStateId ?? raw.InitialStateId) ?? '',
    isDefault: Boolean(raw.isDefault ?? false),
    isActive: raw.isActive !== false,
    sortOrder: raw.sortOrder != null && raw.sortOrder !== '' ? Number(raw.sortOrder) : null,
    transitions,
  };
}

export async function ocListStateFlowsForWorkspace(workspaceId: string): Promise<OpStateFlow[]> {
  const rows = await ocListDataset(OC_DATASETS.stateFlows, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'sortOrder:asc,name:asc',
    limit: 100,
  });
  return rows
    .map((r) => mapOpStateFlow(r as Record<string, unknown>))
    .filter((f) => f.__dataId && f.name && f.workspaceId === workspaceId);
}

export async function ocCreateStateFlow(
  payload: Record<string, unknown>
): Promise<string | null> {
  const raw = await ocCreate(OC_DATASETS.stateFlows, payload);
  const record =
    parseSingleDgRecord(raw) ??
    (raw && typeof raw === 'object' && !Array.isArray(raw) ? (raw as Record<string, unknown>) : null);
  if (!record) return null;
  const id = String(record.__dataId ?? record.dataId ?? '');
  return id || null;
}

export async function ocUpdateStateFlow(flowId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.stateFlows, flowId, payload);
}

export async function ocDeleteStateFlow(flowId: string) {
  await ocDelete(OC_DATASETS.stateFlows, flowId);
}
