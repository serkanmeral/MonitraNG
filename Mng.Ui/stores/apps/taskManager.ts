import { defineStore } from 'pinia';
import {
  TM_DATASETS,
  tmListDataset,
  tmGetById,
  tmCreate,
  tmUpdate,
  tmDelete,
} from '@/services/taskManagerService';
import type {
  TmProject,
  TmProjectWorkflow,
  TmProjectPermissions,
  TmProjectSelections,
  TmIssueCreateLayout,
  TmIssueCreateFormTemplate,
  TmBoard,
  TmIssue,
  TmStatus,
  TmIssueType,
  TmPriority,
  TmFieldDefinition,
  TmLabel,
  TmBoardConfig,
  TmIssueComment,
} from '@/types/apps/taskManager';
import {
  buildDefaultWorkflow,
  buildBoardConfigFromWorkflow,
  getEffectiveWorkflow,
  sortTmStatusesByName,
} from '@/utils/taskManagerWorkflow';
import {
  DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH,
  normalizeDialogMaxWidthPx,
} from '@/utils/taskManagerNewIssueForm';
import { parseIssueHistory } from '@/utils/taskManagerIssueHistory';
import { assigneeUserId } from '@/composables/useTaskManagerHelpers';
import { useUserStore } from '@/stores/apps/user';

function rid(v: unknown): string {
  if (v == null) return '';
  if (typeof v === 'string') return v;
  if (typeof v === 'object' && v !== null && ('__dataId' in v || 'dataId' in v))
    return (v as any).__dataId ?? (v as any).dataId ?? '';
  return String(v);
}

/** DataGateway POST yanıtından oluşturulan kaydın kimliği */
function extractCreatedRecordId(res: unknown): string {
  const created = res as Record<string, unknown> | null;
  if (!created || typeof created !== 'object') return '';
  const nested =
    created.data != null && typeof created.data === 'object' && !Array.isArray(created.data)
      ? (created.data as Record<string, unknown>)
      : null;
  return rid(
    created.__dataId ??
      created.DataId ??
      created.dataId ??
      nested?.__dataId ??
      nested?.DataId ??
      nested?.dataId
  );
}

function mapSelections(raw: unknown): TmProjectSelections | null {
  if (raw == null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const ids = (v: unknown) => (Array.isArray(v) ? (v as unknown[]).map((x) => rid(x)).filter(Boolean) : []);
  const fk = (v: unknown) => (Array.isArray(v) ? (v as unknown[]).map((x) => String(x)).filter(Boolean) : []);
  const priorityIds = ids(o.priorityIds);
  const issueTypeIds = ids(o.issueTypeIds);
  const fieldKeys = fk(o.fieldKeys);
  if (!priorityIds.length && !issueTypeIds.length && !fieldKeys.length) return null;
  return { priorityIds, issueTypeIds, fieldKeys };
}

function mapIssueCreateLayout(raw: unknown): TmIssueCreateLayout | null {
  if (raw == null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const rows = o.rows ?? o.Rows;
  if (!Array.isArray(rows)) return null;
  const r = rows.map((x) => String(x).trim()).filter(Boolean);
  if (!r.length) return null;

  const csRaw = o.columnSections ?? o.ColumnSections;
  let columnSections: Record<string, string> | undefined;
  if (csRaw != null && typeof csRaw === 'object' && !Array.isArray(csRaw)) {
    columnSections = {};
    for (const [k, v] of Object.entries(csRaw as Record<string, unknown>)) {
      if (!k || v == null) continue;
      const s = String(v).trim();
      if (s) columnSections[k] = s;
    }
    if (!Object.keys(columnSections).length) columnSections = undefined;
  }

  const stRaw = o.sectionTitles ?? o.SectionTitles;
  let sectionTitles: Record<string, string> | undefined;
  if (stRaw != null && typeof stRaw === 'object' && !Array.isArray(stRaw)) {
    sectionTitles = {};
    for (const [k, v] of Object.entries(stRaw as Record<string, unknown>)) {
      if (!k || v == null) continue;
      const s = String(v).trim();
      if (s) sectionTitles[k] = s;
    }
    if (!Object.keys(sectionTitles).length) sectionTitles = undefined;
  }

  const fh = o.formHeading ?? o.FormHeading;
  const formHeading =
    fh != null && String(fh).trim() ? String(fh).trim() : undefined;
  const fi = o.formIntro ?? o.FormIntro;
  const formIntro = fi != null && String(fi).trim() ? String(fi).trim() : undefined;

  const fcRaw = o.fieldCols ?? o.FieldCols;
  let fieldCols: Record<string, number> | undefined;
  if (fcRaw != null && typeof fcRaw === 'object' && !Array.isArray(fcRaw)) {
    fieldCols = {};
    for (const [k, v] of Object.entries(fcRaw as Record<string, unknown>)) {
      if (!k || v == null) continue;
      const n = typeof v === 'number' ? v : Number.parseInt(String(v), 10);
      if (!Number.isFinite(n) || n < 1 || n > 12) continue;
      fieldCols[k] = Math.round(n);
    }
    if (!Object.keys(fieldCols).length) fieldCols = undefined;
  }

  const dwRaw = o.dialogMaxWidth ?? o.DialogMaxWidth;
  let dialogMaxWidth: number | undefined;
  if (dwRaw != null && String(dwRaw).trim() !== '') {
    const w = normalizeDialogMaxWidthPx(dwRaw);
    if (w !== DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH) dialogMaxWidth = w;
  }

  const soRaw = o.sectionOrder ?? o.SectionOrder;
  let sectionOrder: string[] | undefined;
  if (Array.isArray(soRaw)) {
    const arr = soRaw.map((x) => String(x).trim()).filter(Boolean);
    if (arr.length) sectionOrder = arr;
  }

  const secColRaw = o.sectionCols ?? o.SectionCols;
  let sectionCols: Record<string, number> | undefined;
  if (secColRaw != null && typeof secColRaw === 'object' && !Array.isArray(secColRaw)) {
    sectionCols = {};
    for (const [k, v] of Object.entries(secColRaw as Record<string, unknown>)) {
      if (!k || v == null) continue;
      const n = typeof v === 'number' ? v : Number.parseInt(String(v), 10);
      if (!Number.isFinite(n) || n < 1 || n > 12) continue;
      sectionCols[k] = Math.round(n);
    }
    if (!Object.keys(sectionCols).length) sectionCols = undefined;
  }

  return {
    rows: r,
    ...(columnSections ? { columnSections } : {}),
    ...(sectionTitles ? { sectionTitles } : {}),
    ...(formHeading ? { formHeading } : {}),
    ...(formIntro ? { formIntro } : {}),
    ...(fieldCols ? { fieldCols } : {}),
    ...(dialogMaxWidth != null ? { dialogMaxWidth } : {}),
    ...(sectionOrder ? { sectionOrder } : {}),
    ...(sectionCols ? { sectionCols } : {}),
  };
}

function newIssueFormTemplateId(): string {
  return `tm-form-${Math.random().toString(36).slice(2, 11)}`;
}

function mapIssueCreateFormTemplate(raw: unknown): TmIssueCreateFormTemplate | null {
  if (raw == null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  let id = String(o.id ?? o.Id ?? '').trim();
  if (!id) id = newIssueFormTemplateId();
  const name = String(o.name ?? o.Name ?? '').trim() || id;
  const layout = mapIssueCreateLayout(o.layout ?? o.Layout);
  if (!layout?.rows?.length) return null;
  return { id, name, layout };
}

function mapProject(raw: any): TmProject {
  const wfRaw = raw.workflow ?? raw.Workflow;
  const uk = raw.useKanban ?? raw.UseKanban;
  const icl = mapIssueCreateLayout(raw.issueCreateLayout ?? raw.IssueCreateLayout);
  const ipl = mapIssueCreateLayout(raw.issueProfileLayout ?? raw.IssueProfileLayout);

  let issueProfileForms: TmIssueCreateFormTemplate[] | undefined;
  const rawProfileForms = raw.issueProfileForms ?? raw.IssueProfileForms;
  if (rawProfileForms != null && Array.isArray(rawProfileForms)) {
    const plist: TmIssueCreateFormTemplate[] = [];
    for (const item of rawProfileForms) {
      const t = mapIssueCreateFormTemplate(item);
      if (t) plist.push(t);
    }
    if (plist.length) issueProfileForms = plist;
  }
  if (!issueProfileForms?.length && ipl?.rows?.length) {
    issueProfileForms = [{ id: 'legacy', name: 'Default', layout: ipl }];
  }

  let defaultIssueProfileFormId: string | null =
    String(raw.defaultIssueProfileFormId ?? raw.DefaultIssueProfileFormId ?? '').trim() || null;
  if (issueProfileForms?.length) {
    const pids = new Set(issueProfileForms.map((f) => f.id));
    if (!defaultIssueProfileFormId || !pids.has(defaultIssueProfileFormId)) {
      defaultIssueProfileFormId = issueProfileForms[0]!.id;
    }
  } else {
    defaultIssueProfileFormId = null;
  }

  let issueCreateForms: TmIssueCreateFormTemplate[] | undefined;
  const rawForms = raw.issueCreateForms ?? raw.IssueCreateForms;
  if (rawForms != null && Array.isArray(rawForms)) {
    const list: TmIssueCreateFormTemplate[] = [];
    for (const item of rawForms) {
      const t = mapIssueCreateFormTemplate(item);
      if (t) list.push(t);
    }
    if (list.length) issueCreateForms = list;
  }
  if (!issueCreateForms?.length && icl?.rows?.length) {
    issueCreateForms = [{ id: 'legacy', name: 'Default', layout: icl }];
  }

  let defaultIssueCreateFormId: string | null =
    String(raw.defaultIssueCreateFormId ?? raw.DefaultIssueCreateFormId ?? '').trim() || null;
  if (issueCreateForms?.length) {
    const ids = new Set(issueCreateForms.map((f) => f.id));
    if (!defaultIssueCreateFormId || !ids.has(defaultIssueCreateFormId)) {
      defaultIssueCreateFormId = issueCreateForms[0]!.id;
    }
  } else {
    defaultIssueCreateFormId = null;
  }

  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    key: raw.key ?? raw.Key ?? '',
    description: raw.description ?? raw.Description ?? null,
    lead: raw.lead ?? raw.Lead,
    avatarUrl: raw.avatarUrl ?? raw.AvatarUrl ?? null,
    permissions: raw.permissions ?? raw.Permissions ?? null,
    selections: mapSelections(raw.selections ?? raw.Selections),
    workflow: wfRaw != null ? (wfRaw as TmProjectWorkflow) : null,
    useKanban: uk === null || uk === undefined ? undefined : Boolean(uk),
    issueCreateLayout: icl,
    ...(ipl?.rows?.length ? { issueProfileLayout: ipl } : {}),
    ...(issueProfileForms ? { issueProfileForms } : {}),
    ...(defaultIssueProfileFormId ? { defaultIssueProfileFormId } : {}),
    ...(issueCreateForms ? { issueCreateForms } : {}),
    ...(defaultIssueCreateFormId ? { defaultIssueCreateFormId } : {}),
  };
}

function mapBoard(raw: any): TmBoard {
  const cfg = raw.config ?? raw.Config ?? null;
  let issueCreateFormId: string | null = null;
  const top = raw.issueCreateFormId ?? raw.IssueCreateFormId;
  if (top != null && String(top).trim()) issueCreateFormId = String(top).trim();
  else if (cfg != null && typeof cfg === 'object' && !Array.isArray(cfg)) {
    const c = cfg as Record<string, unknown>;
    const nested = c.issueCreateFormId ?? c.IssueCreateFormId;
    if (nested != null && String(nested).trim()) issueCreateFormId = String(nested).trim();
  }
  let issueProfileFormId: string | null = null;
  const topPf = raw.issueProfileFormId ?? raw.IssueProfileFormId;
  if (topPf != null && String(topPf).trim()) issueProfileFormId = String(topPf).trim();
  else if (cfg != null && typeof cfg === 'object' && !Array.isArray(cfg)) {
    const c = cfg as Record<string, unknown>;
    const nestedPf = c.issueProfileFormId ?? c.IssueProfileFormId;
    if (nestedPf != null && String(nestedPf).trim()) issueProfileFormId = String(nestedPf).trim();
  }
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    projectId: rid(raw.projectId ?? raw.ProjectId),
    type: raw.type ?? raw.Type ?? 'kanban',
    config: cfg,
    ...(issueCreateFormId ? { issueCreateFormId } : {}),
    ...(issueProfileFormId ? { issueProfileFormId } : {}),
  };
}

function mapLabelIds(raw: unknown): string[] | null {
  if (raw == null) return null;
  if (!Array.isArray(raw)) return null;
  const ids = raw.map((x) => (typeof x === 'string' ? x : rid(x))).filter(Boolean);
  return ids.length ? ids : null;
}

const ISSUE_RAW_KEYS_USED = new Set([
  '__dataId',
  'DataId',
  'dataId',
  'key',
  'Key',
  'projectKey',
  'ProjectKey',
  'projectId',
  'ProjectId',
  'issueTypeId',
  'IssueTypeId',
  'title',
  'Title',
  'description',
  'Description',
  'statusId',
  'StatusId',
  'priorityId',
  'PriorityId',
  'assignee',
  'Assignee',
  'epicId',
  'EpicId',
  'sprintId',
  'SprintId',
  'labels',
  'Labels',
  'dueDate',
  'DueDate',
  'storyPoints',
  'StoryPoints',
  'order',
  'Order',
  '__history',
  '__History',
  'issueHistory',
  'IssueHistory',
]);

function mapIssueComment(raw: any): TmIssueComment {
  const parentRaw = raw.parentCommentId ?? raw.ParentCommentId;
  const parentId = parentRaw != null && String(parentRaw).trim() !== '' ? rid(parentRaw) : '';
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    issueId: rid(raw.issueId ?? raw.IssueId),
    projectId: rid(raw.projectId ?? raw.ProjectId),
    author: raw.author ?? raw.Author,
    body: String(raw.body ?? raw.Body ?? ''),
    parentCommentId: parentId || null,
    createdAt: raw.createdAt != null ? String(raw.createdAt ?? raw.CreatedAt) : null,
    updatedAt: raw.updatedAt != null ? String(raw.updatedAt ?? raw.UpdatedAt) : null,
  };
}

function mapIssue(raw: any): TmIssue {
  const histRaw = raw.__history ?? raw.__History ?? raw.issueHistory ?? raw.IssueHistory;
  const issueHistoryParsed = parseIssueHistory(histRaw);

  const mapped: TmIssue = {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    key: raw.key ?? raw.Key ?? '',
    projectKey: raw.projectKey ?? raw.ProjectKey ?? '',
    projectId: rid(raw.projectId ?? raw.ProjectId),
    issueTypeId: rid(raw.issueTypeId ?? raw.IssueTypeId),
    title: raw.title ?? raw.Title ?? '',
    description: raw.description ?? raw.Description ?? null,
    statusId: rid(raw.statusId ?? raw.StatusId),
    priorityId: raw.priorityId != null ? rid(raw.priorityId) : null,
    assignee: raw.assignee ?? raw.Assignee,
    epicId: raw.epicId != null ? rid(raw.epicId) : null,
    sprintId: raw.sprintId != null ? rid(raw.sprintId) : null,
    labels: mapLabelIds(raw.labels ?? raw.Labels),
    dueDate: raw.dueDate ?? raw.DueDate ?? null,
    storyPoints: raw.storyPoints ?? raw.StoryPoints ?? null,
    order: raw.order ?? raw.Order ?? null,
    ...(issueHistoryParsed.length ? { issueHistory: issueHistoryParsed } : {}),
  };
  if (raw && typeof raw === 'object') {
    const extra: Record<string, unknown> = {};
    for (const k of Object.keys(raw)) {
      if (!ISSUE_RAW_KEYS_USED.has(k)) extra[k] = raw[k];
    }
    if (Object.keys(extra).length) mapped.extraFields = extra;
  }
  return mapped;
}

function mapLabel(raw: any): TmLabel {
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    color: raw.color ?? raw.Color ?? null,
    projectId: raw.projectId != null ? rid(raw.projectId) : null,
  };
}

function mapStatus(raw: any): TmStatus {
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    icon: raw.icon ?? raw.Icon ?? null,
    color: raw.color ?? raw.Color ?? null,
    description: raw.description ?? raw.Description ?? null,
  };
}

function mapIssueType(raw: any): TmIssueType {
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    icon: raw.icon ?? raw.Icon ?? null,
    color: raw.color ?? raw.Color ?? null,
    description: raw.description ?? raw.Description ?? null,
  };
}

function mapPriority(raw: any): TmPriority {
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    icon: raw.icon ?? raw.Icon ?? null,
    color: raw.color ?? raw.Color ?? null,
    description: raw.description ?? raw.Description ?? null,
  };
}

function mapFieldDefinition(raw: any): TmFieldDefinition {
  const cardinality = raw.cardinality ?? raw.Cardinality ?? null;
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    key: raw.key ?? raw.Key ?? '',
    label: raw.label ?? raw.Label ?? '',
    fieldType: raw.fieldType ?? raw.FieldType ?? '',
    scope: raw.scope ?? raw.Scope ?? '',
    description: raw.description ?? raw.Description ?? null,
    sortOrder: raw.sortOrder ?? raw.SortOrder ?? null,
    cardinality:
      cardinality === 'multi' || cardinality === 'Multi'
        ? 'multi'
        : cardinality === 'single' || cardinality === 'Single'
          ? 'single'
          : null,
    optionsJson: raw.optionsJson ?? raw.OptionsJson ?? null,
  };
}

export function canViewProject(project: TmProject, userId: string): boolean {
  const perm = project.permissions?.view;
  if (!perm) return true;
  const personIds = perm.personIds ?? (perm as any).PersonIds ?? [];
  const groupIds = perm.groupIds ?? (perm as any).GroupIds ?? [];
  if ((!personIds || personIds.length === 0) && (!groupIds || groupIds.length === 0)) return true;
  if (Array.isArray(personIds) && personIds.includes(userId)) return true;
  return false;
}

interface TaskManagerState {
  projects: TmProject[];
  boards: TmBoard[];
  issues: TmIssue[];
  /** issue __dataId → yorum listesi (görev detayında yüklenir) */
  commentsByIssueId: Record<string, TmIssueComment[]>;
  statuses: TmStatus[];
  issueTypes: TmIssueType[];
  priorities: TmPriority[];
  fieldDefinitions: TmFieldDefinition[];
  labels: TmLabel[];
  loading: boolean;
  error: string | null;
  currentProjectId: string | null;
  currentBoardId: string | null;
}

export const useTaskManagerStore = defineStore('taskManager', {
  state: (): TaskManagerState => ({
    projects: [],
    boards: [],
    issues: [],
    commentsByIssueId: {},
    statuses: [],
    issueTypes: [],
    priorities: [],
    fieldDefinitions: [],
    labels: [],
    loading: false,
    error: null,
    currentProjectId: null,
    currentBoardId: null,
  }),

  getters: {
    visibleProjects: (state) => state.projects,

    boardsForProject: (state) => (projectId: string) =>
      state.boards.filter((b) => b.projectId === projectId),

    issuesForProject: (state) => (projectId: string) =>
      state.issues.filter((i) => i.projectId === projectId),

    statusById: (state) => (id: string) => state.statuses.find((s) => s.__dataId === id),

    issueTypeById: (state) => (id: string) => state.issueTypes.find((t) => t.__dataId === id),

    priorityById: (state) => (id: string) => state.priorities.find((p) => p.__dataId === id),

    sortedFieldDefinitions: (state): TmFieldDefinition[] => {
      const list = [...state.fieldDefinitions];
      return list.sort((a, b) => {
        const oa = a.sortOrder ?? 999;
        const ob = b.sortOrder ?? 999;
        if (oa !== ob) return oa - ob;
        return a.label.localeCompare(b.label, undefined, { sensitivity: 'base' });
      });
    },

    defaultTaskIssueTypeId: (state): string | null => {
      const t = state.issueTypes.find((x) => x.name === 'Task');
      return t?.__dataId ?? state.issueTypes[0]?.__dataId ?? null;
    },

    firstStatusId: (state): string | null => {
      const sorted = sortTmStatusesByName(state.statuses);
      return sorted[0]?.__dataId ?? null;
    },
  },

  actions: {
    filterProjectsForUser(userId: string) {
      this.projects = this.projects.filter((p) => canViewProject(p, userId));
    },

    async loadLookups() {
      const [st, it, pr] = await Promise.all([
        tmListDataset(TM_DATASETS.statuses, { limit: 200, sort: 'name:asc' }),
        tmListDataset(TM_DATASETS.issueTypes, { limit: 100, sort: 'name:asc' }),
        tmListDataset(TM_DATASETS.priorities, { limit: 100, sort: 'name:asc' }),
      ]);
      this.statuses = (st as any[]).map(mapStatus);
      this.issueTypes = (it as any[]).map(mapIssueType);
      this.priorities = (pr as any[]).map(mapPriority);
    },

    async createStatus(payload: {
      name: string;
      description?: string | null;
      icon?: string | null;
      color?: string | null;
    }) {
      const body: Record<string, unknown> = {
        name: payload.name.trim(),
      };
      if (payload.description !== undefined && payload.description !== null && String(payload.description).trim() !== '')
        body.description = String(payload.description).trim();
      if (payload.icon != null && String(payload.icon).trim() !== '') body.icon = String(payload.icon).trim();
      if (payload.color != null && String(payload.color).trim() !== '') body.color = String(payload.color).trim();
      await tmCreate(TM_DATASETS.statuses, body);
      await this.loadLookups();
    },

    async updateStatus(
      statusId: string,
      payload: {
        name?: string;
        description?: string | null;
        icon?: string | null;
        color?: string | null;
      }
    ) {
      const body: Record<string, unknown> = {};
      if (payload.name != null) body.name = payload.name.trim();
      if (payload.description !== undefined) body.description = payload.description?.trim() || null;
      if (payload.icon !== undefined) body.icon = payload.icon?.trim() || null;
      if (payload.color !== undefined) body.color = payload.color?.trim() || null;
      await tmUpdate(TM_DATASETS.statuses, statusId, body);
      await this.loadLookups();
    },

    async deleteStatus(statusId: string) {
      await tmDelete(TM_DATASETS.statuses, statusId);
      await this.loadLookups();
    },

    async createIssueType(payload: {
      name: string;
      description?: string | null;
      icon?: string | null;
      color?: string | null;
    }) {
      const body: Record<string, unknown> = { name: payload.name.trim() };
      if (payload.description !== undefined && payload.description !== null && String(payload.description).trim() !== '')
        body.description = String(payload.description).trim();
      if (payload.icon != null && String(payload.icon).trim() !== '') body.icon = String(payload.icon).trim();
      if (payload.color != null && String(payload.color).trim() !== '') body.color = String(payload.color).trim();
      await tmCreate(TM_DATASETS.issueTypes, body);
      await this.loadLookups();
    },

    async updateIssueType(
      issueTypeId: string,
      payload: {
        name?: string;
        description?: string | null;
        icon?: string | null;
        color?: string | null;
      }
    ) {
      const body: Record<string, unknown> = {};
      if (payload.name != null) body.name = payload.name.trim();
      if (payload.description !== undefined) body.description = payload.description?.trim() || null;
      if (payload.icon !== undefined) body.icon = payload.icon?.trim() || null;
      if (payload.color !== undefined) body.color = payload.color?.trim() || null;
      await tmUpdate(TM_DATASETS.issueTypes, issueTypeId, body);
      await this.loadLookups();
    },

    async deleteIssueType(issueTypeId: string) {
      await tmDelete(TM_DATASETS.issueTypes, issueTypeId);
      await this.loadLookups();
    },

    async createPriority(payload: {
      name: string;
      description?: string | null;
      icon?: string | null;
      color?: string | null;
    }) {
      const body: Record<string, unknown> = { name: payload.name.trim() };
      if (payload.description !== undefined && payload.description !== null && String(payload.description).trim() !== '')
        body.description = String(payload.description).trim();
      if (payload.icon != null && String(payload.icon).trim() !== '') body.icon = String(payload.icon).trim();
      if (payload.color != null && String(payload.color).trim() !== '') body.color = String(payload.color).trim();
      await tmCreate(TM_DATASETS.priorities, body);
      await this.loadLookups();
    },

    async updatePriority(
      priorityId: string,
      payload: {
        name?: string;
        description?: string | null;
        icon?: string | null;
        color?: string | null;
      }
    ) {
      const body: Record<string, unknown> = {};
      if (payload.name != null) body.name = payload.name.trim();
      if (payload.description !== undefined) body.description = payload.description?.trim() || null;
      if (payload.icon !== undefined) body.icon = payload.icon?.trim() || null;
      if (payload.color !== undefined) body.color = payload.color?.trim() || null;
      await tmUpdate(TM_DATASETS.priorities, priorityId, body);
      await this.loadLookups();
    },

    async deletePriority(priorityId: string) {
      await tmDelete(TM_DATASETS.priorities, priorityId);
      await this.loadLookups();
    },

    /** Alan havuzu (`tm_field_definitions`) */
    async loadFieldDefinitions() {
      const raw = await tmListDataset(TM_DATASETS.fieldDefinitions, { limit: 200, sort: 'sortOrder:asc' });
      this.fieldDefinitions = (raw as any[]).map(mapFieldDefinition);
    },

    async createFieldDefinition(payload: {
      key: string;
      label: string;
      fieldType: string;
      scope: string;
      description?: string | null;
      sortOrder?: number | null;
      cardinality?: string | null;
      optionsJson?: string | null;
    }) {
      const body: Record<string, unknown> = {
        key: payload.key.trim(),
        label: payload.label.trim(),
        fieldType: payload.fieldType.trim(),
        scope: String(payload.scope).trim().toLowerCase(),
      };
      if (payload.description !== undefined) {
        const d = payload.description?.trim();
        body.description = d && d.length ? d : null;
      }
      if (payload.sortOrder != null && !Number.isNaN(Number(payload.sortOrder))) {
        body.sortOrder = Number(payload.sortOrder);
      }
      const card = payload.cardinality?.trim().toLowerCase();
      if (card === 'single' || card === 'multi') body.cardinality = card;
      else body.cardinality = null;
      if (payload.optionsJson !== undefined) {
        const o = payload.optionsJson?.trim();
        body.optionsJson = o && o.length ? o : null;
      }
      await tmCreate(TM_DATASETS.fieldDefinitions, body);
      await this.loadFieldDefinitions();
    },

    async updateFieldDefinition(
      dataId: string,
      payload: {
        label: string;
        fieldType: string;
        scope: string;
        description?: string | null;
        sortOrder?: number | null;
        cardinality?: string | null;
        optionsJson?: string | null;
      }
    ) {
      const body: Record<string, unknown> = {
        label: payload.label.trim(),
        fieldType: payload.fieldType.trim(),
        scope: String(payload.scope).trim().toLowerCase(),
      };
      if (payload.description !== undefined) {
        const d = payload.description?.trim();
        body.description = d && d.length ? d : null;
      }
      if (payload.sortOrder != null && !Number.isNaN(Number(payload.sortOrder))) {
        body.sortOrder = Number(payload.sortOrder);
      } else {
        body.sortOrder = null;
      }
      const card = payload.cardinality?.trim().toLowerCase();
      if (card === 'single' || card === 'multi') body.cardinality = card;
      else body.cardinality = null;
      if (payload.optionsJson !== undefined) {
        const o = payload.optionsJson?.trim();
        body.optionsJson = o && o.length ? o : null;
      }
      await tmUpdate(TM_DATASETS.fieldDefinitions, dataId, body);
      await this.loadFieldDefinitions();
    },

    async deleteFieldDefinition(dataId: string) {
      await tmDelete(TM_DATASETS.fieldDefinitions, dataId);
      await this.loadFieldDefinitions();
    },

    async loadProjects() {
      this.loading = true;
      this.error = null;
      try {
        const raw = await tmListDataset(TM_DATASETS.projects, { limit: 500, sort: 'name:asc' });
        this.projects = (raw as any[]).map(mapProject);
      } catch (e: any) {
        this.error = e.message ?? 'Projeler yüklenemedi';
        this.projects = [];
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async createProject(payload: {
      name: string;
      key: string;
      description?: string;
      lead?: string | null;
      avatarUrl?: string | null;
      permissions?: TmProjectPermissions | null;
      selections?: TmProjectSelections | null;
      workflow?: TmProjectWorkflow | null;
      useKanban?: boolean;
      issueCreateLayout?: TmIssueCreateLayout | null;
      issueProfileLayout?: TmIssueCreateLayout | null;
      issueProfileForms?: TmIssueCreateFormTemplate[] | null;
      defaultIssueProfileFormId?: string | null;
      issueCreateForms?: TmIssueCreateFormTemplate[] | null;
      defaultIssueCreateFormId?: string | null;
    }) {
      this.loading = true;
      this.error = null;
      try {
        if (!this.statuses.length) await this.loadLookups();
        const workflow = payload.workflow ?? buildDefaultWorkflow(this.statuses);
        const body: Record<string, unknown> = {
          name: payload.name.trim(),
          key: payload.key.trim().toUpperCase(),
          description: payload.description?.trim() || undefined,
          workflow,
        };
        if (payload.lead !== undefined) body.lead = payload.lead || null;
        if (payload.avatarUrl !== undefined) body.avatarUrl = payload.avatarUrl?.trim() || null;
        if (payload.permissions !== undefined) body.permissions = payload.permissions;
        if (payload.selections !== undefined) body.selections = payload.selections;
        if (payload.useKanban !== undefined) body.useKanban = payload.useKanban;
        if (payload.issueCreateLayout !== undefined) body.issueCreateLayout = payload.issueCreateLayout;
        if (payload.issueProfileLayout !== undefined) body.issueProfileLayout = payload.issueProfileLayout;
        if (payload.issueProfileForms !== undefined) body.issueProfileForms = payload.issueProfileForms;
        if (payload.defaultIssueProfileFormId !== undefined) body.defaultIssueProfileFormId = payload.defaultIssueProfileFormId;
        if (payload.issueCreateForms !== undefined) body.issueCreateForms = payload.issueCreateForms;
        if (payload.defaultIssueCreateFormId !== undefined) body.defaultIssueCreateFormId = payload.defaultIssueCreateFormId;
        const created = (await tmCreate(TM_DATASETS.projects, body)) as Record<string, unknown>;
        await this.loadProjects();
        const nested = created?.data && typeof created.data === 'object' ? (created.data as Record<string, unknown>) : null;
        let newId = rid(created?.__dataId ?? created?.DataId ?? created?.dataId ?? nested?.__dataId ?? nested?.dataId);
        if (!newId) {
          const k = payload.key.trim().toUpperCase();
          newId = this.projects.find((p) => p.key === k)?.__dataId ?? '';
        }
        return newId || null;
      } catch (e: any) {
        this.error = e.message ?? 'Proje oluşturulamadı';
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async updateProject(
      projectId: string,
      payload: {
        name?: string;
        key?: string;
        description?: string | null;
        lead?: string | null;
        avatarUrl?: string | null;
        permissions?: TmProjectPermissions | null;
        selections?: TmProjectSelections | null;
        workflow?: TmProjectWorkflow | null;
        useKanban?: boolean;
        issueCreateLayout?: TmIssueCreateLayout | null;
        issueProfileLayout?: TmIssueCreateLayout | null;
        issueProfileForms?: TmIssueCreateFormTemplate[] | null;
        defaultIssueProfileFormId?: string | null;
        issueCreateForms?: TmIssueCreateFormTemplate[] | null;
        defaultIssueCreateFormId?: string | null;
      }
    ) {
      const body: Record<string, unknown> = {};
      if (payload.name != null) body.name = payload.name.trim();
      if (payload.key != null) body.key = payload.key.trim().toUpperCase();
      if (payload.description !== undefined) body.description = payload.description?.trim() || null;
      if (payload.lead !== undefined) body.lead = payload.lead || null;
      if (payload.avatarUrl !== undefined) body.avatarUrl = payload.avatarUrl?.trim() || null;
      if (payload.permissions !== undefined) body.permissions = payload.permissions;
      if (payload.selections !== undefined) body.selections = payload.selections;
      if (payload.workflow !== undefined) body.workflow = payload.workflow;
      if (payload.useKanban !== undefined) body.useKanban = payload.useKanban;
      if (payload.issueCreateLayout !== undefined) body.issueCreateLayout = payload.issueCreateLayout;
      if (payload.issueProfileLayout !== undefined) body.issueProfileLayout = payload.issueProfileLayout;
      if (payload.issueProfileForms !== undefined) body.issueProfileForms = payload.issueProfileForms;
      if (payload.defaultIssueProfileFormId !== undefined) body.defaultIssueProfileFormId = payload.defaultIssueProfileFormId;
      if (payload.issueCreateForms !== undefined) body.issueCreateForms = payload.issueCreateForms;
      if (payload.defaultIssueCreateFormId !== undefined) body.defaultIssueCreateFormId = payload.defaultIssueCreateFormId;
      await tmUpdate(TM_DATASETS.projects, projectId, body);
      await this.loadProjects();
    },

    /** Proje ve bağlı kayıtlar (issues, boards, proje etiketleri) — sırayla silinir */
    async deleteProject(projectId: string) {
      const issueRows = await tmListDataset(TM_DATASETS.issues, {
        limit: 5000,
        filter: `projectId:eq:${projectId}`,
      });
      for (const row of issueRows as any[]) {
        const id = row.__dataId ?? row.dataId;
        if (id) await tmDelete(TM_DATASETS.issues, id);
      }
      const boardRows = await tmListDataset(TM_DATASETS.boards, {
        limit: 500,
        filter: `projectId:eq:${projectId}`,
      });
      for (const row of boardRows as any[]) {
        const id = row.__dataId ?? row.dataId;
        if (id) await tmDelete(TM_DATASETS.boards, id);
      }
      const labelRows = await tmListDataset(TM_DATASETS.labels, {
        limit: 500,
        filter: `projectId:eq:${projectId}`,
      });
      for (const row of labelRows as any[]) {
        const id = row.__dataId ?? row.dataId;
        if (id) await tmDelete(TM_DATASETS.labels, id);
      }
      await tmDelete(TM_DATASETS.projects, projectId);
      this.boards = this.boards.filter((b) => b.projectId !== projectId);
      this.issues = this.issues.filter((i) => i.projectId !== projectId);
      this.labels = this.labels.filter((l) => l.projectId !== projectId);
      await this.loadProjects();
    },

    async deleteBoard(boardId: string, projectId: string) {
      await tmDelete(TM_DATASETS.boards, boardId);
      this.boards = this.boards.filter((b) => b.__dataId !== boardId);
      await this.loadBoards(projectId);
    },

    async deleteIssue(issueId: string, projectId: string) {
      await tmDelete(TM_DATASETS.issues, issueId);
      this.issues = this.issues.filter((i) => i.__dataId !== issueId);
      await this.loadIssues(projectId);
    },

    async loadLabels(projectId: string) {
      const raw = await tmListDataset(TM_DATASETS.labels, { limit: 500, sort: 'name:asc' });
      const all = (raw as any[]).map(mapLabel);
      this.labels = all.filter((l) => l.projectId === projectId);
    },

    async createLabel(projectId: string, name: string, color?: string) {
      await tmCreate(TM_DATASETS.labels, {
        name: name.trim(),
        color: color ?? '#5eead4',
        projectId,
      });
      await this.loadLabels(projectId);
    },

    async updateLabel(labelId: string, projectId: string, payload: { name: string; color?: string | null }) {
      const cur = this.labels.find((l) => l.__dataId === labelId && l.projectId === projectId);
      if (!cur) throw new Error('Etiket bulunamadı.');
      await tmUpdate(TM_DATASETS.labels, labelId, {
        name: payload.name.trim(),
        color: payload.color !== undefined ? payload.color?.trim() || null : cur.color ?? null,
        projectId,
      });
      await this.loadLabels(projectId);
    },

    async deleteLabel(labelId: string, projectId: string) {
      await tmDelete(TM_DATASETS.labels, labelId);
      await this.loadLabels(projectId);
    },

    /** `tm_issues` tek kayıt — `__history` dahil (DG `showHistory=true`). */
    async hydrateIssueWithHistory(issueId: string) {
      if (!issueId) return null;
      const raw = await tmGetById(TM_DATASETS.issues, issueId, { showHistory: true });
      const arr = Array.isArray(raw) ? raw : raw != null ? [raw] : [];
      const doc = arr[0] as Record<string, unknown> | undefined;
      if (!doc) return null;
      const mapped = mapIssue(doc);
      const idx = this.issues.findIndex((x) => x.__dataId === mapped.__dataId);
      if (idx >= 0) this.issues[idx] = mapped;
      else this.issues.push(mapped);
      return mapped;
    },

    async loadBoards(projectId: string) {
      const filter = `projectId:eq:${projectId}`;
      const raw = await tmListDataset(TM_DATASETS.boards, { limit: 200, filter, sort: 'name:asc' });
      this.boards = (raw as any[]).map(mapBoard);
    },

    /** Tüm projelerin board'ları (workspace ağacı vb.) */
    async loadAllBoards() {
      const raw = await tmListDataset(TM_DATASETS.boards, { limit: 500, sort: 'name:asc' });
      this.boards = (raw as any[]).map(mapBoard);
    },

    async createBoard(
      projectId: string,
      name: string,
      type: string = 'kanban',
      issueCreateFormId?: string | null,
      issueProfileFormId?: string | null
    ) {
      if (!this.statuses.length) await this.loadLookups();
      const project = this.projects.find((p) => p.__dataId === projectId);
      const wf = getEffectiveWorkflow(project ?? null, this.statuses);
      const config = buildBoardConfigFromWorkflow(wf, this.statuses);
      const body: Record<string, unknown> = {
        name: name.trim(),
        projectId,
        type,
        config,
      };
      const fid = issueCreateFormId != null ? String(issueCreateFormId).trim() : '';
      if (fid) body.issueCreateFormId = fid;
      const pfid = issueProfileFormId != null ? String(issueProfileFormId).trim() : '';
      if (pfid) body.issueProfileFormId = pfid;
      await tmCreate(TM_DATASETS.boards, body);
      await this.loadBoards(projectId);
    },

    async loadBoard(boardId: string) {
      const raw = await tmGetById(TM_DATASETS.boards, boardId);
      const arr = Array.isArray(raw) ? raw : [raw];
      const b = arr[0];
      if (!b) throw new Error('Board bulunamadı');
      const mapped = mapBoard(b);
      const idx = this.boards.findIndex((x) => x.__dataId === mapped.__dataId);
      if (idx >= 0) this.boards[idx] = mapped;
      else this.boards.push(mapped);
      return mapped;
    },

    async updateBoard(
      boardId: string,
      projectId: string,
      patch: {
        name?: string;
        config?: TmBoardConfig | null;
        issueCreateFormId?: string | null;
        issueProfileFormId?: string | null;
      }
    ) {
      const b = this.boards.find((x) => x.__dataId === boardId);
      if (!b) throw new Error('Board bulunamadı');
      const body: Record<string, unknown> = {};
      if (patch.name != null) body.name = patch.name.trim();
      if (patch.config !== undefined) {
        body.config = { ...(b.config || {}), ...patch.config };
      }
      if (patch.issueCreateFormId !== undefined) {
        const v = patch.issueCreateFormId;
        body.issueCreateFormId = v != null && String(v).trim() ? String(v).trim() : null;
      }
      if (patch.issueProfileFormId !== undefined) {
        const v = patch.issueProfileFormId;
        body.issueProfileFormId = v != null && String(v).trim() ? String(v).trim() : null;
      }
      await tmUpdate(TM_DATASETS.boards, boardId, body);
      await this.loadBoards(projectId);
    },

    async loadIssues(projectId: string) {
      const filter = `projectId:eq:${projectId}`;
      const raw = await tmListDataset(TM_DATASETS.issues, { limit: 1000, filter, sort: 'order:asc' });
      this.issues = (raw as any[]).map(mapIssue);
    },

    async createIssue(payload: {
      projectId: string;
      projectKey: string;
      title: string;
      description?: string;
      statusId: string;
      issueTypeId: string;
      priorityId?: string | null;
      assignee?: string | null;
      labels?: string[] | null;
      dueDate?: string | null;
      storyPoints?: number | null;
      /** tm_issues üst düzey veya şemada tanımlı ek alanlar */
      extraFields?: Record<string, unknown>;
      /** Görev oluşturulduktan sonra eklenecek isteğe bağlı ilk yorum */
      initialComment?: string;
      /** Keycloak `sub`; yorum için gerekli */
      initialCommentAuthorId?: string | null;
    }) {
      const body: Record<string, unknown> = {
        projectKey: payload.projectKey,
        projectId: payload.projectId,
        title: payload.title.trim(),
        description: payload.description?.trim() || undefined,
        statusId: payload.statusId,
        issueTypeId: payload.issueTypeId,
      };
      if (payload.priorityId) body.priorityId = payload.priorityId;
      if (payload.assignee !== undefined && payload.assignee !== null && payload.assignee !== '')
        body.assignee = payload.assignee;
      if (payload.labels?.length) body.labels = payload.labels;
      if (payload.dueDate) body.dueDate = payload.dueDate;
      if (payload.storyPoints != null && !Number.isNaN(Number(payload.storyPoints))) {
        body.storyPoints = Number(payload.storyPoints);
      }
      if (payload.extraFields) {
        for (const [k, v] of Object.entries(payload.extraFields)) {
          if (v === undefined || v === null) continue;
          if (typeof v === 'string' && !v.trim()) continue;
          if (Array.isArray(v) && v.length === 0) continue;
          body[k] = v;
        }
      }
      const createdRes = await tmCreate(TM_DATASETS.issues, body);
      await this.loadIssues(payload.projectId);
      const newIssueId = extractCreatedRecordId(createdRes);
      const ic = String(payload.initialComment ?? '').trim();
      const authorId = String(payload.initialCommentAuthorId ?? '').trim();
      if (newIssueId && ic && authorId) {
        await this.createIssueComment({
          issueId: newIssueId,
          projectId: payload.projectId,
          authorId,
          body: ic,
        });
      }
    },

    async updateIssue(
      issueId: string,
      patch: Partial<
        Pick<
          TmIssue,
          | 'statusId'
          | 'title'
          | 'description'
          | 'order'
          | 'assignee'
          | 'priorityId'
          | 'issueTypeId'
          | 'labels'
          | 'dueDate'
          | 'storyPoints'
        >
      > & { projectId?: string; extraFields?: Record<string, unknown> },
      options?: { skipReload?: boolean }
    ) {
      const body: Record<string, unknown> = {};
      if (patch.statusId != null) body.statusId = patch.statusId;
      if (patch.title != null) body.title = patch.title;
      if (patch.description !== undefined) body.description = patch.description;
      if (patch.order !== undefined) body.order = patch.order;
      if (patch.assignee !== undefined) body.assignee = patch.assignee;
      if (patch.priorityId !== undefined) body.priorityId = patch.priorityId;
      if (patch.issueTypeId !== undefined) body.issueTypeId = patch.issueTypeId;
      if (patch.labels !== undefined) body.labels = patch.labels;
      if (patch.dueDate !== undefined) body.dueDate = patch.dueDate;
      if (patch.storyPoints !== undefined) body.storyPoints = patch.storyPoints;
      if (patch.extraFields) {
        for (const [k, v] of Object.entries(patch.extraFields)) {
          if (v === undefined || v === null) continue;
          if (typeof v === 'string' && !v.trim()) continue;
          if (Array.isArray(v) && v.length === 0) continue;
          body[k] = v;
        }
      }
      await tmUpdate(TM_DATASETS.issues, issueId, body);
      const pid = patch.projectId;
      if (pid && !options?.skipReload) {
        await this.loadIssues(pid);
        await this.hydrateIssueWithHistory(issueId);
      }
    },

    /** Issue anahtarı (örn. PROJ-0001) domain içinde tekil kabul edilir */
    async fetchIssueByKey(key: string) {
      const filter = `key:eq:${key}`;
      const raw = await tmListDataset(TM_DATASETS.issues, { limit: 10, filter });
      const list = (raw as any[]).map(mapIssue);
      return list.find((i) => i.key === key) ?? null;
    },

    async loadIssueComments(issueId: string) {
      if (!issueId) return;
      const filter = `issueId:eq:${issueId}`;
      let raw: unknown[];
      try {
        raw = (await tmListDataset(TM_DATASETS.issueComments, {
          limit: 500,
          filter,
          sort: 'createdAt:asc',
        })) as unknown[];
      } catch {
        raw = (await tmListDataset(TM_DATASETS.issueComments, { limit: 500, filter })) as unknown[];
      }
      const list = (raw as any[]).map(mapIssueComment);
      list.sort((a, b) => String(a.createdAt ?? '').localeCompare(String(b.createdAt ?? '')));
      this.commentsByIssueId = { ...this.commentsByIssueId, [issueId]: list };
    },

    async createIssueComment(payload: {
      issueId: string;
      projectId: string;
      authorId: string;
      body: string;
      parentCommentId?: string | null;
    }) {
      const body = String(payload.body ?? '').trim();
      if (!body || !payload.authorId) throw new Error('Yorum gövdesi ve yazar gerekli.');
      const iso = new Date().toISOString();
      const userStore = useUserStore();
      const sub = String(payload.authorId).trim();
      const me = userStore.getUserById(sub);
      /** DG `persons` + @users genişlemesi `foreignField: __dataId` — Keycloak `sub` yerine Keeper kullanıcı id */
      const authorRef = String(me?.id || me?.userId || sub).trim();
      const row: Record<string, unknown> = {
        issueId: payload.issueId,
        projectId: payload.projectId,
        author: authorRef,
        body,
        createdAt: iso,
      };
      if (payload.parentCommentId) row.parentCommentId = payload.parentCommentId;
      await tmCreate(TM_DATASETS.issueComments, row);
      await this.loadIssueComments(payload.issueId);
    },

    async updateIssueComment(issueId: string, commentId: string, body: string) {
      const list = this.commentsByIssueId[issueId] ?? [];
      const cur = list.find((c) => c.__dataId === commentId);
      if (!cur) throw new Error('Yorum bulunamadı.');
      const userStore = useUserStore();
      const rawId = assigneeUserId(cur.author) || (typeof cur.author === 'string' ? cur.author : '');
      const u = rawId ? userStore.getUserById(rawId) : undefined;
      const authorForDg = String(u?.id || u?.userId || rawId).trim() || rawId;
      await tmUpdate(TM_DATASETS.issueComments, commentId, {
        issueId: cur.issueId,
        projectId: cur.projectId,
        author: authorForDg || cur.author,
        body: String(body).trim(),
        ...(cur.parentCommentId ? { parentCommentId: cur.parentCommentId } : {}),
        createdAt: cur.createdAt,
        updatedAt: new Date().toISOString(),
      });
      await this.loadIssueComments(issueId);
    },

    async deleteIssueComment(issueId: string, commentId: string) {
      await tmDelete(TM_DATASETS.issueComments, commentId);
      await this.loadIssueComments(issueId);
    },

    setCurrentProject(id: string | null) {
      this.currentProjectId = id;
    },

    setCurrentBoard(id: string | null) {
      this.currentBoardId = id;
    },
  },
});
