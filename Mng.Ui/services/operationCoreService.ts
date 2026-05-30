import { fetchBlobFromDataGateway, fetchFromDataGateway, fetchFromOperations } from '@/services/apiService';
import type {
  OcAttachment,
  OcBoardCatalogs,
  OcBoardColumn,
  OcBoardListColumn,
  OcBoardListRequest,
  OcBoardRuntimeContext,
  OcCatalogDisplayEntry,
  OcColumnFormat,
  OcComment,
  OcPersonDisplay,
  OcQueryExecuteResponse,
  OcSlaSnapshot,
  OcTimelineEntry,
  OcTimelinePage,
  OcWorkItemCard,
  OcWorkItemLinkSummary,
  OcWorkItemProfile,
  OcWorkItemSummary,
  OpBoard,
  OpBoardColumnConfig,
  OpBoardListColumnConfig,
  OpBoardSortConfig,
  OpProfile,
  OpForm,
  OpFormFieldBehavior,
  OpFormLayoutSection,
  OpRule,
  OpSlaPolicy,
  OpWorkItemSchedule,
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
import { validateOcFormModel } from '@/utils/ocFormValidation';

export const OC_DATASETS = {
  workspaces: 'op_workspaces',
  boards: 'op_boards',
  forms: 'op_forms',
  states: 'op_states',
  priorities: 'op_priorities',
  workItemTypes: 'op_work_item_types',
  fields: 'op_fields',
  stateFlows: 'op_state_flows',
  rules: 'op_rules',
  slaPolicies: 'op_sla_policies',
  workItemSchedules: 'op_work_item_schedules',
  profiles: 'op_profiles',
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
    enabledTypeIds: readEnabledTypeIdsFromWorkspaceRaw(raw),
    enabledStateIds: readEnabledStateIdsFromWorkspaceRaw(raw),
    enabledPriorityIds: readEnabledPriorityIdsFromWorkspaceRaw(raw),
    enabledFieldIds: resolveRelationIds(raw.enabledFieldIds ?? raw.EnabledFieldIds),
    defaultStateFlowId: resolveRelationId(raw.defaultStateFlowId ?? raw.DefaultStateFlowId),
    settings: parseJsonRecord(raw.settings ?? raw.Settings),
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
    const defaultTransitionKey =
      o.defaultTransitionKey != null
        ? String(o.defaultTransitionKey)
        : o.DefaultTransitionKey != null
          ? String(o.DefaultTransitionKey)
          : null;
    result.push({
      stateId,
      title: o.title != null ? String(o.title) : o.Title != null ? String(o.Title) : null,
      queryKey: o.queryKey != null ? String(o.queryKey) : o.QueryKey != null ? String(o.QueryKey) : 'wi_board_column',
      defaultTransitionKey: defaultTransitionKey?.trim() || null,
    });
  }
  return result;
}

function parseBoardListColumns(configRaw: unknown): OpBoardListColumnConfig[] {
  const config = parseJsonRecord(configRaw);
  const cols = config.listColumns ?? config.ListColumns;
  if (!Array.isArray(cols)) return [];

  const out: OpBoardListColumnConfig[] = [];
  const seen = new Set<string>();
  for (const item of cols) {
    if (!item || typeof item !== 'object') continue;
    const o = item as Record<string, unknown>;
    const key = o.key != null ? String(o.key) : o.Key != null ? String(o.Key) : '';
    if (!key.trim() || seen.has(key)) continue;
    seen.add(key);
    const fmt = o.format ?? o.Format;
    out.push({
      key,
      sortable: Boolean(o.sortable ?? o.Sortable ?? false),
      filterable: Boolean(o.filterable ?? o.Filterable ?? false),
      format: fmt != null && String(fmt).trim() ? (String(fmt) as OcColumnFormat) : null,
    });
  }
  return out;
}

function parseBoardDefaultSort(configRaw: unknown): OpBoardSortConfig | null {
  const config = parseJsonRecord(configRaw);
  const s = config.defaultSort ?? config.DefaultSort;
  if (!s || typeof s !== 'object') return null;
  const o = s as Record<string, unknown>;
  const field = o.field != null ? String(o.field) : o.Field != null ? String(o.Field) : '';
  if (!field.trim()) return null;
  const dir = String(o.direction ?? o.Direction ?? 'asc').toLowerCase();
  return { field, direction: dir === 'desc' ? 'desc' : 'asc' };
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
    if (Array.isArray(value) && value.length === 0) continue;
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
  return validateOcFormModel(ctx, model);
}

export { collectOcFormValidationIssues } from '@/utils/ocFormValidation';

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

function mapWorkItemSummary(raw: Record<string, unknown>): OcWorkItemSummary {
  return {
    id: pickStr(raw, 'id', 'Id') ?? '',
    key: pickStr(raw, 'key', 'Key') ?? '',
    title: pickStr(raw, 'title', 'Title') ?? '',
    description: pickStr(raw, 'description', 'Description') ?? null,
    stateId: pickStr(raw, 'stateId', 'StateId') ?? '',
    stateFlowId: pickStr(raw, 'stateFlowId', 'StateFlowId') ?? null,
    category: pickStr(raw, 'category', 'Category') ?? null,
    workspaceKey: pickStr(raw, 'workspaceKey', 'WorkspaceKey') ?? null,
    assignee: pickStr(raw, 'assignee', 'Assignee') ?? null,
    reporter: pickStr(raw, 'reporter', 'Reporter') ?? null,
    typeId: pickStr(raw, 'typeId', 'TypeId') ?? null,
    boardId: pickStr(raw, 'boardId', 'BoardId') ?? null,
    priorityId: pickStr(raw, 'priorityId', 'PriorityId') ?? null,
    createdAt: pickStr(raw, 'createdAt', 'CreatedAt') ?? null,
    lastStateChangeAt: pickStr(raw, 'lastStateChangeAt', 'LastStateChangeAt') ?? null,
    closedAt: pickStr(raw, 'closedAt', 'ClosedAt') ?? null,
  };
}

function mapWorkItemLink(raw: Record<string, unknown>): OcWorkItemLinkSummary {
  return {
    id: pickStr(raw, 'id', 'Id') ?? '',
    linkType: pickStr(raw, 'linkType', 'LinkType') ?? '',
    direction: pickStr(raw, 'direction', 'Direction') ?? '',
    otherWorkItemId: pickStr(raw, 'otherWorkItemId', 'OtherWorkItemId') ?? '',
    description: pickStr(raw, 'description', 'Description') ?? null,
  };
}

function mapWorkItemProfile(raw: Record<string, unknown>): OcWorkItemProfile {
  const wiRaw = (raw.workItem ?? raw.WorkItem ?? {}) as Record<string, unknown>;
  const perms = (raw.permissions ?? raw.Permissions ?? {}) as Record<string, unknown>;
  const watchers = raw.watchers ?? raw.Watchers;
  const links = raw.links ?? raw.Links;
  const summary = mapWorkItemSummary(wiRaw);
  return {
    workspaceId: pickStr(raw, 'workspaceId', 'WorkspaceId') ?? '',
    workItem: summary,
    permissions: {
      canView: Boolean(perms.canView ?? perms.CanView ?? true),
      canEdit: Boolean(perms.canEdit ?? perms.CanEdit ?? false),
      canComment: Boolean(perms.canComment ?? perms.CanComment ?? false),
    },
    sla: mapSlaSnapshot(raw.sla ?? raw.Sla),
    watchers: Array.isArray(watchers) ? watchers.map(String) : [],
    links: Array.isArray(links) ? links.map((l) => mapWorkItemLink(l as Record<string, unknown>)) : [],
    people: parsePeopleMap(raw.people ?? raw.People),
    createdBy: pickStr(wiRaw, 'createdBy', 'CreatedBy') ?? null,
    attachments: parseAttachments(raw.attachments ?? raw.Attachments),
  };
}

/** DG saklı file nesnesini (path/file_name/...) OcAttachment'a çevirir; ham nesneyi `raw`'da saklar. */
function mapAttachment(raw: Record<string, unknown>): OcAttachment {
  const path = pickStr(raw, 'path', 'Path') ?? '';
  const sizeRaw = raw.file_size ?? raw.fileSize ?? raw.FileSize;
  const sizeNum = typeof sizeRaw === 'number' ? sizeRaw : Number(sizeRaw);
  return {
    path,
    fileName: pickStr(raw, 'file_name', 'fileName', 'FileName') ?? path.split('/').pop() ?? path,
    fileExt: pickStr(raw, 'file_ext', 'fileExt', 'FileExt') ?? null,
    fileSizeKb: Number.isFinite(sizeNum) ? sizeNum : null,
    uploadPerson: pickStr(raw, 'upload_person', 'uploadPerson', 'UploadPerson') ?? null,
    uploadTime: pickStr(raw, 'upload_time', 'uploadTime', 'UploadTime') ?? null,
    raw,
  };
}

function parseAttachments(raw: unknown): OcAttachment[] {
  if (!Array.isArray(raw)) return [];
  return raw
    .filter((a): a is Record<string, unknown> => !!a && typeof a === 'object')
    .map((a) => mapAttachment(a))
    .filter((a) => !!a.path);
}

function mapTimelineEntry(raw: Record<string, unknown>): OcTimelineEntry {
  return {
    type: pickStr(raw, 'type', 'Type') ?? '',
    id: pickStr(raw, 'id', 'Id') ?? null,
    actor: pickStr(raw, 'actor', 'Actor') ?? null,
    text: pickStr(raw, 'text', 'Text') ?? null,
    at: pickStr(raw, 'at', 'At') ?? null,
    activityType: pickStr(raw, 'activityType', 'ActivityType') ?? null,
  };
}

function mapComment(raw: Record<string, unknown>): OcComment {
  return {
    id: pickStr(raw, 'id', 'Id') ?? '',
    workItemId: pickStr(raw, 'workItemId', 'WorkItemId') ?? '',
    body: pickStr(raw, 'body', 'Body') ?? '',
    author: pickStr(raw, 'author', 'Author') ?? null,
    parentCommentId: pickStr(raw, 'parentCommentId', 'ParentCommentId') ?? null,
    commentDate: pickStr(raw, 'commentDate', 'CommentDate') ?? null,
  };
}

/** İş kaydı profil context'i (sidebar: SLA/meta/people/links + izinler). */
export async function ocGetWorkItemProfile(workItemId: string): Promise<OcWorkItemProfile> {
  const raw = (await fetchFromOperations(
    `/api/v1/runtime/work-items/${encodeURIComponent(workItemId)}/profile`,
    'GET'
  )) as Record<string, unknown>;
  return mapWorkItemProfile(raw);
}

/** İş kaydı aktivite/yorum zaman tüneli (sayfalı). */
export async function ocGetWorkItemTimeline(
  workItemId: string,
  skip = 0,
  take = 50
): Promise<OcTimelinePage> {
  const qs = new URLSearchParams({ skip: String(skip), take: String(take) });
  const raw = (await fetchFromOperations(
    `/api/v1/runtime/work-items/${encodeURIComponent(workItemId)}/timeline?${qs.toString()}`,
    'GET'
  )) as Record<string, unknown>;
  const items = raw.items ?? raw.Items;
  return {
    items: Array.isArray(items) ? items.map((i) => mapTimelineEntry(i as Record<string, unknown>)) : [],
    skip: Number(raw.skip ?? raw.Skip ?? skip),
    take: Number(raw.take ?? raw.Take ?? take),
    total: Number(raw.total ?? raw.Total ?? 0),
  };
}

/** İş kaydına yorum ekler. `mentions` = etiketlenen kişi id'leri (in-app bildirim tetikler). */
export async function ocAddWorkItemComment(
  workItemId: string,
  body: string,
  parentCommentId?: string | null,
  mentions?: string[]
): Promise<OcComment> {
  const payload: Record<string, unknown> = { body };
  if (parentCommentId) payload.parentCommentId = parentCommentId;
  if (mentions && mentions.length) payload.mentions = [...new Set(mentions)];
  const raw = (await fetchFromOperations(
    `/api/v1/work-items/${encodeURIComponent(workItemId)}/comments`,
    'POST',
    payload
  )) as Record<string, unknown>;
  return mapComment(raw);
}

/**
 * İş kaydına ek ekler. DG file alanı inline işlenir: mevcut ekler ham (path'li) nesneleriyle
 * geri gönderilir (DG korur), yeni dosya base64 `content` ile gönderilir (DG MinIO'ya yükler).
 * `file` bir tarayıcı File nesnesidir.
 */
export async function ocAddWorkItemAttachment(
  workItemId: string,
  existing: OcAttachment[],
  file: File
): Promise<OcWorkItemProfile> {
  const content = await fileToBase64(file);
  const attachments: unknown[] = [
    ...existing.map((a) => a.raw),
    { content, originalFileName: file.name },
  ];
  await ocUpdateWorkItem(workItemId, { fields: { attachments } });
  return ocGetWorkItemProfile(workItemId);
}

/** İş kaydından bir eki kaldırır (kalan ekler ham haliyle PATCH edilir). */
export async function ocRemoveWorkItemAttachment(
  workItemId: string,
  attachments: OcAttachment[],
  removePath: string
): Promise<OcWorkItemProfile> {
  const remaining = attachments.filter((a) => a.path !== removePath).map((a) => a.raw);
  await ocUpdateWorkItem(workItemId, { fields: { attachments: remaining } });
  return ocGetWorkItemProfile(workItemId);
}

/** Eki DG'den indirir ve tarayıcıda kaydetme akışını tetikler. */
export async function ocDownloadAttachment(att: OcAttachment): Promise<void> {
  const url = `/api/v1/files/download?filePath=${encodeURIComponent(att.path)}`;
  const blob = await fetchBlobFromDataGateway(url);
  const objectUrl = URL.createObjectURL(blob);
  try {
    const a = document.createElement('a');
    a.href = objectUrl;
    a.download = att.fileName || att.path.split('/').pop() || 'download';
    document.body.appendChild(a);
    a.click();
    a.remove();
  } finally {
    URL.revokeObjectURL(objectUrl);
  }
}

/** Tarayıcı File -> base64 (data URI prefix'i olmadan; DG sadece base64 içerik bekler). */
function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result ?? '');
      const comma = result.indexOf(',');
      resolve(comma >= 0 ? result.slice(comma + 1) : result);
    };
    reader.onerror = () => reject(reader.error ?? new Error('Dosya okunamadı.'));
    reader.readAsDataURL(file);
  });
}

/** Edit modu form runtime context (mevcut değerlerle). */
export async function ocGetFormEditContext(workItemId: string): Promise<OcFormRuntimeContext> {
  const raw = (await fetchFromOperations(
    `/api/v1/runtime/work-items/${encodeURIComponent(workItemId)}/form?mode=edit`,
    'GET'
  )) as Record<string, unknown>;
  return mapFormRuntimeContext(raw);
}

export interface OcUpdateWorkItemRequest {
  title?: string;
  description?: string | null;
  assignee?: string | null;
  priorityId?: string | null;
  boardId?: string | null;
  fields?: Record<string, unknown>;
}

/** PATCH'te değiştirilemeyen / ayrı endpoint'e ait üst-seviye anahtarlar. */
const OC_PATCH_TOP_LEVEL_KEYS = new Set([
  'title',
  'description',
  'assignee',
  'priorityId',
  'boardId',
  'typeId',
  'stateId',
  'key',
  'workspaceId',
]);

function normalizeNullableScalar(value: unknown): string | null {
  if (value == null) return null;
  const s = String(value).trim();
  return s ? s : null;
}

/**
 * Sadece değişen alanlardan PATCH gövdesi kurar (readonly/değişmemiş alanları göndermez).
 * `changed` yalnızca düzenlenen anahtarları içermeli (dialog initial↔current diff'i).
 */
export function buildUpdateWorkItemRequest(changed: Record<string, unknown>): OcUpdateWorkItemRequest {
  const body: OcUpdateWorkItemRequest = {};

  if ('title' in changed) {
    const t = String(changed.title ?? '').trim();
    if (t) body.title = t;
  }
  if ('description' in changed) {
    body.description = normalizeNullableScalar(changed.description);
  }
  if ('assignee' in changed) {
    body.assignee = normalizeNullableScalar(changed.assignee);
  }
  if ('priorityId' in changed) {
    body.priorityId = normalizeNullableScalar(changed.priorityId);
  }
  if ('boardId' in changed) {
    body.boardId = normalizeNullableScalar(changed.boardId);
  }

  const fields: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(changed)) {
    if (OC_PATCH_TOP_LEVEL_KEYS.has(key)) continue;
    fields[key] = value === '' ? null : value;
  }
  if (Object.keys(fields).length) body.fields = fields;

  return body;
}

export function hasUpdateWorkItemChanges(patch: OcUpdateWorkItemRequest): boolean {
  return (
    patch.title !== undefined ||
    patch.description !== undefined ||
    patch.assignee !== undefined ||
    patch.priorityId !== undefined ||
    patch.boardId !== undefined ||
    (patch.fields != null && Object.keys(patch.fields).length > 0)
  );
}

export async function ocUpdateWorkItem(
  workItemId: string,
  patch: OcUpdateWorkItemRequest
): Promise<OcCreateWorkItemResult> {
  const body: Record<string, unknown> = {};
  if (patch.title !== undefined) body.title = patch.title;
  if (patch.description !== undefined) body.description = patch.description;
  if (patch.assignee !== undefined) body.assignee = patch.assignee;
  if (patch.priorityId !== undefined) body.priorityId = patch.priorityId;
  if (patch.boardId !== undefined) body.boardId = patch.boardId;
  if (patch.fields && Object.keys(patch.fields).length) body.fields = patch.fields;

  const raw = await fetchFromOperations(
    `/api/v1/work-items/${encodeURIComponent(workItemId)}`,
    'PATCH',
    body
  );
  return mapCreateWorkItemResult(raw);
}

export async function ocDeleteWorkItem(workItemId: string): Promise<void> {
  await fetchFromOperations(`/api/v1/work-items/${encodeURIComponent(workItemId)}`, 'DELETE');
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
    defaultProfileId: resolveRelationId(raw.defaultProfileId ?? raw.DefaultProfileId),
    defaultTypeId: resolveRelationId(raw.defaultTypeId ?? raw.DefaultTypeId),
    defaultPriorityId: resolveRelationId(raw.defaultPriorityId ?? raw.DefaultPriorityId),
    defaultStateId: resolveRelationId(raw.defaultStateId ?? raw.DefaultStateId),
    visibleFields: parseStringArray(raw.visibleFields ?? raw.VisibleFields),
    viewGroups: parseStringArray(raw.viewGroups ?? raw.ViewGroups),
    editGroups: parseStringArray(raw.editGroups ?? raw.EditGroups),
    columns: parseBoardColumns(raw.config ?? raw.Config),
    listColumns: parseBoardListColumns(raw.config ?? raw.Config),
    defaultSort: parseBoardDefaultSort(raw.config ?? raw.Config),
  };
}

export function mapOpProfile(raw: Record<string, unknown>): OpProfile {
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? ''),
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId) ?? '',
  };
}

export async function ocListProfilesForWorkspace(workspaceId: string): Promise<OpProfile[]> {
  const rows = await ocListDataset(OC_DATASETS.profiles, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'name:asc',
    limit: 100,
  });
  return rows
    .map((r) => mapOpProfile(r as Record<string, unknown>))
    .filter((p) => p.__dataId && p.name && p.workspaceId === workspaceId);
}

/** Tek kayıt başlığı (politika özeti — listede olmayan id'ler için). */
export async function ocGetDatasetRecordTitle(
  dataset: string,
  dataId: string,
  titleField = 'name'
): Promise<string | null> {
  const id = dataId?.trim();
  if (!id) return null;
  const rows = await ocListDataset(dataset, {
    filter: `__dataId:eq:${id}`,
    limit: 1,
  });
  const raw = rows[0] as Record<string, unknown> | undefined;
  if (!raw) return null;
  const title = raw[titleField] ?? raw.name ?? raw.title;
  if (title == null || title === '') return null;
  return String(title).trim() || null;
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

/**
 * Global katalog kaynakları — CRUD MO üzerinden gider (write-through):
 * MO DG'ye yazar ve aynı işlemde kendi cache'ini düşürür. Silmede kullanım guard'ı (409) uygular.
 */
export type OcCatalogSource = 'states' | 'priorities' | 'types' | 'fields';

async function ocCatalogCreate(source: OcCatalogSource, payload: Record<string, unknown>) {
  return fetchFromOperations(`/api/v1/catalogs/${source}`, 'POST', payload);
}

async function ocCatalogUpdate(source: OcCatalogSource, id: string, payload: Record<string, unknown>) {
  return fetchFromOperations(`/api/v1/catalogs/${source}/${encodeURIComponent(id)}`, 'PUT', payload);
}

async function ocCatalogDelete(source: OcCatalogSource, id: string) {
  return fetchFromOperations(`/api/v1/catalogs/${source}/${encodeURIComponent(id)}`, 'DELETE');
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

/** DG alanı yokken settings yedek (UI seçimi). */
const OC_SETTINGS_ENABLED_STATE_IDS_KEY = 'enabledStateIds';
const OC_SETTINGS_ENABLED_TYPE_IDS_KEY = 'enabledTypeIds';
const OC_SETTINGS_ENABLED_PRIORITY_IDS_KEY = 'enabledPriorityIds';

function readEnabledStateIdsFromWorkspaceRaw(raw: Record<string, unknown>): string[] {
  const fromField = resolveRelationIds(raw.enabledStateIds ?? raw.EnabledStateIds);
  if (fromField.length) return fromField;
  const settings = parseJsonRecord(raw.settings ?? raw.Settings);
  return resolveRelationIds(settings[OC_SETTINGS_ENABLED_STATE_IDS_KEY]);
}

function readEnabledTypeIdsFromWorkspaceRaw(raw: Record<string, unknown>): string[] {
  const fromField = resolveRelationIds(raw.enabledTypeIds ?? raw.EnabledTypeIds);
  if (fromField.length) return fromField;
  const settings = parseJsonRecord(raw.settings ?? raw.Settings);
  return resolveRelationIds(settings[OC_SETTINGS_ENABLED_TYPE_IDS_KEY]);
}

function readEnabledPriorityIdsFromWorkspaceRaw(raw: Record<string, unknown>): string[] {
  const fromField = resolveRelationIds(raw.enabledPriorityIds ?? raw.EnabledPriorityIds);
  if (fromField.length) return fromField;
  const settings = parseJsonRecord(raw.settings ?? raw.Settings);
  return resolveRelationIds(settings[OC_SETTINGS_ENABLED_PRIORITY_IDS_KEY]);
}

export type OcListWorkspaceCatalogOptions = {
  /** Seçim boşsa tüm global katalog */
  fallbackAll?: boolean;
};

async function collectStateIdsFromWorkspaceFlows(workspaceId: string): Promise<string[]> {
  const flows = await ocListStateFlowsForWorkspace(workspaceId);
  const ids = new Set<string>();
  for (const flow of flows) {
    if (flow.initialStateId) ids.add(flow.initialStateId);
    for (const tr of flow.transitions ?? []) {
      if (tr.fromStateId) ids.add(tr.fromStateId);
      if (tr.toStateId) ids.add(tr.toStateId);
    }
  }
  return [...ids];
}

export type OcListStatesForWorkspaceOptions = OcListWorkspaceCatalogOptions & {
  /** enabledStateIds boşsa workspace akışlarındaki state’leri ekle */
  includeFlowStates?: boolean;
};

/** Workspace’te kullanılabilir durumlar (seçim → akış → isteğe bağlı tüm katalog). */
export async function ocListStatesForWorkspace(
  workspaceId: string,
  options?: OcListStatesForWorkspaceOptions
): Promise<OpState[]> {
  const fallbackAll = options?.fallbackAll ?? false;
  const includeFlowStates = options?.includeFlowStates ?? true;
  const all = await ocListStates();
  if (!workspaceId?.trim()) {
    return fallbackAll ? all : [];
  }

  const ws = await ocGetWorkspace(workspaceId);
  const allowed = new Set(ws?.enabledStateIds ?? []);

  if (!allowed.size && includeFlowStates) {
    for (const id of await collectStateIdsFromWorkspaceFlows(workspaceId)) {
      allowed.add(id);
    }
  }

  if (!allowed.size) {
    return fallbackAll ? all : [];
  }

  return all.filter((s) => allowed.has(s.__dataId));
}

/** Durum seçimini kaydet (alan + settings yedek). */
export async function ocSaveWorkspaceEnabledStateIds(workspaceId: string, stateIds: string[]) {
  await ocSaveWorkspaceEnabledRelationIds(workspaceId, OC_SETTINGS_ENABLED_STATE_IDS_KEY, 'enabledStateIds', stateIds);
}

/** Tip seçimini kaydet (alan + settings yedek). */
export async function ocSaveWorkspaceEnabledTypeIds(workspaceId: string, typeIds: string[]) {
  await ocSaveWorkspaceEnabledRelationIds(workspaceId, OC_SETTINGS_ENABLED_TYPE_IDS_KEY, 'enabledTypeIds', typeIds);
}

/** Öncelik seçimini kaydet (alan + settings yedek). */
export async function ocSaveWorkspaceEnabledPriorityIds(workspaceId: string, priorityIds: string[]) {
  await ocSaveWorkspaceEnabledRelationIds(
    workspaceId,
    OC_SETTINGS_ENABLED_PRIORITY_IDS_KEY,
    'enabledPriorityIds',
    priorityIds
  );
}

async function ocSaveWorkspaceEnabledRelationIds(
  workspaceId: string,
  settingsKey: string,
  payloadKey: string,
  ids: string[]
) {
  const ws = await ocGetWorkspace(workspaceId);
  const settings =
    ws?.settings && typeof ws.settings === 'object' && !Array.isArray(ws.settings)
      ? { ...(ws.settings as Record<string, unknown>) }
      : {};
  settings[settingsKey] = ids;
  await ocUpdateWorkspace(workspaceId, {
    [payloadKey]: ids,
    settings,
  });
}

export async function ocCreateState(payload: Record<string, unknown>) {
  await ocCatalogCreate('states', payload);
}

export async function ocUpdateState(stateId: string, payload: Record<string, unknown>) {
  await ocCatalogUpdate('states', stateId, payload);
}

export async function ocDeleteState(stateId: string) {
  await ocCatalogDelete('states', stateId);
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

/** Workspace’te kullanılabilir öncelikler (enabledPriorityIds). */
export async function ocListPrioritiesForWorkspace(
  workspaceId: string,
  options?: OcListWorkspaceCatalogOptions
): Promise<OpPriority[]> {
  const fallbackAll = options?.fallbackAll ?? false;
  const all = await ocListPriorities();
  if (!workspaceId?.trim()) {
    return fallbackAll ? all : [];
  }

  const ws = await ocGetWorkspace(workspaceId);
  const allowed = new Set(ws?.enabledPriorityIds ?? []);

  if (!allowed.size) {
    return fallbackAll ? all : [];
  }

  return all.filter((p) => allowed.has(p.__dataId));
}

export async function ocCreatePriority(payload: Record<string, unknown>) {
  await ocCatalogCreate('priorities', payload);
}

export async function ocUpdatePriority(priorityId: string, payload: Record<string, unknown>) {
  await ocCatalogUpdate('priorities', priorityId, payload);
}

export async function ocDeletePriority(priorityId: string) {
  await ocCatalogDelete('priorities', priorityId);
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
  await ocCatalogCreate('types', payload);
}

export async function ocUpdateWorkItemType(typeId: string, payload: Record<string, unknown>) {
  await ocCatalogUpdate('types', typeId, payload);
}

export async function ocDeleteWorkItemType(typeId: string) {
  await ocCatalogDelete('types', typeId);
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
  await ocCatalogCreate('fields', payload);
}

export async function ocUpdateField(fieldId: string, payload: Record<string, unknown>) {
  await ocCatalogUpdate('fields', fieldId, payload);
}

export async function ocDeleteField(fieldId: string) {
  await ocCatalogDelete('fields', fieldId);
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

  return {
    transitionKey,
    fromStateId,
    toStateId,
    label: o.label != null ? String(o.label) : o.Label != null ? String(o.Label) : null,
    order,
    requiredFields: requiredFields?.length ? requiredFields : undefined,
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

/** Workspace’te kullanılabilir tipler (enabledTypeIds + workspace’e özel tipler). */
export async function ocListWorkItemTypesForWorkspace(
  workspaceId: string,
  options?: OcListWorkspaceCatalogOptions
): Promise<OpWorkItemType[]> {
  const fallbackAll = options?.fallbackAll ?? false;
  const wsId = workspaceId?.trim() ?? '';

  const [globalRows, scopedRows] = await Promise.all([
    ocListGlobalWorkItemTypes(),
    wsId
      ? ocListDataset(OC_DATASETS.workItemTypes, {
          filter: `workspaceId:eq:${wsId}`,
          sort: 'category:asc,sortOrder:asc,name:asc',
          limit: 200,
        })
      : Promise.resolve([]),
  ]);
  const scoped = scopedRows
    .map((r) => mapOpWorkItemType(r as Record<string, unknown>))
    .filter((t) => t.__dataId && t.name && t.workspaceId === wsId);
  const seen = new Set<string>();
  const merged = [...globalRows, ...scoped].filter((t) => {
    if (seen.has(t.__dataId)) return false;
    seen.add(t.__dataId);
    return true;
  });

  if (!wsId) {
    return fallbackAll ? merged : [];
  }

  const ws = await ocGetWorkspace(wsId);
  const allowed = new Set(ws?.enabledTypeIds ?? []);

  for (const t of merged) {
    if (t.workspaceId === wsId) allowed.add(t.__dataId);
  }

  if (!allowed.size) {
    return fallbackAll ? merged : [];
  }

  return merged.filter((t) => allowed.has(t.__dataId));
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

function mapOpRule(raw: Record<string, unknown>): OpRule {
  const conditions = raw.conditions ?? raw.Conditions;
  const actions = raw.actions ?? raw.Actions;
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? ''),
    description:
      raw.description != null
        ? String(raw.description)
        : raw.Description != null
          ? String(raw.Description)
          : null,
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId) ?? '',
    ruleType: String(raw.ruleType ?? raw.RuleType ?? '').toLowerCase(),
    trigger: String(raw.trigger ?? raw.Trigger ?? ''),
    transitionKey:
      raw.transitionKey != null
        ? String(raw.transitionKey)
        : raw.TransitionKey != null
          ? String(raw.TransitionKey)
          : null,
    typeId: resolveRelationId(raw.typeId ?? raw.TypeId) || null,
    boardId: resolveRelationId(raw.boardId ?? raw.BoardId) || null,
    stateId: resolveRelationId(raw.stateId ?? raw.StateId) || null,
    fromStateId: resolveRelationId(raw.fromStateId ?? raw.FromStateId) || null,
    toStateId: resolveRelationId(raw.toStateId ?? raw.ToStateId) || null,
    isActive: raw.isActive !== false && raw.IsActive !== false,
    priority:
      raw.priority != null && raw.priority !== ''
        ? Number(raw.priority)
        : raw.Priority != null && raw.Priority !== ''
          ? Number(raw.Priority)
          : null,
    conditions,
    actions: Array.isArray(actions) ? actions : [],
    errorMessage:
      raw.errorMessage != null
        ? String(raw.errorMessage)
        : raw.ErrorMessage != null
          ? String(raw.ErrorMessage)
          : null,
    applyMode:
      raw.applyMode != null
        ? String(raw.applyMode)
        : raw.ApplyMode != null
          ? String(raw.ApplyMode)
          : null,
  };
}

export async function ocListRulesForWorkspace(workspaceId: string): Promise<OpRule[]> {
  const rows = await ocListDataset(OC_DATASETS.rules, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'priority:asc,name:asc',
    limit: 200,
  });
  return rows
    .map((r) => mapOpRule(r as Record<string, unknown>))
    .filter((rule) => rule.__dataId && rule.name && rule.workspaceId === workspaceId);
}

export async function ocCreateRule(payload: Record<string, unknown>): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.rules, payload);
}

export async function ocUpdateRule(ruleId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.rules, ruleId, payload);
}

export async function ocDeleteRule(ruleId: string) {
  await ocDelete(OC_DATASETS.rules, ruleId);
}

export function mapOpSlaPolicy(raw: Record<string, unknown>): OpSlaPolicy {
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? ''),
    description:
      raw.description != null
        ? String(raw.description)
        : raw.Description != null
          ? String(raw.Description)
          : null,
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId) ?? '',
    typeId: resolveRelationId(raw.typeId ?? raw.TypeId) || null,
    priorityId: resolveRelationId(raw.priorityId ?? raw.PriorityId) || null,
    responseTargetMinutes:
      raw.responseTargetMinutes != null
        ? Number(raw.responseTargetMinutes)
        : raw.ResponseTargetMinutes != null
          ? Number(raw.ResponseTargetMinutes)
          : null,
    resolveTargetMinutes:
      raw.resolveTargetMinutes != null
        ? Number(raw.resolveTargetMinutes)
        : raw.ResolveTargetMinutes != null
          ? Number(raw.ResolveTargetMinutes)
          : null,
    isActive: raw.isActive !== false && raw.IsActive !== false,
    priority:
      raw.priority != null
        ? Number(raw.priority)
        : raw.Priority != null
          ? Number(raw.Priority)
          : 100,
  };
}

export async function ocListSlaPoliciesForWorkspace(workspaceId: string): Promise<OpSlaPolicy[]> {
  const rows = await ocListDataset(OC_DATASETS.slaPolicies, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'priority:desc,name:asc',
    limit: 200,
  });
  return rows
    .map((r) => mapOpSlaPolicy(r as Record<string, unknown>))
    .filter((p) => p.__dataId && p.name && p.workspaceId === workspaceId);
}

export async function ocCreateSlaPolicy(payload: Record<string, unknown>): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.slaPolicies, payload);
}

export async function ocUpdateSlaPolicy(policyId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.slaPolicies, policyId, payload);
}

export async function ocDeleteSlaPolicy(policyId: string) {
  await ocDelete(OC_DATASETS.slaPolicies, policyId);
}

export function mapOpWorkItemSchedule(raw: Record<string, unknown>): OpWorkItemSchedule {
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId) ?? '',
    name: String(raw.name ?? ''),
    description:
      raw.description != null
        ? String(raw.description)
        : raw.Description != null
          ? String(raw.Description)
          : null,
    isActive: raw.isActive !== false && raw.IsActive !== false,
    cronExpression: String(raw.cronExpression ?? raw.CronExpression ?? ''),
    timezone: String(raw.timezone ?? raw.Timezone ?? 'Europe/Istanbul'),
    boardId: resolveRelationId(raw.boardId ?? raw.BoardId) ?? '',
    typeId: resolveRelationId(raw.typeId ?? raw.TypeId) ?? '',
    assignee: String(raw.assignee ?? raw.Assignee ?? ''),
    priorityId: resolveRelationId(raw.priorityId ?? raw.PriorityId) || null,
    title: String(raw.title ?? raw.Title ?? ''),
    templateDescription:
      raw.templateDescription != null
        ? String(raw.templateDescription)
        : raw.TemplateDescription != null
          ? String(raw.TemplateDescription)
          : null,
    fields:
      raw.fields && typeof raw.fields === 'object' && !Array.isArray(raw.fields)
        ? (raw.fields as Record<string, unknown>)
        : raw.Fields && typeof raw.Fields === 'object' && !Array.isArray(raw.Fields)
          ? (raw.Fields as Record<string, unknown>)
          : null,
    initialTransitionKey:
      raw.initialTransitionKey != null
        ? String(raw.initialTransitionKey)
        : raw.InitialTransitionKey != null
          ? String(raw.InitialTransitionKey)
          : null,
    schedulerJobId:
      raw.schedulerJobId != null
        ? String(raw.schedulerJobId)
        : raw.SchedulerJobId != null
          ? String(raw.SchedulerJobId)
          : null,
    lastRunAt:
      raw.lastRunAt != null
        ? String(raw.lastRunAt)
        : raw.LastRunAt != null
          ? String(raw.LastRunAt)
          : null,
    lastWorkItemId: resolveRelationId(raw.lastWorkItemId ?? raw.LastWorkItemId) || null,
  };
}

export async function ocListSchedulesForWorkspace(
  workspaceId: string
): Promise<OpWorkItemSchedule[]> {
  const rows = await ocListDataset(OC_DATASETS.workItemSchedules, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'name:asc',
    limit: 200,
  });
  return rows
    .map((r) => mapOpWorkItemSchedule(r as Record<string, unknown>))
    .filter((s) => s.__dataId && s.name && s.workspaceId === workspaceId);
}

/** Admin job explorer — tüm workspace schedule kayıtları (lastRunAt birleştirmesi). */
export async function ocListAllWorkItemSchedules(limit = 500): Promise<OpWorkItemSchedule[]> {
  const rows = await ocListDataset(OC_DATASETS.workItemSchedules, {
    sort: 'lastRunAt:desc',
    limit,
  });
  return rows
    .map((r) => mapOpWorkItemSchedule(r as Record<string, unknown>))
    .filter((s) => s.__dataId && s.name);
}

export async function ocCreateWorkItemSchedule(
  payload: Record<string, unknown>
): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.workItemSchedules, payload);
}

export async function ocUpdateWorkItemSchedule(
  scheduleId: string,
  payload: Record<string, unknown>
) {
  await ocUpdate(OC_DATASETS.workItemSchedules, scheduleId, payload);
}

export async function ocDeleteWorkItemSchedule(scheduleId: string) {
  await ocDelete(OC_DATASETS.workItemSchedules, scheduleId);
}

/** SW-3b: DG kaydı sonrası MngScheduler User Job senkronu. */
export async function ocSyncWorkItemScheduleScheduler(scheduleId: string) {
  return fetchFromOperations(
    `/api/v1/work-item-schedules/${encodeURIComponent(scheduleId)}/sync-scheduler`,
    'POST'
  );
}

/** SW-3b: DG silmeden önce Scheduler job kaldırma. */
export async function ocUnlinkWorkItemScheduleScheduler(scheduleId: string) {
  return fetchFromOperations(
    `/api/v1/work-item-schedules/${encodeURIComponent(scheduleId)}/unlink-scheduler`,
    'POST'
  );
}

/** SW-2: MO execute endpoint — henüz yoksa hata fırlatır. */
export async function ocRunWorkItemScheduleNow(scheduleId: string) {
  return fetchFromOperations(
    `/api/v1/work-item-schedules/${encodeURIComponent(scheduleId)}/execute`,
    'POST'
  );
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
  const fieldsRaw = raw.fields ?? raw.Fields;
  const fields =
    fieldsRaw && typeof fieldsRaw === 'object' && !Array.isArray(fieldsRaw)
      ? (fieldsRaw as Record<string, unknown>)
      : undefined;
  return {
    id: pickStr(raw, 'id', 'Id') ?? '',
    key: pickStr(raw, 'key', 'Key') ?? '',
    title: pickStr(raw, 'title', 'Title') ?? '',
    stateId: pickStr(raw, 'stateId', 'StateId'),
    assignee: pickStr(raw, 'assignee', 'Assignee'),
    priorityId: pickStr(raw, 'priorityId', 'PriorityId'),
    typeId: pickStr(raw, 'typeId', 'TypeId'),
    createdAt: pickStr(raw, 'createdAt', 'CreatedAt') ?? null,
    createdBy: pickStr(raw, 'createdBy', 'CreatedBy') ?? null,
    updatedAt: pickStr(raw, 'updatedAt', 'UpdatedAt') ?? null,
    lastStateChangeAt: pickStr(raw, 'lastStateChangeAt', 'LastStateChangeAt') ?? null,
    closedAt: pickStr(raw, 'closedAt', 'ClosedAt') ?? null,
    sla: mapSlaSnapshot(raw.sla ?? raw.Sla),
    fields,
  };
}

function mapSlaSnapshot(raw: unknown): OcSlaSnapshot | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  return {
    slaPolicyId: pickStr(o, 'slaPolicyId', 'SlaPolicyId') ?? null,
    responseDueAt: pickStr(o, 'responseDueAt', 'ResponseDueAt') ?? null,
    resolveDueAt: pickStr(o, 'resolveDueAt', 'ResolveDueAt') ?? null,
    responseBreached: Boolean(o.responseBreached ?? o.ResponseBreached ?? false),
    resolveBreached: Boolean(o.resolveBreached ?? o.ResolveBreached ?? false),
    calculatedAt: pickStr(o, 'calculatedAt', 'CalculatedAt') ?? null,
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

function parseCatalogDisplayMap(raw: unknown): Record<string, OcCatalogDisplayEntry> {
  const out: Record<string, OcCatalogDisplayEntry> = {};
  if (!raw || typeof raw !== 'object') return out;
  for (const [id, val] of Object.entries(raw as Record<string, unknown>)) {
    if (!val || typeof val !== 'object') continue;
    const o = val as Record<string, unknown>;
    out[id] = {
      id: String(o.id ?? o.Id ?? id),
      name: String(o.name ?? o.Name ?? ''),
      color: o.color != null ? String(o.color) : o.Color != null ? String(o.Color) : null,
      icon: o.icon != null ? String(o.icon) : o.Icon != null ? String(o.Icon) : null,
    };
  }
  return out;
}

function parseBoardCatalogs(raw: unknown): OcBoardCatalogs {
  const o = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
  return {
    states: parseCatalogDisplayMap(o.states ?? o.States),
    priorities: parseCatalogDisplayMap(o.priorities ?? o.Priorities),
    types: parseCatalogDisplayMap(o.types ?? o.Types),
  };
}

function mapBoardListColumn(raw: Record<string, unknown>): OcBoardListColumn {
  const fmt = pickStr(raw, 'format', 'Format');
  return {
    key: pickStr(raw, 'key', 'Key') ?? '',
    sortable: Boolean(raw.sortable ?? raw.Sortable ?? false),
    filterable: Boolean(raw.filterable ?? raw.Filterable ?? false),
    format: (fmt as OcColumnFormat | undefined) ?? null,
  };
}

function mapRuntimeDefaultSort(raw: unknown): OpBoardSortConfig | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const field = pickStr(o, 'field', 'Field');
  if (!field?.trim()) return null;
  const dir = String(o.direction ?? o.Direction ?? 'asc').toLowerCase();
  return { field, direction: dir === 'desc' ? 'desc' : 'asc' };
}

function mapBoardRuntimeContext(raw: Record<string, unknown>): OcBoardRuntimeContext {
  const cols = raw.columns ?? raw.Columns;
  const listCols = raw.listColumns ?? raw.ListColumns;
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
    listColumns: Array.isArray(listCols)
      ? listCols.map((c) => mapBoardListColumn(c as Record<string, unknown>)).filter((c) => c.key)
      : [],
    defaultSort: mapRuntimeDefaultSort(raw.defaultSort ?? raw.DefaultSort),
    initialStateId: pickStr(raw, 'initialStateId', 'InitialStateId') ?? null,
    catalogs: parseBoardCatalogs(raw.catalogs ?? raw.Catalogs),
  };
}

function parsePeopleMap(raw: unknown): Record<string, OcPersonDisplay> {
  const out: Record<string, OcPersonDisplay> = {};
  if (!raw || typeof raw !== 'object') return out;
  for (const [id, value] of Object.entries(raw as Record<string, unknown>)) {
    const v = (value ?? {}) as Record<string, unknown>;
    out[id] = {
      id: pickStr(v, 'id', 'Id') ?? id,
      name: pickStr(v, 'name', 'Name') ?? undefined,
      title: pickStr(v, 'title', 'Title') ?? null,
      isActive: (v.isActive ?? v.IsActive ?? null) as boolean | null,
    };
  }
  return out;
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
    people: parsePeopleMap(raw.people ?? raw.People),
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

/** Board liste görünümü — tek sunucu tarafı sorgu (sayfalama + sıralama + filtre + arama). */
export async function ocGetBoardListPage(
  boardId: string,
  request: OcBoardListRequest
): Promise<OcQueryExecuteResponse> {
  const body: Record<string, unknown> = {
    skip: Math.max(0, request.skip ?? 0),
    take: request.take ?? 50,
  };
  if (request.sort?.field) {
    body.sort = { field: request.sort.field, direction: request.sort.direction === 'desc' ? 'desc' : 'asc' };
  }
  const filters = (request.filters ?? []).filter((f) => f.field && f.value != null && String(f.value).trim() !== '');
  if (filters.length) {
    body.filters = filters.map((f) => ({ field: f.field, operator: f.operator || 'eq', value: String(f.value) }));
  }
  if (request.search?.trim()) {
    body.search = request.search.trim();
  }

  const raw = (await fetchFromOperations(
    `/api/v1/runtime/boards/${encodeURIComponent(boardId)}/list`,
    'POST',
    body
  )) as Record<string, unknown>;
  return mapQueryExecuteResponse(raw);
}
