import { fetchFromDataGateway, fetchFromOperations } from '@/services/apiService';
import type {
  OcBoardColumn,
  OcBoardRuntimeContext,
  OcQueryExecuteResponse,
  OcWorkItemCard,
  OpBoard,
  OpBoardColumnConfig,
  OpForm,
  OpFormFieldBehavior,
  OpFormLayoutSection,
  OcFormRuntimeContext,
  OcFormFieldRuntimeDto,
  OcFieldBehaviorDto,
  OpField,
  OpPriority,
  OpState,
  OpStateFlow,
  OpWorkspaceDetail,
  OpWorkItemType,
  OpWorkspace,
} from '@/types/apps/operationCore';
import { buildOcFormLayoutPayload, normalizeOcGridCol, parseOpFormLayout } from '@/utils/ocFormLayout';

export const OC_DATASETS = {
  workspaces: 'op_workspaces',
  boards: 'op_boards',
  forms: 'op_forms',
  states: 'op_states',
  priorities: 'op_priorities',
  workItemTypes: 'op_work_item_types',
  fields: 'op_fields',
  stateFlows: 'op_state_flows',
} as const;

function parseSingleDgRecord(response: unknown): Record<string, unknown> | null {
  if (Array.isArray(response)) {
    const first = response[0];
    return first && typeof first === 'object' ? (first as Record<string, unknown>) : null;
  }
  if (response && typeof response === 'object') {
    const obj = response as Record<string, unknown>;
    if (Array.isArray(obj.items) && obj.items[0] && typeof obj.items[0] === 'object') {
      return obj.items[0] as Record<string, unknown>;
    }
    if (Array.isArray(obj.data) && obj.data[0] && typeof obj.data[0] === 'object') {
      return obj.data[0] as Record<string, unknown>;
    }
    return obj;
  }
  return null;
}

/** DG hata gövdesinden okunabilir mesaj çıkarır */
export function ocExtractDgErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) return fallback;
  const data = (error as { data?: unknown }).data;
  if (data && typeof data === 'object') {
    const d = data as Record<string, unknown>;
    const nested = d.error;
    if (nested && typeof nested === 'object') {
      const err = nested as Record<string, unknown>;
      const details = err.details;
      if (Array.isArray(details) && details.length > 0) {
        const parts = details
          .map((item) => {
            if (item && typeof item === 'object') {
              const row = item as Record<string, unknown>;
              const field = row.field ?? row.Field;
              const message = row.message ?? row.Message;
              if (field && message) return `${field}: ${message}`;
              if (message) return String(message);
            }
            return null;
          })
          .filter(Boolean);
        if (parts.length) return parts.join('; ');
      }
      if (err.message) return String(err.message);
      if (err.code) return String(err.code);
    }
    if (d.message) return String(d.message);
    if (d.errorDescription) return String(d.errorDescription);
  }
  return error.message || fallback;
}

function parseListResponse(response: unknown): unknown[] {
  if (Array.isArray(response)) return response;
  if (response && typeof response === 'object' && 'items' in response && Array.isArray((response as { items: unknown[] }).items))
    return (response as { items: unknown[] }).items;
  if (response && typeof response === 'object' && 'data' in response && Array.isArray((response as { data: unknown[] }).data))
    return (response as { data: unknown[] }).data;
  return [];
}

function buildQuery(params: {
  skip?: number;
  limit?: number;
  sort?: string;
  filter?: string;
  search?: string;
}): string {
  const q = new URLSearchParams();
  q.set('skip', String(params.skip ?? 0));
  q.set('limit', String(params.limit ?? 500));
  if (params.sort) q.set('sort', params.sort);
  if (params.filter) q.set('filter', params.filter);
  if (params.search) q.set('search', params.search);
  return q.toString();
}

function resolveRelationIds(raw: unknown): string[] {
  if (raw == null) return [];
  if (Array.isArray(raw)) {
    return raw.map((item) => resolveRelationId(item)).filter((id): id is string => !!id);
  }
  const single = resolveRelationId(raw);
  return single ? [single] : [];
}

function mapWorkspace(raw: Record<string, unknown>): OpWorkspace {
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? ''),
    description: raw.description != null ? String(raw.description) : undefined,
    workspaceType: raw.workspaceType != null ? String(raw.workspaceType) : undefined,
    workItemKeyPrefix: raw.workItemKeyPrefix != null ? String(raw.workItemKeyPrefix) : undefined,
  };
}

function mapWorkspaceDetail(raw: Record<string, unknown>): OpWorkspaceDetail {
  const base = mapWorkspace(raw);
  const seqRaw = raw.workItemSequenceStart;
  let workItemSequenceStart: number | null = null;
  if (seqRaw != null && seqRaw !== '') {
    const n = Number(seqRaw);
    workItemSequenceStart = Number.isFinite(n) ? n : null;
  }
  return {
    ...base,
    key: raw.key != null ? String(raw.key) : undefined,
    workItemKeyFormat:
      raw.workItemKeyFormat != null ? String(raw.workItemKeyFormat) : undefined,
    workItemSequenceStart,
    enabledTypeIds: resolveRelationIds(raw.enabledTypeIds ?? raw.EnabledTypeIds),
    enabledFieldIds: resolveRelationIds(raw.enabledFieldIds ?? raw.EnabledFieldIds),
    defaultStateFlowId: resolveRelationId(raw.defaultStateFlowId ?? raw.DefaultStateFlowId),
  };
}

function parseJsonRecord(raw: unknown): Record<string, unknown> {
  if (raw && typeof raw === 'object' && !Array.isArray(raw)) {
    return raw as Record<string, unknown>;
  }
  if (typeof raw === 'string' && raw.trim()) {
    try {
      const parsed = JSON.parse(raw) as unknown;
      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
        return parsed as Record<string, unknown>;
      }
    } catch {
      /* ignore */
    }
  }
  return {};
}

function parseStringArray(raw: unknown): string[] {
  if (raw == null) return [];
  if (Array.isArray(raw)) {
    return raw.map((v) => String(v).trim()).filter(Boolean);
  }
  if (typeof raw === 'string' && raw.trim()) return [raw.trim()];
  return [];
}

export { buildOcFormLayoutPayload } from '@/utils/ocFormLayout';

function parseFieldBehaviorsMap(raw: unknown): Record<string, OpFormFieldBehavior> {
  const obj = parseJsonRecord(raw);
  const result: Record<string, OpFormFieldBehavior> = {};
  for (const [key, value] of Object.entries(obj)) {
    if (!value || typeof value !== 'object' || Array.isArray(value)) continue;
    const b = value as Record<string, unknown>;
    result[key] = {
      visible: b.visible !== false && b.Visible !== false,
      required: b.required === true || b.Required === true,
      readonly: b.readonly === true || b.readOnly === true || b.Readonly === true,
      masked: b.masked === true || b.Masked === true,
    };
  }
  return result;
}

function parseDefaultValuesMap(raw: unknown): Record<string, unknown> {
  const obj = parseJsonRecord(raw);
  const result: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(obj)) {
    if (value !== undefined) result[key] = value;
  }
  return result;
}

function parseBoardColumns(configRaw: unknown): OpBoardColumnConfig[] {
  const config = parseJsonRecord(configRaw);
  const cols = config.columns ?? config.Columns;
  if (!Array.isArray(cols)) return [];

  const result: OpBoardColumnConfig[] = [];
  for (const item of cols) {
    if (!item || typeof item !== 'object') continue;
    const o = item as Record<string, unknown>;
    const stateId = resolveRelationId(o.stateId ?? o.StateId);
    if (!stateId) continue;
    result.push({
      stateId,
      title: o.title != null ? String(o.title) : o.Title != null ? String(o.Title) : null,
      queryKey: o.queryKey != null ? String(o.queryKey) : o.QueryKey != null ? String(o.QueryKey) : 'wi_board_column',
    });
  }
  return result;
}

export function mapOpForm(raw: Record<string, unknown>): OpForm {
  const ws = raw.workspaceId ?? raw.WorkspaceId;
  const parsedLayout = parseOpFormLayout(parseJsonRecord(raw.layout ?? raw.Layout));
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? ''),
    workspaceId: resolveRelationId(ws) ?? '',
    description: raw.description != null ? String(raw.description) : null,
    defaultTypeId: resolveRelationId(raw.defaultTypeId ?? raw.DefaultTypeId),
    defaultStateFlowId: resolveRelationId(raw.defaultStateFlowId ?? raw.DefaultStateFlowId),
    defaultStateId: resolveRelationId(raw.defaultStateId ?? raw.DefaultStateId),
    defaultPriorityId: resolveRelationId(raw.defaultPriorityId ?? raw.DefaultPriorityId),
    isDefault: Boolean(raw.isDefault ?? false),
    formHeading: parsedLayout.formHeading || undefined,
    formIntro: parsedLayout.formIntro || undefined,
    dialogMaxWidth: parsedLayout.dialogMaxWidth,
    layoutSections: parsedLayout.sections,
    sectionCols: parsedLayout.sectionCols,
    fieldCols: parsedLayout.fieldCols,
    fieldBehaviors: parseFieldBehaviorsMap(raw.fieldBehaviors ?? raw.FieldBehaviors),
    defaultValues: parseDefaultValuesMap(raw.defaultValues ?? raw.DefaultValues),
  };
}

function mapFieldBehaviorDto(raw: Record<string, unknown>): OcFieldBehaviorDto {
  return {
    visible: raw.visible !== false && raw.Visible !== false,
    readonly: raw.readonly === true || raw.readOnly === true || raw.Readonly === true,
    required: raw.required === true || raw.Required === true,
    masked: raw.masked === true || raw.Masked === true,
  };
}

function mapFormFieldRuntimeDto(raw: Record<string, unknown>): OcFormFieldRuntimeDto {
  return {
    key: String(raw.key ?? raw.Key ?? ''),
    label: raw.label != null ? String(raw.label) : raw.Label != null ? String(raw.Label) : undefined,
    fieldType:
      raw.fieldType != null ? String(raw.fieldType) : raw.FieldType != null ? String(raw.FieldType) : undefined,
    value: raw.value ?? raw.Value,
  };
}

export function mapFormRuntimeContext(raw: Record<string, unknown>): OcFormRuntimeContext {
  const fieldsRaw = raw.fields ?? raw.Fields;
  const behaviorsRaw = raw.fieldBehaviors ?? raw.FieldBehaviors;
  const fields: Record<string, OcFormFieldRuntimeDto> = {};
  const fieldBehaviors: Record<string, OcFieldBehaviorDto> = {};

  if (fieldsRaw && typeof fieldsRaw === 'object' && !Array.isArray(fieldsRaw)) {
    for (const [key, val] of Object.entries(fieldsRaw as Record<string, unknown>)) {
      if (val && typeof val === 'object') fields[key] = mapFormFieldRuntimeDto(val as Record<string, unknown>);
    }
  }

  if (behaviorsRaw && typeof behaviorsRaw === 'object' && !Array.isArray(behaviorsRaw)) {
    for (const [key, val] of Object.entries(behaviorsRaw as Record<string, unknown>)) {
      if (val && typeof val === 'object') fieldBehaviors[key] = mapFieldBehaviorDto(val as Record<string, unknown>);
    }
  }

  let layout: OcFormRuntimeContext['layout'] = null;
  const layoutObj = parseJsonRecord(raw.layout ?? raw.Layout);
  if (Object.keys(layoutObj).length > 0) {
    const parsed = parseOpFormLayout(layoutObj);
    if (parsed.sections.length) {
      layout = layoutFromParsed(parsed);
    }
  }

  const perms = (raw.permissions ?? raw.Permissions ?? {}) as Record<string, unknown>;
  const typesRaw = raw.types ?? raw.Types;
  const types = Array.isArray(typesRaw)
    ? typesRaw
        .map((t) => {
          if (!t || typeof t !== 'object') return null;
          const o = t as Record<string, unknown>;
          const id = String(o.id ?? o.Id ?? '');
          if (!id) return null;
          return {
            id,
            name: String(o.name ?? o.Name ?? id),
            category: o.category != null ? String(o.category) : o.Category != null ? String(o.Category) : null,
          };
        })
        .filter((t): t is NonNullable<typeof t> => !!t)
    : [];

  return {
    mode: String(raw.mode ?? raw.Mode ?? 'create'),
    workspaceId: String(raw.workspaceId ?? raw.WorkspaceId ?? ''),
    workItemId: raw.workItemId != null ? String(raw.workItemId) : raw.WorkItemId != null ? String(raw.WorkItemId) : null,
    formId: raw.formId != null ? String(raw.formId) : raw.FormId != null ? String(raw.FormId) : null,
    formName: raw.formName != null ? String(raw.formName) : raw.FormName != null ? String(raw.FormName) : null,
    defaultTypeId:
      raw.defaultTypeId != null ? String(raw.defaultTypeId) : raw.DefaultTypeId != null ? String(raw.DefaultTypeId) : null,
    initialStateId:
      raw.initialStateId != null ? String(raw.initialStateId) : raw.InitialStateId != null ? String(raw.InitialStateId) : null,
    layout,
    fields,
    fieldBehaviors,
    permissions: {
      canView: perms.canView !== false && perms.CanView !== false,
      canEdit: perms.canEdit === true || perms.CanEdit === true,
      canComment: perms.canComment === true || perms.CanComment === true,
    },
    types,
  };
}

function layoutFromParsed(parsed: ReturnType<typeof parseOpFormLayout>): NonNullable<OcFormRuntimeContext['layout']> {
  return {
    formHeading: parsed.formHeading || null,
    formIntro: parsed.formIntro || null,
    dialogMaxWidth: parsed.dialogMaxWidth,
    sectionCols: parsed.sectionCols,
    fieldCols: parsed.fieldCols,
    sections: parsed.sections.map((s) => ({
      key: s.key,
      title: s.title ?? null,
      cols: s.cols,
      fields: [...s.fields],
    })),
  };
}

export interface BuildFormPreviewContextInput {
  workspaceId: string;
  formName: string;
  formHeading?: string;
  formIntro?: string;
  dialogMaxWidth?: number;
  defaultTypeId?: string;
  sections: OpFormLayoutSection[];
  fieldCols: Record<string, number>;
  fieldBehaviors: Record<string, OpFormFieldBehavior>;
  defaultValues: Record<string, unknown>;
  layoutFieldItems: {
    value: string;
    title: string;
    displayLabel?: string;
    fieldType?: string;
    cardinality?: string | null;
    relationDataset?: string | null;
  }[];
  types: { id: string; name: string; category?: string | null }[];
  formId?: string | null;
}

/**
 * Editördeki kaydedilmemiş taslağı runtime form context'e çevirir.
 * Önizleme MO metadata önbelleğine bağlı kalmaz; layout/fieldCols anında yansır.
 */
export function buildFormPreviewContextFromDraft(input: BuildFormPreviewContextInput): OcFormRuntimeContext {
  const layoutPayload = buildOcFormLayoutPayload({
    formHeading: input.formHeading,
    formIntro: input.formIntro,
    dialogMaxWidth: input.dialogMaxWidth,
    sections: input.sections,
    fieldCols: input.fieldCols,
  });
  const parsed = parseOpFormLayout(layoutPayload);

  const fields: Record<string, OcFormFieldRuntimeDto> = {};
  const fieldBehaviors: Record<string, OcFieldBehaviorDto> = {};

  for (const section of parsed.sections) {
    for (const key of section.fields) {
      const item = input.layoutFieldItems.find((i) => i.value === key);
      const rawDefault = input.defaultValues[key];
      fields[key] = {
        key,
        label: item?.displayLabel ?? item?.title ?? key,
        fieldType: item?.fieldType,
        cardinality: item?.cardinality ?? 'single',
        relationDataset: item?.relationDataset ?? null,
        value:
          rawDefault !== undefined && rawDefault !== null && String(rawDefault).trim() !== ''
            ? rawDefault
            : undefined,
      };

      const b = input.fieldBehaviors[key];
      fieldBehaviors[key] = {
        visible: b?.visible !== false,
        required: b?.required === true,
        readonly: b?.readonly === true,
        masked: b?.masked === true,
      };
    }
  }

  return {
    mode: 'create',
    workspaceId: input.workspaceId,
    workItemId: null,
    formId: input.formId ?? null,
    formName: input.formName,
    defaultTypeId: input.defaultTypeId ?? null,
    initialStateId: null,
    layout: layoutFromParsed(parsed),
    fields,
    fieldBehaviors,
    permissions: {
      canView: true,
      canEdit: true,
      canComment: true,
    },
    types: input.types,
  };
}

export async function ocGetFormCreateContext(
  workspaceId: string,
  options?: { formId?: string }
): Promise<OcFormRuntimeContext> {
  const qs = new URLSearchParams({ workspaceId, mode: 'create' });
  if (options?.formId) qs.set('formId', options.formId);
  const raw = (await fetchFromOperations(
    `/api/v1/runtime/work-items/form?${qs.toString()}`,
    'GET'
  )) as Record<string, unknown>;
  return mapFormRuntimeContext(raw);
}

const OC_CREATE_TOP_LEVEL_KEYS = new Set([
  'title',
  'typeId',
  'description',
  'boardId',
  'assignee',
  'priorityId',
]);

export interface OcCreateWorkItemRequest {
  workspaceId: string;
  typeId: string;
  title: string;
  description?: string;
  boardId?: string;
  assignee?: string;
  priorityId?: string;
  fields?: Record<string, unknown>;
}

export interface OcCreateWorkItemResult {
  id: string;
  key: string;
}

export function buildCreateWorkItemRequest(
  model: Record<string, unknown>,
  workspaceId: string,
  boardId?: string
): OcCreateWorkItemRequest {
  const title = String(model.title ?? '').trim();
  const typeId = String(model.typeId ?? '').trim();
  const body: OcCreateWorkItemRequest = { workspaceId, typeId, title };

  if (boardId) body.boardId = boardId;

  const description = model.description;
  if (description != null && String(description).trim()) {
    body.description = String(description).trim();
  }

  for (const key of ['assignee', 'priorityId'] as const) {
    const value = model[key];
    if (value != null && String(value).trim()) {
      body[key] = String(value).trim();
    }
  }

  const fields: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(model)) {
    if (OC_CREATE_TOP_LEVEL_KEYS.has(key)) continue;
    if (value === undefined || value === null || value === '') continue;
    fields[key] = value;
  }
  if (Object.keys(fields).length) body.fields = fields;

  return body;
}

export function initialFormModelFromContext(ctx: OcFormRuntimeContext): Record<string, unknown> {
  const model: Record<string, unknown> = {};
  for (const [key, meta] of Object.entries(ctx.fields)) {
    if (meta.value !== undefined && meta.value !== null && meta.value !== '') {
      model[key] = meta.value;
    }
  }
  if (ctx.defaultTypeId && (model.typeId === undefined || model.typeId === null || model.typeId === '')) {
    model.typeId = ctx.defaultTypeId;
  }
  return model;
}

export function validateCreateFormModel(
  ctx: OcFormRuntimeContext,
  model: Record<string, unknown>
): boolean {
  if (!String(model.title ?? '').trim()) return false;
  if (!String(model.typeId ?? '').trim()) return false;

  for (const [key, behavior] of Object.entries(ctx.fieldBehaviors)) {
    if (behavior.required !== true || behavior.visible === false) continue;
    const value = model[key];
    if (value === undefined || value === null || String(value).trim() === '') return false;
  }
  return true;
}

function mapCreateWorkItemResult(raw: unknown): OcCreateWorkItemResult {
  const root = raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : {};
  const wi =
    root.workItem && typeof root.workItem === 'object'
      ? (root.workItem as Record<string, unknown>)
      : root.WorkItem && typeof root.WorkItem === 'object'
        ? (root.WorkItem as Record<string, unknown>)
        : root;
  return {
    id: String(wi.id ?? wi.Id ?? ''),
    key: String(wi.key ?? wi.Key ?? ''),
  };
}

export async function ocCreateWorkItem(payload: OcCreateWorkItemRequest): Promise<OcCreateWorkItemResult> {
  const body: Record<string, unknown> = {
    workspaceId: payload.workspaceId,
    typeId: payload.typeId,
    title: payload.title,
  };
  if (payload.description) body.description = payload.description;
  if (payload.boardId) body.boardId = payload.boardId;
  if (payload.assignee) body.assignee = payload.assignee;
  if (payload.priorityId) body.priorityId = payload.priorityId;
  if (payload.fields && Object.keys(payload.fields).length) body.fields = payload.fields;

  const raw = await fetchFromOperations('/api/v1/work-items', 'POST', body);
  return mapCreateWorkItemResult(raw);
}

function mapBoard(raw: Record<string, unknown>): OpBoard {
  const ws = raw.workspaceId;
  const workspaceId =
    typeof ws === 'string'
      ? ws
      : ws && typeof ws === 'object'
        ? String((ws as Record<string, unknown>).__dataId ?? (ws as Record<string, unknown>).dataId ?? '')
        : '';
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? ''),
    workspaceId,
    viewType: raw.viewType != null ? String(raw.viewType) : undefined,
    defaultFormId: resolveRelationId(raw.defaultFormId ?? raw.DefaultFormId),
    defaultStateFlowId: resolveRelationId(raw.defaultStateFlowId ?? raw.DefaultStateFlowId),
    visibleFields: parseStringArray(raw.visibleFields ?? raw.VisibleFields),
    columns: parseBoardColumns(raw.config ?? raw.Config),
  };
}

export async function ocListDataset(
  dataset: string,
  options?: { skip?: number; limit?: number; sort?: string; filter?: string; search?: string }
) {
  const qs = buildQuery(options ?? {});
  const url = `/api/v1/data/${encodeURIComponent(dataset)}?${qs}`;
  const raw = await fetchFromDataGateway(url, 'GET');
  return parseListResponse(raw);
}

export async function ocCreate(dataset: string, body: Record<string, unknown>) {
  const url = `/api/v1/data/${encodeURIComponent(dataset)}`;
  return fetchFromDataGateway(url, 'POST', body);
}

export async function ocUpdate(dataset: string, dataId: string, body: Record<string, unknown>) {
  const url = `/api/v1/data/${encodeURIComponent(dataset)}/${encodeURIComponent(dataId)}`;
  return fetchFromDataGateway(url, 'PUT', body);
}

export async function ocDelete(dataset: string, dataId: string) {
  const url = `/api/v1/data/${encodeURIComponent(dataset)}/${encodeURIComponent(dataId)}`;
  return fetchFromDataGateway(url, 'DELETE');
}

export function mapOpState(raw: Record<string, unknown>): OpState {
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? ''),
    category: String(raw.category ?? ''),
    description: raw.description != null ? String(raw.description) : null,
    color: raw.color != null ? String(raw.color) : null,
    icon: raw.icon != null ? String(raw.icon) : null,
    isInitial: Boolean(raw.isInitial ?? false),
    isStart: Boolean(raw.isStart ?? false),
    isClosed: Boolean(raw.isClosed ?? false),
    isTerminal: Boolean(raw.isTerminal ?? false),
    allowReopen: Boolean(raw.allowReopen ?? false),
    sortOrder: raw.sortOrder != null && raw.sortOrder !== '' ? Number(raw.sortOrder) : null,
  };
}

export async function ocListStates(): Promise<OpState[]> {
  const rows = await ocListDataset(OC_DATASETS.states, { sort: 'sortOrder:asc,name:asc', limit: 500 });
  return rows
    .map((r) => mapOpState(r as Record<string, unknown>))
    .filter((s) => s.__dataId && s.name);
}

export async function ocCreateState(payload: Record<string, unknown>) {
  await ocCreate(OC_DATASETS.states, payload);
}

export async function ocUpdateState(stateId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.states, stateId, payload);
}

export async function ocDeleteState(stateId: string) {
  await ocDelete(OC_DATASETS.states, stateId);
}

export function mapOpPriority(raw: Record<string, unknown>): OpPriority {
  const levelRaw = raw.level;
  let level: number | null = null;
  if (levelRaw != null && levelRaw !== '') {
    const n = Number(levelRaw);
    level = Number.isFinite(n) ? n : null;
  }
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? ''),
    level,
    description: raw.description != null ? String(raw.description) : null,
    color: raw.color != null ? String(raw.color) : null,
    icon: raw.icon != null ? String(raw.icon) : null,
    sortOrder: raw.sortOrder != null && raw.sortOrder !== '' ? Number(raw.sortOrder) : null,
  };
}

export async function ocListPriorities(): Promise<OpPriority[]> {
  const rows = await ocListDataset(OC_DATASETS.priorities, {
    sort: 'sortOrder:asc,level:asc,name:asc',
    limit: 500,
  });
  return rows
    .map((r) => mapOpPriority(r as Record<string, unknown>))
    .filter((p) => p.__dataId && p.name);
}

export async function ocCreatePriority(payload: Record<string, unknown>) {
  await ocCreate(OC_DATASETS.priorities, payload);
}

export async function ocUpdatePriority(priorityId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.priorities, priorityId, payload);
}

export async function ocDeletePriority(priorityId: string) {
  await ocDelete(OC_DATASETS.priorities, priorityId);
}

function resolveRelationId(raw: unknown): string | null {
  if (raw == null || raw === '') return null;
  if (typeof raw === 'string') return raw;
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    const id = o.__dataId ?? o.dataId ?? o.DataId;
    return id != null ? String(id) : null;
  }
  return null;
}

export function mapOpWorkItemType(raw: Record<string, unknown>): OpWorkItemType {
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? ''),
    category: String(raw.category ?? ''),
    description: raw.description != null ? String(raw.description) : null,
    color: raw.color != null ? String(raw.color) : null,
    icon: raw.icon != null ? String(raw.icon) : null,
    sortOrder: raw.sortOrder != null && raw.sortOrder !== '' ? Number(raw.sortOrder) : null,
    isSystem: Boolean(raw.isSystem ?? false),
    workspaceId: resolveRelationId(raw.workspaceId),
  };
}

/** Sistem tanımlaması: yalnızca global tipler (workspaceId boş) */
export async function ocListGlobalWorkItemTypes(): Promise<OpWorkItemType[]> {
  const rows = await ocListDataset(OC_DATASETS.workItemTypes, {
    sort: 'category:asc,sortOrder:asc,name:asc',
    limit: 500,
  });
  return rows
    .map((r) => mapOpWorkItemType(r as Record<string, unknown>))
    .filter((t) => t.__dataId && t.name && !t.workspaceId);
}

export async function ocCreateWorkItemType(payload: Record<string, unknown>) {
  await ocCreate(OC_DATASETS.workItemTypes, payload);
}

export async function ocUpdateWorkItemType(typeId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.workItemTypes, typeId, payload);
}

export async function ocDeleteWorkItemType(typeId: string) {
  await ocDelete(OC_DATASETS.workItemTypes, typeId);
}

export function mapOpField(raw: Record<string, unknown>): OpField {
  const optionsRaw = raw.options ?? raw.Options;
  let options: Record<string, unknown> | null = null;
  if (optionsRaw && typeof optionsRaw === 'object' && !Array.isArray(optionsRaw)) {
    options = optionsRaw as Record<string, unknown>;
  }

  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    key: String(raw.key ?? raw.Key ?? ''),
    label: String(raw.label ?? raw.Label ?? ''),
    fieldType: String(raw.fieldType ?? raw.FieldType ?? 'text'),
    scope: String(raw.scope ?? raw.Scope ?? 'pool'),
    category: raw.category != null ? String(raw.category) : null,
    description: raw.description != null ? String(raw.description) : null,
    cardinality: raw.cardinality != null ? String(raw.cardinality) : null,
    relationDatasetName:
      raw.relationDatasetName != null ? String(raw.relationDatasetName) : null,
    options,
    isSystem: Boolean(raw.isSystem ?? false),
    isSensitive: Boolean(raw.isSensitive ?? false),
    sortOrder: raw.sortOrder != null && raw.sortOrder !== '' ? Number(raw.sortOrder) : null,
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId),
  };
}

export async function ocListGlobalPoolFields(): Promise<OpField[]> {
  const rows = await ocListDataset(OC_DATASETS.fields, {
    sort: 'category:asc,sortOrder:asc,key:asc',
    limit: 500,
  });
  return rows
    .map((r) => mapOpField(r as Record<string, unknown>))
    .filter(
      (f) =>
        f.__dataId &&
        f.key &&
        f.scope === 'pool' &&
        !f.workspaceId
    );
}

export async function ocCreateField(payload: Record<string, unknown>) {
  await ocCreate(OC_DATASETS.fields, payload);
}

export async function ocUpdateField(fieldId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.fields, fieldId, payload);
}

export async function ocDeleteField(fieldId: string) {
  await ocDelete(OC_DATASETS.fields, fieldId);
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
  return {
    transitionKey,
    fromStateId,
    toStateId,
    label: o.label != null ? String(o.label) : o.Label != null ? String(o.Label) : null,
    order,
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

export async function ocListWorkspaces(): Promise<OpWorkspace[]> {
  const rows = await ocListDataset(OC_DATASETS.workspaces, { sort: 'name:asc', limit: 200 });
  return rows
    .map((r) => mapWorkspace(r as Record<string, unknown>))
    .filter((w) => w.__dataId && w.name);
}

export async function ocGetWorkspace(workspaceId: string): Promise<OpWorkspaceDetail | null> {
  const url = `/api/v1/data/${encodeURIComponent(OC_DATASETS.workspaces)}/${encodeURIComponent(workspaceId)}`;
  try {
    const raw = await fetchFromDataGateway(url, 'GET');
    const record = parseSingleDgRecord(raw);
    if (record) {
      const detail = mapWorkspaceDetail(record);
      if (detail.__dataId) return detail;
    }
  } catch {
    // GET by id başarısız olursa listeden fallback
  }

  const rows = await ocListDataset(OC_DATASETS.workspaces, { limit: 500 });
  const match = rows.find((r) => {
    const id = String((r as Record<string, unknown>).__dataId ?? (r as Record<string, unknown>).dataId ?? '');
    return id === workspaceId;
  });
  if (!match) return null;
  return mapWorkspaceDetail(match as Record<string, unknown>);
}

export async function ocUpdateWorkspace(workspaceId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.workspaces, workspaceId, payload);
}

/** Global + workspace'e özel tipler (seçim ve listeleme) */
export async function ocListWorkItemTypesForWorkspace(workspaceId: string): Promise<OpWorkItemType[]> {
  const [globalRows, scopedRows] = await Promise.all([
    ocListGlobalWorkItemTypes(),
    ocListDataset(OC_DATASETS.workItemTypes, {
      filter: `workspaceId:eq:${workspaceId}`,
      sort: 'category:asc,sortOrder:asc,name:asc',
      limit: 200,
    }),
  ]);
  const scoped = scopedRows
    .map((r) => mapOpWorkItemType(r as Record<string, unknown>))
    .filter((t) => t.__dataId && t.name && t.workspaceId === workspaceId);
  const seen = new Set<string>();
  return [...globalRows, ...scoped].filter((t) => {
    if (seen.has(t.__dataId)) return false;
    seen.add(t.__dataId);
    return true;
  });
}

/** Workspace'e özel tipler (CRUD tablosu) */
export async function ocListWorkspaceScopedWorkItemTypes(
  workspaceId: string
): Promise<OpWorkItemType[]> {
  const rows = await ocListDataset(OC_DATASETS.workItemTypes, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'category:asc,sortOrder:asc,name:asc',
    limit: 200,
  });
  return rows
    .map((r) => mapOpWorkItemType(r as Record<string, unknown>))
    .filter((t) => t.__dataId && t.name && t.workspaceId === workspaceId);
}

/** Global pool + workspace'e özel pool alanları */
export async function ocListPoolFieldsForWorkspace(workspaceId: string): Promise<OpField[]> {
  const [globalRows, scopedRows] = await Promise.all([
    ocListGlobalPoolFields(),
    ocListDataset(OC_DATASETS.fields, {
      filter: `workspaceId:eq:${workspaceId}`,
      sort: 'category:asc,sortOrder:asc,key:asc',
      limit: 200,
    }),
  ]);
  const scoped = scopedRows
    .map((r) => mapOpField(r as Record<string, unknown>))
    .filter((f) => f.__dataId && f.key && f.scope === 'pool' && f.workspaceId === workspaceId);
  const seen = new Set<string>();
  return [...globalRows, ...scoped].filter((f) => {
    if (seen.has(f.__dataId)) return false;
    seen.add(f.__dataId);
    return true;
  });
}

/** Workspace'e özel pool alanları (CRUD tablosu) */
export async function ocListWorkspaceScopedFields(workspaceId: string): Promise<OpField[]> {
  const rows = await ocListDataset(OC_DATASETS.fields, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'category:asc,sortOrder:asc,key:asc',
    limit: 200,
  });
  return rows
    .map((r) => mapOpField(r as Record<string, unknown>))
    .filter((f) => f.__dataId && f.key && f.scope === 'pool' && f.workspaceId === workspaceId);
}

export async function ocListBoardsForWorkspace(workspaceId: string): Promise<OpBoard[]> {
  const rows = await ocListDataset(OC_DATASETS.boards, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'name:asc',
    limit: 200,
  });
  return rows
    .map((r) => mapBoard(r as Record<string, unknown>))
    .filter((b) => b.__dataId && b.name && b.workspaceId === workspaceId);
}

export async function ocListFormsForWorkspace(workspaceId: string): Promise<OpForm[]> {
  const rows = await ocListDataset(OC_DATASETS.forms, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'name:asc',
    limit: 100,
  });
  return rows
    .map((r) => mapOpForm(r as Record<string, unknown>))
    .filter((f) => f.__dataId && f.name && f.workspaceId === workspaceId);
}

async function ocCreateRecordId(
  dataset: string,
  payload: Record<string, unknown>
): Promise<string | null> {
  const raw = await ocCreate(dataset, payload);
  const record =
    parseSingleDgRecord(raw) ??
    (raw && typeof raw === 'object' && !Array.isArray(raw) ? (raw as Record<string, unknown>) : null);
  if (!record) return null;
  const id = String(record.__dataId ?? record.dataId ?? '');
  return id || null;
}

export async function ocCreateForm(payload: Record<string, unknown>): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.forms, payload);
}

export async function ocUpdateForm(formId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.forms, formId, payload);
}

export async function ocDeleteForm(formId: string) {
  await ocDelete(OC_DATASETS.forms, formId);
}

export async function ocCreateBoard(payload: Record<string, unknown>): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.boards, payload);
}

export async function ocUpdateBoard(boardId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.boards, boardId, payload);
}

export async function ocDeleteBoard(boardId: string) {
  await ocDelete(OC_DATASETS.boards, boardId);
}

export async function ocOperationsLive() {
  return fetchFromOperations('/api/v1/health/live', 'GET');
}

export async function ocOperationsHealth() {
  return fetchFromOperations('/api/v1/health', 'GET');
}

function pickStr(obj: Record<string, unknown>, ...keys: string[]): string | undefined {
  for (const k of keys) {
    const v = obj[k];
    if (v != null && v !== '') return String(v);
  }
  return undefined;
}

function mapWorkItemCard(raw: Record<string, unknown>): OcWorkItemCard {
  return {
    id: pickStr(raw, 'id', 'Id') ?? '',
    key: pickStr(raw, 'key', 'Key') ?? '',
    title: pickStr(raw, 'title', 'Title') ?? '',
    stateId: pickStr(raw, 'stateId', 'StateId'),
    assignee: pickStr(raw, 'assignee', 'Assignee'),
    priorityId: pickStr(raw, 'priorityId', 'PriorityId'),
    typeId: pickStr(raw, 'typeId', 'TypeId'),
  };
}

function mapBoardColumn(raw: Record<string, unknown>): OcBoardColumn {
  const alt = raw.alternativeTransitionKeys ?? raw.AlternativeTransitionKeys;
  const params = raw.parametersTemplate ?? raw.ParametersTemplate;
  const template: Record<string, string> = {};
  if (params && typeof params === 'object' && !Array.isArray(params)) {
    for (const [k, v] of Object.entries(params as Record<string, unknown>)) {
      if (v != null) template[k] = String(v);
    }
  }
  return {
    stateId: pickStr(raw, 'stateId', 'StateId') ?? '',
    title: pickStr(raw, 'title', 'Title'),
    dropEligible: Boolean(raw.dropEligible ?? raw.DropEligible ?? true),
    defaultTransitionKey: pickStr(raw, 'defaultTransitionKey', 'DefaultTransitionKey'),
    alternativeTransitionKeys: Array.isArray(alt) ? alt.map(String) : [],
    queryKey: pickStr(raw, 'queryKey', 'QueryKey') ?? 'wi_board_column',
    parametersTemplate: template,
    suggestedPageSize: Number(raw.suggestedPageSize ?? raw.SuggestedPageSize ?? 50),
  };
}

function mapBoardRuntimeContext(raw: Record<string, unknown>): OcBoardRuntimeContext {
  const cols = raw.columns ?? raw.Columns;
  const cardKeys = raw.cardFieldKeys ?? raw.CardFieldKeys;
  const perms = (raw.permissions ?? raw.Permissions ?? {}) as Record<string, unknown>;
  return {
    boardId: pickStr(raw, 'boardId', 'BoardId') ?? '',
    workspaceId: pickStr(raw, 'workspaceId', 'WorkspaceId') ?? '',
    name: pickStr(raw, 'name', 'Name'),
    viewType: pickStr(raw, 'viewType', 'ViewType'),
    permissions: {
      canView: Boolean(perms.canView ?? perms.CanView ?? true),
      canEdit: Boolean(perms.canEdit ?? perms.CanEdit ?? false),
      canComment: Boolean(perms.canComment ?? perms.CanComment ?? false),
    },
    columns: Array.isArray(cols)
      ? cols.map((c) => mapBoardColumn(c as Record<string, unknown>)).filter((c) => c.stateId)
      : [],
    cardFieldKeys: Array.isArray(cardKeys) ? cardKeys.map(String) : ['title', 'assignee', 'priorityId', 'key'],
  };
}

function mapQueryExecuteResponse(raw: Record<string, unknown>): OcQueryExecuteResponse {
  const items = raw.items ?? raw.Items;
  return {
    dataset: pickStr(raw, 'dataset', 'Dataset') ?? 'op_work_items',
    queryKey: pickStr(raw, 'queryKey', 'QueryKey') ?? '',
    items: Array.isArray(items)
      ? items.map((i) => mapWorkItemCard(i as Record<string, unknown>)).filter((c) => c.id)
      : [],
    skip: Number(raw.skip ?? raw.Skip ?? 0),
    take: Number(raw.take ?? raw.Take ?? 0),
    total: Number(raw.total ?? raw.Total ?? 0),
  };
}

export async function ocGetBoardContext(boardId: string): Promise<OcBoardRuntimeContext> {
  const raw = (await fetchFromOperations(
    `/api/v1/runtime/boards/${encodeURIComponent(boardId)}`,
    'GET'
  )) as Record<string, unknown>;
  return mapBoardRuntimeContext(raw);
}

export async function ocExecuteQuery(
  queryKey: string,
  request: {
    dataset?: string;
    parameters?: Record<string, string | number | boolean | null>;
    skip?: number;
    take?: number;
  }
): Promise<OcQueryExecuteResponse> {
  const raw = (await fetchFromOperations(
    `/api/v1/runtime/queries/${encodeURIComponent(queryKey)}/execute`,
    'POST',
    {
      dataset: request.dataset ?? 'op_work_items',
      parameters: request.parameters ?? {},
      skip: request.skip ?? 0,
      take: request.take ?? 50,
    }
  )) as Record<string, unknown>;
  return mapQueryExecuteResponse(raw);
}

export function ocBuildColumnQueryRequest(column: OcBoardColumn) {
  return {
    dataset: 'op_work_items',
    parameters: { ...column.parametersTemplate },
    skip: 0,
    take: column.suggestedPageSize || 50,
  };
}
