import type {
  TmBoard,
  TmFieldDefinition,
  TmIssue,
  TmIssueType,
  TmLabel,
  TmPriority,
  TmProject,
  TmStatus,
} from '@/types/apps/taskManager';
import { assigneeDisplayLabel } from '@/composables/useTaskManagerHelpers';
import { stripHtmlToPlainText } from '@/utils/htmlPlainText';

export const DEFAULT_BOARD_TABLE_COLUMN_IDS = ['key', 'title', 'status', 'priority', 'assignee', 'dueDate'] as const;

/** Tablo sütun kimliği: yerleşik anahtar veya tm_issues / havuz alan anahtarı */
export type BoardTableColumnId = string;

/**
 * Bu projede tabloya eklenebilecek sütun kimlikleri:
 * - çekirdek: key, title, status, assignee, dueDate
 * - öncelik / tip: proje selections’ta ilgili havuz seçiliyse
 * - storyPoints, labels, description: yalnızca proje alan seçiminde (fieldKeys) varsa
 * - diğer havuz alanları: selections.fieldKeys içinde ve tanımda olanlar
 */
export function selectableBoardColumnIdsForProject(
  project: TmProject | null | undefined,
  fieldDefinitions: TmFieldDefinition[]
): BoardTableColumnId[] {
  const set = new Set<string>();
  const sel = project?.selections;

  (['key', 'title', 'status', 'assignee', 'dueDate'] as const).forEach((k) => set.add(k));

  const pri = sel?.priorityIds;
  if (!sel || pri == null || pri.length > 0) {
    set.add('priority');
  }
  const ity = sel?.issueTypeIds;
  if (!sel || ity == null || ity.length > 0) {
    set.add('issueType');
  }

  const fieldKeys = sel?.fieldKeys ?? [];
  for (const fk of fieldKeys) {
    if (!fk) continue;
    if (BUILTIN_I18N[fk]) {
      set.add(fk);
      continue;
    }
    if (fieldDefinitions.some((f) => f.key === fk)) {
      set.add(fk);
    }
  }

  const order = [
    'key',
    'title',
    'status',
    'priority',
    'issueType',
    'assignee',
    'dueDate',
    'storyPoints',
    'labels',
    'description',
  ];
  return [...set].sort((a, b) => {
    const ai = order.indexOf(a);
    const bi = order.indexOf(b);
    if (ai !== -1 && bi !== -1) return ai - bi;
    if (ai !== -1) return -1;
    if (bi !== -1) return 1;
    return a.localeCompare(b, undefined, { sensitivity: 'base' });
  });
}

/** Board’da tableColumns yoksa kullanılacak varsayılan (projeye izin verilenlerle kesişim) */
export function defaultBoardTableColumnIdsForProject(
  project: TmProject | null | undefined,
  fieldDefinitions: TmFieldDefinition[]
): BoardTableColumnId[] {
  const allowed = selectableBoardColumnIdsForProject(project, fieldDefinitions);
  const allowedSet = new Set(allowed);
  const preferred: BoardTableColumnId[] = [
    'key',
    'title',
    'status',
    'priority',
    'assignee',
    'dueDate',
    'issueType',
  ];
  const out = preferred.filter((id) => allowedSet.has(id));
  if (out.length) return out;
  return allowed.length ? allowed : ['key', 'title', 'status', 'assignee', 'dueDate'].filter((id) => allowedSet.has(id));
}

export function resolveBoardTableColumnIds(
  board: TmBoard | null | undefined,
  project: TmProject | null | undefined,
  fieldDefinitions: TmFieldDefinition[]
): BoardTableColumnId[] {
  const allowed = new Set(selectableBoardColumnIdsForProject(project, fieldDefinitions));
  const raw = board?.config?.tableColumns;
  if (Array.isArray(raw) && raw.length > 0) {
    const list = raw.map((x) => String(x).trim()).filter(Boolean).filter((id) => allowed.has(id));
    if (list.length) return list;
  }
  return defaultBoardTableColumnIdsForProject(project, fieldDefinitions);
}

function formatDue(iso: string | null | undefined): string {
  if (!iso) return '—';
  try {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '—';
    return d.toLocaleDateString();
  } catch {
    return '—';
  }
}

export type BoardTableColumnTitleFn = (columnId: string) => string;

/** Yerleşik sütun başlıkları i18n anahtarı → fallback (mt ile çağrılır) */
const BUILTIN_I18N: Record<string, { key: string; fallback: string }> = {
  key: { key: 'taskManager.workspaceTableKey', fallback: 'Anahtar' },
  title: { key: 'taskManager.issueTitle', fallback: 'Başlık' },
  status: { key: 'taskManager.status', fallback: 'Durum' },
  priority: { key: 'taskManager.priority', fallback: 'Öncelik' },
  assignee: { key: 'taskManager.assignee', fallback: 'Atanan' },
  issueType: { key: 'taskManager.issueType', fallback: 'Görev tipi' },
  dueDate: { key: 'taskManager.dueDate', fallback: 'Bitiş tarihi' },
  storyPoints: { key: 'taskManager.storyPoints', fallback: 'Story point' },
  labels: { key: 'taskManager.labels', fallback: 'Etiketler' },
  description: { key: 'taskManager.description', fallback: 'Açıklama' },
};

export function boardTableColumnTitle(
  columnId: string,
  fieldDefinitions: TmFieldDefinition[],
  mt: (key: string, fallback: string) => string
): string {
  const bi = BUILTIN_I18N[columnId];
  if (bi) return mt(bi.key, bi.fallback);
  const fd = fieldDefinitions.find((f) => f.key === columnId);
  return fd?.label ?? columnId;
}

export interface BoardTableCellContext {
  store: {
    statusById: (id: string) => TmStatus | undefined;
    priorities: TmPriority[];
    issueTypes: TmIssueType[];
  };
  userStore: { getUserById: (id: string) => { firstName?: string; lastName?: string; username?: string } | undefined };
  labels: TmLabel[];
}

function labelsDisplay(issue: TmIssue, labels: TmLabel[]): string {
  const ids = issue.labels;
  if (!ids?.length) return '—';
  const byId = new Map(labels.map((l) => [l.__dataId, l.name]));
  return ids.map((id) => byId.get(id) ?? id.slice(0, 6)).join(', ') || '—';
}

function extraFieldValue(issue: TmIssue, key: string): string {
  const v = issue.extraFields?.[key];
  if (v == null || v === '') return '—';
  if (typeof v === 'object') return JSON.stringify(v);
  return String(v);
}

export function boardTableCellValue(issue: TmIssue, columnId: string, ctx: BoardTableCellContext): string {
  const { store, userStore, labels } = ctx;
  switch (columnId) {
    case 'key':
      return issue.key ?? '—';
    case 'title':
      return issue.title ?? '—';
    case 'status':
      return store.statusById(issue.statusId)?.name ?? '—';
    case 'priority':
      return store.priorities.find((p) => p.__dataId === issue.priorityId)?.name ?? '—';
    case 'assignee':
      return assigneeDisplayLabel(issue.assignee, (id) => userStore.getUserById(id));
    case 'issueType':
      return store.issueTypes.find((t) => t.__dataId === issue.issueTypeId)?.name ?? '—';
    case 'dueDate':
      return formatDue(issue.dueDate);
    case 'storyPoints':
      return issue.storyPoints != null ? String(issue.storyPoints) : '—';
    case 'labels':
      return labelsDisplay(issue, labels);
    case 'description': {
      const d = stripHtmlToPlainText(issue.description ?? '').trim();
      if (!d) return '—';
      return d.length > 120 ? `${d.slice(0, 117)}…` : d;
    }
    default:
      return extraFieldValue(issue, columnId);
  }
}

export function buildBoardTableRow(
  issue: TmIssue,
  columnIds: BoardTableColumnId[],
  ctx: BoardTableCellContext
): Record<string, unknown> {
  const row: Record<string, unknown> = {
    __issueId: issue.__dataId,
    key: issue.key,
  };
  for (const col of columnIds) {
    row[col] = boardTableCellValue(issue, col, ctx);
  }
  return row;
}

