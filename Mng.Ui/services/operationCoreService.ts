import { fetchBlobFromDataGateway, fetchFromDataGateway, fetchFromOperations } from '@/services/apiService';
import type {
  OcAttachment,
  OcBoardCatalogs,
  OcBoardColumn,
  OcBoardColumnTransition,
  OcBoardListColumn,
  OcBoardListRequest,
  OcBoardRuntimeContext,
  OcCatalogDisplayEntry,
  OcColumnFormat,
  OcComment,
  OcDashboard,
  OcDashboardLayout,
  OcDashboardListItem,
  OcDashboardRecord,
  OcDashboardWidgetDef,
  OcDashboardWidget,
  OcDashboardWidgetExecution,
  OcPersonDisplay,
  OcProfileAction,
  OcQueryExecuteResponse,
  OcResolvedPolicy,
  OcSlaSnapshot,
  OcTimelineChange,
  OcTimelineEntry,
  OcTimelinePage,
  OcWorkItemCard,
  OcWorkItemLinkSummary,
  OcWorkItemProfile,
  OcWorkItemProfileView,
  OcWorkItemSummary,
  OpBoard,
  OpBoardColumnConfig,
  OpBoardListColumnConfig,
  OpBoardSortConfig,
  OpProfile,
  OpForm,
  OpFormFieldBehavior,
  OpFormLayoutSection,
  OcFormRuntimeContext,
  OcFormFieldRuntimeDto,
  OcFieldBehaviorDto,
  OpField,
  OpPriority,
  OpState,
  OpWorkspaceDetail,
  OpWorkItemType,
  OpWorkspace,
} from '@/types/apps/operationCore';
import { buildBoardDgPayload } from '@/utils/ocBoardDgPayload';
import { buildOcFormLayoutPayload, normalizeOcGridCol, parseOpFormLayout } from '@/utils/ocFormLayout';
import { validateOcFormModel } from '@/utils/ocFormValidation';
import {
  collectNewFileUploadsFromChangedFields,
  collectWorkItemAttachmentsFromFormModel,
  resolveOcFormFileFieldKeys,
} from '@/utils/ocWorkItemFileFields';

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
  notificationPolicies: 'op_notification_policies',
  workItemSchedules: 'op_work_item_schedules',
  profiles: 'op_profiles',
  tags: 'op_tags',
  dashboards: 'op_dashboards',
} as const;

export function parseSingleDgRecord(response: unknown): Record<string, unknown> | null {
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
  return parseListResponseWithTotal(response).items;
}

function parseListResponseWithTotal(response: unknown): { items: unknown[]; total: number } {
  if (Array.isArray(response)) {
    return { items: response, total: response.length };
  }
  if (response && typeof response === 'object') {
    const obj = response as Record<string, unknown>;
    const items = Array.isArray(obj.items)
      ? obj.items
      : Array.isArray(obj.data)
        ? obj.data
        : [];
    const totalRaw = obj.total ?? obj.totalCount ?? obj.count;
    const total =
      typeof totalRaw === 'number' && Number.isFinite(totalRaw) ? totalRaw : items.length;
    return { items, total };
  }
  return { items: [], total: 0 };
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
    viewGroups: resolveRelationIds(raw.viewGroups ?? raw.ViewGroups),
    editGroups: resolveRelationIds(raw.editGroups ?? raw.EditGroups),
    adminGroups: resolveRelationIds(raw.adminGroups ?? raw.AdminGroups),
    ownerGroups: resolveRelationIds(raw.ownerGroups ?? raw.OwnerGroups),
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
    const computed = Boolean(o.computed ?? o.Computed ?? false);
    const expr = o.expr ?? o.Expr;
    const label = o.label ?? o.Label;
    out.push({
      key,
      sortable: !computed && Boolean(o.sortable ?? o.Sortable ?? false),
      filterable: !computed && Boolean(o.filterable ?? o.Filterable ?? false),
      format: fmt != null && String(fmt).trim() ? (String(fmt) as OcColumnFormat) : null,
      computed,
      expr: computed && expr != null && String(expr).trim() ? String(expr).trim() : null,
      label: label != null && String(label).trim() ? String(label).trim() : null,
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
    helpMarkdown: parsedLayout.helpMarkdown || undefined,
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
    helpMarkdown: parsed.helpMarkdown || null,
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
  helpMarkdown?: string;
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
    helpMarkdown: input.helpMarkdown,
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
  boardId?: string,
  formContext?: OcFormRuntimeContext | null
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

  const fileFieldKeys = new Set(
    formContext ? resolveOcFormFileFieldKeys(formContext).map((k) => k.toLowerCase()) : []
  );
  const attachmentUploads = formContext
    ? collectWorkItemAttachmentsFromFormModel(model, formContext)
    : [];

  const fields: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(model)) {
    if (OC_CREATE_TOP_LEVEL_KEYS.has(key)) continue;
    if (fileFieldKeys.has(key.toLowerCase())) continue;
    if (value === undefined || value === null || value === '') continue;
    if (Array.isArray(value) && value.length === 0) continue;
    fields[key] = value;
  }
  if (attachmentUploads.length) {
    fields.attachments = attachmentUploads;
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

function mapProfileAction(raw: Record<string, unknown>): OcProfileAction | null {
  if (!raw || typeof raw !== 'object') return null;
  const transitionKey = pickStr(raw, 'transitionKey', 'TransitionKey') ?? '';
  const toStateId = resolveRelationId(raw.toStateId ?? raw.ToStateId) ?? '';
  if (!transitionKey || !toStateId) return null;
  const orderRaw = raw.order ?? raw.Order;
  const requiredFieldsRaw = raw.requiredFields ?? raw.RequiredFields;
  return {
    transitionKey,
    label: pickStr(raw, 'label', 'Label') ?? null,
    fromStateId: resolveRelationId(raw.fromStateId ?? raw.FromStateId) ?? null,
    toStateId,
    enabled: Boolean(raw.enabled ?? raw.Enabled ?? true),
    order: typeof orderRaw === 'number' ? orderRaw : Number(orderRaw) || 0,
    requiredFields: Array.isArray(requiredFieldsRaw)
      ? requiredFieldsRaw.map((f) => String(f).trim()).filter((f) => f.length > 0)
      : [],
  };
}

function mapWorkItemProfile(raw: Record<string, unknown>): OcWorkItemProfile {
  const wiRaw = (raw.workItem ?? raw.WorkItem ?? {}) as Record<string, unknown>;
  const perms = (raw.permissions ?? raw.Permissions ?? {}) as Record<string, unknown>;
  const watchers = raw.watchers ?? raw.Watchers;
  const links = raw.links ?? raw.Links;
  const actions = raw.actions ?? raw.Actions;
  const summary = mapWorkItemSummary(wiRaw);
  return {
    workspaceId: pickStr(raw, 'workspaceId', 'WorkspaceId') ?? '',
    workItem: summary,
    permissions: {
      canView: Boolean(perms.canView ?? perms.CanView ?? true),
      canEdit: Boolean(perms.canEdit ?? perms.CanEdit ?? false),
      canComment: Boolean(perms.canComment ?? perms.CanComment ?? false),
    },
    actions: Array.isArray(actions)
      ? actions
          .map((a) => mapProfileAction(a as Record<string, unknown>))
          .filter((a): a is OcProfileAction => !!a)
      : [],
    sla: mapSlaSnapshot(raw.sla ?? raw.Sla),
    watchers: Array.isArray(watchers) ? watchers.map(String) : [],
    links: Array.isArray(links) ? links.map((l) => mapWorkItemLink(l as Record<string, unknown>)) : [],
    people: parsePeopleMap(raw.people ?? raw.People),
    groups: parsePeopleMap(raw.groups ?? raw.Groups),
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

function mapTimelineChanges(raw: unknown): OcTimelineChange[] | undefined {
  const arr = Array.isArray(raw) ? raw : null;
  if (!arr) return undefined;
  const mapped = arr
    .filter((c): c is Record<string, unknown> => !!c && typeof c === 'object')
    .map((c) => ({
      field: pickStr(c, 'field', 'Field') ?? '',
      label: pickStr(c, 'label', 'Label') ?? null,
      fieldType: pickStr(c, 'fieldType', 'FieldType') ?? null,
      fromDisplay: pickStr(c, 'fromDisplay', 'FromDisplay') ?? null,
      toDisplay: pickStr(c, 'toDisplay', 'ToDisplay') ?? null,
    }))
    .filter((c) => !!c.field);
  return mapped.length ? mapped : undefined;
}

function mapTimelineEntry(raw: Record<string, unknown>): OcTimelineEntry {
  const atts = parseAttachments(raw.attachments ?? raw.Attachments);
  const changes = mapTimelineChanges(raw.changes ?? raw.Changes);
  return {
    type: pickStr(raw, 'type', 'Type') ?? '',
    id: pickStr(raw, 'id', 'Id') ?? null,
    actor: pickStr(raw, 'actor', 'Actor') ?? null,
    actorId: pickStr(raw, 'actorId', 'ActorId') ?? null,
    text: pickStr(raw, 'text', 'Text') ?? null,
    at: pickStr(raw, 'at', 'At') ?? null,
    activityType: pickStr(raw, 'activityType', 'ActivityType') ?? null,
    editedAt: pickStr(raw, 'editedAt', 'EditedAt') ?? null,
    parentId: pickStr(raw, 'parentId', 'ParentId') ?? null,
    attachments: atts.length ? atts : undefined,
    changes,
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

function mapTimelinePage(raw: unknown, skip = 0, take = 50): OcTimelinePage {
  const o = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
  const items = o.items ?? o.Items;
  return {
    items: Array.isArray(items) ? items.map((i) => mapTimelineEntry(i as Record<string, unknown>)) : [],
    skip: Number(o.skip ?? o.Skip ?? skip),
    take: Number(o.take ?? o.Take ?? take),
    total: Number(o.total ?? o.Total ?? 0),
  };
}

function mapResolvedPolicy(raw: unknown): OcResolvedPolicy {
  const o = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
  const matchedRaw = o.matchedSlaPolicy ?? o.MatchedSlaPolicy;
  const rulesRaw = o.applicableRules ?? o.ApplicableRules;
  const matched =
    matchedRaw && typeof matchedRaw === 'object'
      ? (() => {
          const m = matchedRaw as Record<string, unknown>;
          const id = pickStr(m, 'id', 'Id') ?? '';
          if (!id) return null;
          const resp = m.responseTargetMinutes ?? m.ResponseTargetMinutes;
          const resv = m.resolveTargetMinutes ?? m.ResolveTargetMinutes;
          return {
            id,
            name: pickStr(m, 'name', 'Name') ?? null,
            responseTargetMinutes: resp != null && resp !== '' ? Number(resp) : null,
            resolveTargetMinutes: resv != null && resv !== '' ? Number(resv) : null,
            derived: Boolean(m.derived ?? m.Derived ?? false),
          };
        })()
      : null;
  return {
    matchedSlaPolicy: matched,
    applicableRules: Array.isArray(rulesRaw)
      ? rulesRaw
          .filter((r): r is Record<string, unknown> => !!r && typeof r === 'object')
          .map((r) => ({
            id: pickStr(r, 'id', 'Id') ?? '',
            name: pickStr(r, 'name', 'Name') ?? null,
            trigger: pickStr(r, 'trigger', 'Trigger') ?? null,
            ruleType: pickStr(r, 'ruleType', 'RuleType') ?? null,
            description: pickStr(r, 'description', 'Description') ?? null,
          }))
          .filter((r) => !!r.id)
      : [],
  };
}

function parseStringMap(raw: unknown): Record<string, string> {
  const out: Record<string, string> = {};
  if (!raw || typeof raw !== 'object') return out;
  for (const [k, v] of Object.entries(raw as Record<string, unknown>)) {
    if (v != null) out[k] = String(v);
  }
  return out;
}

/** Kısa süreli profil-view önbelleği — board↔profil geçişlerinde tekrar MO çağrısını önler. */
const profileViewCache = new Map<string, { at: number; value: OcWorkItemProfileView }>();
const PROFILE_VIEW_CACHE_TTL_MS = 45_000;

export function ocInvalidateWorkItemProfileView(workItemId?: string): void {
  if (workItemId) profileViewCache.delete(workItemId);
  else profileViewCache.clear();
}

/**
 * Profil ekranının TEK toplu paketi (MO profile-view ucu): profile + edit form + katalog +
 * pool alanlar + alan görünen değerleri + politika + ilk sayfa timeline. UI'nın ~18 çağrısını 1'e indirir.
 */
export async function ocGetWorkItemProfileView(
  workItemId: string,
  options?: { force?: boolean }
): Promise<OcWorkItemProfileView> {
  const force = options?.force ?? false;
  const cached = profileViewCache.get(workItemId);
  if (!force && cached && Date.now() - cached.at < PROFILE_VIEW_CACHE_TTL_MS) {
    return cached.value;
  }

  const raw = (await fetchFromOperations(
    `/api/v1/runtime/work-items/${encodeURIComponent(workItemId)}/profile-view`,
    'GET'
  )) as Record<string, unknown>;

  const poolRaw = raw.poolFields ?? raw.PoolFields;

  const mapped: OcWorkItemProfileView = {
    profile: mapWorkItemProfile((raw.profile ?? raw.Profile ?? {}) as Record<string, unknown>),
    form: mapFormRuntimeContext((raw.form ?? raw.Form ?? {}) as Record<string, unknown>),
    catalogs: parseBoardCatalogs(raw.catalogs ?? raw.Catalogs),
    boards: parseStringMap(raw.boards ?? raw.Boards),
    poolFields: Array.isArray(poolRaw)
      ? poolRaw
          .filter((f): f is Record<string, unknown> => !!f && typeof f === 'object')
          .map((f) => mapOpField(f))
          .filter((f) => f.__dataId && f.key)
      : [],
    fieldDisplays: parseStringMap(raw.fieldDisplays ?? raw.FieldDisplays),
    policy: mapResolvedPolicy(raw.policy ?? raw.Policy),
    timeline: mapTimelinePage(raw.timeline ?? raw.Timeline, 0, 100),
  };

  profileViewCache.set(workItemId, { at: Date.now(), value: mapped });
  return mapped;
}

/**
 * İş kaydına yorum ekler. `mentions` = etiketlenen kişi id'leri (in-app bildirim tetikler).
 * `files` = yorum ekleri (tarayıcı File); base64 `content` ile gönderilir, DG MinIO'ya yükler.
 */
export async function ocAddWorkItemComment(
  workItemId: string,
  body: string,
  parentCommentId?: string | null,
  mentions?: string[],
  files?: File[]
): Promise<OcComment> {
  const payload: Record<string, unknown> = { body };
  if (parentCommentId) payload.parentCommentId = parentCommentId;
  if (mentions && mentions.length) payload.mentions = [...new Set(mentions)];
  if (files && files.length) {
    payload.attachments = await Promise.all(
      files.map(async (file) => ({
        content: await fileToBase64(file),
        originalFileName: file.name,
      }))
    );
  }
  const raw = (await fetchFromOperations(
    `/api/v1/work-items/${encodeURIComponent(workItemId)}/comments`,
    'POST',
    payload
  )) as Record<string, unknown>;
  return mapComment(raw);
}

/**
 * Kendi yorumunun gövdesini günceller (MO yalnızca yazara izin verir; aksi 403).
 * Yalnızca gövde güncellenir; mention/ek değişmez.
 */
export async function ocUpdateWorkItemComment(
  workItemId: string,
  commentId: string,
  body: string
): Promise<OcComment> {
  const raw = (await fetchFromOperations(
    `/api/v1/work-items/${encodeURIComponent(workItemId)}/comments/${encodeURIComponent(commentId)}`,
    'PUT',
    { body }
  )) as Record<string, unknown>;
  return mapComment(raw);
}

/** Kendi yorumunu siler (MO yalnızca yazara izin verir; aksi 403). */
export async function ocDeleteWorkItemComment(
  workItemId: string,
  commentId: string
): Promise<void> {
  await fetchFromOperations(
    `/api/v1/work-items/${encodeURIComponent(workItemId)}/comments/${encodeURIComponent(commentId)}`,
    'DELETE'
  );
}

/**
 * İş kaydına durum geçişi uygular (MO `POST /work-items/{id}/transitions/{key}`).
 * MO yetki + koşul + `requiredFields` doğrulamasını yapar; başarı sonrası güncel profil döner.
 */
export async function ocApplyTransition(
  workItemId: string,
  transitionKey: string,
  options?: { comment?: string | null; fields?: Record<string, unknown> | null }
): Promise<OcWorkItemProfile> {
  const payload: Record<string, unknown> = {};
  const comment = options?.comment?.trim();
  if (comment) payload.comment = comment;
  const fields = options?.fields;
  if (fields && Object.keys(fields).length > 0) payload.fields = fields;
  await fetchFromOperations(
    `/api/v1/work-items/${encodeURIComponent(workItemId)}/transitions/${encodeURIComponent(transitionKey)}`,
    'POST',
    payload
  );
  return ocGetWorkItemProfile(workItemId);
}

// Bildirimler (op_notifications) → services/operationCore/notifications.ts
export * from '@/services/operationCore/notifications';

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

/** Eki DG'den blob olarak çeker (önizleme için; indirme akışını tetiklemez). */
export async function ocFetchAttachmentBlob(att: OcAttachment): Promise<Blob> {
  const url = `/api/v1/files/download?filePath=${encodeURIComponent(att.path)}`;
  return fetchBlobFromDataGateway(url);
}

/** Eki DG'den indirir ve tarayıcıda kaydetme akışını tetikler. */
export async function ocDownloadAttachment(att: OcAttachment): Promise<void> {
  const blob = await ocFetchAttachmentBlob(att);
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

/**
 * Profil düzenle PATCH — yeni file yüklemeleri mevcut `attachments` ile birleştirilir;
 * file alan anahtarları extraFields'a yazılmaz.
 */
export function buildUpdateWorkItemRequestFromFormEdit(
  changed: Record<string, unknown>,
  formContext: OcFormRuntimeContext,
  existingAttachments: OcAttachment[] = []
): OcUpdateWorkItemRequest {
  const fileFieldKeys = new Set(resolveOcFormFileFieldKeys(formContext).map((k) => k.toLowerCase()));
  const nonFileChanged: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(changed)) {
    if (!fileFieldKeys.has(key.toLowerCase())) {
      nonFileChanged[key] = value;
    }
  }

  const body = buildUpdateWorkItemRequest(nonFileChanged);
  const newUploads = collectNewFileUploadsFromChangedFields(changed, formContext);
  if (newUploads.length) {
    const attachmentRows: unknown[] = [
      ...existingAttachments.map((a) => a.raw),
      ...newUploads,
    ];
    body.fields = { ...(body.fields ?? {}), attachments: attachmentRows };
  }
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
  ocInvalidateWorkItemProfileView(workItemId);
  return mapCreateWorkItemResult(raw);
}

export async function ocDeleteWorkItem(workItemId: string, force = false): Promise<void> {
  const qs = force ? '?force=true' : '';
  await fetchFromOperations(`/api/v1/work-items/${encodeURIComponent(workItemId)}${qs}`, 'DELETE');
}

/** MngOperations hata gövdesinden `code` döndürür (guard ayrımı için, örn. WORK_ITEM_HAS_RELATIONS). */
export function ocErrorCode(error: unknown): string | null {
  if (!(error instanceof Error)) return null;
  const data = (error as { data?: unknown }).data;
  if (data && typeof data === 'object') {
    const code = (data as Record<string, unknown>).code;
    if (typeof code === 'string') return code;
  }
  return null;
}

/** MngOperations hata gövdesinden Türkçe mesajı (messageTr) tercih ederek okunabilir mesaj döndürür. */
export function ocExtractOperationsMessage(error: unknown, fallback: string): string {
  if (error instanceof Error) {
    const data = (error as { data?: unknown }).data;
    if (data && typeof data === 'object') {
      const d = data as Record<string, unknown>;
      if (typeof d.messageTr === 'string' && d.messageTr) return d.messageTr;
      if (typeof d.message === 'string' && d.message) return d.message;
    }
  }
  return ocExtractDgErrorMessage(error, fallback);
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
    defaultDashboardId: resolveRelationId(raw.defaultDashboardId ?? raw.DefaultDashboardId),
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

/** Sayfalı dataset listesi — modal lookup picker (L4). */
export async function ocListDatasetPage(
  dataset: string,
  options?: { skip?: number; limit?: number; sort?: string; filter?: string; search?: string }
): Promise<{ items: unknown[]; total: number }> {
  const qs = buildQuery(options ?? {});
  const url = `/api/v1/data/${encodeURIComponent(dataset)}?${qs}`;
  const raw = await fetchFromDataGateway(url, 'GET');
  return parseListResponseWithTotal(raw);
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

export function resolveRelationId(raw: unknown): string | null {
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

export async function ocCreateField(payload: Record<string, unknown>): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.fields, payload);
}

export async function ocUpdateField(fieldId: string, payload: Record<string, unknown>) {
  await ocCatalogUpdate('fields', fieldId, payload);
}

export async function ocDeleteField(fieldId: string) {
  await ocCatalogDelete('fields', fieldId);
}

// Durum akışları (op_state_flows) → services/operationCore/flows.ts
export * from '@/services/operationCore/flows';

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

export interface OcMetadataCacheReloadResult {
  workspaceId: string;
  keysRemoved: number;
}

/** MO metadata önbelleğini workspace kapsamında düşürür (form layout vb. DG güncellemeleri). */
export async function ocReloadWorkspaceMetadataCache(
  workspaceId: string
): Promise<OcMetadataCacheReloadResult> {
  const raw = (await fetchFromOperations(
    `/api/v1/workspaces/${encodeURIComponent(workspaceId)}/metadata-cache/reload`,
    'POST'
  )) as Record<string, unknown>;
  return {
    workspaceId: String(raw.workspaceId ?? raw.WorkspaceId ?? workspaceId),
    keysRemoved: Number(raw.keysRemoved ?? raw.KeysRemoved ?? 0),
  };
}

export async function ocCreateWorkspace(payload: Record<string, unknown>): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.workspaces, payload);
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
    .filter((f) => f.__dataId && f.key && f.scope === 'pool');
  const seen = new Set<string>();
  return [...globalRows, ...scoped].filter((f) => {
    if (seen.has(f.__dataId)) return false;
    seen.add(f.__dataId);
    return true;
  });
}

/**
 * Form yerleşim editörü — core dışı alanlar: workspace'e özel tanımlar + enabledFieldIds ile
 * aktive edilmiş global havuz alanları (file vb.).
 */
export async function ocListFormLayoutPoolFields(workspaceId: string): Promise<OpField[]> {
  const ws = await ocGetWorkspace(workspaceId);
  const enabledSet = new Set(ws?.enabledFieldIds ?? []);
  const [globalAll, scoped] = await Promise.all([
    ocListGlobalPoolFields(),
    ocListWorkspaceScopedFields(workspaceId),
  ]);
  const enabledGlobal = globalAll.filter((f) => enabledSet.has(f.__dataId));
  const byKey = new Map<string, OpField>();
  for (const f of enabledGlobal) byKey.set(f.key.toLowerCase(), f);
  for (const f of scoped) byKey.set(f.key.toLowerCase(), f);
  return [...byKey.values()].sort(
    (a, b) => (a.sortOrder ?? 999) - (b.sortOrder ?? 999) || a.key.localeCompare(b.key)
  );
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
    .filter((f) => f.__dataId && f.key && f.scope === 'pool');
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

export async function ocCreateRecordId(
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

// Kurallar (op_rules) → services/operationCore/rules.ts
export * from '@/services/operationCore/rules';

// SLA politikaları (op_sla_policies) → services/operationCore/sla.ts
export * from '@/services/operationCore/sla';

// Bildirim politikaları (op_notification_policies) → services/operationCore/notificationPolicies.ts
export * from '@/services/operationCore/notificationPolicies';

// İş kaydı zamanlamaları (op_work_item_schedules) → services/operationCore/schedules.ts
export * from '@/services/operationCore/schedules';

// Etiketler (op_tags, workspace-kapsamlı) → services/operationCore/tags.ts
export * from '@/services/operationCore/tags';

export async function ocCreateBoard(payload: Record<string, unknown>): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.boards, payload);
}

export async function ocUpdateBoard(boardId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.boards, boardId, payload);
}

export async function ocGetBoard(boardId: string): Promise<OpBoard | null> {
  const id = boardId?.trim();
  if (!id) return null;
  const url = `/api/v1/data/${encodeURIComponent(OC_DATASETS.boards)}/${encodeURIComponent(id)}`;
  try {
    const raw = await fetchFromDataGateway(url, 'GET');
    const record = parseSingleDgRecord(raw);
    if (record) {
      const board = mapBoard(record);
      if (board.__dataId) return board;
    }
  } catch {
    // fallback: workspace listesi
  }
  const rows = await ocListDataset(OC_DATASETS.boards, { limit: 500 });
  const match = rows.find((r) => {
    const rid = String((r as Record<string, unknown>).__dataId ?? (r as Record<string, unknown>).dataId ?? '');
    return rid === id;
  });
  if (!match) return null;
  return mapBoard(match as Record<string, unknown>);
}

/** Board varsayılan panosunu günceller (tam board gövdesi — DG PUT). */
export async function ocSetBoardDefaultDashboard(
  board: OpBoard,
  defaultDashboardId: string | null,
  poolFieldKeys: string[] = []
) {
  const next: OpBoard = { ...board, defaultDashboardId: defaultDashboardId?.trim() || null };
  await ocUpdateBoard(board.__dataId, buildBoardDgPayload(next, poolFieldKeys));
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

export function pickStr(obj: Record<string, unknown>, ...keys: string[]): string | undefined {
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

function mapBoardColumnTransition(raw: Record<string, unknown>): OcBoardColumnTransition | null {
  const transitionKey = pickStr(raw, 'transitionKey', 'TransitionKey') ?? '';
  if (!transitionKey) return null;
  const reqRaw = raw.requiredFields ?? raw.RequiredFields;
  return {
    transitionKey,
    fromStateId: resolveRelationId(raw.fromStateId ?? raw.FromStateId) ?? '',
    requiredFields: Array.isArray(reqRaw)
      ? reqRaw.map((f) => String(f).trim()).filter((f) => f.length > 0)
      : [],
  };
}

function mapBoardColumn(raw: Record<string, unknown>): OcBoardColumn {
  const alt = raw.alternativeTransitionKeys ?? raw.AlternativeTransitionKeys;
  const incoming = raw.incomingTransitions ?? raw.IncomingTransitions;
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
    incomingTransitions: Array.isArray(incoming)
      ? incoming
          .map((tr) => mapBoardColumnTransition(tr as Record<string, unknown>))
          .filter((tr): tr is OcBoardColumnTransition => !!tr)
      : [],
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
  const computed = Boolean(raw.computed ?? raw.Computed ?? false);
  const expr = pickStr(raw, 'expr', 'Expr');
  const label = pickStr(raw, 'label', 'Label');
  return {
    key: pickStr(raw, 'key', 'Key') ?? '',
    sortable: !computed && Boolean(raw.sortable ?? raw.Sortable ?? false),
    filterable: !computed && Boolean(raw.filterable ?? raw.Filterable ?? false),
    format: (fmt as OcColumnFormat | undefined) ?? null,
    computed,
    expr: computed && expr?.trim() ? expr.trim() : null,
    label: label?.trim() ? label.trim() : null,
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
    groups: parsePeopleMap(raw.groups ?? raw.Groups),
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

// ===== Dashboards (D-A) =====

function mapDashboardWidgetExecution(raw: unknown): OcDashboardWidgetExecution | null {
  if (!raw || typeof raw !== 'object') return null;
  const r = raw as Record<string, unknown>;
  const items = r.items ?? r.Items;
  const agg = r.aggregation ?? r.Aggregation;
  return {
    success: Boolean(r.success ?? r.Success ?? false),
    errorCode: pickStr(r, 'errorCode', 'ErrorCode') ?? null,
    errorMessage: pickStr(r, 'errorMessage', 'ErrorMessage') ?? null,
    total: Number(r.total ?? r.Total ?? 0),
    skip: Number(r.skip ?? r.Skip ?? 0),
    take: Number(r.take ?? r.Take ?? 0),
    items: Array.isArray(items)
      ? items.map((i) => mapWorkItemCard(i as Record<string, unknown>)).filter((c) => c.id)
      : [],
    aggregation: Array.isArray(agg)
      ? agg.map((b) => {
          const o = b as Record<string, unknown>;
          return {
            key: (o.key ?? o.Key ?? null) as string | null,
            count: Number(o.count ?? o.Count ?? 0),
          };
        })
      : [],
    executedAt: pickStr(r, 'executedAt', 'ExecutedAt') ?? null,
  };
}

function mapDashboardWidget(raw: Record<string, unknown>): OcDashboardWidget {
  return {
    key: pickStr(raw, 'key', 'Key') ?? '',
    widgetType: pickStr(raw, 'widgetType', 'WidgetType') ?? 'list',
    title: pickStr(raw, 'title', 'Title') ?? null,
    dataset: pickStr(raw, 'dataset', 'Dataset') ?? null,
    queryKey: pickStr(raw, 'queryKey', 'QueryKey') ?? null,
    chartType: pickStr(raw, 'chartType', 'ChartType') ?? null,
    groupBy: pickStr(raw, 'groupBy', 'GroupBy') ?? null,
    accentColor: pickStr(raw, 'accentColor', 'AccentColor') ?? null,
    icon: pickStr(raw, 'icon', 'Icon') ?? null,
    resolvedParameters: (raw.resolvedParameters ?? raw.ResolvedParameters ?? null) as
      | Record<string, unknown>
      | null,
    execution: mapDashboardWidgetExecution(raw.execution ?? raw.Execution),
  };
}

function mapDashboardLayout(raw: unknown): OcDashboard['layout'] {
  if (!raw || typeof raw !== 'object') return null;
  const r = raw as Record<string, unknown>;
  const rows = r.rows ?? r.Rows;
  return {
    type: pickStr(r, 'type', 'Type') ?? 'rows',
    rows: Array.isArray(rows) ? (rows as OcDashboardLayout['rows']) : [],
  };
}

/** MO tek toplu dashboard context (widget'lar server-side çalıştırılmış + katalog/person çözülmüş). */
export async function ocGetDashboard(dashboardId: string): Promise<OcDashboard> {
  const raw = (await fetchFromOperations(
    `/api/v1/runtime/dashboards/${encodeURIComponent(dashboardId)}`,
    'GET'
  )) as Record<string, unknown>;
  const widgets = raw.widgets ?? raw.Widgets;
  const perm = (raw.permissions ?? raw.Permissions ?? {}) as Record<string, unknown>;
  return {
    dashboardId: pickStr(raw, 'dashboardId', 'DashboardId') ?? dashboardId,
    workspaceId: pickStr(raw, 'workspaceId', 'WorkspaceId') ?? null,
    name: pickStr(raw, 'name', 'Name') ?? null,
    description: pickStr(raw, 'description', 'Description') ?? null,
    scope: pickStr(raw, 'scope', 'Scope') ?? null,
    layout: mapDashboardLayout(raw.layout ?? raw.Layout),
    permissions: {
      canView: Boolean(perm.canView ?? perm.CanView ?? true),
      canEdit: Boolean(perm.canEdit ?? perm.CanEdit ?? false),
      canComment: Boolean(perm.canComment ?? perm.CanComment ?? false),
    },
    widgets: Array.isArray(widgets)
      ? widgets.map((w) => mapDashboardWidget(w as Record<string, unknown>)).filter((w) => w.key)
      : [],
    catalogs: parseBoardCatalogs(raw.catalogs ?? raw.Catalogs),
    people: parsePeopleMap(raw.people ?? raw.People),
    groups: parsePeopleMap(raw.groups ?? raw.Groups),
  };
}

/** Bir workspace'e ait dashboard'ları (ada göre) listeler — hub. */
function mapDashboardListItem(raw: Record<string, unknown>): OcDashboardListItem {
  return {
    id: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? raw.Name ?? ''),
    description: (raw.description ?? raw.Description ?? null) as string | null,
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId) ?? null,
    isActive: raw.isActive == null && raw.IsActive == null ? true : Boolean(raw.isActive ?? raw.IsActive),
    isDefault: Boolean(raw.isDefault ?? raw.IsDefault ?? false),
  };
}

export async function ocListDashboardsForWorkspace(workspaceId: string): Promise<OcDashboardListItem[]> {
  if (!workspaceId) return [];
  const rows = await ocListDataset(OC_DATASETS.dashboards, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'name:asc',
    limit: 200,
  });
  return rows
    .map((r) => mapDashboardListItem(r as Record<string, unknown>))
    .filter((d) => d.id && d.workspaceId === workspaceId);
}

/** Tüm panolar (workspace filtresi olmadan) — gezinme tree'sinde board→pano adını çözmek için. */
export async function ocListAllDashboards(): Promise<OcDashboardListItem[]> {
  const rows = await ocListDataset(OC_DATASETS.dashboards, {
    sort: 'name:asc',
    limit: 1000,
  });
  return rows
    .map((r) => mapDashboardListItem(r as Record<string, unknown>))
    .filter((d) => d.id);
}

function mapDashboardWidgetDef(raw: Record<string, unknown>): OcDashboardWidgetDef {
  const params = raw.parameters ?? raw.Parameters;
  const take = raw.take ?? raw.Take;
  const style = readDashboardWidgetStyleFromRaw(raw);
  return {
    key: String(raw.key ?? raw.Key ?? ''),
    type: String(raw.type ?? raw.Type ?? raw.widgetType ?? raw.WidgetType ?? 'list'),
    title: (raw.title ?? raw.Title ?? null) as string | null,
    dataset: (raw.dataset ?? raw.Dataset ?? null) as string | null,
    queryKey: (raw.queryKey ?? raw.QueryKey ?? null) as string | null,
    parameters:
      params && typeof params === 'object' ? (params as Record<string, unknown>) : null,
    take: take == null ? null : Number(take),
    chartType: (raw.chartType ?? raw.ChartType ?? null) as string | null,
    groupBy: (raw.groupBy ?? raw.GroupBy ?? null) as string | null,
    accentColor: style.accentColor,
    icon: style.icon,
  };
}

function readDashboardWidgetStyleFromRaw(raw: Record<string, unknown>): {
  accentColor: string | null;
  icon: string | null;
} {
  let accentColor = pickStr(raw, 'accentColor', 'AccentColor');
  let icon = pickStr(raw, 'icon', 'Icon');
  const cfg = raw.config ?? raw.Config;
  if (cfg && typeof cfg === 'object') {
    const c = cfg as Record<string, unknown>;
    accentColor = accentColor ?? pickStr(c, 'accentColor', 'AccentColor');
    icon = icon ?? pickStr(c, 'icon', 'Icon');
  }
  return { accentColor, icon };
}

/** op_dashboards ham kaydını DG'den okur (admin editörü için — runtime context DEĞİL). */
export async function ocGetDashboardRecord(dashboardId: string): Promise<OcDashboardRecord | null> {
  const id = dashboardId?.trim();
  if (!id) return null;
  const rows = await ocListDataset(OC_DATASETS.dashboards, {
    filter: `__dataId:eq:${id}`,
    limit: 1,
  });
  const raw = rows[0] as Record<string, unknown> | undefined;
  if (!raw) return null;
  const widgets = raw.widgets ?? raw.Widgets;
  return {
    id: String(raw.__dataId ?? raw.dataId ?? id),
    name: String(raw.name ?? raw.Name ?? ''),
    description: (raw.description ?? raw.Description ?? null) as string | null,
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId) ?? null,
    scope: (raw.scope ?? raw.Scope ?? null) as string | null,
    isDefault: Boolean(raw.isDefault ?? raw.IsDefault ?? false),
    isActive: raw.isActive == null && raw.IsActive == null ? true : Boolean(raw.isActive ?? raw.IsActive),
    layout: mapDashboardLayout(raw.layout ?? raw.Layout),
    widgets: Array.isArray(widgets)
      ? widgets.map((w) => mapDashboardWidgetDef(w as Record<string, unknown>)).filter((w) => w.key)
      : [],
  };
}

/** Yeni pano oluşturur (op_dashboards — UI→DG direkt; MO read-only runtime). __dataId döner. */
export async function ocCreateDashboard(body: Record<string, unknown>): Promise<string | null> {
  const res = (await ocCreate(OC_DATASETS.dashboards, body)) as Record<string, unknown> | undefined;
  const data = (res?.data ?? res?.Data ?? res) as Record<string, unknown> | undefined;
  return (data?.__dataId ?? data?.dataId ?? data?.DataId ?? null) as string | null;
}

/** Mevcut panoyu günceller (op_dashboards). */
export async function ocUpdateDashboard(dashboardId: string, body: Record<string, unknown>) {
  return ocUpdate(OC_DATASETS.dashboards, dashboardId, body);
}

/** Panoyu siler (op_dashboards). */
export async function ocDeleteDashboard(dashboardId: string) {
  return ocDelete(OC_DATASETS.dashboards, dashboardId);
}
