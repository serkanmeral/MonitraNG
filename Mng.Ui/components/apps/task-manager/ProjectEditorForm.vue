<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import ProjectWorkflowEditor from '@/components/apps/task-manager/ProjectWorkflowEditor.vue';
import ProjectIssueCreateLayoutEditor from '@/components/apps/task-manager/ProjectIssueCreateLayoutEditor.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';
import { useGroupStore } from '@/stores/apps/group';
import type {
  TmProject,
  TmProjectPermissions,
  TmProjectSelections,
  TmProjectWorkflow,
  TmIssueCreateLayout,
  TmIssueCreateFormTemplate,
  TmFieldDefinition,
} from '@/types/apps/taskManager';
import {
  DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH,
  defaultNewIssueFormColumnIds,
  issueCreateDialogMaxWidth,
  mergeIssueCreateLayoutColumnIds,
  mergeIssueCreateLayoutColumnSections,
  naturalSectionOrderFromLayout,
  normalizeDialogMaxWidthPx,
} from '@/utils/taskManagerNewIssueForm';
import { buildDefaultWorkflow, getEffectiveWorkflow, normalizeWorkflow } from '@/utils/taskManagerWorkflow';

const props = defineProps<{
  mode: 'new' | 'edit';
  /** edit modunda zorunlu */
  projectId?: string;
}>();

const route = useRoute();
const router = useRouter();
const store = useTaskManagerStore();
const auth = useAuthStore();
const userStore = useUserStore();
const groupStore = useGroupStore();

const canEditIssueCreateLayout = computed(() => auth.isManager);

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

function rid(v: unknown): string {
  if (v == null) return '';
  if (typeof v === 'string') return v;
  if (typeof v === 'object' && v !== null && ('__dataId' in v || 'dataId' in v))
    return String((v as { __dataId?: string; dataId?: string }).__dataId ?? (v as { dataId?: string }).dataId ?? '');
  return String(v);
}

function leadUserId(lead: unknown): string | null {
  if (lead == null) return null;
  if (typeof lead === 'string') return lead;
  if (typeof lead === 'object' && lead !== null) {
    const o = lead as Record<string, unknown>;
    const x = rid(o.__dataId ?? o.userId ?? o.id);
    return x || null;
  }
  return null;
}

const emptyPermissions = (): TmProjectPermissions => ({
  view: { personIds: [], groupIds: [] },
  edit: { personIds: [], groupIds: [] },
  admin: { personIds: [], groupIds: [] },
});

const activeTab = ref('general');
watch(activeTab, (v) => {
  router.replace({ query: { ...route.query, tab: v } });
});

const loading = ref(true);
const saving = ref(false);
const errorMsg = ref<string | null>(null);

const general = ref({
  name: '',
  key: '',
  description: '',
  lead: null as string | null,
  avatarUrl: '' as string | null,
  useKanban: true,
});

const workflowDraft = ref<TmProjectWorkflow | null>(null);

const selections = ref<TmProjectSelections>({
  priorityIds: [],
  issueTypeIds: [],
  fieldKeys: [],
});

const permissions = ref<TmProjectPermissions>(emptyPermissions());

interface IssueFormDraft {
  id: string;
  name: string;
  rows: string[];
  columnSections: Record<string, string>;
  formHeading: string;
  formIntro: string;
  sectionTitles: Record<string, string>;
  fieldCols: Record<string, number>;
  dialogMaxWidth: number;
  sectionOrder: string[];
  sectionCols: Record<string, number>;
}

function newFormDraftId(): string {
  return `tm-form-${Math.random().toString(36).slice(2, 11)}`;
}

/** Yalnızca manager — çoklu yeni görev formu taslakları */
const issueFormDrafts = ref<IssueFormDraft[]>([]);
const activeIssueFormId = ref('');
const defaultIssueCreateFormId = ref('');

const activeDraftIndex = computed(() => issueFormDrafts.value.findIndex((x) => x.id === activeIssueFormId.value));

const project = computed(() =>
  props.mode === 'edit' && props.projectId ? store.projects.find((p) => p.__dataId === props.projectId) : null
);

/** Proje düzenlemede gerçek proje; yeni proje sihirbazında seçimlere göre sentetik proje (layout önizlemesi için) */
const projectForLayout = computed((): TmProject | null => {
  if (props.mode === 'edit' && project.value) return project.value;
  return {
    __dataId: '',
    name: general.value.name,
    key: general.value.key,
    selections: {
      priorityIds: selections.value.priorityIds,
      issueTypeIds: selections.value.issueTypeIds,
      fieldKeys: selections.value.fieldKeys,
    },
    useKanban: general.value.useKanban,
  };
});

function hydrateIssueForms() {
  const p = projectForLayout.value;
  if (!p || !store.fieldDefinitions.length) return;

  const drafts: IssueFormDraft[] = [];

  if (props.mode === 'edit' && project.value?.issueCreateForms?.length) {
    for (const f of project.value.issueCreateForms) {
      const merged = mergeIssueCreateLayoutColumnIds(p, store.fieldDefinitions, f.layout);
      const sections = mergeIssueCreateLayoutColumnSections(store.fieldDefinitions, merged, null, f.layout);
      drafts.push({
        id: f.id,
        name: f.name,
        rows: merged,
        columnSections: sections,
        formHeading: f.layout.formHeading ?? '',
        formIntro: f.layout.formIntro ?? '',
        sectionTitles: { ...(f.layout.sectionTitles ?? {}) },
        fieldCols: { ...(f.layout.fieldCols ?? {}) },
        dialogMaxWidth: issueCreateDialogMaxWidth(f.layout),
        sectionOrder: [...(f.layout.sectionOrder ?? [])],
        sectionCols: { ...(f.layout.sectionCols ?? {}) },
      });
    }
  } else {
    const icl = props.mode === 'edit' ? project.value?.issueCreateLayout : null;
    const merged = mergeIssueCreateLayoutColumnIds(p, store.fieldDefinitions, icl ?? undefined);
    const sections = mergeIssueCreateLayoutColumnSections(
      store.fieldDefinitions,
      merged,
      null,
      icl ?? null
    );
    drafts.push({
      id: props.mode === 'edit' && icl?.rows?.length ? 'legacy' : newFormDraftId(),
      name: mt('taskManager.editorIssueFormDefaultName', 'Varsayılan form'),
      rows: merged,
      columnSections: sections,
      formHeading: icl?.formHeading ?? '',
      formIntro: icl?.formIntro ?? '',
      sectionTitles: { ...(icl?.sectionTitles ?? {}) },
      fieldCols: { ...(icl?.fieldCols ?? {}) },
      dialogMaxWidth: issueCreateDialogMaxWidth(icl ?? undefined),
      sectionOrder: [...(icl?.sectionOrder ?? [])],
      sectionCols: { ...(icl?.sectionCols ?? {}) },
    });
  }

  issueFormDrafts.value = drafts;
  let def = props.mode === 'edit' ? project.value?.defaultIssueCreateFormId : null;
  if (!def || !drafts.find((d) => d.id === def)) def = drafts[0]?.id ?? '';
  defaultIssueCreateFormId.value = def;
  if (!activeIssueFormId.value || !drafts.find((d) => d.id === activeIssueFormId.value)) {
    activeIssueFormId.value = def;
  }
  hydrateIssueProfileForms();
}

/** Görev profil (tam ekran) — çoklu şablon; `issueProfileForms` / `issueProfileLayout` */
const issueProfileFormDrafts = ref<IssueFormDraft[]>([]);
const activeIssueProfileFormId = ref('');
const defaultIssueProfileFormId = ref('');

const activeProfileDraftIndex = computed(() =>
  issueProfileFormDrafts.value.findIndex((x) => x.id === activeIssueProfileFormId.value)
);

function hydrateIssueProfileForms() {
  const p = projectForLayout.value;
  if (!p || !store.fieldDefinitions.length) {
    issueProfileFormDrafts.value = [];
    activeIssueProfileFormId.value = '';
    defaultIssueProfileFormId.value = '';
    return;
  }

  const drafts: IssueFormDraft[] = [];

  if (props.mode === 'edit' && project.value?.issueProfileForms?.length) {
    for (const f of project.value.issueProfileForms) {
      const merged = mergeIssueCreateLayoutColumnIds(p, store.fieldDefinitions, f.layout);
      const sections = mergeIssueCreateLayoutColumnSections(store.fieldDefinitions, merged, null, f.layout);
      drafts.push({
        id: f.id,
        name: f.name,
        rows: merged,
        columnSections: sections,
        formHeading: f.layout.formHeading ?? '',
        formIntro: f.layout.formIntro ?? '',
        sectionTitles: { ...(f.layout.sectionTitles ?? {}) },
        fieldCols: { ...(f.layout.fieldCols ?? {}) },
        dialogMaxWidth: issueCreateDialogMaxWidth(f.layout),
        sectionOrder: [...(f.layout.sectionOrder ?? [])],
        sectionCols: { ...(f.layout.sectionCols ?? {}) },
      });
    }
  } else {
    const icl = props.mode === 'edit' ? project.value?.issueProfileLayout : null;
    const merged = mergeIssueCreateLayoutColumnIds(p, store.fieldDefinitions, icl ?? undefined);
    const sections = mergeIssueCreateLayoutColumnSections(store.fieldDefinitions, merged, null, icl ?? null);
    drafts.push({
      id: props.mode === 'edit' && icl?.rows?.length ? 'legacy' : newFormDraftId(),
      name: mt('taskManager.editorIssueProfileFormDefaultName', 'Varsayılan profil'),
      rows: merged,
      columnSections: sections,
      formHeading: icl?.formHeading ?? '',
      formIntro: icl?.formIntro ?? '',
      sectionTitles: { ...(icl?.sectionTitles ?? {}) },
      fieldCols: { ...(icl?.fieldCols ?? {}) },
      dialogMaxWidth: issueCreateDialogMaxWidth(icl ?? undefined),
      sectionOrder: [...(icl?.sectionOrder ?? [])],
      sectionCols: { ...(icl?.sectionCols ?? {}) },
    });
  }

  issueProfileFormDrafts.value = drafts;
  let def = props.mode === 'edit' ? project.value?.defaultIssueProfileFormId : null;
  if (!def || !drafts.find((d) => d.id === def)) def = drafts[0]?.id ?? '';
  defaultIssueProfileFormId.value = def;
  if (!activeIssueProfileFormId.value || !drafts.find((d) => d.id === activeIssueProfileFormId.value)) {
    activeIssueProfileFormId.value = def;
  }
}

function addIssueProfileFormDraft() {
  const p = projectForLayout.value;
  if (!p || !store.fieldDefinitions.length) return;
  const rows = defaultNewIssueFormColumnIds(p, store.fieldDefinitions);
  const sections = mergeIssueCreateLayoutColumnSections(store.fieldDefinitions, rows, null, null);
  const id = newFormDraftId();
  issueProfileFormDrafts.value.push({
    id,
    name: mt('taskManager.editorIssueProfileFormNewName', 'Yeni profil şablonu'),
    rows,
    columnSections: sections,
    formHeading: '',
    formIntro: '',
    sectionTitles: {},
    fieldCols: {},
    dialogMaxWidth: DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH,
    sectionOrder: [],
    sectionCols: {},
  });
  activeIssueProfileFormId.value = id;
}

function removeActiveIssueProfileFormDraft() {
  if (issueProfileFormDrafts.value.length <= 1) return;
  const id = activeIssueProfileFormId.value;
  const idx = issueProfileFormDrafts.value.findIndex((x) => x.id === id);
  if (idx < 0) return;
  issueProfileFormDrafts.value.splice(idx, 1);
  if (defaultIssueProfileFormId.value === id) {
    defaultIssueProfileFormId.value = issueProfileFormDrafts.value[0]!.id;
  }
  activeIssueProfileFormId.value = issueProfileFormDrafts.value[Math.max(0, idx - 1)]?.id ?? issueProfileFormDrafts.value[0]!.id;
}

function addIssueFormDraft() {
  const p = projectForLayout.value;
  if (!p || !store.fieldDefinitions.length) return;
  const rows = defaultNewIssueFormColumnIds(p, store.fieldDefinitions);
  const sections = mergeIssueCreateLayoutColumnSections(store.fieldDefinitions, rows, null, null);
  const id = newFormDraftId();
  issueFormDrafts.value.push({
    id,
    name: mt('taskManager.editorIssueFormNewName', 'Yeni form'),
    rows,
    columnSections: sections,
    formHeading: '',
    formIntro: '',
    sectionTitles: {},
    fieldCols: {},
    dialogMaxWidth: DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH,
    sectionOrder: [],
    sectionCols: {},
  });
  activeIssueFormId.value = id;
}

function removeActiveIssueFormDraft() {
  if (issueFormDrafts.value.length <= 1) return;
  const id = activeIssueFormId.value;
  const idx = issueFormDrafts.value.findIndex((x) => x.id === id);
  if (idx < 0) return;
  issueFormDrafts.value.splice(idx, 1);
  if (defaultIssueCreateFormId.value === id) {
    defaultIssueCreateFormId.value = issueFormDrafts.value[0]!.id;
  }
  activeIssueFormId.value = issueFormDrafts.value[Math.max(0, idx - 1)]?.id ?? issueFormDrafts.value[0]!.id;
}

function buildLayoutPayloadFromDraft(d: IssueFormDraft, p: TmProject): TmIssueCreateLayout {
  const rows = mergeIssueCreateLayoutColumnIds(p, store.fieldDefinitions, { rows: d.rows });
  const columnSections = mergeIssueCreateLayoutColumnSections(
    store.fieldDefinitions,
    rows,
    d.columnSections,
    null
  );
  const base: TmIssueCreateLayout = { rows, columnSections };
  const fh = d.formHeading.trim();
  const fi = d.formIntro.trim();
  if (fh) base.formHeading = fh;
  if (fi) base.formIntro = fi;
  const st = { ...d.sectionTitles };
  for (const k of Object.keys(st)) {
    if (!String(st[k]).trim()) delete st[k];
  }
  if (Object.keys(st).length) base.sectionTitles = st;
  const fc = { ...d.fieldCols };
  for (const k of Object.keys(fc)) {
    const n = fc[k];
    if (!Number.isFinite(n) || n >= 12) delete fc[k];
  }
  if (Object.keys(fc).length) base.fieldCols = fc;
  const dw = normalizeDialogMaxWidthPx(d.dialogMaxWidth);
  if (dw !== DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH) base.dialogMaxWidth = dw;
  const secKeys = new Set(
    naturalSectionOrderFromLayout(rows, columnSections, store.fieldDefinitions)
  );
  if (d.sectionOrder?.length) {
    const ordered = d.sectionOrder.filter((k) => secKeys.has(k));
    for (const k of secKeys) {
      if (!ordered.includes(k)) ordered.push(k);
    }
    base.sectionOrder = ordered;
  }
  const scOut: Record<string, number> = {};
  for (const [k, raw] of Object.entries(d.sectionCols ?? {})) {
    if (!secKeys.has(k)) continue;
    const n = typeof raw === 'number' ? raw : Number(raw);
    if (Number.isFinite(n) && n >= 1 && n < 12) scOut[k] = Math.round(n);
  }
  if (Object.keys(scOut).length) base.sectionCols = scOut;
  return base;
}

watch(
  () => [selections.value.priorityIds, selections.value.issueTypeIds, selections.value.fieldKeys, activeIssueFormId.value],
  () => {
    const p = projectForLayout.value;
    const d = issueFormDrafts.value.find((x) => x.id === activeIssueFormId.value);
    if (!p || !store.fieldDefinitions.length || !d) return;
    const prevSections = { ...d.columnSections };
    const curLayout: TmIssueCreateLayout = {
      rows: d.rows,
      columnSections: d.columnSections,
      sectionTitles: d.sectionTitles,
      formHeading: d.formHeading,
      formIntro: d.formIntro,
      fieldCols: d.fieldCols,
      dialogMaxWidth: d.dialogMaxWidth,
      sectionOrder: d.sectionOrder,
      sectionCols: d.sectionCols,
    };
    d.rows = mergeIssueCreateLayoutColumnIds(p, store.fieldDefinitions, { rows: d.rows });
    d.columnSections = mergeIssueCreateLayoutColumnSections(
      store.fieldDefinitions,
      d.rows,
      prevSections,
      curLayout
    );
  },
  { deep: true }
);

watch(
  () => store.fieldDefinitions.length,
  (n) => {
    if (n > 0 && projectForLayout.value) hydrateIssueForms();
  }
);

watch(
  () => [selections.value.priorityIds, selections.value.issueTypeIds, selections.value.fieldKeys],
  () => {
    const p = projectForLayout.value;
    if (!p || !store.fieldDefinitions.length || !issueProfileFormDrafts.value.length) return;
    for (const d of issueProfileFormDrafts.value) {
      const prevSections = { ...d.columnSections };
      const curLayout: TmIssueCreateLayout = {
        rows: d.rows,
        columnSections: d.columnSections,
        sectionTitles: d.sectionTitles,
        formHeading: d.formHeading,
        formIntro: d.formIntro,
        fieldCols: d.fieldCols,
        dialogMaxWidth: d.dialogMaxWidth,
        sectionOrder: d.sectionOrder,
        sectionCols: d.sectionCols,
      };
      d.rows = mergeIssueCreateLayoutColumnIds(p, store.fieldDefinitions, { rows: d.rows });
      d.columnSections = mergeIssueCreateLayoutColumnSections(
        store.fieldDefinitions,
        d.rows,
        prevSections,
        curLayout
      );
    }
  },
  { deep: true }
);

const userItems = computed(() =>
  userStore.users.map((u) => ({
    title: `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.username || u.id,
    value: u.id || u.userId,
    subtitle: u.email ?? '',
  }))
);

/** Yeni görev formu önizlemesi — seçimlerle süzülmüş listeler */
const previewIssueTypeItems = computed(() => {
  const p = projectForLayout.value;
  if (!p) return [];
  const ids = p.selections?.issueTypeIds;
  let list = store.issueTypes;
  if (ids?.length) list = list.filter((t) => ids.includes(t.__dataId));
  return list.map((t) => ({ title: t.name, value: t.__dataId }));
});

const previewPriorityItems = computed(() => {
  const p = projectForLayout.value;
  if (!p) return [];
  const ids = p.selections?.priorityIds;
  let list = store.priorities;
  if (ids?.length) list = list.filter((x) => ids.includes(x.__dataId));
  return list.map((x) => ({ title: x.name, value: x.__dataId }));
});

const previewLabelItems = computed(() => {
  const pid = project.value?.__dataId ?? '';
  return store.labels
    .filter((l) => l.projectId === pid)
    .map((l) => ({ title: l.name, value: l.__dataId }));
});

const previewUserItems = computed(() =>
  userStore.users.map((u) => ({
    title: `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.username || u.id,
    value: u.id || u.userId,
  }))
);

const groupItems = computed(() =>
  (groupStore.groups || []).map((g) => ({
    title: g.name,
    value: g.id || g.groupId,
  }))
);

function hydrateFromProject() {
  const p = project.value;
  if (!p) return;
  general.value = {
    name: p.name,
    key: p.key,
    description: p.description ?? '',
    lead: leadUserId(p.lead),
    avatarUrl: p.avatarUrl ?? '',
    useKanban: p.useKanban !== false,
  };
  workflowDraft.value = JSON.parse(JSON.stringify(getEffectiveWorkflow(p, store.statuses))) as TmProjectWorkflow;
  const sel = p.selections;
  selections.value = {
    priorityIds: sel?.priorityIds?.length ? [...sel.priorityIds] : store.priorities.map((x) => x.__dataId),
    issueTypeIds: sel?.issueTypeIds?.length ? [...sel.issueTypeIds] : store.issueTypes.map((x) => x.__dataId),
    fieldKeys:
      sel?.fieldKeys?.length ? [...sel.fieldKeys] : store.fieldDefinitions.filter((f) => f.scope === 'pool').map((f) => f.key),
  };
  const perm = p.permissions;
  permissions.value = perm
    ? JSON.parse(JSON.stringify(perm))
    : emptyPermissions();
  for (const k of ['view', 'edit', 'admin'] as const) {
    permissions.value[k] = permissions.value[k] || { personIds: [], groupIds: [] };
    permissions.value[k]!.personIds = permissions.value[k]!.personIds ?? [];
    permissions.value[k]!.groupIds = permissions.value[k]!.groupIds ?? [];
  }
  hydrateIssueForms();
}

async function bootstrap() {
  loading.value = true;
  errorMsg.value = null;
  try {
    await store.loadLookups();
    await store.loadFieldDefinitions().catch(() => {});
    await userStore.fetchUsers({ page: 1, pageSize: 500, isActive: true }).catch(() => {});
    await groupStore.fetchGroups({ page: 1, pageSize: 200 }).catch(() => {});

    if (props.mode === 'edit' && props.projectId) {
      await store.loadProjects();
      await store.loadLabels(props.projectId).catch(() => {});
      hydrateFromProject();
      if (!project.value) errorMsg.value = mt('taskManager.projectNotFound', 'Proje bulunamadı.');
    } else {
      workflowDraft.value = normalizeWorkflow(buildDefaultWorkflow(store.statuses), store.statuses);
      selections.value = {
        priorityIds: store.priorities.map((x) => x.__dataId),
        issueTypeIds: store.issueTypes.map((x) => x.__dataId),
        fieldKeys: store.fieldDefinitions.filter((f) => f.scope === 'pool').map((f) => f.key),
      };
      permissions.value = emptyPermissions();
      hydrateIssueForms();
    }
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  const t = route.query.tab;
  if (typeof t === 'string' && t) activeTab.value = t;
  bootstrap();
});

watch(
  () => props.projectId,
  () => {
    if (props.mode === 'edit') bootstrap();
  }
);

const canSave = computed(() => general.value.name.trim() && general.value.key.trim() && workflowDraft.value);

async function save() {
  if (!canSave.value || !workflowDraft.value) return;
  saving.value = true;
  errorMsg.value = null;
  try {
    const wf = normalizeWorkflow(workflowDraft.value, store.statuses);
    const sel: TmProjectSelections = {
      priorityIds: [...new Set(selections.value.priorityIds)],
      issueTypeIds: [...new Set(selections.value.issueTypeIds)],
      fieldKeys: [...new Set(selections.value.fieldKeys)],
    };
    const perm = JSON.parse(JSON.stringify(permissions.value)) as TmProjectPermissions;

    const multiFormPayload =
      canEditIssueCreateLayout.value && projectForLayout.value
        ? (() => {
            const p = projectForLayout.value!;
            const templates: TmIssueCreateFormTemplate[] = issueFormDrafts.value.map((d) => ({
              id: d.id,
              name: (d.name || d.id).trim(),
              layout: buildLayoutPayloadFromDraft(d, p),
            }));
            let defId = defaultIssueCreateFormId.value;
            if (!templates.find((t) => t.id === defId)) defId = templates[0]!.id;
            const defaultLayout = templates.find((t) => t.id === defId)?.layout ?? templates[0]!.layout;
            return { templates, defId, defaultLayout };
          })()
        : undefined;

    const multiProfilePayload =
      canEditIssueCreateLayout.value && projectForLayout.value && issueProfileFormDrafts.value.length
        ? (() => {
            const p = projectForLayout.value!;
            const templates: TmIssueCreateFormTemplate[] = issueProfileFormDrafts.value.map((d) => ({
              id: d.id,
              name: (d.name || d.id).trim(),
              layout: buildLayoutPayloadFromDraft(d, p),
            }));
            let defId = defaultIssueProfileFormId.value;
            if (!templates.find((t) => t.id === defId)) defId = templates[0]!.id;
            const defaultLayout = templates.find((t) => t.id === defId)?.layout ?? templates[0]!.layout;
            return { templates, defId, defaultLayout };
          })()
        : undefined;

    if (props.mode === 'new') {
      const id = await store.createProject({
        name: general.value.name,
        key: general.value.key,
        description: general.value.description || undefined,
        lead: general.value.lead,
        avatarUrl: general.value.avatarUrl?.trim() || null,
        permissions: perm,
        selections: sel,
        workflow: wf,
        useKanban: general.value.useKanban,
        issueCreateLayout: multiFormPayload?.defaultLayout,
        issueCreateForms: multiFormPayload?.templates,
        defaultIssueCreateFormId: multiFormPayload?.defId,
        issueProfileLayout: multiProfilePayload?.defaultLayout,
        issueProfileForms: multiProfilePayload?.templates,
        defaultIssueProfileFormId: multiProfilePayload?.defId,
      });
      if (id) await router.replace(`/apps/task-manager/projects/${id}/edit`);
    } else if (props.projectId) {
      await store.updateProject(props.projectId, {
        name: general.value.name,
        key: general.value.key,
        description: general.value.description || null,
        lead: general.value.lead,
        avatarUrl: general.value.avatarUrl?.trim() || null,
        permissions: perm,
        selections: sel,
        workflow: wf,
        useKanban: general.value.useKanban,
        issueCreateLayout: multiFormPayload?.defaultLayout,
        issueCreateForms: multiFormPayload?.templates,
        defaultIssueCreateFormId: multiFormPayload?.defId,
        issueProfileLayout: multiProfilePayload?.defaultLayout,
        issueProfileForms: multiProfilePayload?.templates,
        defaultIssueProfileFormId: multiProfilePayload?.defId,
      });
    }
  } catch (e: any) {
    errorMsg.value = e?.message ?? mt('taskManager.editorSaveError', 'Kaydedilemedi.');
  } finally {
    saving.value = false;
  }
}

const breadcrumbs = computed(() => {
  const base = [
    { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
    { text: mt('taskManager.pageTitle', 'Görevler'), disabled: false, href: '/apps/task-manager' },
    { text: mt('taskManager.projectsListTitle', 'Projeler'), disabled: false, href: '/apps/task-manager/projects' },
  ];
  if (props.mode === 'edit' && project.value) {
    base.push({ text: project.value.name, disabled: false, href: `/apps/task-manager/projects/${props.projectId}` });
  }
  base.push({
    text: props.mode === 'new' ? mt('taskManager.editorNewTitle', 'Yeni proje') : mt('taskManager.editorEditTitle', 'Projeyi düzenle'),
    disabled: true,
    href: '#',
  });
  return base;
});

function toggleId(list: string[], id: string, on: boolean) {
  const set = new Set(list);
  if (on) set.add(id);
  else set.delete(id);
  return [...set];
}

const selectableFieldDefinitionsForProject = computed(() => {
  const list = store.fieldDefinitions.filter((f) => {
    const s = String(f.scope ?? 'pool').toLowerCase();
    return s === 'pool' || s === 'core';
  });
  return [...list].sort((a, b) => {
    const ac = String(a.scope ?? '').toLowerCase() === 'core' ? 0 : 1;
    const bc = String(b.scope ?? '').toLowerCase() === 'core' ? 0 : 1;
    if (ac !== bc) return ac - bc;
    const ao = a.sortOrder ?? 999;
    const bo = b.sortOrder ?? 999;
    if (ao !== bo) return ao - bo;
    return a.label.localeCompare(b.label, 'tr', { sensitivity: 'base' });
  });
});

function fieldDefScopeTag(fd: TmFieldDefinition): string {
  return String(fd.scope ?? 'pool').toLowerCase() === 'core'
    ? mt('taskManager.editorFieldScopeCoreTag', 'temel')
    : mt('taskManager.editorFieldScopePoolTag', 'havuz');
}
</script>

<template>
  <div class="tm-flow">
    <BaseBreadcrumb
      :title="mode === 'new' ? mt('taskManager.editorNewTitle', 'Yeni proje') : mt('taskManager.editorEditTitle', 'Projeyi düzenle')"
      :breadcrumbs="breadcrumbs"
    />

    <v-alert v-if="errorMsg" type="error" variant="tonal" class="mb-4" closable @click:close="errorMsg = null">{{ errorMsg }}</v-alert>

    <div v-if="loading" class="d-flex justify-center py-12">
      <v-progress-circular indeterminate color="primary" size="48" />
    </div>

    <template v-else-if="mode === 'edit' && !project">
      <v-alert type="warning" variant="tonal">{{ mt('taskManager.projectNotFound', 'Proje bulunamadı.') }}</v-alert>
    </template>

    <template v-else-if="workflowDraft">
      <v-tabs v-model="activeTab" class="mb-4" color="primary" density="comfortable">
        <v-tab value="general">{{ mt('taskManager.editorTabGeneral', 'Genel bilgiler') }}</v-tab>
        <v-tab value="workflow">{{ mt('taskManager.workflowPageTitle', 'Durum akışı') }}</v-tab>
        <v-tab value="priorities">{{ mt('taskManager.editorTabPriorities', 'Öncelikler') }}</v-tab>
        <v-tab value="types">{{ mt('taskManager.editorTabTypes', 'Görev tipleri') }}</v-tab>
        <v-tab value="fields">{{ mt('taskManager.editorTabFields', 'Alanlar') }}</v-tab>
        <v-tab v-if="canEditIssueCreateLayout" value="issueCreate">
          {{ mt('taskManager.editorTabIssueCreate', 'Yeni görev formu') }}
        </v-tab>
        <v-tab v-if="canEditIssueCreateLayout" value="issueProfile">
          {{ mt('taskManager.editorTabIssueProfile', 'Görev profil ekranı') }}
        </v-tab>
        <v-tab value="auth">{{ mt('taskManager.editorTabAuth', 'Yetkilendirme') }}</v-tab>
      </v-tabs>

      <v-window v-model="activeTab">
        <v-window-item value="general">
          <v-card class="tm-panel pa-6 rounded-xl" flat>
            <v-row>
              <v-col cols="12" md="6">
                <v-text-field v-model="general.name" :label="mt('taskManager.projectName', 'Proje adı')" required variant="outlined" density="comfortable" />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-model="general.key"
                  :label="mt('taskManager.projectKey', 'Proje kodu (PROJ)')"
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>
              <v-col cols="12">
                <v-textarea v-model="general.description" :label="mt('taskManager.description', 'Açıklama')" rows="3" variant="outlined" />
              </v-col>
              <v-col cols="12" md="6">
                <v-autocomplete
                  v-model="general.lead"
                  :items="userItems"
                  item-title="title"
                  item-value="value"
                  clearable
                  variant="outlined"
                  density="comfortable"
                  :label="mt('taskManager.projectLead', 'Proje lideri')"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field v-model="general.avatarUrl" :label="mt('taskManager.projectAvatarUrl', 'Avatar URL')" variant="outlined" density="comfortable" />
              </v-col>
              <v-col cols="12">
                <v-checkbox
                  v-model="general.useKanban"
                  :label="mt('taskManager.useKanban', 'Kanban kullan')"
                  hide-details
                  density="comfortable"
                  color="primary"
                />
                <p class="text-caption text-medium-emphasis mt-1 mb-0">
                  {{ mt('taskManager.useKanbanHint', 'Kapalıysa yalnızca liste görünümü kullanılır; sürükle-bırak Kanban gösterilmez.') }}
                </p>
              </v-col>
            </v-row>
          </v-card>
        </v-window-item>

        <v-window-item value="workflow">
          <ProjectWorkflowEditor v-model="workflowDraft" />
        </v-window-item>

        <v-window-item value="priorities">
          <v-card class="tm-panel pa-6 rounded-xl" flat>
            <p class="text-body-2 text-medium-emphasis mb-4">{{ mt('taskManager.editorPrioritiesHint', 'Bu projede kullanılacak öncelikleri işaretleyin.') }}</p>
            <div class="d-flex flex-wrap ga-2">
              <v-checkbox
                v-for="pr in store.priorities"
                :key="pr.__dataId"
                :model-value="selections.priorityIds.includes(pr.__dataId)"
                :label="pr.name"
                hide-details
                density="compact"
                @update:model-value="(v) => (selections.priorityIds = toggleId(selections.priorityIds, pr.__dataId, !!v))"
              />
            </div>
          </v-card>
        </v-window-item>

        <v-window-item value="types">
          <v-card class="tm-panel pa-6 rounded-xl" flat>
            <p class="text-body-2 text-medium-emphasis mb-4">{{ mt('taskManager.editorTypesHint', 'Bu projede kullanılacak görev tiplerini seçin.') }}</p>
            <div class="d-flex flex-wrap ga-2">
              <v-checkbox
                v-for="t in store.issueTypes"
                :key="t.__dataId"
                :model-value="selections.issueTypeIds.includes(t.__dataId)"
                :label="t.name"
                hide-details
                density="compact"
                @update:model-value="(v) => (selections.issueTypeIds = toggleId(selections.issueTypeIds, t.__dataId, !!v))"
              />
            </div>
          </v-card>
        </v-window-item>

        <v-window-item value="fields">
          <v-card class="tm-panel pa-6 rounded-xl" flat>
            <p class="text-body-2 text-medium-emphasis mb-4">
              {{ mt('taskManager.editorFieldsHint', 'Havuz ve temel alan tanımlarından bu projede kullanılacakları seçin.') }}
            </p>
            <div class="d-flex flex-column ga-1">
              <v-checkbox
                v-for="fd in selectableFieldDefinitionsForProject"
                :key="fd.key"
                :model-value="selections.fieldKeys.includes(fd.key)"
                :label="`${fd.label} (${fd.key}) — ${fieldDefScopeTag(fd)}`"
                hide-details
                density="compact"
                @update:model-value="(v) => (selections.fieldKeys = toggleId(selections.fieldKeys, fd.key, !!v))"
              />
            </div>
          </v-card>
        </v-window-item>

        <v-window-item v-if="canEditIssueCreateLayout" value="issueCreate">
          <v-card class="tm-panel pa-6 rounded-xl" flat>
            <div class="d-flex flex-wrap align-center gap-2 mb-4">
              <v-tabs v-model="activeIssueFormId" class="tm-issue-form-tabs flex-grow-1" color="primary" density="compact">
                <v-tab v-for="d in issueFormDrafts" :key="d.id" :value="d.id" class="text-none">
                  {{ d.name || d.id }}
                </v-tab>
              </v-tabs>
              <v-btn size="small" variant="tonal" rounded="lg" class="text-none" @click="addIssueFormDraft">
                {{ mt('taskManager.editorIssueFormAdd', 'Form ekle') }}
              </v-btn>
              <v-btn
                v-if="issueFormDrafts.length > 1"
                size="small"
                variant="text"
                color="error"
                class="text-none"
                @click="removeActiveIssueFormDraft"
              >
                {{ mt('taskManager.editorIssueFormRemove', 'Formu sil') }}
              </v-btn>
            </div>
            <p class="text-body-2 text-medium-emphasis mb-3">
              {{ mt('taskManager.editorIssueFormDefaultHint', 'Varsayılan form, board oluştururken “Proje varsayılanı” seçildiğinde ve board’a özel form atanmadığında kullanılır.') }}
            </p>
            <v-radio-group
              v-model="defaultIssueCreateFormId"
              :label="mt('taskManager.editorIssueFormDefaultLabel', 'Varsayılan şablon')"
              density="compact"
              class="mb-4"
              hide-details
              inline
            >
              <v-radio
                v-for="d in issueFormDrafts"
                :key="`def-${d.id}`"
                :label="d.name || d.id"
                :value="d.id"
              />
            </v-radio-group>
            <v-text-field
              v-if="activeDraftIndex >= 0"
              v-model="issueFormDrafts[activeDraftIndex].name"
              class="mb-4"
              density="comfortable"
              variant="outlined"
              hide-details="auto"
              :label="mt('taskManager.editorIssueFormName', 'Form adı')"
            />
            <ProjectIssueCreateLayoutEditor
              v-if="activeDraftIndex >= 0"
              v-model="issueFormDrafts[activeDraftIndex].rows"
              v-model:column-sections="issueFormDrafts[activeDraftIndex].columnSections"
              v-model:form-heading="issueFormDrafts[activeDraftIndex].formHeading"
              v-model:form-intro="issueFormDrafts[activeDraftIndex].formIntro"
              v-model:section-titles="issueFormDrafts[activeDraftIndex].sectionTitles"
              v-model:field-cols="issueFormDrafts[activeDraftIndex].fieldCols"
              v-model:dialog-max-width="issueFormDrafts[activeDraftIndex].dialogMaxWidth"
              v-model:section-order="issueFormDrafts[activeDraftIndex].sectionOrder"
              v-model:section-cols="issueFormDrafts[activeDraftIndex].sectionCols"
              :project="projectForLayout"
              :field-definitions="store.fieldDefinitions"
              :preview-issue-type-items="previewIssueTypeItems"
              :preview-priority-items="previewPriorityItems"
              :preview-label-items="previewLabelItems"
              :preview-user-items="previewUserItems"
            />
          </v-card>
        </v-window-item>

        <v-window-item v-if="canEditIssueCreateLayout" value="issueProfile">
          <v-card class="tm-panel pa-6 rounded-xl" flat>
            <div class="d-flex flex-wrap align-center gap-2 mb-4">
              <v-tabs v-model="activeIssueProfileFormId" class="tm-issue-form-tabs flex-grow-1" color="primary" density="compact">
                <v-tab v-for="d in issueProfileFormDrafts" :key="d.id" :value="d.id" class="text-none">
                  {{ d.name || d.id }}
                </v-tab>
              </v-tabs>
              <v-btn size="small" variant="tonal" rounded="lg" class="text-none" @click="addIssueProfileFormDraft">
                {{ mt('taskManager.editorIssueProfileFormAdd', 'Profil şablonu ekle') }}
              </v-btn>
              <v-btn
                v-if="issueProfileFormDrafts.length > 1"
                size="small"
                variant="text"
                color="error"
                class="text-none"
                @click="removeActiveIssueProfileFormDraft"
              >
                {{ mt('taskManager.editorIssueProfileFormRemove', 'Şablonu sil') }}
              </v-btn>
            </div>
            <p class="text-body-2 text-medium-emphasis mb-3">
              {{ mt('taskManager.editorIssueProfileFormDefaultHint', 'Varsayılan şablon, board’da “Proje varsayılanı” seçildiğinde kullanılır.') }}
            </p>
            <v-radio-group
              v-model="defaultIssueProfileFormId"
              :label="mt('taskManager.editorIssueProfileFormDefaultLabel', 'Varsayılan profil şablonu')"
              density="compact"
              class="mb-4"
              hide-details
              inline
            >
              <v-radio v-for="d in issueProfileFormDrafts" :key="`pf-def-${d.id}`" :label="d.name || d.id" :value="d.id" />
            </v-radio-group>
            <p class="text-body-2 text-medium-emphasis mb-4">
              {{ mt('taskManager.editorIssueProfileHint', 'Tablo ve listelerdeki “Profil” ile açılan tam ekran görünümün alan sırası ve bölümleri. Board ayarından şablon seçilebilir; tanımsızsa yeni görev formundaki etkin düzen kullanılır.') }}
            </p>
            <v-text-field
              v-if="activeProfileDraftIndex >= 0"
              v-model="issueProfileFormDrafts[activeProfileDraftIndex].name"
              class="mb-4"
              density="comfortable"
              variant="outlined"
              hide-details="auto"
              :label="mt('taskManager.editorIssueProfileFormName', 'Şablon adı')"
            />
            <ProjectIssueCreateLayoutEditor
              v-if="activeProfileDraftIndex >= 0"
              v-model="issueProfileFormDrafts[activeProfileDraftIndex].rows"
              v-model:column-sections="issueProfileFormDrafts[activeProfileDraftIndex].columnSections"
              v-model:form-heading="issueProfileFormDrafts[activeProfileDraftIndex].formHeading"
              v-model:form-intro="issueProfileFormDrafts[activeProfileDraftIndex].formIntro"
              v-model:section-titles="issueProfileFormDrafts[activeProfileDraftIndex].sectionTitles"
              v-model:field-cols="issueProfileFormDrafts[activeProfileDraftIndex].fieldCols"
              v-model:dialog-max-width="issueProfileFormDrafts[activeProfileDraftIndex].dialogMaxWidth"
              v-model:section-order="issueProfileFormDrafts[activeProfileDraftIndex].sectionOrder"
              v-model:section-cols="issueProfileFormDrafts[activeProfileDraftIndex].sectionCols"
              :project="projectForLayout"
              :field-definitions="store.fieldDefinitions"
              :preview-issue-type-items="previewIssueTypeItems"
              :preview-priority-items="previewPriorityItems"
              :preview-label-items="previewLabelItems"
              :preview-user-items="previewUserItems"
            />
          </v-card>
        </v-window-item>

        <v-window-item value="auth">
          <v-card class="tm-panel pa-6 rounded-xl" flat>
            <p class="text-body-2 text-medium-emphasis mb-4">{{ mt('taskManager.editorAuthHint', 'Görüntüleme, düzenleme ve yönetim için kişi ve grup atları.') }}</p>
            <v-expansion-panels multiple>
              <v-expansion-panel>
                <v-expansion-panel-title>{{ mt('taskManager.permView', 'Görüntüleme') }}</v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-autocomplete
                    v-model="permissions.view.personIds"
                    :items="userItems"
                    item-title="title"
                    item-value="value"
                    multiple
                    chips
                    closable-chips
                    variant="outlined"
                    density="comfortable"
                    class="mb-3"
                    :label="mt('taskManager.permPersons', 'Kullanıcılar')"
                  />
                  <v-autocomplete
                    v-model="permissions.view.groupIds"
                    :items="groupItems"
                    item-title="title"
                    item-value="value"
                    multiple
                    chips
                    closable-chips
                    variant="outlined"
                    density="comfortable"
                    :label="mt('taskManager.permGroups', 'Gruplar')"
                  />
                </v-expansion-panel-text>
              </v-expansion-panel>
              <v-expansion-panel>
                <v-expansion-panel-title>{{ mt('taskManager.permEdit', 'Düzenleme') }}</v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-autocomplete
                    v-model="permissions.edit.personIds"
                    :items="userItems"
                    item-title="title"
                    item-value="value"
                    multiple
                    chips
                    closable-chips
                    variant="outlined"
                    density="comfortable"
                    class="mb-3"
                    :label="mt('taskManager.permPersons', 'Kullanıcılar')"
                  />
                  <v-autocomplete
                    v-model="permissions.edit.groupIds"
                    :items="groupItems"
                    item-title="title"
                    item-value="value"
                    multiple
                    chips
                    closable-chips
                    variant="outlined"
                    density="comfortable"
                    :label="mt('taskManager.permGroups', 'Gruplar')"
                  />
                </v-expansion-panel-text>
              </v-expansion-panel>
              <v-expansion-panel>
                <v-expansion-panel-title>{{ mt('taskManager.permAdmin', 'Yönetim') }}</v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-autocomplete
                    v-model="permissions.admin.personIds"
                    :items="userItems"
                    item-title="title"
                    item-value="value"
                    multiple
                    chips
                    closable-chips
                    variant="outlined"
                    density="comfortable"
                    class="mb-3"
                    :label="mt('taskManager.permPersons', 'Kullanıcılar')"
                  />
                  <v-autocomplete
                    v-model="permissions.admin.groupIds"
                    :items="groupItems"
                    item-title="title"
                    item-value="value"
                    multiple
                    chips
                    closable-chips
                    variant="outlined"
                    density="comfortable"
                    :label="mt('taskManager.permGroups', 'Gruplar')"
                  />
                </v-expansion-panel-text>
              </v-expansion-panel>
            </v-expansion-panels>
          </v-card>
        </v-window-item>
      </v-window>

      <div class="d-flex flex-wrap gap-2 mt-6">
        <v-btn color="primary" size="large" rounded="lg" :loading="saving" :disabled="!canSave" @click="save">
          {{ mt('taskManager.save', 'Kaydet') }}
        </v-btn>
        <v-btn variant="tonal" rounded="lg" :to="'/apps/task-manager/projects'">{{ mt('taskManager.editorBackList', 'Projeler listesi') }}</v-btn>
        <v-btn
          v-if="mode === 'edit' && projectId"
          variant="tonal"
          rounded="lg"
          :to="`/apps/task-manager/projects/${projectId}`"
        >
          {{ mt('taskManager.editorBackProject', 'Proje özeti') }}
        </v-btn>
      </div>
    </template>
  </div>
</template>
