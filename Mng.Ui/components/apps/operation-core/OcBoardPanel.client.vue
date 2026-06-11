<script setup lang="ts">
import { computed, watch, onMounted, onUnmounted, ref, defineAsyncComponent } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcBoardCatalogLabel from '@/components/apps/operation-core/OcBoardCatalogLabel.vue';
// Kanban yalnız kanban görünümünde render edilir; list-only board'da bundle'a girmesin.
const OcBoardKanban = defineAsyncComponent(
  () => import('@/components/apps/operation-core/OcBoardKanban.vue')
);
import OcBoardDashboardLink from '@/components/apps/operation-core/OcBoardDashboardLink.vue';
import OcBoardListFilters from '@/components/apps/operation-core/OcBoardListFilters.vue';
import type { OcBoardFilterColumn, OcBoardFilterKind } from '@/components/apps/operation-core/OcBoardListFilters.vue';
import OcWorkItemFormDialog from '@/components/apps/operation-core/OcWorkItemFormDialog.vue';
import OcSlaStatusChip from '@/components/apps/operation-core/OcSlaStatusChip.vue';
import { useOcBoardListLookups } from '@/composables/useOcBoardListLookups';
import { useOcBoardRelationLookups } from '@/composables/useOcBoardRelationLookups';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useOperationCoreStore } from '@/stores/apps/operationCore';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ocApplyTransition,
  ocDeleteWorkItem,
  ocErrorCode,
  ocExtractDgErrorMessage,
  ocExtractOperationsMessage,
  ocGetDashboardRecord,
  ocListPoolFieldsForWorkspace,
} from '@/services/operationCoreService';
import type {
  OcBoardListFilter,
  OcBoardListRequest,
  OcColumnFormat,
  OcWorkItemCard,
  OpBoard,
  OpField,
} from '@/types/apps/operationCore';
import {
  defaultFormatForKey,
  isBuiltInListColumn,
  isCoreListColumn,
  isSystemListColumn,
  listTableCellValue,
  listTablePoolCellDisplay,
  normalizeListTableColumns,
  resolveListColumnFormat,
  systemColumnRawValue,
} from '@/utils/ocBoardListColumns';
import { formatCellValue, type OcFormatOptions } from '@/utils/ocColumnFormat';
import { evaluateComputedExpr } from '@/utils/ocComputedColumns';
import { buildWorkItemProfilePath } from '@/utils/ocWorkItemProfileNav';

defineOptions({ name: 'OcBoardPanel' });

const props = withDefaults(
  defineProps<{
    boardId: string;
    /** Workspace hub sağ panelinde; breadcrumb ve geri navigasyon gizlenir. */
    embedded?: boolean;
  }>(),
  { embedded: false }
);

const emit = defineEmits<{
  'dashboard-assigned': [dashboardId: string | null];
}>();

const { t, locale } = useAppI18n();
const route = useRoute();
const router = useRouter();
const store = useOperationCoreStore();

const boardId = computed(() => props.boardId.trim());

type BoardDisplayMode = 'list' | 'kanban';

const displayMode = ref<BoardDisplayMode>('list');

const boardIsKanban = computed(() => store.boardContext?.viewType === 'kanban');

const showKanbanToggle = computed(() => boardIsKanban.value);

function syncDisplayModeFromRoute() {
  if (props.embedded) return;
  const v = route.query.view;
  if (v === 'kanban' && boardIsKanban.value) {
    displayMode.value = 'kanban';
    return;
  }
  displayMode.value = 'list';
}

function applyDefaultDisplayMode() {
  if (props.embedded || route.query.view !== 'kanban' || !boardIsKanban.value) {
    displayMode.value = 'list';
    return;
  }
  displayMode.value = 'kanban';
}

function setDisplayMode(mode: BoardDisplayMode) {
  displayMode.value = mode;
  if (props.embedded) return;
  router.replace({
    query: { ...route.query, view: mode },
  });
}

const showList = computed(() => !boardIsKanban.value || displayMode.value === 'list');
const showKanban = computed(() => boardIsKanban.value && displayMode.value === 'kanban');

const boardContextMatches = computed(
  () => !!store.boardContext && store.boardContext.boardId === boardId.value
);

const boardListDataPending = computed(() => {
  if (!boardContextMatches.value || !showList.value) return false;
  return store.listLoading && store.listItems.length === 0 && !store.listError;
});

const showBoardLoadingPanel = computed(() => {
  if (!boardId.value) return false;
  if (store.boardError && !store.boardContext) return false;
  if (store.loadingBoardContext) return true;
  if (!boardContextMatches.value && !!boardId.value) return true;
  return boardListDataPending.value;
});

const loadingBoardName = computed(() => {
  const id = boardId.value;
  if (!id) return '';
  for (const boards of Object.values(store.boardsByWorkspace)) {
    const match = boards.find((b) => b.__dataId === id);
    if (match?.name) return match.name;
  }
  return store.boardContext?.name ?? '';
});

const loadingPanelTitle = computed(() => {
  if (store.boardContext && store.boardContext.boardId !== boardId.value) {
    return t('operationCore.board.loadingPanelSwitching');
  }
  return t('operationCore.board.loadingPanelTitle');
});

const workspaceName = computed(() => {
  const wsId = store.boardContext?.workspaceId;
  if (!wsId) return '';
  return store.workspaces.find((w) => w.__dataId === wsId)?.name ?? '';
});

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  workspace: computed(() => {
    const ctx = store.boardContext;
    if (!ctx) return null;
    return {
      id: ctx.workspaceId,
      name: workspaceName.value || ctx.workspaceId,
    };
  }),
  board: computed(() => {
    const ctx = store.boardContext;
    if (!ctx) return null;
    return { id: ctx.boardId, name: ctx.name || ctx.boardId };
  }),
});

const workspaceId = computed(() => store.boardContext?.workspaceId ?? null);

const profileNavFrom = computed(() => (props.embedded ? 'workspace' as const : 'board' as const));

function workItemProfilePath(workItemId: string): string {
  return buildWorkItemProfilePath(workItemId, {
    boardId: boardId.value,
    workspaceId: props.embedded ? workspaceId.value : null,
    from: profileNavFrom.value,
  });
}

const hubBoard = computed((): OpBoard | null => {
  const wsId = workspaceId.value;
  const id = boardId.value;
  if (!wsId || !id) return null;
  return store.boardsForWorkspace(wsId).find((b) => b.__dataId === id) ?? null;
});

const hubBoardDashboardName = ref<string | null>(null);

async function onHubBoardDashboardAssigned(dashboardId: string | null) {
  const wsId = workspaceId.value;
  if (!wsId) return;
  await store.loadBoardsForWorkspace(wsId, true);
  hubBoardDashboardName.value = null;
  if (dashboardId && hubBoard.value?.defaultDashboardId === dashboardId) {
    const rec = await ocGetDashboardRecord(dashboardId);
    hubBoardDashboardName.value = rec?.name ?? null;
  }
  if (props.embedded) {
    emit('dashboard-assigned', dashboardId);
  }
}

watch(
  () => hubBoard.value?.defaultDashboardId,
  async (dashId) => {
    hubBoardDashboardName.value = null;
    if (!dashId) return;
    const rec = await ocGetDashboardRecord(dashId);
    hubBoardDashboardName.value = rec?.name ?? null;
  },
  { immediate: true }
);

watch(
  workspaceId,
  (wsId) => {
    if (wsId) void store.loadBoardsForWorkspace(wsId, true);
  },
  { immediate: true }
);
const boardCatalogs = computed(() => store.boardContext?.catalogs ?? null);
const boardPeople = computed(() => store.boardPeople);
const boardGroups = computed(() => store.boardGroups);

const {
  resolveState,
  resolvePriority,
  resolveType,
  resolveAssigneeName,
  resolvePersonValue,
  stateById,
  priorityById,
  typeById,
} = useOcBoardListLookups(workspaceId, boardCatalogs, boardPeople);

const poolFields = ref<OpField[]>([]);

const poolFieldLabelByKey = computed(
  () => new Map(poolFields.value.filter((f) => f.key).map((f) => [f.key, f.label?.trim() || f.key]))
);

const poolFieldKeys = computed(() => poolFields.value.map((f) => f.key).filter((k): k is string => !!k));

const poolFieldTypeByKey = computed(
  () => new Map(poolFields.value.filter((f) => f.key).map((f) => [f.key, f.fieldType || '']))
);

function listCellFormatOptions(key: string, item?: OcWorkItemCard): OcFormatOptions {
  const opts: OcFormatOptions = { locale: locale() };
  if (key === 'age' && item) opts.anchorEnd = item.closedAt ?? null;
  if ((poolFieldTypeByKey.value.get(key) ?? '').toLowerCase() === 'date') {
    opts.dateOnly = true;
  }
  return opts;
}

function effectiveColumnFormat(key: string): OcColumnFormat | null {
  return resolveListColumnFormat(key, columnFormat(key), poolFieldTypeByKey.value.get(key));
}

const personPoolKeySet = computed(
  () =>
    new Set(
      poolFields.value
        .filter((f) => f.key && ['persons', 'person'].includes((f.fieldType || '').toLowerCase()))
        .map((f) => f.key)
    )
);

// Pool person grup alanları: değer = grup id('leri); ad MO Groups map'inden (store.boardGroups) çözülür.
const groupPoolKeySet = computed(
  () =>
    new Set(
      poolFields.value
        .filter((f) => f.key && ['persongroups', 'persongroup', 'group'].includes((f.fieldType || '').toLowerCase()))
        .map((f) => f.key)
    )
);

/** Grup alan değerini (id / id[] / nesne) okunabilir grup adı/adlarına çevirir. */
function resolveGroupValue(value: unknown): string {
  const ids = collectGroupIds(value);
  if (!ids.length) return '—';
  const map = boardGroups.value;
  const names = ids.map((id) => map[id]?.name?.trim() || id).filter((n) => n && n !== '—');
  return names.length ? names.join(', ') : '—';
}

function collectGroupIds(value: unknown): string[] {
  if (value === null || value === undefined || value === '') return [];
  if (Array.isArray(value)) return value.flatMap((v) => collectGroupIds(v));
  if (typeof value === 'object') {
    const o = value as Record<string, unknown>;
    const id = o.__dataId ?? o.id ?? o.groupId;
    return id != null ? [String(id).trim()].filter(Boolean) : [];
  }
  const s = String(value).trim();
  return s ? [s] : [];
}

// Grup filtresi opsiyonları: değer=grup id, başlık=grup adı (MO Groups map'i = yüklü satırlardan toplanır).
// Tüm grup alanları (assignmentGroups + grup pool) aynı havuz adlarını paylaşır; her key'e aynı liste verilir.
const groupOptions = computed<{ value: string; title: string }[]>(() =>
  Object.entries(boardGroups.value)
    .map(([id, display]) => ({ value: id, title: display?.name?.trim() || id }))
    .sort((a, b) => a.title.localeCompare(b.title, 'tr'))
);

const groupOptionsByKey = computed<Record<string, { value: string; title: string }[]>>(() => {
  const keys = ['assignmentGroups', ...groupPoolKeySet.value];
  const opts = groupOptions.value;
  const out: Record<string, { value: string; title: string }[]> = {};
  for (const key of keys) out[key] = opts;
  return out;
});

const numberPoolKeySet = computed(
  () =>
    new Set(
      poolFields.value
        .filter((f) => f.key && (f.fieldType || '').toLowerCase() === 'number')
        .map((f) => f.key)
    )
);

const datePoolKeySet = computed(
  () =>
    new Set(
      poolFields.value
        .filter((f) => f.key && ['date', 'datetime'].includes((f.fieldType || '').toLowerCase()))
        .map((f) => f.key)
    )
);

// Pool tags alanları: çoklu serbest etiket. Filtrede combobox (in/nin) + yüklü satırlardan öneri.
const tagsPoolKeySet = computed(
  () =>
    new Set(
      poolFields.value
        .filter((f) => f.key && (f.fieldType || '').toLowerCase() === 'tags')
        .map((f) => f.key)
    )
);

// key → mevcut etiket değerleri (yüklü liste satırlarından toplanır; combobox önerisi, serbest giriş açık).
const tagOptionsByKey = computed<Record<string, string[]>>(() => {
  const keys = [...tagsPoolKeySet.value];
  if (!keys.length) return {};
  const acc: Record<string, Set<string>> = {};
  for (const k of keys) acc[k] = new Set<string>();
  for (const item of store.listItems) {
    for (const k of keys) {
      const v = item.fields?.[k];
      const arr = Array.isArray(v) ? v : v == null || v === '' ? [] : [v];
      for (const tag of arr) {
        const s = String(tag).trim();
        if (s) acc[k].add(s);
      }
    }
  }
  const out: Record<string, string[]> = {};
  for (const k of keys) out[k] = [...acc[k]].sort((a, b) => a.localeCompare(b));
  return out;
});

const listColumnsMeta = computed(() => store.boardContext?.listColumns ?? []);

const sortableKeySet = computed(
  () => new Set(listColumnsMeta.value.filter((c) => c.sortable).map((c) => c.key))
);

const listColumnKeys = computed(() => {
  const meta = listColumnsMeta.value;
  if (meta.length) return meta.map((c) => c.key);
  return normalizeListTableColumns(store.boardContext?.cardFieldKeys, poolFieldKeys.value);
});

const computedColumnByKey = computed(() => {
  const map = new Map<string, { expr: string | null; label: string | null }>();
  for (const c of listColumnsMeta.value) {
    if (c.computed) map.set(c.key, { expr: c.expr ?? null, label: c.label ?? null });
  }
  return map;
});

function columnLabel(key: string): string {
  const meta = listColumnsMeta.value.find((c) => c.key === key);
  if (meta?.label?.trim()) return meta.label.trim();
  const computed = computedColumnByKey.value.get(key);
  if (computed) return computed.label?.trim() || key;
  if (isBuiltInListColumn(key)) {
    return t(`operationCore.workspaceDefinitions.boards.listTableColumns.${key}`);
  }
  return poolFieldLabelByKey.value.get(key) ?? key;
}

/** Computed sütun için satır bağlamı: core alanlar + pool fields. */
function buildComputedScope(item: OcWorkItemCard): Record<string, unknown> {
  return {
    ...(item.fields ?? {}),
    key: item.key,
    title: item.title,
    stateId: item.stateId,
    assignee: item.assignee,
    priorityId: item.priorityId,
    typeId: item.typeId,
    createdBy: item.createdBy,
    createdAt: item.createdAt,
    updatedAt: item.updatedAt,
    closedAt: item.closedAt,
    lastStateChangeAt: item.lastStateChangeAt,
  };
}

function computedCellValue(key: string, item: OcWorkItemCard): string {
  const def = computedColumnByKey.value.get(key);
  if (!def?.expr) return '—';
  const result = evaluateComputedExpr(def.expr, buildComputedScope(item));
  if (!result.ok) return '⚠';
  if (result.value === null || result.value === undefined || result.value === '') return '—';
  return formatCellValue(result.value, columnFormat(key), listCellFormatOptions(key));
}

const initialStateId = computed(() => store.boardContext?.initialStateId ?? null);

const columnFormatByKey = computed(() => {
  const map = new Map<string, OcColumnFormat | null>();
  for (const c of listColumnsMeta.value) {
    map.set(c.key, c.format ?? defaultFormatForKey(c.key));
  }
  return map;
});

function columnFormat(key: string): OcColumnFormat | null {
  return columnFormatByKey.value.get(key) ?? defaultFormatForKey(key);
}

function isColumnSortable(key: string): boolean {
  // listColumns meta varsa onu kullan; yoksa (eski board) key/title client-side.
  if (listColumnsMeta.value.length) return sortableKeySet.value.has(key);
  return key === 'key' || key === 'title';
}

const listHeaders = computed(() => {
  const cols = listColumnKeys.value.map((key) => ({
    title: columnLabel(key),
    key,
    sortable: isColumnSortable(key),
  }));
  cols.push({
    title: t('operationCore.board.actions.header'),
    key: 'actions',
    sortable: false,
    align: 'end',
    width: 140,
  } as (typeof cols)[number]);
  return cols;
});

function isRelationPoolKey(key: string): boolean {
  const f = poolFields.value.find((x) => x.key === key);
  return (f?.fieldType || '').toLowerCase() === 'relation' && !!f.relationDatasetName?.trim();
}

function filterKind(key: string): OcBoardFilterKind {
  if (key === 'stateId') return 'state';
  if (key === 'priorityId') return 'priority';
  if (key === 'typeId') return 'type';
  if (key === 'assignee' || key === 'createdBy' || personPoolKeySet.value.has(key)) return 'person';
  if (key === 'assignmentGroups' || groupPoolKeySet.value.has(key)) return 'group';
  if (isRelationPoolKey(key)) return 'relation';
  if (tagsPoolKeySet.value.has(key)) return 'tags';
  // Tarih: format ipucu 'date' (createdAt/lastStateChangeAt/closedAt…) veya pool date/datetime alanı.
  if (columnFormat(key) === 'date' || datePoolKeySet.value.has(key)) return 'date';
  // Sayısal: format 'number'/'money' veya pool number alanı.
  const fmt = columnFormat(key);
  if (fmt === 'number' || fmt === 'money' || numberPoolKeySet.value.has(key)) return 'number';
  return 'text';
}

const filterableColumns = computed<OcBoardFilterColumn[]>(() =>
  listColumnsMeta.value
    .filter((c) => c.filterable)
    .map((c) => ({ key: c.key, label: columnLabel(c.key), kind: filterKind(c.key) }))
);

const {
  relationPoolKeySet,
  relationOptionsByKey,
  resolveRelationValue,
  ensureRelationOptions,
} = useOcBoardRelationLookups(poolFields, listColumnKeys, filterableColumns);

const stateFilterOptions = computed(() =>
  Array.from(stateById.value.values()).map((c) => ({ value: c.id, title: c.name || c.id }))
);
const priorityFilterOptions = computed(() =>
  Array.from(priorityById.value.values()).map((c) => ({ value: c.id, title: c.name || c.id }))
);
const typeFilterOptions = computed(() =>
  Array.from(typeById.value.values()).map((c) => ({ value: c.id, title: c.name || c.id }))
);

const listRows = computed(() =>
  store.listItems.map((item) => {
    const stateLabel = resolveState(item.stateId, null)?.name ?? item.stateId ?? null;
    // Çözülmüş katalog/kişi değerlerini burada (cache'li computed) önceden hesapla;
    // şablon slotları her render'da resolveState/Priority/Type'ı yeniden çağırmasın.
    const row: Record<string, unknown> = {
      id: item.id,
      keyText: item.key ?? '',
      titleText: item.title ?? '',
      profileTo: workItemProfilePath(item.id),
      stateColumnTitle: stateLabel,
      rawStateId: item.stateId ?? null,
      rawAssignee: item.assignee ?? null,
      rawPriorityId: item.priorityId ?? null,
      rawTypeId: item.typeId ?? null,
      rawCreatedBy: item.createdBy ?? null,
      // Şablonun doğrudan bağlanacağı çözülmüş değerler (davranış: eski slot çağrılarıyla birebir).
      stateItem: resolveState(item.stateId, stateLabel),
      priorityItem: resolvePriority(item.priorityId),
      typeItem: resolveType(item.typeId),
      assigneeName: resolveAssigneeName(item.assignee),
      createdByName: resolveAssigneeName(item.createdBy),
      // SLA chip + person sütunları slot ile render edilir; ham kartı taşı.
      __card: item as OcWorkItemCard,
    };
    for (const key of listColumnKeys.value) {
      // createdBy (kişi) ve sla (chip) slot ile render edilir — satır metni gerekmez.
      if (key === 'createdBy' || key === 'sla') continue;
      if (computedColumnByKey.value.has(key)) {
        row[key] = computedCellValue(key, item);
      } else if (isSystemListColumn(key)) {
        row[key] = formatCellValue(systemColumnRawValue(item, key), effectiveColumnFormat(key), {
          ...listCellFormatOptions(key, item),
        });
      } else if (isCoreListColumn(key)) {
        row[key] = listTableCellValue(item, key, { stateLabel: stateLabel ?? undefined });
      } else if (personPoolKeySet.value.has(key)) {
        row[key] = resolvePersonValue(item.fields?.[key]);
      } else if (groupPoolKeySet.value.has(key) || key === 'assignmentGroups') {
        row[key] = resolveGroupValue(item.fields?.[key]);
      } else if (relationPoolKeySet.value.has(key)) {
        row[key] =
          item.fieldDisplays?.[key] ?? resolveRelationValue(key, item.fields?.[key]);
      } else {
        row[key] = listTablePoolCellDisplay(
          item.fields,
          key,
          effectiveColumnFormat(key),
          listCellFormatOptions(key, item),
          item.fieldDisplays
        );
      }
    }
    return row;
  })
);

// --- Server-side liste durumu ---
const page = ref(1);
const itemsPerPage = ref(25);
const sortBy = ref<{ key: string; order: 'asc' | 'desc' }[]>([]);
const searchInput = ref('');
const activeFilters = ref<OcBoardListFilter[]>([]);
const itemsPerPageOptions = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
  { value: 100, title: '100' },
];

let lastSignature = '';
let searchTimer: ReturnType<typeof setTimeout> | null = null;

function buildListRequest(): OcBoardListRequest {
  const primary = sortBy.value[0];
  const sort = primary
    ? { field: primary.key, direction: primary.order === 'desc' ? 'desc' : 'asc' as const }
    : (store.boardContext?.defaultSort ?? null);
  return {
    skip: Math.max(0, (page.value - 1) * itemsPerPage.value),
    take: itemsPerPage.value,
    sort,
    filters: activeFilters.value,
    search: (searchInput.value ?? '').trim() || null,
  };
}

async function fetchList(force = false) {
  if (!store.boardContext) return;
  const req = buildListRequest();
  const sig = JSON.stringify(req);
  if (!force && sig === lastSignature) return;
  lastSignature = sig;
  await store.loadBoardListPage(req);
}

function onListOptions(opts: { page: number; itemsPerPage: number; sortBy: { key: string; order: 'asc' | 'desc' }[] }) {
  page.value = opts.page;
  itemsPerPage.value = opts.itemsPerPage > 0 ? opts.itemsPerPage : 25;
  sortBy.value = Array.isArray(opts.sortBy) ? opts.sortBy : [];
  void fetchList();
}

function onFiltersUpdate(filters: OcBoardListFilter[]) {
  activeFilters.value = filters;
  page.value = 1;
  void fetchList(true);
}

watch(searchInput, () => {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    page.value = 1;
    void fetchList();
  }, 400);
});

const hasSearch = computed(() => (searchInput.value ?? '').trim().length > 0);

function clearSearch() {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = null;
  searchInput.value = '';
  page.value = 1;
  void fetchList(true);
}

async function reloadList(force = true) {
  await fetchList(force);
}

async function loadPoolFields() {
  const ctxFields = store.boardContext?.poolFields;
  if (ctxFields?.length) {
    poolFields.value = ctxFields;
    return;
  }
  const wsId = workspaceId.value;
  if (!wsId) {
    poolFields.value = [];
    return;
  }
  try {
    poolFields.value = await ocListPoolFieldsForWorkspace(wsId);
  } catch {
    poolFields.value = [];
  }
}

watch(
  () => store.boardContext?.poolFields,
  (fields) => {
    if (fields?.length) poolFields.value = fields;
  },
  { immediate: true }
);

watch(workspaceId, () => {
  void loadPoolFields();
}, { immediate: true });

watch(
  filterableColumns,
  (cols) => {
    if (cols.some((c) => c.kind === 'relation')) {
      void ensureRelationOptions();
    }
  },
  { immediate: true }
);

function onFiltersAdvancedOpen() {
  void ensureRelationOptions();
}

const canEdit = computed(() => store.boardContext?.permissions.canEdit === true);

type FormDialogMode = 'create' | 'edit';
const formDialogOpen = ref(false);
const formDialogMode = ref<FormDialogMode>('create');
const editWorkItemId = ref<string | null>(null);

function openCreateDialog() {
  formDialogMode.value = 'create';
  editWorkItemId.value = null;
  formDialogOpen.value = true;
}

// Düzenleme artık iş kaydı profil ekranından (in-place) yapılır; listede edit butonu yoktur.

function onRefresh() {
  if (showKanban.value) {
    void store.refreshBoard();
  } else {
    void reloadList(true);
  }
}

function onWorkItemSaved() {
  if (showKanban.value) {
    void store.refreshBoard();
  } else {
    void reloadList(true);
  }
}

const deleteDialogOpen = ref(false);
const deleting = ref(false);
const deleteError = ref<string | null>(null);
// İlişki guard'ı (409 WORK_ITEM_HAS_RELATIONS) yakalandığında "yine de sil" (force) moduna geçeriz.
const deleteHasRelations = ref(false);
const deleteTarget = ref<{ id: string; key: string; title: string } | null>(null);

function askDelete(row: { id: string; keyText: string; titleText: string }) {
  deleteTarget.value = { id: row.id, key: row.keyText, title: row.titleText };
  deleteError.value = null;
  deleteHasRelations.value = false;
  deleteDialogOpen.value = true;
}

async function confirmDelete(force = false) {
  const target = deleteTarget.value;
  if (!target) return;
  deleting.value = true;
  deleteError.value = null;
  try {
    await ocDeleteWorkItem(target.id, force);
    deleteDialogOpen.value = false;
    deleteTarget.value = null;
    deleteHasRelations.value = false;
    if (showKanban.value) {
      await store.refreshBoard();
    } else {
      await reloadList(true);
    }
  } catch (e: unknown) {
    const status = (e as { statusCode?: number; status?: number })?.statusCode ?? (e as { status?: number })?.status;
    if (!force && (ocErrorCode(e) === 'WORK_ITEM_HAS_RELATIONS' || status === 409)) {
      deleteHasRelations.value = true;
      deleteError.value = ocExtractOperationsMessage(e, t('operationCore.board.actions.deleteHasRelations'));
    } else {
      deleteError.value = ocExtractOperationsMessage(e, t('operationCore.board.actions.deleteError'));
    }
  } finally {
    deleting.value = false;
  }
}

// --- Kanban DnD transition ---
const transitionSnackbar = ref(false);
const transitionMsg = ref('');
const transitionMsgColor = ref<'success' | 'error' | 'info'>('info');
const pendingProfileId = ref<string | null>(null);

function showTransitionMsg(msg: string, color: 'success' | 'error' | 'info', profileId: string | null = null) {
  transitionMsg.value = msg;
  transitionMsgColor.value = color;
  pendingProfileId.value = profileId;
  transitionSnackbar.value = true;
}

function openPendingProfile() {
  const id = pendingProfileId.value;
  transitionSnackbar.value = false;
  if (id) {
    void router.push(workItemProfilePath(id));
  }
}

async function onKanbanTransition(payload: { card: OcWorkItemCard; fromStateId: string; toStateId: string }) {
  const { card, fromStateId, toStateId } = payload;
  const targetColumn = store.boardContext?.columns.find((c) => c.stateId === toStateId);
  const transition = targetColumn?.incomingTransitions.find((tr) => tr.fromStateId === fromStateId) ?? null;

  if (!transition) {
    // from=A → to=B geçişi tanımlı değil; optimistic taşımayı geri al.
    showTransitionMsg(t('operationCore.board.transition.invalid'), 'error');
    await store.refreshBoard();
    return;
  }

  if (transition.requiredFields.length > 0) {
    // Board'da form yok → kartı geri al + profile yönlendir (profil zorunlu alan toplar).
    showTransitionMsg(t('operationCore.board.transition.requiredFields'), 'info', card.id);
    await store.refreshBoard();
    return;
  }

  try {
    await ocApplyTransition(card.id, transition.transitionKey);
    showTransitionMsg(t('operationCore.board.transition.success'), 'success');
  } catch (e: unknown) {
    showTransitionMsg(ocExtractDgErrorMessage(e, t('operationCore.board.transition.error')), 'error');
  } finally {
    await store.refreshBoard();
  }
}

const backToWorkspaceTo = computed(() => {
  const ctx = store.boardContext;
  if (!ctx) return '/apps/operation-core/workspace';
  const qs = new URLSearchParams({
    workspaceId: ctx.workspaceId,
    boardId: ctx.boardId,
  });
  return `/apps/operation-core/workspace?${qs.toString()}`;
});

async function loadPage() {
  const id = boardId.value;
  if (!id) {
    store.clearBoardState();
    return;
  }
  if (!store.workspaces.length) {
    await store.loadWorkspaces();
  }
  await store.loadBoard(id);
  applyDefaultDisplayMode();
  // Liste görünümü server-side ilk sayfayı çeker (kanban kolon sorgularıyla yüklenir).
  lastSignature = '';
  if (showList.value) {
    void fetchList(true);
  }
}

watch(boardId, () => {
  void loadPage();
});

watch(
  () => route.query.view,
  () => syncDisplayModeFromRoute()
);

onMounted(() => {
  if (!props.embedded) {
    syncDisplayModeFromRoute();
  }
  void loadPage();
});

onUnmounted(() => {
  store.clearBoardState();
});
</script>

<template>
  <div class="oc-flow oc-board-page" :class="{ 'oc-board-panel--embedded': embedded }">
    <BaseBreadcrumb
      v-if="!embedded"
      :title="store.boardContext?.name || t('operationCore.board.placeholderTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <v-alert
      v-if="store.boardError"
      type="error"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="store.clearBoardError()"
    >
      {{ store.boardError }}
    </v-alert>

    <v-card
      variant="outlined"
      :class="embedded ? 'rounded-0 border-0 mb-0' : 'rounded-lg mb-4'"
    >
      <v-card-title class="d-flex align-center flex-wrap gap-2 py-3">
        <v-btn
          v-if="!embedded"
          icon="mdi-arrow-left"
          variant="text"
          size="small"
          :to="backToWorkspaceTo"
          :title="t('operationCore.board.backToWorkspace')"
        />
        <div v-if="!embedded" class="min-width-0">
          <div class="text-subtitle-1 font-weight-bold text-truncate">
            {{ store.boardContext?.name || t('operationCore.board.loadingTitle') }}
          </div>
          <div v-if="workspaceName" class="text-caption text-medium-emphasis text-truncate">
            {{ workspaceName }}
          </div>
        </div>
        <v-spacer />
        <OcBoardDashboardLink
          v-if="!embedded && hubBoard && workspaceId"
          :workspace-id="workspaceId"
          :board="hubBoard"
          :dashboard-name="hubBoardDashboardName"
          density="compact"
          class="mr-1"
          @assigned="onHubBoardDashboardAssigned"
        />
        <v-btn-toggle
          v-if="store.boardContext && showKanbanToggle"
          :model-value="displayMode"
          mandatory
          density="compact"
          color="primary"
          variant="outlined"
          divided
          class="mr-1"
          @update:model-value="setDisplayMode($event as BoardDisplayMode)"
        >
          <v-btn value="list" size="small" class="text-none px-3">
            <v-icon icon="mdi-format-list-bulleted" start size="18" />
            {{ t('operationCore.board.viewList') }}
          </v-btn>
          <v-btn value="kanban" size="small" class="text-none px-3">
            <v-icon icon="mdi-view-column-outline" start size="18" />
            {{ t('operationCore.board.viewKanban') }}
          </v-btn>
        </v-btn-toggle>
        <v-btn
          variant="tonal"
          color="primary"
          size="small"
          rounded="lg"
          class="text-none"
          :loading="store.loadingBoardContext || store.listLoading"
          @click="onRefresh()"
        >
          <v-icon icon="mdi-refresh" start size="18" />
          {{ t('operationCore.board.refresh') }}
        </v-btn>
        <v-btn
          v-if="canEdit"
          color="primary"
          size="small"
          variant="flat"
          rounded="lg"
          class="text-none"
          @click="openCreateDialog"
        >
          <v-icon icon="mdi-plus" start size="18" />
          {{ t('operationCore.board.newWorkItem') }}
        </v-btn>
      </v-card-title>
    </v-card>

    <div
      class="oc-board-panel__content position-relative"
      :class="{ 'oc-board-panel__content--embedded': embedded }"
    >
      <div v-if="showBoardLoadingPanel" class="oc-board-loading-panel">
        <v-card variant="flat" class="oc-board-loading-panel__card text-center pa-8 rounded-xl">
          <v-progress-circular indeterminate color="primary" size="48" width="4" class="mb-4" />
          <div class="text-subtitle-1 font-weight-medium mb-1">
            {{ loadingPanelTitle }}
          </div>
          <p v-if="loadingBoardName" class="text-body-2 font-weight-medium text-primary mb-1">
            {{ loadingBoardName }}
          </p>
          <p class="text-body-2 text-medium-emphasis mb-0">
            {{ t('operationCore.board.loadingPanelHint') }}
          </p>
        </v-card>
      </div>

      <template v-else-if="boardContextMatches">
      <v-card v-if="showList" variant="outlined" class="rounded-lg">
        <div class="pa-3 pb-0">
          <div class="d-flex align-center flex-wrap ga-2 mb-1">
            <v-text-field
              v-model="searchInput"
              :placeholder="t('operationCore.board.searchPlaceholder')"
              prepend-inner-icon="mdi-magnify"
              variant="outlined"
              density="compact"
              hide-details
              clearable
              style="max-width: 360px"
              @click:clear="clearSearch"
            />
            <v-btn
              v-if="hasSearch"
              variant="text"
              size="small"
              class="text-none"
              prepend-icon="mdi-close"
              @click="clearSearch"
            >
              {{ t('operationCore.board.searchClear') }}
            </v-btn>
            <OcBoardListFilters
              v-if="filterableColumns.length"
              :columns="filterableColumns"
              :state-options="stateFilterOptions"
              :priority-options="priorityFilterOptions"
              :type-options="typeFilterOptions"
              :relation-options-by-key="relationOptionsByKey"
              :group-options-by-key="groupOptionsByKey"
              :tag-options-by-key="tagOptionsByKey"
              @update:filters="onFiltersUpdate"
              @advanced-open="onFiltersAdvancedOpen"
            />
            <v-spacer />
            <v-alert
              v-if="store.listError"
              type="error"
              variant="tonal"
              density="compact"
              class="mb-0 py-1"
            >
              {{ store.listError }}
            </v-alert>
          </div>
        </div>
        <v-divider />
        <v-data-table-server
          :headers="listHeaders"
          :items="listRows"
          :items-length="store.listTotal"
          :page="page"
          :items-per-page="itemsPerPage"
          :items-per-page-options="itemsPerPageOptions"
          :sort-by="sortBy"
          item-value="id"
          density="comfortable"
          :loading="store.listLoading"
          class="oc-board-list-table"
          @update:options="onListOptions"
        >
          <template v-if="listColumnKeys.includes('key')" #item.key="{ item }">
            <NuxtLink :to="item.profileTo" class="text-primary font-weight-medium text-decoration-none">
              {{ item.key }}
            </NuxtLink>
          </template>
          <template v-if="listColumnKeys.includes('title')" #item.title="{ item }">
            <NuxtLink :to="item.profileTo" class="text-decoration-none text-reset">
              {{ item.title }}
            </NuxtLink>
          </template>
          <template v-if="listColumnKeys.includes('stateId')" #item.stateId="{ item }">
            <OcBoardCatalogLabel :item="item.stateItem" />
          </template>
          <template v-if="listColumnKeys.includes('priorityId')" #item.priorityId="{ item }">
            <OcBoardCatalogLabel :item="item.priorityItem" />
          </template>
          <template v-if="listColumnKeys.includes('typeId')" #item.typeId="{ item }">
            <OcBoardCatalogLabel :item="item.typeItem" />
          </template>
          <template v-if="listColumnKeys.includes('assignee')" #item.assignee="{ item }">
            <span>{{ item.assigneeName }}</span>
          </template>
          <template v-if="listColumnKeys.includes('createdBy')" #item.createdBy="{ item }">
            <span>{{ item.createdByName }}</span>
          </template>
          <template v-if="listColumnKeys.includes('sla')" #item.sla="{ item }">
            <OcSlaStatusChip
              :sla="item.__card?.sla"
              :state-id="item.rawStateId"
              :initial-state-id="initialStateId"
              :closed-at="item.__card?.closedAt"
              dense
            />
          </template>
          <template #item.actions="{ item }">
            <div class="d-inline-flex align-center justify-end ga-1">
              <v-btn
                icon="mdi-eye-outline"
                variant="text"
                size="small"
                density="comfortable"
                :to="item.profileTo"
                :title="t('operationCore.board.actions.viewProfile')"
              />
              <v-btn
                v-if="canEdit"
                icon="mdi-trash-can-outline"
                variant="text"
                size="small"
                density="comfortable"
                color="error"
                :title="t('operationCore.board.actions.delete')"
                @click="askDelete(item)"
              />
            </div>
          </template>
        </v-data-table-server>
      </v-card>

      <OcBoardKanban
        v-else-if="showKanban"
        :columns="store.boardContext.columns"
        :column-items="store.columnItems"
        :column-loading="store.columnLoading"
        :column-loading-more="store.columnLoadingMore"
        :board-id="boardId"
        :workspace-id="embedded ? workspaceId : null"
        :profile-from="profileNavFrom"
        :editable="canEdit"
        @transition="onKanbanTransition"
        @load-more="store.loadMoreColumn($event)"
      />
      </template>

      <v-card v-else-if="!showBoardLoadingPanel" variant="outlined" class="rounded-lg">
        <v-card-text class="pa-8 text-center text-medium-emphasis">
          {{ t('operationCore.board.notFound') }}
          <div class="mt-4">
            <v-btn variant="tonal" color="primary" class="text-none" to="/apps/operation-core/workspace">
              {{ t('operationCore.board.backToWorkspace') }}
            </v-btn>
          </div>
        </v-card-text>
      </v-card>
    </div>

    <v-snackbar
      v-model="transitionSnackbar"
      :color="transitionMsgColor"
      :timeout="pendingProfileId ? 8000 : 3500"
      location="bottom right"
    >
      {{ transitionMsg }}
      <template #actions>
        <v-btn
          v-if="pendingProfileId"
          variant="text"
          class="text-none"
          @click="openPendingProfile"
        >
          {{ t('operationCore.board.transition.openProfile') }}
        </v-btn>
        <v-btn icon="mdi-close" variant="text" size="small" @click="transitionSnackbar = false" />
      </template>
    </v-snackbar>

    <OcWorkItemFormDialog
      v-if="store.boardContext"
      v-model="formDialogOpen"
      :mode="formDialogMode"
      :workspace-id="store.boardContext.workspaceId"
      :board-id="store.boardContext.boardId"
      :work-item-id="editWorkItemId"
      @saved="onWorkItemSaved"
    />

    <v-dialog v-model="deleteDialogOpen" max-width="460" persistent>
      <v-card rounded="xl">
        <v-card-title class="d-flex align-center ga-2 pt-4">
          <v-icon icon="mdi-alert-circle-outline" color="error" />
          <span class="text-h6 font-weight-bold">{{ t('operationCore.board.actions.deleteTitle') }}</span>
        </v-card-title>
        <v-card-text>
          <p class="text-body-2 mb-2">
            {{ t('operationCore.board.actions.deleteConfirm') }}
          </p>
          <p v-if="deleteTarget" class="text-body-2 font-weight-medium mb-0">
            <span v-if="deleteTarget.key" class="text-primary">{{ deleteTarget.key }}</span>
            <span v-if="deleteTarget.key && deleteTarget.title"> — </span>
            <span>{{ deleteTarget.title }}</span>
          </p>
          <p class="text-caption text-medium-emphasis mt-2 mb-0">
            {{ t('operationCore.board.actions.deleteIrreversible') }}
          </p>
          <v-alert
            v-if="deleteError"
            :type="deleteHasRelations ? 'warning' : 'error'"
            variant="tonal"
            class="mt-3 rounded-lg"
            density="compact"
          >
            {{ deleteError }}
          </v-alert>
        </v-card-text>
        <v-card-actions class="px-4 pb-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" :disabled="deleting" @click="deleteDialogOpen = false">
            {{ t('operationCore.create.cancel') }}
          </v-btn>
          <v-btn
            v-if="deleteHasRelations"
            color="error"
            variant="flat"
            rounded="lg"
            class="text-none"
            :loading="deleting"
            @click="confirmDelete(true)"
          >
            {{ t('operationCore.board.actions.deleteForce') }}
          </v-btn>
          <v-btn
            v-else
            color="error"
            variant="flat"
            rounded="lg"
            class="text-none"
            :loading="deleting"
            @click="confirmDelete()"
          >
            {{ t('operationCore.board.actions.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.min-width-0 {
  min-width: 0;
}

.oc-board-panel--embedded {
  min-height: 100%;
}

.oc-board-panel--embedded > .v-card:first-of-type {
  position: sticky;
  top: 0;
  z-index: 2;
  background: rgb(var(--v-theme-surface));
}

/* "İşlemler" sütunu (her zaman son sütun) sağa sabitlenir — çok sütunlu/yatay scroll'da hep görünür. */
.oc-board-list-table :deep(table) > thead > tr > th:last-child,
.oc-board-list-table :deep(table) > tbody > tr > td:last-child {
  position: sticky;
  right: 0;
  background: rgb(var(--v-theme-surface));
  box-shadow: -6px 0 6px -6px rgba(0, 0, 0, 0.18);
}

.oc-board-list-table :deep(table) > tbody > tr > td:last-child {
  z-index: 1;
}

.oc-board-list-table :deep(table) > thead > tr > th:last-child {
  z-index: 2;
}

.oc-board-panel__content {
  min-height: 280px;
}

.oc-board-panel__content--embedded {
  min-height: 360px;
}

.oc-board-loading-panel {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: inherit;
  padding: 2rem 1rem;
  background: rgba(var(--v-theme-surface), 0.92);
}

.oc-board-loading-panel__card {
  max-width: 420px;
  width: 100%;
  background: transparent !important;
}
</style>
