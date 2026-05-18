import type { TmBoard, TmFieldDefinition, TmIssue, TmIssueCreateLayout, TmProject } from '@/types/apps/taskManager';
import { assigneeUserId } from '@/composables/useTaskManagerHelpers';
import { selectableBoardColumnIdsForProject } from '@/utils/boardTableColumns';
import { effectiveFieldCardinality } from '@/utils/taskManagerFieldDefinitions';

/** Yerleşik tm_issues alanları (yeni görev formu) */
export type NewIssueBuiltinFormKey =
  | 'title'
  | 'description'
  | 'issueTypeId'
  | 'priorityId'
  | 'assignee'
  | 'dueDate'
  | 'labels'
  | 'storyPoints';

export type NewIssueFormRow =
  | { kind: 'builtin'; key: NewIssueBuiltinFormKey }
  | { kind: 'extra'; definition: TmFieldDefinition };

const BUILTIN_COLUMN_TO_FORM: Partial<Record<string, NewIssueBuiltinFormKey>> = {
  title: 'title',
  description: 'description',
  issueType: 'issueTypeId',
  priority: 'priorityId',
  assignee: 'assignee',
  dueDate: 'dueDate',
  labels: 'labels',
  storyPoints: 'storyPoints',
};

/** Tablo sütun sırasına yakın form alanı sırası */
const FORM_COLUMN_ORDER = [
  'title',
  'description',
  'issueType',
  'priority',
  'assignee',
  'dueDate',
  'labels',
  'storyPoints',
];

const SKIP_IN_FORM = new Set(['key', 'status', 'order']);

export interface IssueFormModel {
  title: string;
  description: string;
  issueTypeId: string | null;
  priorityId: string | null;
  assignee: string | null;
  /** ISO veya date input (YYYY-MM-DD) */
  dueDate: string | null;
  labels: string[];
  storyPoints: number | null;
  /** Havuz / ek alanlar (tm_issues üst düzey veya şemada tanımlı) */
  extra: Record<string, unknown>;
  /** Kayıt sonrası tm_issue_comments ile eklenen isteğe bağlı ilk yorum */
  initialComment: string;
}

export function emptyIssueForm(): IssueFormModel {
  return {
    title: '',
    description: '',
    issueTypeId: null,
    priorityId: null,
    assignee: null,
    dueDate: null,
    labels: [],
    storyPoints: null,
    extra: {},
    initialComment: '',
  };
}

/** ISO / DG datetime → `YYYY-MM-DD` (date input) */
export function dueDateInputFromIso(iso: string | null | undefined): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  return d.toISOString().slice(0, 10);
}

/** Mevcut görev → yeni görev formu modeli (board/proje create layout ile aynı alan bağlama) */
export function issueToIssueFormModel(issue: TmIssue, fieldDefinitions: TmFieldDefinition[]): IssueFormModel {
  const base = emptyIssueForm();
  base.title = issue.title ?? '';
  base.description = issue.description ?? '';
  base.issueTypeId = issue.issueTypeId || null;
  base.priorityId = issue.priorityId ?? null;
  base.assignee = assigneeUserId(issue.assignee) || null;
  base.dueDate = dueDateInputFromIso(issue.dueDate);
  base.labels = issue.labels ? [...issue.labels] : [];
  base.storyPoints = issue.storyPoints ?? null;
  base.initialComment = '';

  const xf = issue.extraFields ?? {};
  const extra: Record<string, unknown> = { ...xf };
  for (const def of fieldDefinitions) {
    if (extra[def.key] === undefined) {
      extra[def.key] = defaultExtraValue(def);
    }
  }
  base.extra = extra;
  return base;
}

/** Formda kullanılabilecek sütun kimlikleri (key/status hariç), varsayılan sıra. */
export function defaultNewIssueFormColumnIds(
  project: TmProject | null | undefined,
  fieldDefinitions: TmFieldDefinition[]
): string[] {
  const allowed = new Set(selectableBoardColumnIdsForProject(project, fieldDefinitions));
  const ids: string[] = [];

  for (const colId of FORM_COLUMN_ORDER) {
    if (SKIP_IN_FORM.has(colId)) continue;
    if (!allowed.has(colId)) continue;
    if (BUILTIN_COLUMN_TO_FORM[colId]) ids.push(colId);
  }

  const builtinCols = new Set([...Object.keys(BUILTIN_COLUMN_TO_FORM), ...SKIP_IN_FORM]);
  const extras: TmFieldDefinition[] = [];
  for (const id of allowed) {
    if (builtinCols.has(id)) continue;
    const def = fieldDefinitions.find((f) => f.key === id);
    if (def) extras.push(def);
  }
  extras.sort(
    (a, b) =>
      (a.sortOrder ?? 999) - (b.sortOrder ?? 999) || a.label.localeCompare(b.label, undefined, { sensitivity: 'base' })
  );
  for (const def of extras) ids.push(def.key);

  return ids;
}

/**
 * Kayıtlı layout sırasını güncel izinli alan kümesi ile birleştirir: önce layout’taki sıra (geçerli id’ler),
 * sonra layout’ta olmayan yeni alanlar varsayılan sırayla eklenir.
 */
export function mergeIssueCreateLayoutColumnIds(
  project: TmProject | null | undefined,
  fieldDefinitions: TmFieldDefinition[],
  layout: TmIssueCreateLayout | null | undefined
): string[] {
  const defaultIds = defaultNewIssueFormColumnIds(project, fieldDefinitions);
  const layoutRows = layout?.rows;
  if (!layoutRows?.length) return defaultIds;

  const allowed = new Set(defaultIds);
  const seen = new Set<string>();
  const ordered: string[] = [];
  for (const id of layoutRows) {
    if (!id || !allowed.has(id) || seen.has(id)) continue;
    seen.add(id);
    ordered.push(id);
  }
  for (const id of defaultIds) {
    if (!seen.has(id)) ordered.push(id);
  }
  return ordered;
}

/** Yerleşik sütun kimliği için varsayılan bölüm (columnSections yokken). */
export function defaultSectionKeyForColumnId(columnId: string, fieldDefinitions: TmFieldDefinition[]): string {
  const core = new Set(['title', 'description', 'issueType', 'priority']);
  const assignment = new Set(['assignee', 'dueDate', 'storyPoints']);
  if (core.has(columnId)) return 'core';
  if (assignment.has(columnId)) return 'assignment';
  if (columnId === 'labels') return 'labels';
  if (fieldDefinitions.some((f) => f.key === columnId)) return 'extra';
  return 'extra';
}

const BUILTIN_KEY_TO_COLUMN_ID: Record<NewIssueBuiltinFormKey, string> = {
  title: 'title',
  description: 'description',
  issueTypeId: 'issueType',
  priorityId: 'priority',
  assignee: 'assignee',
  dueDate: 'dueDate',
  labels: 'labels',
  storyPoints: 'storyPoints',
};

export function columnIdForNewIssueRow(row: NewIssueFormRow): string {
  if (row.kind === 'extra') return row.definition.key;
  return BUILTIN_KEY_TO_COLUMN_ID[row.key];
}

/** Vuetify 12 sütunlu ızgarada alan genişliği; tanım yoksa 12. */
export const DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH = 560;
const MIN_ISSUE_CREATE_DIALOG_WIDTH = 360;
const MAX_ISSUE_CREATE_DIALOG_WIDTH = 1400;

/** Kayıt / API için piksel genişliği sınırla. */
export function normalizeDialogMaxWidthPx(raw: unknown): number {
  const n = typeof raw === 'number' ? raw : Number.parseInt(String(raw ?? ''), 10);
  if (!Number.isFinite(n)) return DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH;
  return Math.min(MAX_ISSUE_CREATE_DIALOG_WIDTH, Math.max(MIN_ISSUE_CREATE_DIALOG_WIDTH, Math.round(n)));
}

/** Yeni görev modalı `max-width` (piksel sayı). */
export function issueCreateDialogMaxWidth(layout: TmIssueCreateLayout | null | undefined): number {
  if (layout?.dialogMaxWidth == null) return DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH;
  return normalizeDialogMaxWidthPx(layout.dialogMaxWidth);
}

/** Alan sırasına göre bölüm anahtarlarının ilk görünüş sırası. */
export function naturalSectionOrderFromLayout(
  orderedColumnIds: string[],
  columnSections: Record<string, string> | null | undefined,
  fieldDefinitions: TmFieldDefinition[]
): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const colId of orderedColumnIds) {
    const fromLayout = columnSections?.[colId];
    const key =
      fromLayout != null && String(fromLayout).trim() !== ''
        ? String(fromLayout).trim()
        : defaultSectionKeyForColumnId(colId, fieldDefinitions);
    if (seen.has(key)) continue;
    seen.add(key);
    out.push(key);
  }
  return out;
}

/** Bölüm dış bloğu için 12 sütunlu ızgara span. */
export function sectionColSpanFor(sectionKey: string, layout: TmIssueCreateLayout | null | undefined): number {
  const raw = layout?.sectionCols?.[sectionKey];
  const n = typeof raw === 'number' ? raw : Number.parseInt(String(raw ?? ''), 10);
  if (!Number.isFinite(n) || n < 1) return 12;
  return Math.min(12, Math.max(1, Math.round(n)));
}

export function fieldColSpanFor(columnId: string, layout: TmIssueCreateLayout | null | undefined): number {
  const raw = layout?.fieldCols?.[columnId];
  const n = typeof raw === 'number' ? raw : Number.parseInt(String(raw ?? ''), 10);
  if (!Number.isFinite(n) || n < 1) return 12;
  return Math.min(12, Math.max(1, Math.round(n)));
}

/**
 * Board → proje varsayılanı → ilk şablon → legacy issueCreateLayout sırasıyla etkin form düzeni.
 */
/**
 * Profil tam ekran — board şablonu → proje varsayılanı → ilk şablon → `issueProfileLayout` sırası.
 * Çağıran, null dönerse etkin yeni görev formu layout’una düşebilir.
 */
export function resolveEffectiveIssueProfileLayout(
  project: TmProject | null | undefined,
  board: TmBoard | null | undefined
): TmIssueCreateLayout | null {
  const forms = project?.issueProfileForms;
  const pickById = (id: string | null | undefined): TmIssueCreateLayout | null => {
    if (!id || !forms?.length) return null;
    const hit = forms.find((f) => f.id === id);
    const lay = hit?.layout;
    return lay?.rows?.length ? lay : null;
  };

  const fromBoard = pickById(board?.issueProfileFormId ?? undefined);
  if (fromBoard) return fromBoard;

  const fromDefault = pickById(project?.defaultIssueProfileFormId ?? undefined);
  if (fromDefault) return fromDefault;

  if (forms?.length) {
    const first = forms[0]?.layout;
    if (first?.rows?.length) return first;
  }

  const legacy = project?.issueProfileLayout;
  if (legacy?.rows?.length) return legacy;
  return null;
}

export function resolveEffectiveIssueCreateLayout(
  project: TmProject | null | undefined,
  board: TmBoard | null | undefined
): TmIssueCreateLayout | null {
  const forms = project?.issueCreateForms;
  const pickById = (id: string | null | undefined): TmIssueCreateLayout | null => {
    if (!id || !forms?.length) return null;
    const hit = forms.find((f) => f.id === id);
    const lay = hit?.layout;
    return lay?.rows?.length ? lay : null;
  };

  const fromBoard = pickById(board?.issueCreateFormId ?? undefined);
  if (fromBoard) return fromBoard;

  const fromDefault = pickById(project?.defaultIssueCreateFormId ?? undefined);
  if (fromDefault) return fromDefault;

  if (forms?.length) {
    const first = forms[0]?.layout;
    if (first?.rows?.length) return first;
  }

  const legacy = project?.issueCreateLayout;
  if (legacy?.rows?.length) return legacy;
  return null;
}

/**
 * Sıralı sütun listesi için bölüm haritası: önce taslak (düzenleyici), sonra kayıtlı layout, sonra varsayılan.
 */
export function mergeIssueCreateLayoutColumnSections(
  fieldDefinitions: TmFieldDefinition[],
  orderedColumnIds: string[],
  draft: Record<string, string> | null | undefined,
  savedLayout: TmIssueCreateLayout | null | undefined
): Record<string, string> {
  const out: Record<string, string> = {};
  const saved = savedLayout?.columnSections ?? {};
  const d = draft ?? {};
  for (const id of orderedColumnIds) {
    if (d[id] != null && String(d[id]).trim() !== '') {
      out[id] = String(d[id]).trim();
    } else if (saved[id] != null && String(saved[id]).trim() !== '') {
      out[id] = String(saved[id]).trim();
    } else {
      out[id] = defaultSectionKeyForColumnId(id, fieldDefinitions);
    }
  }
  return out;
}

function columnIdsToRows(ids: string[], fieldDefinitions: TmFieldDefinition[]): NewIssueFormRow[] {
  const rows: NewIssueFormRow[] = [];
  for (const colId of ids) {
    const formKey = BUILTIN_COLUMN_TO_FORM[colId];
    if (formKey) {
      rows.push({ kind: 'builtin', key: formKey });
      continue;
    }
    const def = fieldDefinitions.find((f) => f.key === colId);
    if (def) rows.push({ kind: 'extra', definition: def });
  }
  return rows;
}

/**
 * Proje seçimleri + alan havuzu ile uyumlu yeni görev formu satırları.
 * Durum / anahtar otomatik olduğu için listede yok.
 * @param layoutOverride — verilmezse proje kaydındaki `issueCreateLayout` kullanılır.
 */
export function resolveNewIssueFormRows(
  project: TmProject | null | undefined,
  fieldDefinitions: TmFieldDefinition[],
  layoutOverride?: TmIssueCreateLayout | null
): NewIssueFormRow[] {
  const layout = layoutOverride !== undefined ? layoutOverride : project?.issueCreateLayout ?? null;
  const ids = mergeIssueCreateLayoutColumnIds(project, fieldDefinitions, layout);
  return columnIdsToRows(ids, fieldDefinitions);
}

/** date input → ISO benzeri (DG datetime) */
export function normalizeDueDateInput(raw: string | null | undefined): string | null {
  if (raw == null || !String(raw).trim()) return null;
  const s = String(raw).trim();
  if (/^\d{4}-\d{2}-\d{2}$/.test(s)) return `${s}T00:00:00.000Z`;
  return s;
}

/** Boş ek alanları create isteğinden çıkarır */
export function pruneIssueExtraFields(extra: Record<string, unknown>): Record<string, unknown> | undefined {
  const out: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(extra)) {
    if (v === undefined || v === null) continue;
    if (typeof v === 'string' && !v.trim()) continue;
    if (Array.isArray(v) && v.length === 0) continue;
    out[k] = v;
  }
  return Object.keys(out).length ? out : undefined;
}

export function defaultExtraValue(def: TmFieldDefinition): unknown {
  const ft = (def.fieldType || '').toLowerCase();
  if (ft === 'number') return null;
  if (ft === 'bool' || ft === 'boolean') return false;
  const multi = effectiveFieldCardinality(def) === 'multi';
  if (ft === 'tags' || ft === 'array' || ft === 'relation' || ft === 'persons' || ft === 'person' || ft === 'group') {
    return multi ? [] : null;
  }
  return '';
}
