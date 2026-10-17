<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import DiCloneResourceDialog from '@/components/apps/document-intelligence/DiCloneResourceDialog.vue';
import DiDrawioEditorDialog from '@/components/apps/document-intelligence/DiDrawioEditorDialog.vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiFileVersionHistoryDialog from '@/components/apps/document-intelligence/DiFileVersionHistoryDialog.vue';
import DiFolderPickerList from '@/components/apps/document-intelligence/DiFolderPickerList.vue';
import DiGenerateFromTemplateDialog from '@/components/apps/document-intelligence/DiGenerateFromTemplateDialog.vue';
import DiLifecycleBar from '@/components/apps/document-intelligence/DiLifecycleBar.vue';
import DiMarkdownEditor from '@/components/apps/document-intelligence/DiMarkdownEditor.vue';
import DiMarkdownViewer from '@/components/apps/document-intelligence/DiMarkdownViewer.vue';
import DiMarkdownVersionHistoryDialog from '@/components/apps/document-intelligence/DiMarkdownVersionHistoryDialog.vue';
import DiPermissionsDialog from '@/components/apps/document-intelligence/DiPermissionsDialog.vue';
import DiResourceEditorDialog from '@/components/apps/document-intelligence/DiResourceEditorDialog.vue';
import DiResourceInfoDialog from '@/components/apps/document-intelligence/DiResourceInfoDialog.vue';
import DiResourcePreviewProvider from '@/components/apps/document-intelligence/DiResourcePreviewProvider.vue';
import DiResourceTagsDialog from '@/components/apps/document-intelligence/DiResourceTagsDialog.vue';
import DiResourceTree from '@/components/apps/document-intelligence/DiResourceTree.vue';
import DiSavePageDialog from '@/components/apps/document-intelligence/DiSavePageDialog.vue';
import type { DiSavePageMode } from '@/components/apps/document-intelligence/DiSavePageDialog.vue';
import PmLibraryBindDialog from '@/components/apps/project-management/PmLibraryBindDialog.vue';
import PmPickDocumentDialog from '@/components/apps/project-management/PmPickDocumentDialog.vue';
import type { PmLibraryBindKind } from '@/components/apps/project-management/PmLibraryBindDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  DI_CHILDREN_PAGE_SIZE,
  DI_CHILDREN_PAGE_SIZE_OPTIONS,
  diCreateFileResource,
  diCreateFolder,
  diCreateMarkdown,
  diCreateNativeDocument,
  diCreateNativePresentation,
  diCreateNativeSheet,
  diDelete,
  diDownloadResource,
  diErrorStatus,
  diFetchResourceExportPdf,
  diMove,
  diRename,
  diReplaceFileContent,
  diSearchTreeFolders,
  diGetBrowseContext,
  diGetById,
  diDiffResource,
  diGetSummary,
  diCancelBulkIndex,
  diGetBulkIndex,
  diIndexResource,
  diStartBulkIndex,
  diSummarizeResource,
  diGetLetterhead,
  diGetMarkdownContent,
  diGetTreeChildren,
  diListLetterheads,
  diSearch,
  diUpdateMarkdown,
  diUpdateResourceMetadata,
} from '@/services/documentIntelligenceService';
import type { DiBulkIndexJob, DiResourceDiffResult, DiResourceIndexResult, DiResourceSummaryResult } from '@/services/documentIntelligenceService';
import type {
  DiBreadcrumb,
  DiGenerateDocumentResult,
  DiLetterhead,
  DiLetterheadHeaderFields,
  DiResource,
  DiTreeNode,
} from '@/types/apps/documentIntelligence';
import { diFullPermission } from '@/types/apps/documentIntelligence';
import { isDiCloneable, isDiDrawioFile, isDiIndexable, isDiManagedDocument, isDiOfficeEditable, isDiOfficePdfExportable, isDiPreviewable, isDiPresentation, isDiSheet, isDiUploadedFile } from '@/utils/diFilePreview';
import { diPageResourceIcon, diPageResourceLabel } from '@/utils/diPageResource';
import { diCreateBlankDrawio } from '@/utils/diDrawio';
import { buildDiFolderUrl } from '@/utils/diResourceLink';
import { useAuthStore } from '@/stores/auth';
import type { PmWbsItem } from '@/types/apps/projectManagement';
import {
  ensurePmLibraryTags,
  ensureProjectLibrary,
  isPmLibraryDefaultFolder,
  isPmLibraryMissingRoot,
  pmLibraryTagColor,
  resolvePmLibraryAutoTags,
} from '@/utils/pmProjectLibrary';

const props = defineProps<{
  projectId: string;
  projectCode: string;
  hubFolderId?: string | null;
  focusResourceId?: string | null;
  wbs?: PmWbsItem[];
}>();

const emit = defineEmits<{
  'hub-ready': [id: string];
  bound: [];
  focused: [];
}>();

const MAX_FILE_MB = 20;

const { t } = useAppI18n();
const authStore = useAuthStore();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const booting = ref(false);
const missingRoot = ref(false);
const hubId = ref<string | null>(null);
const selectedFolderId = ref<string | null>(null);
const selectedFolder = ref<DiResource | null>(null);
const breadcrumb = ref<DiBreadcrumb[]>([]);
const children = ref<DiResource[]>([]);
const listingTotal = ref(0);
const listingPage = ref(1);
const listingPageSize = ref(DI_CHILDREN_PAGE_SIZE);
const listingPageSizeOptions = [...DI_CHILDREN_PAGE_SIZE_OPTIONS];
const suppressListingWatch = ref(false);
const treeNodes = ref<DiTreeNode[]>([]);
const treeLoading = ref(false);
const listingLoading = ref(false);
const searchQuery = ref('');
const indexListFilter = ref<'all' | 'done' | 'missing'>('all');
const indexingId = ref<string | null>(null);
const indexResult = ref<DiResourceIndexResult | null>(null);
const indexDialog = ref(false);
const summarizingId = ref<string | null>(null);
const summaryResult = ref<DiResourceSummaryResult | null>(null);
const aiDialog = ref(false);
const aiTab = ref<'index' | 'summary' | 'diff'>('summary');
const aiResource = ref<DiResource | null>(null);
const summaryDraft = ref('');
const summaryDraftTouched = ref(false);
const savingSummary = ref(false);
const summaryNow = ref(Date.now());
const diffTargets = ref<DiResource[]>([]);
const diffResult = ref<DiResourceDiffResult | null>(null);
const diffLoading = ref(false);
const diffPickerOpen = ref(false);
const diffGroup = ref<'changed' | 'removed' | 'added' | null>(null);
const diffExpanded = ref<Record<string, boolean>>({});
const diffShowNoise = ref(false);
const savingDiff = ref(false);
const diffPeerId = ref<string | null>(null);
let summaryClock: ReturnType<typeof setInterval> | undefined;

const summaryPercent = computed(() => {
  const result = summaryResult.value;
  if (!result) return 0;
  if (result.status === 'done') return 100;
  if (!result.total) return 0;
  return Math.min(100, Math.round((result.completed / result.total) * 100));
});

const summaryElapsedLabel = computed(() => {
  const result = summaryResult.value;
  if (!result || result.status !== 'running' || !result.updatedAt) return '';
  const then = Date.parse(result.updatedAt);
  if (Number.isNaN(then)) return '';
  const seconds = Math.max(0, Math.floor((summaryNow.value - then) / 1000));
  const minutes = Math.floor(seconds / 60);
  if (minutes >= 8) return t('documentIntelligence.summaryStall', { minutes });
  if (minutes > 0) return t('documentIntelligence.summaryElapsed', { minutes, seconds: seconds % 60 });
  return t('documentIntelligence.summaryElapsedSeconds', { seconds });
});

watch(aiDialog, (open) => {
  if (summaryClock) {
    clearInterval(summaryClock);
    summaryClock = undefined;
  }
  if (!open) return;
  summaryNow.value = Date.now();
  summaryClock = setInterval(() => {
    summaryNow.value = Date.now();
  }, 1000);
});

onBeforeUnmount(() => {
  if (summaryClock) clearInterval(summaryClock);
  clearBulkTimer();
  document.body.style.cursor = '';
  document.body.style.userSelect = '';
});
let summaryTicket = 0;
const tagColorTick = ref(0);
const TREE_WIDTH_KEY = 'pm-library-tree-width';
const TREE_WIDTH_MIN = 200;
const TREE_WIDTH_MAX = 640;

function loadTreeWidth(): number {
  if (typeof window === 'undefined') return 260;
  const raw = Number(localStorage.getItem(TREE_WIDTH_KEY));
  if (!Number.isFinite(raw)) return 260;
  return Math.min(TREE_WIDTH_MAX, Math.max(TREE_WIDTH_MIN, raw));
}

const treeWidth = ref(loadTreeWidth());
const treeResizeActive = ref(false);
let treeResizeStartX = 0;
let treeResizeStartWidth = 260;

function onTreeResizeStart(event: PointerEvent) {
  const handle = event.currentTarget as HTMLElement | null;
  if (!handle) return;
  treeResizeActive.value = true;
  treeResizeStartX = event.clientX;
  treeResizeStartWidth = treeWidth.value;
  handle.setPointerCapture(event.pointerId);
  document.body.style.cursor = 'col-resize';
  document.body.style.userSelect = 'none';
}

function onTreeResizeMove(event: PointerEvent) {
  if (!treeResizeActive.value) return;
  const next = treeResizeStartWidth + (event.clientX - treeResizeStartX);
  treeWidth.value = Math.min(TREE_WIDTH_MAX, Math.max(TREE_WIDTH_MIN, next));
}

function onTreeResizeEnd() {
  if (!treeResizeActive.value) return;
  treeResizeActive.value = false;
  document.body.style.cursor = '';
  document.body.style.userSelect = '';
  try {
    localStorage.setItem(TREE_WIDTH_KEY, String(Math.round(treeWidth.value)));
  } catch {
    // Width still applies for this visit when storage is unavailable.
  }
}
const busy = ref(false);
const folderDialog = ref(false);
const pageDialog = ref(false);
const drawingDialog = ref(false);
const drawingName = ref('');
const drawingEditorOpen = ref(false);
const drawingEditorId = ref<string | null>(null);
const drawingEditorTitle = ref('');
const nativeDocDialog = ref(false);
const nativeSheetDialog = ref(false);
const nativePresentationDialog = ref(false);
const generateFromTemplateDialog = ref(false);
const folderName = ref('');
const pageTitle = ref('');
const nativeDocName = ref('');
const nativeDocCode = ref('');
const nativeSheetName = ref('');
const nativeSheetCode = ref('');
const nativePresentationName = ref('');
const nativePresentationCode = ref('');
const nativeDocLetterheadId = ref<string | null>(null);
const nativeDocLetterhead = ref<DiLetterhead | null>(null);
const nativeDocHeaderFields = ref<DiLetterheadHeaderFields>({
  documentName: true,
  docNo: true,
  generatedAt: true,
  createPerson: false,
});
const letterheadOptions = ref<DiLetterhead[]>([]);
const fileInputEl = ref<HTMLInputElement | null>(null);
const openDoc = ref<DiResource | null>(null);
const deleteTarget = ref<DiResource | null>(null);
const deleteDialog = ref(false);
const deleting = ref(false);
const docMode = ref<'view' | 'edit'>('view');
const docContent = ref('');
const editContent = ref('');
const docVersion = ref(0);
const docLoading = ref(false);
const savingDoc = ref(false);
const saveDialog = ref(false);
const saveDialogMode = ref<DiSavePageMode>('save');
const pendingSaveAsDraft = ref(false);
const historyDialog = ref(false);
const historyResource = ref<DiResource | null>(null);
const fileHistoryDialog = ref(false);
const fileHistoryTarget = ref<DiResource | null>(null);
const infoDialog = ref(false);
const infoTarget = ref<DiResource | null>(null);
const tagsDialog = ref(false);
const tagsTarget = ref<DiResource | null>(null);
const renameDialog = ref(false);
const renameTarget = ref<DiResource | null>(null);
const renameName = ref('');
const moveDialog = ref(false);
const moveTarget = ref<DiResource | null>(null);
const moveDestId = ref<string | null>(null);
const cloneDialog = ref(false);
const cloneTarget = ref<DiResource | null>(null);
const permissionsDialog = ref(false);
const permTargetId = ref<string | null>(null);
const permTargetName = ref('');
const replaceDialog = ref(false);
const replaceTarget = ref<DiResource | null>(null);
const replaceFile = ref<File | null>(null);
const replaceInputEl = ref<HTMLInputElement | null>(null);
const fileEditorOpen = ref(false);
const fileEditorResource = ref<DiResource | null>(null);
const filePreviewOpen = ref(false);
const filePreviewResource = ref<DiResource | null>(null);
const bindDialog = ref(false);
const bindResource = ref<DiResource | null>(null);
const bindKind = ref<PmLibraryBindKind>('evidence');
let bootToken = 0;
let listingToken = 0;
let searchTimer: ReturnType<typeof setTimeout> | null = null;

const currentPerm = computed(() => selectedFolder.value?.permissions || diFullPermission());
const canCreate = computed(() => currentPerm.value.canCreate);
const canUpload = computed(() => currentPerm.value.canUpload);
const treeSelectedId = computed(() =>
  selectedFolderId.value && hubId.value && selectedFolderId.value !== hubId.value
    ? selectedFolderId.value
    : null,
);
const scopedCrumbs = computed(() => {
  const hub = hubId.value;
  if (!hub) return [];
  const idx = breadcrumb.value.findIndex((row) => row.id === hub);
  if (idx < 0) return [{ id: hub, name: t('projectManagement.library.rootLabel') }];
  return breadcrumb.value.slice(idx);
});
const listingHeaders = computed(() => [
  { title: t('projectManagement.library.colName'), key: 'name', minWidth: 240 },
  { title: t('projectManagement.library.colType'), key: 'type', width: 140 },
  { title: t('projectManagement.library.colSize'), key: 'size', width: 110, sortable: false },
  { title: t('projectManagement.library.colTags'), key: 'tags', sortable: false, minWidth: 180 },
  { title: t('projectManagement.library.colStatus'), key: 'status', width: 130 },
  { title: t('projectManagement.library.colIndex'), key: 'index', width: 150, sortable: false },
  { title: t('projectManagement.library.colCreated'), key: 'createdAt', width: 160 },
  { title: t('projectManagement.library.colCreatedBy'), key: 'createdBy', width: 160, sortable: false },
  { title: t('projectManagement.library.colUpdated'), key: 'updatedAt', width: 160 },
  {
    title: t('projectManagement.actions'),
    key: 'actions',
    width: 64,
    sortable: false,
    align: 'end' as const,
    headerProps: { class: 'pm-library-actions' },
    cellProps: { class: 'pm-library-actions' },
  },
]);
const diHref = computed(() => buildDiFolderUrl(selectedFolderId.value || hubId.value));

function resourceIcon(resource: DiResource): string {
  if (resource.type === 'folder') return 'mdi-folder-outline';
  if (resource.type === 'markdown') return diPageResourceIcon(resource);
  if (isDiDrawioFile(resource)) return 'mdi-vector-polyline';
  const mime = resource.mimeType || '';
  const ext = (resource.extension || '').toLowerCase();
  if (mime.startsWith('image/')) return 'mdi-file-image-outline';
  if (mime.includes('pdf') || ext === 'pdf') return 'mdi-file-pdf-box';
  if (mime.includes('word') || ['doc', 'docx'].includes(ext)) return 'mdi-file-word-box';
  if (mime.includes('sheet') || mime.includes('excel') || ['xls', 'xlsx', 'csv'].includes(ext)) {
    return 'mdi-file-excel-box';
  }
  if (mime.includes('presentation') || ['ppt', 'pptx'].includes(ext)) return 'mdi-file-powerpoint-box';
  return 'mdi-file-outline';
}

function resourceLabel(resource: DiResource): string {
  return diPageResourceLabel(resource);
}

function isIndexable(resource: DiResource): boolean {
  return isDiIndexable(resource);
}

function matchesIndexFilter(resource: DiResource): boolean {
  if (resource.type === 'folder' || indexListFilter.value === 'all') return true;
  const indexed = resource.indexStatus === 'done' || resource.indexStatus === 'stale';
  return indexListFilter.value === 'done' ? indexed : !indexed;
}

function indexChipLabel(status: string | null | undefined): string {
  if (status === 'done') return t('documentIntelligence.indexDone');
  if (status === 'stale') return t('documentIntelligence.indexStale');
  if (status === 'failed') return t('documentIntelligence.indexFailed');
  if (status === 'running') return t('documentIntelligence.indexRunning');
  return t('documentIntelligence.indexNone');
}

function indexChipColor(status: string | null | undefined): string {
  if (status === 'done') return 'success';
  if (status === 'stale') return 'warning';
  if (status === 'failed') return 'error';
  if (status === 'running') return 'info';
  return 'default';
}

function pageFiltered(rows: DiResource[]) {
  const filtered = rows.filter((row) => matchesIndexFilter(row));
  listingTotal.value = filtered.length;
  const pageCount = Math.max(1, Math.ceil(filtered.length / listingPageSize.value) || 1);
  if (listingPage.value > pageCount) {
    suppressListingWatch.value = true;
    listingPage.value = pageCount;
    suppressListingWatch.value = false;
  }
  const skip = (listingPage.value - 1) * listingPageSize.value;
  children.value = filtered.slice(skip, skip + listingPageSize.value);
}

const bulkDialog = ref(false);
const bulkStarting = ref(false);
const bulkJob = ref<DiBulkIndexJob | null>(null);
const bulkFolderId = ref<string | null>(null);
let bulkTimer: ReturnType<typeof setTimeout> | null = null;

const bulkDone = computed(() => {
  const job = bulkJob.value;
  if (!job) return 0;
  return job.indexedCount + job.skippedCount + job.failedCount;
});

const bulkPercent = computed(() => {
  const total = bulkJob.value?.total ?? 0;
  if (total <= 0) return 0;
  return Math.min(100, Math.round((bulkDone.value / total) * 100));
});

function clearBulkTimer() {
  if (bulkTimer) {
    clearTimeout(bulkTimer);
    bulkTimer = null;
  }
}

function scheduleBulkPoll() {
  clearBulkTimer();
  bulkTimer = setTimeout(() => {
    void pollBulk();
  }, 3000);
}

async function pollBulk() {
  const id = bulkFolderId.value;
  if (!id || !bulkDialog.value) return;
  try {
    const job = await diGetBulkIndex(id);
    if (!bulkDialog.value) return;
    bulkJob.value = job;
    if (job.status === 'running' || job.status === 'stopping') {
      scheduleBulkPoll();
      return;
    }
    if (selectedFolderId.value) await loadListing(selectedFolderId.value);
  } catch {
    if (bulkDialog.value) scheduleBulkPoll();
  }
}

async function openBulkIndex(resource: DiResource) {
  bulkFolderId.value = resource.id;
  bulkJob.value = null;
  bulkDialog.value = true;
  bulkStarting.value = true;
  clearBulkTimer();
  try {
    const existing = await diGetBulkIndex(resource.id);
    if (existing.status === 'running' || existing.status === 'stopping') {
      bulkJob.value = existing;
      scheduleBulkPoll();
      return;
    }
    bulkJob.value = await diStartBulkIndex(resource.id);
    if (bulkJob.value.status === 'running' || bulkJob.value.status === 'stopping') {
      scheduleBulkPoll();
    } else if (selectedFolderId.value) {
      await loadListing(selectedFolderId.value);
    }
  } catch (error) {
    panelError(error);
  } finally {
    bulkStarting.value = false;
  }
}

async function stopBulkIndex() {
  const id = bulkFolderId.value;
  if (!id) return;
  try {
    bulkJob.value = await diCancelBulkIndex(id);
  } catch (error) {
    panelError(error);
  }
}

function onBulkDialogToggle(open: boolean) {
  if (!open) clearBulkTimer();
}

function closeBulkIndex() {
  bulkDialog.value = false;
  clearBulkTimer();
}

async function indexDocument(resource: DiResource) {
  if (!isIndexable(resource) || indexingId.value) return;
  indexingId.value = resource.id;
  indexResult.value = null;
  try {
    const result = await diIndexResource(resource.id);
    indexResult.value = result;
    indexDialog.value = true;
    if (openDoc.value?.id === result.resource.id) openDoc.value = result.resource;
    const row = children.value.findIndex((item) => item.id === result.resource.id);
    if (row >= 0) children.value[row] = result.resource;
    if (result.status === 'done') {
      toast.push({
        title: t('projectManagement.notify.successTitle'),
        message: t('documentIntelligence.indexResult', { count: result.charCount }),
        severity: 'success',
      });
    } else if (result.error) {
      toast.push({
        title: t('errors.dg.toastTitle'),
        message: result.error,
        severity: 'error',
      });
    }
    if (selectedFolderId.value) await loadListing(selectedFolderId.value);
  } catch (error) {
    panelError(error);
  } finally {
    indexingId.value = null;
  }
}

function wait(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function formatFileSize(bytes: number | null | undefined): string {
  if (bytes == null || bytes < 0) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function summaryFileTitle(resource: DiResource): string {
  const stem = (resource.title || resource.name || 'Belge').replace(/\.[^.]+$/, '');
  return `${stem} — Özet`;
}

async function openAi(resource: DiResource) {
  if (resource.indexStatus !== 'done') return;
  aiResource.value = resource;
  aiTab.value = 'summary';
  summaryDraftTouched.value = false;
  summaryDraft.value = '';
  summaryResult.value = null;
  diffTargets.value = [];
  diffResult.value = null;
  diffPickerOpen.value = false;
  aiDialog.value = true;
  try {
    const existing = await diGetSummary(resource.id);
    if (aiResource.value?.id !== resource.id) return;
    summaryResult.value = existing;
    if (existing.text) summaryDraft.value = existing.text;
  } catch (error) {
    panelError(error);
  }
}

function onDiffTargetsPicked(rows: DiResource[]) {
  diffTargets.value = rows.slice(0, 4);
  diffResult.value = null;
}

function onDiffHubReady(id: string) {
  if (!id || hubId.value === id) return;
  hubId.value = id;
  emit('hub-ready', id);
}

function removeDiffTarget(id: string) {
  diffTargets.value = diffTargets.value.filter((item) => item.id !== id);
  diffResult.value = null;
}

async function runDiff() {
  const resource = aiResource.value;
  if (!resource || !diffTargets.value.length || diffLoading.value) return;
  diffLoading.value = true;
  diffGroup.value = null;
  diffExpanded.value = {};
  diffShowNoise.value = false;
  try {
    diffResult.value = await diDiffResource(resource.id, diffTargets.value.map((item) => item.id));
    diffPeerId.value = diffResult.value.comparisons[0]?.id ?? null;
  } catch (error) {
    panelError(error);
  } finally {
    diffLoading.value = false;
  }
}

function diffHasChanges(item: DiResourceDiffResult['comparisons'][number]): boolean {
  return item.addedCount > 0 || item.removedCount > 0 || item.changedCount > 0;
}

function toggleDiffGroup(group: 'changed' | 'removed' | 'added') {
  diffGroup.value = diffGroup.value === group ? null : group;
}

function showDiffGroup(group: 'changed' | 'removed' | 'added'): boolean {
  return diffGroup.value === null || diffGroup.value === group;
}

function diffCountText(shown: number, total: number): string {
  return shown < total ? `${shown} / ${total}` : String(total);
}

function groupDiffLines<T extends { heading: string }>(rows: T[]) {
  const groups: { heading: string; rows: Array<T & { index: number }> }[] = [];
  rows.forEach((row, index) => {
    const heading = row.heading || '';
    const tagged = { ...row, index };
    const last = groups[groups.length - 1];
    if (last && last.heading === heading) last.rows.push(tagged);
    else groups.push({ heading, rows: [tagged] });
  });
  return groups;
}

function diffLineLong(text: string): boolean {
  return text.length > 280;
}

function toggleDiffLine(key: string) {
  diffExpanded.value = { ...diffExpanded.value, [key]: !diffExpanded.value[key] };
}

interface DiffToken {
  text: string;
  changed: boolean;
}

function wordDiff(base: string, other: string): { base: DiffToken[]; other: DiffToken[] } {
  const left = base.split(/(\s+)/).filter((part) => part.length > 0);
  const right = other.split(/(\s+)/).filter((part) => part.length > 0);
  if (left.length * right.length > 40000) {
    return {
      base: [{ text: base, changed: base !== other }],
      other: [{ text: other, changed: base !== other }],
    };
  }
  const n = left.length;
  const m = right.length;
  const dp = Array.from({ length: n + 1 }, () => Array<number>(m + 1).fill(0));
  for (let i = n - 1; i >= 0; i -= 1) {
    for (let j = m - 1; j >= 0; j -= 1) {
      dp[i][j] = left[i] === right[j] ? dp[i + 1][j + 1] + 1 : Math.max(dp[i + 1][j], dp[i][j + 1]);
    }
  }
  const baseTokens: DiffToken[] = [];
  const otherTokens: DiffToken[] = [];
  let x = 0;
  let y = 0;
  while (x < n && y < m) {
    if (left[x] === right[y]) {
      baseTokens.push({ text: left[x], changed: false });
      otherTokens.push({ text: right[y], changed: false });
      x += 1;
      y += 1;
    } else if (dp[x + 1][y] >= dp[x][y + 1]) {
      baseTokens.push({ text: left[x], changed: true });
      x += 1;
    } else {
      otherTokens.push({ text: right[y], changed: true });
      y += 1;
    }
  }
  while (x < n) {
    baseTokens.push({ text: left[x], changed: true });
    x += 1;
  }
  while (y < m) {
    otherTokens.push({ text: right[y], changed: true });
    y += 1;
  }
  return { base: mergeDiffTokens(baseTokens), other: mergeDiffTokens(otherTokens) };
}

function mergeDiffTokens(tokens: DiffToken[]): DiffToken[] {
  const merged: DiffToken[] = [];
  for (const token of tokens) {
    const last = merged[merged.length - 1];
    if (last && last.changed === token.changed) last.text += token.text;
    else merged.push({ text: token.text, changed: token.changed });
  }
  return merged;
}

function diffDocLabel(name: string): string {
  return name.replace(/\.[^.]+$/, '');
}

const diffComparisons = computed(() => diffResult.value?.comparisons ?? []);

const activeDiff = computed(() => {
  const rows = diffComparisons.value;
  if (!rows.length) return null;
  return rows.find((row) => row.id === diffPeerId.value) ?? rows[0];
});

function selectDiffPeer(id: string) {
  if (diffPeerId.value === id) return;
  diffPeerId.value = id;
  diffGroup.value = null;
  diffShowNoise.value = false;
  diffExpanded.value = {};
}

function onDiffPeerTab(value: unknown) {
  if (typeof value === 'string') selectDiffPeer(value);
}

function diffNoiseCount(item: DiResourceDiffResult['comparisons'][number]): number {
  return item.noiseAddedCount + item.noiseRemovedCount + item.noiseChangedCount;
}

function diffFileTitle(resource: DiResource): string {
  const stem = (resource.title || resource.name || 'Belge').replace(/\.[^.]+$/, '');
  return `${stem} — Fark`;
}

function quoteDiff(text: string): string {
  return text.split('\n').map((line) => `> ${line}`).join('\n');
}

function appendDiffLines(lines: string[], heading: string, rows: { heading: string; text: string }[]) {
  if (!rows.length) return;
  lines.push('', `### ${heading}`);
  for (const group of groupDiffLines(rows)) {
    lines.push('', `#### ${group.heading || t('documentIntelligence.diffNoHeading')}`);
    for (const row of group.rows) lines.push('', quoteDiff(row.text));
  }
}

function appendDiffChanges(lines: string[], heading: string, rows: DiResourceDiffResult['comparisons'][number]['changed']) {
  if (!rows.length) return;
  lines.push('', `### ${heading}`);
  for (const group of groupDiffLines(rows)) {
    lines.push('', `#### ${group.heading || t('documentIntelligence.diffNoHeading')}`);
    for (const row of group.rows) {
      lines.push('', `**${t('documentIntelligence.diffBase')}**`, '', quoteDiff(row.base), '', `**${t('documentIntelligence.diffOther')}**`, '', quoteDiff(row.other));
    }
  }
}

function diffMarkdown(result: DiResourceDiffResult, includeNoise: boolean, resource: DiResource): string {
  const lines = [`# ${diffFileTitle(resource)}`, '', `${t('documentIntelligence.diffBase')}: ${result.baseName}`];
  for (const item of result.comparisons) {
    lines.push('', `## ${item.name}`);
    if (!item.compared) {
      if (item.reason) lines.push('', item.reason);
      continue;
    }
    lines.push('', t('documentIntelligence.diffSame', { count: item.unchangedCount }));
    appendDiffChanges(lines, t('documentIntelligence.diffChanged'), item.changed);
    appendDiffLines(lines, t('documentIntelligence.diffRemoved'), item.removed);
    appendDiffLines(lines, t('documentIntelligence.diffAdded'), item.added);
    if (includeNoise) {
      appendDiffChanges(lines, `${t('documentIntelligence.diffChanged')} — ${t('documentIntelligence.diffNoise', { count: item.noiseChangedCount })}`, item.noiseChanged);
      appendDiffLines(lines, `${t('documentIntelligence.diffRemoved')} — ${t('documentIntelligence.diffNoise', { count: item.noiseRemovedCount })}`, item.noiseRemoved);
      appendDiffLines(lines, `${t('documentIntelligence.diffAdded')} — ${t('documentIntelligence.diffNoise', { count: item.noiseAddedCount })}`, item.noiseAdded);
    }
  }
  return lines.join('\n');
}

async function saveDiffDocument() {
  const resource = aiResource.value;
  const result = diffResult.value;
  if (!resource || !result || savingDiff.value) return;
  savingDiff.value = true;
  try {
    await diCreateMarkdown({
      parentId: resource.parentId,
      title: diffFileTitle(resource),
      content: diffMarkdown(result, diffShowNoise.value, resource),
    });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.diffSaved'),
      severity: 'success',
    });
    if (selectedFolderId.value) await loadListing(selectedFolderId.value);
  } catch (error) {
    panelError(error);
  } finally {
    savingDiff.value = false;
  }
}

const diffWordMap = computed(() => {
  const map = new Map<string, { base: DiffToken[]; other: DiffToken[] }>();
  for (const item of diffResult.value?.comparisons ?? []) {
    item.changed.forEach((change, index) => {
      map.set(`${item.id}:c:${index}`, wordDiff(change.base, change.other));
    });
    item.noiseChanged?.forEach((change, index) => {
      map.set(`${item.id}:n:${index}`, wordDiff(change.base, change.other));
    });
  }
  return map;
});

async function saveSummaryDocument() {
  const resource = aiResource.value;
  const content = summaryDraft.value.trim();
  if (!resource || !content || savingSummary.value) return;
  savingSummary.value = true;
  try {
    await diCreateMarkdown({
      parentId: resource.parentId,
      title: summaryFileTitle(resource),
      content,
    });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.summarySaved'),
      severity: 'success',
    });
    if (selectedFolderId.value) await loadListing(selectedFolderId.value);
  } catch (error) {
    panelError(error);
  } finally {
    savingSummary.value = false;
  }
}

async function summarizeDocument(resource: DiResource) {
  if (resource.indexStatus !== 'done' || summarizingId.value) return;
  const ticket = ++summaryTicket;
  summarizingId.value = resource.id;
  summaryDraftTouched.value = false;
  summaryResult.value = {
    status: 'running',
    text: null,
    error: null,
    step: t('documentIntelligence.summary'),
    chunked: false,
    summarizedAt: null,
    completed: 0,
    total: 0,
    updatedAt: new Date().toISOString(),
    log: [],
  };
  try {
    let result = await diSummarizeResource(resource.id);
    while (ticket === summaryTicket && result.status === 'running') {
      summaryResult.value = result;
      await wait(3000);
      if (ticket !== summaryTicket) return;
      result = await diGetSummary(resource.id);
    }
    if (ticket !== summaryTicket) return;
    summaryResult.value = result;
    if (result.text && !summaryDraftTouched.value) summaryDraft.value = result.text;
    if (result.status === 'done' && result.text && !result.error) {
      toast.push({
        title: t('projectManagement.notify.successTitle'),
        message: t('documentIntelligence.summaryTitle'),
        severity: 'success',
      });
    } else if (result.error) {
      toast.push({
        title: t('errors.dg.toastTitle'),
        message: result.error,
        severity: 'error',
      });
    }
    if (selectedFolderId.value) await loadListing(selectedFolderId.value);
  } catch (error) {
    panelError(error);
  } finally {
    if (ticket === summaryTicket) summarizingId.value = null;
  }
}

function resourceTypeLabel(resource: DiResource): string {
  if (resource.type === 'folder') return t('projectManagement.library.typeFolder');
  if (resource.type === 'markdown') return t('documentIntelligence.typePage');
  if (resource.type === 'file' && isDiManagedDocument(resource)) {
    if (isDiSheet(resource)) return t('documentIntelligence.typeSpreadsheet');
    if (isDiPresentation(resource)) return t('documentIntelligence.typePresentation');
    return t('documentIntelligence.typeDocument');
  }
  if (resource.type === 'file') return t('documentIntelligence.typeFile');
  return t('projectManagement.library.typeUnknown');
}

function resourceStatusLabel(resource: DiResource): string {
  if (resource.type === 'folder') return '';
  const status = (resource.status || 'published').toLowerCase();
  if (status === 'draft') return t('documentIntelligence.lifecycle.statuses.draft');
  if (status === 'inreview') return t('documentIntelligence.lifecycle.statuses.inReview');
  return t('documentIntelligence.lifecycle.statuses.published');
}

function resourceStatusColor(resource: DiResource): string {
  const status = (resource.status || '').toLowerCase();
  if (status === 'draft') return 'warning';
  if (status === 'inreview') return 'info';
  return 'success';
}

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('tr-TR', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function tagChipColor(name: string): string {
  void tagColorTick.value;
  return pmLibraryTagColor(name) || 'primary';
}

async function currentAutoTags(): Promise<string[] | undefined> {
  return resolvePmLibraryAutoTags([
    selectedFolder.value?.name,
    ...scopedCrumbs.value.map((crumb) => crumb.name),
  ]);
}

function isInSelectedFolder(row: DiResource, folderId: string): boolean {
  return row.parentId === folderId;
}

async function loadTree() {
  if (!hubId.value) {
    treeNodes.value = [];
    return;
  }
  treeLoading.value = true;
  try {
    treeNodes.value = await diGetTreeChildren(hubId.value);
  } catch (error) {
    treeNodes.value = [];
    panelError(error, 'projectManagement.library.ensureFailed');
  } finally {
    treeLoading.value = false;
  }
}

async function loadListing(folderId: string) {
  const token = ++listingToken;
  listingLoading.value = true;
  try {
    const query = searchQuery.value.trim();
    if (query.length >= 2) {
      const result = await diSearch(query, 0, 200);
      if (token !== listingToken) return;
      pageFiltered((result.items || []).filter((row) => isInSelectedFolder(row, folderId)));
      return;
    }

    const filteringIndex = indexListFilter.value !== 'all';
    const skip = filteringIndex ? 0 : (listingPage.value - 1) * listingPageSize.value;
    const limit = filteringIndex ? 200 : listingPageSize.value;
    const context = await diGetBrowseContext(folderId, { skip, limit });
    if (token !== listingToken) return;
    const selected = context.selectedFolder;
    const hub = hubId.value;
    if (
      hub &&
      folderId !== hub &&
      selected &&
      selected.id !== hub &&
      !(selected.ancestorIds || []).includes(hub)
    ) {
      selectedFolderId.value = hub;
      suppressListingWatch.value = true;
      listingPage.value = 1;
      suppressListingWatch.value = false;
      await loadListing(hub);
      return;
    }
    selectedFolder.value = selected;
    breadcrumb.value = context.breadcrumb || [];
    if (filteringIndex) {
      pageFiltered(context.children.items || []);
    } else {
      children.value = context.children.items || [];
      listingTotal.value = context.children.total ?? children.value.length;
      const pageCount = Math.max(1, Math.ceil(listingTotal.value / listingPageSize.value) || 1);
      if (listingTotal.value > 0 && listingPage.value > pageCount) {
        suppressListingWatch.value = true;
        listingPage.value = pageCount;
        suppressListingWatch.value = false;
        await loadListing(folderId);
      }
    }
  } catch (error) {
    if (token !== listingToken) return;
    children.value = [];
    listingTotal.value = 0;
    panelError(error, 'projectManagement.library.ensureFailed');
  } finally {
    if (token === listingToken) listingLoading.value = false;
  }
}

async function selectFolder(folderId: string | null) {
  const next = folderId || hubId.value;
  if (!next) return;
  openDoc.value = null;
  docMode.value = 'view';
  if (searchTimer) clearTimeout(searchTimer);
  suppressListingWatch.value = true;
  listingPage.value = 1;
  searchQuery.value = '';
  suppressListingWatch.value = false;
  selectedFolderId.value = next;
  await loadListing(next);
}

async function loadTreeChildren(parentId: string): Promise<DiTreeNode[]> {
  return diGetTreeChildren(parentId);
}

async function loadLibraryFolders(parentId: string | null): Promise<DiTreeNode[]> {
  const hub = hubId.value;
  if (!hub) return [];
  if (!parentId) {
    let name = t('projectManagement.library.rootLabel');
    let parent: string | null = null;
    try {
      const folder = await diGetById(hub);
      name = folder.name || name;
      parent = folder.parentId;
    } catch {
      // The hub id is enough to root the picker.
    }
    return [{ id: hub, name, parentId: parent, hasChildren: true, children: [] }];
  }
  return diGetTreeChildren(parentId);
}

async function searchLibraryFolders(query: string): Promise<DiTreeNode[]> {
  const hub = hubId.value;
  const rows = await diSearchTreeFolders(query, 50);
  if (!hub) return [];
  const checks = await Promise.all(rows.map(async (row) => {
    if (row.id === hub) return row;
    try {
      const resource = await diGetById(row.id);
      if (resource.id === hub || (resource.ancestorIds || []).includes(hub)) return row;
    } catch {
      return null;
    }
    return null;
  }));
  return checks.filter((row): row is DiTreeNode => row != null);
}

function canManageFolder(resource: DiResource): boolean {
  return resource.type === 'folder' && (authStore.isAdmin || resource.permissions.canShare);
}

function hasLibraryFileActions(resource: DiResource): boolean {
  if (resource.type === 'markdown') return true;
  if (resource.type !== 'file') return false;
  return resource.permissions.canDownload || (resource.permissions.canEdit && isDiUploadedFile(resource));
}

function openResourceInfo(resource: DiResource) {
  infoTarget.value = resource;
  infoDialog.value = true;
}

function openRename(resource: DiResource) {
  if (!resource.permissions.canEdit) return;
  renameTarget.value = resource;
  renameName.value = resource.type === 'markdown' ? resource.title || resource.name : resource.name;
  renameDialog.value = true;
}

async function submitRename() {
  const target = renameTarget.value;
  const name = renameName.value.trim();
  if (!target || !name || busy.value) return;
  busy.value = true;
  try {
    await diRename(target.id, { name });
    renameDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.renamed'),
      severity: 'success',
    });
    if (openDoc.value?.id === target.id) {
      openDoc.value = { ...openDoc.value, name, title: name };
    }
    await refreshAll();
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.rename');
  } finally {
    busy.value = false;
  }
}

function openMove(resource: DiResource) {
  if (!resource.permissions.canMove) return;
  moveTarget.value = resource;
  moveDestId.value = resource.parentId || hubId.value;
  moveDialog.value = true;
}

async function submitMove() {
  const target = moveTarget.value;
  const dest = moveDestId.value;
  const hub = hubId.value;
  if (!target || !dest || !hub || busy.value) return;
  busy.value = true;
  try {
    if (dest !== hub) {
      const folder = await diGetById(dest);
      const inside = folder.id === hub || (folder.ancestorIds || []).includes(hub);
      if (!inside) return;
    }
    await diMove(target.id, { newParentId: dest });
    moveDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.moved'),
      severity: 'success',
    });
    if (openDoc.value?.id === target.id) {
      openDoc.value = null;
      await selectFolder(dest);
    } else {
      await refreshAll();
    }
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.move');
  } finally {
    busy.value = false;
  }
}

function openClone(resource: DiResource) {
  if (!isDiCloneable(resource)) return;
  cloneTarget.value = resource;
  cloneDialog.value = true;
}

async function onCloned(created: DiResource) {
  toast.push({
    title: t('projectManagement.notify.successTitle'),
    message: t('documentIntelligence.cloned'),
    severity: 'success',
  });
  await refreshAll();
  if (created.type === 'markdown') await openMarkdown(created);
}

function openResourceTags(resource: DiResource) {
  if (!resource.permissions.canEdit) return;
  tagsTarget.value = resource;
  tagsDialog.value = true;
}

function onResourceTagsSaved(updated: DiResource) {
  toast.push({
    title: t('projectManagement.notify.successTitle'),
    message: t('documentIntelligence.saved'),
    severity: 'success',
  });
  if (openDoc.value?.id === updated.id) {
    openDoc.value = {
      ...openDoc.value,
      tags: updated.tags,
      classificationTagId: updated.classificationTagId,
      kind: updated.kind,
    };
  }
  void refreshAll();
}

function openPermissions(resource: DiResource) {
  if (!canManageFolder(resource)) return;
  permTargetId.value = resource.id;
  permTargetName.value = resource.name;
  permissionsDialog.value = true;
}

function onPermissionsNotify(text: string, color: 'success' | 'error' | 'info') {
  toast.push({
    title: color === 'error' ? t('errors.dg.toastTitle') : t('projectManagement.notify.successTitle'),
    message: text,
    severity: color,
  });
}

function openRowHistory(resource: DiResource) {
  if (resource.type === 'markdown') {
    historyResource.value = resource;
    historyDialog.value = true;
    return;
  }
  if (resource.type === 'file' && resource.permissions.canDownload) {
    fileHistoryTarget.value = resource;
    fileHistoryDialog.value = true;
  }
}

function openPageHistory() {
  if (!openDoc.value) return;
  openRowHistory(openDoc.value);
}

function openLibraryPreview(resource: DiResource) {
  filePreviewResource.value = resource;
  filePreviewOpen.value = true;
}

function openLibraryEditor(resource: DiResource) {
  if (isDiDrawioFile(resource)) {
    drawingEditorId.value = resource.id;
    drawingEditorTitle.value = resource.name;
    drawingEditorOpen.value = true;
    return;
  }
  fileEditorResource.value = resource;
  fileEditorOpen.value = true;
}

function openReplace(resource: DiResource) {
  if (resource.type !== 'file' || !resource.permissions.canEdit || !isDiUploadedFile(resource)) return;
  replaceTarget.value = resource;
  replaceFile.value = null;
  replaceDialog.value = true;
}

function onReplacePick(event: Event) {
  const input = event.target as HTMLInputElement;
  replaceFile.value = input.files && input.files.length ? input.files[0] : null;
  if (replaceInputEl.value) replaceInputEl.value.value = '';
}

async function submitReplace() {
  const target = replaceTarget.value;
  const file = replaceFile.value;
  if (!target || !file || busy.value) return;
  if (file.size > MAX_FILE_MB * 1024 * 1024) {
    toast.push({
      title: t('documentIntelligence.errors.fileTooLarge', { max: MAX_FILE_MB }),
      message: t('documentIntelligence.errors.fileTooLarge', { max: MAX_FILE_MB }),
      severity: 'error',
    });
    return;
  }
  busy.value = true;
  try {
    await diReplaceFileContent(target.id, {
      content: await fileToBase64(file),
      originalFileName: file.name,
      mimeType: file.type || null,
      extension: fileExtension(file.name) || null,
      changeNote: 'replace',
    });
    replaceDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.fileReplaced'),
      severity: 'success',
    });
    await refreshAll();
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.replace');
  } finally {
    busy.value = false;
  }
}

async function downloadPdfExport(resource: DiResource) {
  if (!resource.permissions.canDownload || !isDiOfficePdfExportable(resource)) return;
  try {
    const blob = await diFetchResourceExportPdf(resource.id);
    const base = resource.fileName || resource.name || 'document';
    const pdfName = base.replace(/\.(docx|xlsx|pptx)$/i, '.pdf');
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = pdfName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  } catch (error) {
    panelError(error, 'documentIntelligence.generateFromTemplate.errors.exportPdf');
  }
}

async function refreshAll() {
  await Promise.all([loadTree(), selectedFolderId.value ? loadListing(selectedFolderId.value) : Promise.resolve()]);
}

async function boot() {
  if (!props.projectId || !props.projectCode.trim()) return;
  const token = ++bootToken;
  booting.value = true;
  missingRoot.value = false;
  try {
    const nextHub = await ensureProjectLibrary(props.projectId, props.projectCode, props.hubFolderId);
    if (token !== bootToken) return;
    hubId.value = nextHub;
    selectedFolderId.value = nextHub;
    suppressListingWatch.value = true;
    listingPage.value = 1;
    suppressListingWatch.value = false;
    emit('hub-ready', nextHub);
    await Promise.all([
      loadTree(),
      loadListing(nextHub),
      ensurePmLibraryTags()
        .then(() => {
          tagColorTick.value += 1;
        })
        .catch(() => undefined),
    ]);
    await focusResource(props.focusResourceId);
  } catch (error) {
    if (token !== bootToken) return;
    hubId.value = null;
    if (isPmLibraryMissingRoot(error)) {
      missingRoot.value = true;
      return;
    }
    panelError(error, 'projectManagement.library.ensureFailed');
  } finally {
    if (token === bootToken) booting.value = false;
  }
}

function suggestDocumentNo() {
  const code = (props.projectCode || 'DOC').trim().replace(/\s+/g, '-');
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${code}-${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}`;
}

function openFolderDialog() {
  folderName.value = '';
  folderDialog.value = true;
}

function openPageDialog() {
  pageTitle.value = '';
  pageDialog.value = true;
}

function openDrawingDialog() {
  drawingName.value = '';
  drawingDialog.value = true;
}

function openNativeDocDialog() {
  nativeDocName.value = '';
  nativeDocCode.value = suggestDocumentNo();
  nativeDocLetterheadId.value = null;
  nativeDocLetterhead.value = null;
  nativeDocHeaderFields.value = {
    documentName: true,
    docNo: true,
    generatedAt: true,
    createPerson: false,
  };
  nativeDocDialog.value = true;
  void loadLetterheadOptions();
}

function openNativeSheetDialog() {
  nativeSheetName.value = '';
  nativeSheetCode.value = suggestDocumentNo();
  nativeSheetDialog.value = true;
}

function openNativePresentationDialog() {
  nativePresentationName.value = '';
  nativePresentationCode.value = suggestDocumentNo();
  nativePresentationDialog.value = true;
}

function openGenerateFromTemplateDialog() {
  if (!selectedFolderId.value) return;
  generateFromTemplateDialog.value = true;
}

function triggerUpload() {
  fileInputEl.value?.click();
}

async function loadLetterheadOptions() {
  try {
    const res = await diListLetterheads(true);
    letterheadOptions.value = res.items;
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.letterheadsLoad');
  }
}

async function onNativeLetterheadChange(letterheadId: string | null) {
  nativeDocLetterhead.value = null;
  if (!letterheadId) return;
  try {
    const letterhead = await diGetLetterhead(letterheadId);
    nativeDocLetterhead.value = letterhead;
    nativeDocHeaderFields.value = { ...letterhead.settings.headerFields };
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.letterheadsLoad');
  }
}

async function openCreatedResource(id: string, options?: { edit?: boolean }) {
  await refreshAll();
  try {
    await openResource(await diGetById(id), options);
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.docLoad');
  }
}

async function focusResource(id?: string | null) {
  if (!id) return;
  emit('focused');
  try {
    const resource = await diGetById(id);
    const folderId = resource.type === 'folder' ? resource.id : resource.parentId;
    if (folderId) await selectFolder(folderId);
    if (resource.type !== 'folder') await openResource(resource);
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.docLoad');
  }
}

async function submitFolder() {
  const name = folderName.value.trim();
  const parentId = selectedFolderId.value;
  if (!name || !parentId) return;
  busy.value = true;
  try {
    await diCreateFolder({ name, parentId });
    folderDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.folderCreated'),
      severity: 'success',
    });
    await refreshAll();
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.create');
  } finally {
    busy.value = false;
  }
}

async function submitDrawing() {
  const title = drawingName.value.trim();
  const parentId = selectedFolderId.value;
  if (!title || !parentId) return;
  busy.value = true;
  try {
    const created = await diCreateBlankDrawio(parentId, title, { tags: await currentAutoTags() });
    drawingDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.drawingCreated'),
      severity: 'success',
    });
    await openCreatedResource(created.id, { edit: true });
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.create');
  } finally {
    busy.value = false;
  }
}

async function submitPage() {
  const title = pageTitle.value.trim();
  const parentId = selectedFolderId.value;
  if (!title || !parentId) return;
  busy.value = true;
  try {
    const created = await diCreateMarkdown({
      parentId,
      title,
      content: '',
      isDraft: false,
      tags: await currentAutoTags(),
    });
    pageDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.pageCreated'),
      severity: 'success',
    });
    await openCreatedResource(created.id, { edit: true });
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.create');
  } finally {
    busy.value = false;
  }
}

async function submitNativeDoc() {
  const name = nativeDocName.value.trim();
  const documentNo = nativeDocCode.value.trim();
  const parentId = selectedFolderId.value;
  if (!name || !documentNo || !parentId) return;
  const letterheadId = nativeDocLetterheadId.value?.trim() || null;
  busy.value = true;
  try {
    const created = await diCreateNativeDocument({
      parentId,
      name,
      documentNo,
      letterheadId,
      selectedHeaderFields: letterheadId ? { ...nativeDocHeaderFields.value } : null,
      tags: await currentAutoTags(),
    });
    nativeDocDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.nativeDocumentCreated'),
      severity: 'success',
    });
    await openCreatedResource(created.id);
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.create');
  } finally {
    busy.value = false;
  }
}

async function submitNativeSheet() {
  const name = nativeSheetName.value.trim();
  const parentId = selectedFolderId.value;
  if (!name || !parentId) return;
  busy.value = true;
  try {
    const created = await diCreateNativeSheet({
      parentId,
      name,
      documentNo: nativeSheetCode.value.trim() || null,
      tags: await currentAutoTags(),
    });
    nativeSheetDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.nativeSheetCreated'),
      severity: 'success',
    });
    await openCreatedResource(created.id);
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.create');
  } finally {
    busy.value = false;
  }
}

async function submitNativePresentation() {
  const name = nativePresentationName.value.trim();
  const parentId = selectedFolderId.value;
  if (!name || !parentId) return;
  busy.value = true;
  try {
    const created = await diCreateNativePresentation({
      parentId,
      name,
      documentNo: nativePresentationCode.value.trim() || null,
      tags: await currentAutoTags(),
    });
    nativePresentationDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.nativePresentationCreated'),
      severity: 'success',
    });
    await openCreatedResource(created.id);
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.create');
  } finally {
    busy.value = false;
  }
}

async function applyAutoTags(resourceId: string) {
  const tags = await currentAutoTags();
  if (!tags?.length) return;
  try {
    const resource = await diGetById(resourceId);
    const merged = [...new Set([...(resource.tags || []), ...tags])];
    await diUpdateResourceMetadata(resourceId, { tags: merged });
  } catch {
    // Tag catalog or metadata update is optional; the document itself already exists.
  }
}

async function onGenerateFromTemplateCreated(result: DiGenerateDocumentResult) {
  generateFromTemplateDialog.value = false;
  toast.push({
    title: t('projectManagement.notify.successTitle'),
    message: t('documentIntelligence.generateFromTemplate.created'),
    severity: 'success',
  });
  if (result.resourceId) {
    await applyAutoTags(result.resourceId);
    await openCreatedResource(result.resourceId);
  } else {
    await refreshAll();
  }
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result || '');
      resolve(result.includes(',') ? result.split(',')[1] : result);
    };
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}

function fileExtension(name: string): string {
  const i = name.lastIndexOf('.');
  return i >= 0 ? name.slice(i + 1).toLowerCase() : '';
}

async function onFilePick(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files && input.files.length ? input.files[0] : null;
  if (fileInputEl.value) fileInputEl.value.value = '';
  if (!file || !selectedFolderId.value) return;
  if (file.size > MAX_FILE_MB * 1024 * 1024) {
    toast.push({
      title: t('documentIntelligence.errors.fileTooLarge', { max: MAX_FILE_MB }),
      message: t('documentIntelligence.errors.fileTooLarge', { max: MAX_FILE_MB }),
      severity: 'error',
    });
    return;
  }
  busy.value = true;
  try {
    await diCreateFileResource({
      parentId: selectedFolderId.value,
      name: file.name,
      originalFileName: file.name,
      content: await fileToBase64(file),
      mimeType: file.type || null,
      extension: fileExtension(file.name) || null,
      size: file.size,
      tags: await currentAutoTags(),
    });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.fileUploaded'),
      severity: 'success',
    });
    await refreshAll();
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.upload');
  } finally {
    busy.value = false;
  }
}

async function openResource(resource: DiResource, options?: { edit?: boolean }) {
  if (resource.type === 'folder') {
    await selectFolder(resource.id);
    return;
  }
  if (resource.type === 'markdown') {
    await openMarkdown(resource, options);
    return;
  }
  if (resource.type === 'file') {
    if (options?.edit && isDiDrawioFile(resource)) {
      drawingEditorId.value = resource.id;
      drawingEditorTitle.value = resource.name;
      drawingEditorOpen.value = true;
      return;
    }
    if (isDiOfficeEditable(resource)) {
      fileEditorResource.value = resource;
      fileEditorOpen.value = true;
      return;
    }
    if (isDiPreviewable(resource)) {
      filePreviewResource.value = resource;
      filePreviewOpen.value = true;
      return;
    }
    await downloadFile(resource);
  }
}

async function openMarkdown(resource: DiResource, options?: { edit?: boolean }) {
  openDoc.value = resource;
  docMode.value = options?.edit ? 'edit' : 'view';
  docLoading.value = true;
  try {
    const content = await diGetMarkdownContent(resource.id);
    docContent.value = content.content;
    editContent.value = content.content;
    docVersion.value = content.currentVersionNumber;
    if (resource.parentId && selectedFolderId.value !== resource.parentId) {
      selectedFolderId.value = resource.parentId;
    }
  } catch (error) {
    openDoc.value = null;
    panelError(error, 'documentIntelligence.errors.docLoad');
  } finally {
    docLoading.value = false;
  }
}

function backToFolder() {
  const parentId = openDoc.value?.parentId || selectedFolderId.value || hubId.value;
  void selectFolder(parentId);
}

function startEdit() {
  editContent.value = docContent.value;
  docMode.value = 'edit';
}

function cancelEdit() {
  docMode.value = 'view';
  editContent.value = docContent.value;
}

function openSaveDialog(asDraft: boolean) {
  if (asDraft) saveDialogMode.value = 'draft';
  else if (openDoc.value?.status === 'draft') saveDialogMode.value = 'publish';
  else saveDialogMode.value = 'save';
  pendingSaveAsDraft.value = asDraft;
  saveDialog.value = true;
}

async function confirmSaveEdit(changeNote: string) {
  const ok = await saveEdit(pendingSaveAsDraft.value, changeNote);
  if (ok) saveDialog.value = false;
}

async function saveEdit(asDraft = false, changeNote = ''): Promise<boolean> {
  if (!openDoc.value) return false;
  savingDoc.value = true;
  try {
    const updated = await diUpdateMarkdown(openDoc.value.id, {
      content: editContent.value,
      expectedVersionNumber: docVersion.value,
      isDraft: asDraft,
      changeNote: changeNote || null,
    });
    docContent.value = editContent.value;
    docVersion.value = updated.currentVersionNumber || docVersion.value + 1;
    openDoc.value = updated;
    docMode.value = 'view';
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: asDraft ? t('documentIntelligence.draftSaved') : t('documentIntelligence.published'),
      severity: 'success',
    });
    return true;
  } catch (error) {
    if (diErrorStatus(error) === 409) {
      panelError(error, 'documentIntelligence.errors.conflict');
      await openMarkdown(openDoc.value);
    } else {
      panelError(error, 'documentIntelligence.errors.save');
    }
    return false;
  } finally {
    savingDoc.value = false;
  }
}

function onLifecycleUpdated(updated: DiResource) {
  if (openDoc.value?.id === updated.id) openDoc.value = updated;
  void refreshAll();
}

async function onVersionRestored(restored: DiResource) {
  await openMarkdown(restored);
}

function onEditorSaved(updated: DiResource) {
  fileEditorResource.value = updated;
  void refreshAll();
}

function onPreviewUpdated(updated: DiResource) {
  filePreviewResource.value = updated;
  void refreshAll();
}

async function downloadFile(resource: DiResource) {
  try {
    const { blob, fileName } = await diDownloadResource(resource.id, resource.fileName || resource.name);
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName || resource.fileName || resource.name || 'dosya';
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.download');
  }
}

function canBindResource(resource: DiResource | null | undefined) {
  return Boolean(resource && resource.type !== 'folder');
}

function suggestBindKind(): PmLibraryBindKind {
  const folder = (selectedFolder.value?.name || '').toLocaleLowerCase('tr');
  if (folder.includes('karar')) return 'decision';
  if (folder.includes('plan') || folder.includes('wiki')) return 'reference';
  return 'evidence';
}

function openBind(resource: DiResource) {
  if (!canBindResource(resource)) return;
  bindResource.value = resource;
  bindKind.value = suggestBindKind();
  bindDialog.value = true;
}

function openDelete(resource: DiResource) {
  if (!resource.permissions.canDelete) return;
  deleteTarget.value = resource;
  deleteDialog.value = true;
}

async function submitLibraryDelete() {
  const target = deleteTarget.value;
  if (!target || deleting.value) return;
  deleting.value = true;
  try {
    await diDelete(target.id);
    deleteDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('documentIntelligence.deleted'),
      severity: 'success',
    });
    const parentId = target.parentId || selectedFolderId.value || hubId.value;
    if (openDoc.value?.id === target.id) {
      openDoc.value = null;
      if (parentId) await selectFolder(parentId);
      else await refreshAll();
    } else {
      await refreshAll();
    }
  } catch (error) {
    panelError(error);
  } finally {
    deleting.value = false;
  }
}

function onBound() {
  emit('bound');
}

function onListingRowClick(_event: Event, ctx: { item: DiResource }) {
  void openResource(ctx.item);
}

watch(indexListFilter, () => {
  if (!selectedFolderId.value) return;
  suppressListingWatch.value = true;
  listingPage.value = 1;
  suppressListingWatch.value = false;
  void loadListing(selectedFolderId.value);
});

watch(searchQuery, () => {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    if (!selectedFolderId.value) return;
    suppressListingWatch.value = true;
    listingPage.value = 1;
    suppressListingWatch.value = false;
    void loadListing(selectedFolderId.value);
  }, 350);
});

watch([listingPage, listingPageSize], () => {
  if (suppressListingWatch.value || !selectedFolderId.value) return;
  void loadListing(selectedFolderId.value);
});

watch(
  () => [props.projectId, props.projectCode] as const,
  () => {
    void boot();
  },
  { immediate: true },
);

watch(
  () => props.focusResourceId,
  (id) => {
    if (!id || booting.value || !hubId.value) return;
    void focusResource(id);
  },
);
</script>

<template>
  <DiResourcePreviewProvider :on-download="downloadFile" flat>
  <div class="pm-library">
    <div class="d-flex align-start justify-space-between flex-wrap ga-2 px-6 pt-4 pb-2">
      <p class="text-body-2 text-medium-emphasis mb-0" style="max-width: 720px">
        {{ t('projectManagement.library.hint') }}
      </p>
      <v-btn
        v-if="hubId"
        :to="diHref"
        variant="text"
        size="small"
        class="text-none"
        prepend-icon="mdi-open-in-new"
      >
        {{ t('projectManagement.library.openInDi') }}
      </v-btn>
    </div>

    <v-alert
      v-if="missingRoot"
      type="warning"
      variant="tonal"
      class="mx-6 mb-4"
    >
      {{ t('projectManagement.library.missingRoot') }}
      <template #append>
        <v-btn :to="'/apps/document-intelligence'" variant="text" class="text-none" size="small">
          {{ t('projectManagement.library.openInDi') }}
        </v-btn>
      </template>
    </v-alert>

    <v-progress-linear v-if="booting" indeterminate color="primary" />

    <div
      v-else-if="hubId"
      class="pm-library__layout d-flex"
      :style="{ '--pm-library-tree-width': treeWidth + 'px' }"
    >
      <div class="pm-library__tree flex-shrink-0">
        <div class="d-flex align-center justify-space-between px-3 py-2 border-b">
          <span class="text-subtitle-2 font-weight-bold">{{ t('documentIntelligence.explorer') }}</span>
        </div>
        <div class="pa-2 pm-library__tree-scroll">
          <v-progress-linear v-if="treeLoading" indeterminate color="primary" class="mb-2" />
          <DiResourceTree
            :nodes="treeNodes"
            :selected-id="treeSelectedId"
            :root-label="t('projectManagement.library.rootLabel')"
            :empty-label="t('documentIntelligence.noFolders')"
            :load-children="loadTreeChildren"
            @select="selectFolder"
          />
        </div>
      </div>
      <div
        class="pm-library__resize flex-shrink-0"
        :class="{ 'pm-library__resize--active': treeResizeActive }"
        role="separator"
        aria-orientation="vertical"
        @pointerdown="onTreeResizeStart"
        @pointermove="onTreeResizeMove"
        @pointerup="onTreeResizeEnd"
        @pointercancel="onTreeResizeEnd"
      />

      <div class="pm-library__content flex-grow-1 min-width-0">
        <template v-if="openDoc">
          <div class="d-flex align-center ga-2 px-4 py-2 border-b flex-wrap">
            <v-btn size="small" variant="text" class="text-none" prepend-icon="mdi-arrow-left" @click="backToFolder">
              {{ t('documentIntelligence.backToFolder') }}
            </v-btn>
            <v-icon :icon="diPageResourceIcon(openDoc)" color="primary" />
            <span class="text-subtitle-1 font-weight-bold text-truncate">{{ resourceLabel(openDoc) }}</span>
            <v-chip size="x-small" variant="tonal">v{{ docVersion }}</v-chip>
            <DiLifecycleBar
              :resource="openDoc"
              :can-edit="openDoc.permissions.canEdit"
              :show-actions="docMode === 'view'"
              @updated="onLifecycleUpdated"
            />
            <v-spacer />
            <template v-if="docMode === 'view'">
              <v-btn
                v-if="openDoc.permissions.canEdit"
                size="small"
                variant="tonal"
                class="text-none"
                prepend-icon="mdi-pencil"
                @click="startEdit"
              >
                {{ t('documentIntelligence.edit') }}
              </v-btn>
              <v-btn size="small" variant="text" class="text-none" prepend-icon="mdi-history" @click="openPageHistory">
                {{ t('documentIntelligence.history') }}
              </v-btn>
              <v-btn
                v-if="isIndexable(openDoc)"
                size="small"
                variant="tonal"
                color="primary"
                class="text-none"
                prepend-icon="mdi-database-search"
                :loading="indexingId === openDoc.id"
                @click="indexDocument(openDoc)"
              >
                {{ t('documentIntelligence.index') }}
              </v-btn>
              <v-btn
                v-if="openDoc.indexStatus === 'done'"
                size="small"
                variant="tonal"
                color="secondary"
                class="text-none"
                prepend-icon="mdi-creation"
                @click="openAi(openDoc)"
              >
                {{ t('documentIntelligence.aiTitle') }}
              </v-btn>
              <v-btn
                size="small"
                variant="tonal"
                class="text-none"
                prepend-icon="mdi-link-plus"
                @click="openBind(openDoc)"
              >
                {{ t('projectManagement.library.bind.action') }}
              </v-btn>
            </template>
            <template v-else>
              <v-btn variant="text" size="small" class="text-none" @click="cancelEdit">{{ t('documentIntelligence.cancel') }}</v-btn>
              <v-btn color="primary" variant="flat" size="small" class="text-none" :loading="savingDoc" @click="openSaveDialog(false)">
                {{ t('documentIntelligence.save') }}
              </v-btn>
            </template>
          </div>
          <div class="pa-4 pm-library__doc-scroll">
            <v-progress-linear v-if="docLoading" indeterminate color="primary" class="mb-3" />
            <DiMarkdownEditor
              v-else-if="docMode === 'edit'"
              v-model="editContent"
              :current-resource-id="openDoc.id"
              :upload-parent-id="openDoc.parentId || selectedFolderId"
              :can-upload="openDoc.permissions.canUpload"
            />
            <DiMarkdownViewer v-else :content="docContent" />
          </div>
        </template>

        <template v-else>
          <div class="d-flex align-center ga-2 px-4 py-2 border-b flex-wrap">
          <v-text-field
            v-model="searchQuery"
            density="compact"
            variant="solo-filled"
            flat
            hide-details
            clearable
            prepend-inner-icon="mdi-magnify"
            :placeholder="t('projectManagement.library.search')"
            class="pm-library__search flex-grow-1"
          />
          <v-btn
            icon
            size="small"
            variant="text"
            :title="t('documentIntelligence.refresh')"
            :loading="listingLoading || treeLoading"
            @click="refreshAll"
          >
            <v-icon>mdi-refresh</v-icon>
          </v-btn>
          <v-spacer />
          <v-btn
            v-if="canUpload"
            size="small"
            variant="outlined"
            color="primary"
            class="text-none"
            prepend-icon="mdi-upload"
            :loading="busy"
            @click="triggerUpload"
          >
            {{ t('documentIntelligence.uploadFile') }}
          </v-btn>
          <v-menu v-if="canCreate" location="bottom end">
            <template #activator="{ props: menuProps }">
              <v-btn
                v-bind="menuProps"
                color="primary"
                variant="flat"
                size="small"
                class="text-none"
                prepend-icon="mdi-plus"
              >
                {{ t('documentIntelligence.browseToolbar.new') }}
              </v-btn>
            </template>
            <v-list density="compact" min-width="260" class="py-1">
              <v-list-subheader class="text-uppercase">
                {{ t('documentIntelligence.browseToolbar.groupFolder') }}
              </v-list-subheader>
              <v-list-item
                prepend-icon="mdi-folder-plus-outline"
                :title="t('documentIntelligence.newFolder')"
                rounded="lg"
                @click="openFolderDialog"
              />
              <v-divider class="my-1" />
              <v-list-subheader class="text-uppercase">
                {{ t('documentIntelligence.browseToolbar.groupContent') }}
              </v-list-subheader>
              <v-list-item
                prepend-icon="mdi-book-plus-outline"
                :title="t('documentIntelligence.newPage')"
                rounded="lg"
                @click="openPageDialog"
              />
              <v-list-item
                prepend-icon="mdi-vector-polyline"
                :title="t('documentIntelligence.newDrawing')"
                rounded="lg"
                @click="openDrawingDialog"
              />
              <v-list-item
                prepend-icon="mdi-file-document-plus-outline"
                :title="t('documentIntelligence.generateFromTemplate.menu')"
                rounded="lg"
                @click="openGenerateFromTemplateDialog"
              />
              <v-divider class="my-1" />
              <v-list-subheader class="text-uppercase">
                {{ t('documentIntelligence.browseToolbar.groupOffice') }}
              </v-list-subheader>
              <v-list-item
                prepend-icon="mdi-file-word-box"
                :title="t('documentIntelligence.newNativeDocument')"
                rounded="lg"
                @click="openNativeDocDialog"
              />
              <v-list-item
                prepend-icon="mdi-file-excel-box"
                :title="t('documentIntelligence.newNativeSheet')"
                rounded="lg"
                @click="openNativeSheetDialog"
              />
              <v-list-item
                prepend-icon="mdi-file-powerpoint-box"
                :title="t('documentIntelligence.newNativePresentation')"
                rounded="lg"
                @click="openNativePresentationDialog"
              />
            </v-list>
          </v-menu>
          <input ref="fileInputEl" type="file" class="d-none" @change="onFilePick" />
        </div>

        <div class="px-4 py-2 d-flex align-center flex-wrap ga-1">
          <a
            v-for="(crumb, index) in scopedCrumbs"
            :key="crumb.id"
            class="pm-library__crumb"
            :class="{ 'pm-library__crumb--current': index === scopedCrumbs.length - 1 }"
            @click="index === scopedCrumbs.length - 1 ? undefined : selectFolder(crumb.id)"
          >
            <span v-if="index > 0" class="text-medium-emphasis mr-1">/</span>
            {{ index === 0 ? t('projectManagement.library.rootLabel') : crumb.name }}
          </a>
        </div>

        <div class="px-4 pt-3 d-flex align-center flex-wrap ga-2">
          <v-chip
            size="small"
            :variant="indexListFilter === 'all' ? 'flat' : 'outlined'"
            :color="indexListFilter === 'all' ? 'primary' : undefined"
            @click="indexListFilter = 'all'"
          >
            {{ t('documentIntelligence.indexFilterAll') }}
          </v-chip>
          <v-chip
            size="small"
            :variant="indexListFilter === 'done' ? 'flat' : 'outlined'"
            :color="indexListFilter === 'done' ? 'success' : undefined"
            @click="indexListFilter = 'done'"
          >
            {{ t('documentIntelligence.indexFilterDone') }}
          </v-chip>
          <v-chip
            size="small"
            :variant="indexListFilter === 'missing' ? 'flat' : 'outlined'"
            :color="indexListFilter === 'missing' ? 'warning' : undefined"
            @click="indexListFilter = 'missing'"
          >
            {{ t('documentIntelligence.indexFilterMissing') }}
          </v-chip>
        </div>

        <div class="pa-4">
          <div class="pm-library-table-wrap">
            <v-data-table-server
              v-model:page="listingPage"
              v-model:items-per-page="listingPageSize"
              :headers="listingHeaders"
              :items="children"
              :items-length="listingTotal"
              :items-per-page-options="listingPageSizeOptions"
              :loading="listingLoading"
              item-value="id"
              density="comfortable"
              hover
              class="rounded-lg border pm-library-table"
              @click:row="onListingRowClick"
            >
              <template #item.name="{ item }">
                <div class="d-flex align-center ga-2 min-width-0">
                  <v-icon :icon="resourceIcon(item)" color="primary" size="20" />
                  <div class="min-width-0">
                    <div class="text-body-2 font-weight-medium text-truncate">{{ resourceLabel(item) }}</div>
                    <div v-if="item.description" class="text-caption text-medium-emphasis text-truncate">
                      {{ item.description }}
                    </div>
                  </div>
                  <v-chip
                    v-if="item.type === 'folder' && isPmLibraryDefaultFolder(item.name)"
                    size="x-small"
                    variant="tonal"
                  >
                    {{ t('projectManagement.library.systemFolder') }}
                  </v-chip>
                </div>
              </template>
              <template #item.type="{ item }">
                <v-chip size="x-small" variant="tonal">{{ resourceTypeLabel(item) }}</v-chip>
              </template>
              <template #item.size="{ item }">
                <span class="text-body-2">{{ item.type === 'folder' ? '—' : formatFileSize(item.size) }}</span>
              </template>
              <template #item.tags="{ item }">
                <div v-if="item.tags?.length" class="d-flex flex-wrap ga-1">
                  <v-chip
                    v-for="tag in item.tags"
                    :key="`${item.id}-${tag}`"
                    size="x-small"
                    :color="tagChipColor(tag)"
                    variant="tonal"
                  >
                    {{ tag }}
                  </v-chip>
                </div>
                <span v-else class="text-medium-emphasis">{{ t('projectManagement.library.noTags') }}</span>
              </template>
              <template #item.status="{ item }">
                <v-chip
                  v-if="item.type !== 'folder'"
                  size="x-small"
                  :color="resourceStatusColor(item)"
                  variant="tonal"
                >
                  {{ resourceStatusLabel(item) }}
                </v-chip>
                <span v-else class="text-medium-emphasis">—</span>
              </template>
              <template #item.index="{ item }">
                <v-chip
                  v-if="item.type !== 'folder'"
                  size="x-small"
                  variant="tonal"
                  :color="indexChipColor(item.indexStatus)"
                >
                  {{ indexChipLabel(item.indexStatus) }}
                </v-chip>
                <span v-else class="text-medium-emphasis">—</span>
              </template>
              <template #item.updatedAt="{ item }">
                <span class="text-body-2">{{ item.type === 'folder' ? '—' : (formatDateTime(item.updatedAt) || '—') }}</span>
              </template>
              <template #item.createdAt="{ item }">
                <span class="text-body-2">{{ item.type === 'folder' ? '—' : (formatDateTime(item.createdAt) || '—') }}</span>
              </template>
              <template #item.createdBy="{ item }">
                <span class="text-body-2 text-truncate d-inline-block library-person">
                  {{ item.type === 'folder' ? '—' : (item.createdBy || '—') }}
                </span>
              </template>
              <template #item.actions="{ item }">
                <div class="d-flex justify-end" @click.stop>
                  <v-menu location="bottom end">
                    <template #activator="{ props: menuProps }">
                      <v-btn
                        v-bind="menuProps"
                        icon
                        size="small"
                        variant="text"
                        :title="t('projectManagement.actions')"
                      >
                        <v-icon size="18">mdi-dots-vertical</v-icon>
                      </v-btn>
                    </template>
                    <v-list density="compact" min-width="240">
                      <v-list-item
                        prepend-icon="mdi-folder-open-outline"
                        :title="t('projectManagement.library.open')"
                        @click="openResource(item)"
                      />
                      <v-list-item
                        prepend-icon="mdi-information-outline"
                        :title="t('documentIntelligence.resourceInfoTitle')"
                        @click="openResourceInfo(item)"
                      />
                      <v-list-item
                        v-if="item.permissions.canEdit"
                        prepend-icon="mdi-pencil-box-outline"
                        :title="t('documentIntelligence.rename')"
                        @click="openRename(item)"
                      />
                      <v-list-item
                        v-if="item.permissions.canEdit"
                        prepend-icon="mdi-tag-outline"
                        :title="t('documentIntelligence.tags.editTitle')"
                        @click="openResourceTags(item)"
                      />
                      <v-list-item
                        v-if="item.permissions.canMove"
                        prepend-icon="mdi-folder-move-outline"
                        :title="t('documentIntelligence.move')"
                        @click="openMove(item)"
                      />
                      <v-list-item
                        v-if="isDiCloneable(item)"
                        prepend-icon="mdi-content-copy"
                        :title="t('documentIntelligence.clone')"
                        @click="openClone(item)"
                      />
                      <template v-if="hasLibraryFileActions(item)">
                        <v-divider class="my-1" />
                        <v-list-item
                          v-if="item.type === 'file' && item.permissions.canDownload"
                          prepend-icon="mdi-download"
                          :title="t('documentIntelligence.download')"
                          @click="downloadFile(item)"
                        />
                        <v-list-item
                          v-if="item.type === 'file' && item.permissions.canDownload && isDiOfficePdfExportable(item)"
                          prepend-icon="mdi-file-pdf-box"
                          :title="t('documentIntelligence.exportPdf')"
                          @click="downloadPdfExport(item)"
                        />
                        <v-list-item
                          v-if="item.type === 'file' && item.permissions.canEdit && isDiUploadedFile(item)"
                          prepend-icon="mdi-file-replace-outline"
                          :title="t('documentIntelligence.replaceFile')"
                          @click="openReplace(item)"
                        />
                        <v-list-item
                          v-if="item.type === 'markdown' || (item.type === 'file' && item.permissions.canDownload)"
                          prepend-icon="mdi-history"
                          :title="t('documentIntelligence.versionHistory')"
                          @click="openRowHistory(item)"
                        />
                        <v-list-item
                          v-if="item.type === 'file' && item.permissions.canDownload && isDiPreviewable(item)"
                          prepend-icon="mdi-file-eye-outline"
                          :title="t('documentIntelligence.preview')"
                          @click="openLibraryPreview(item)"
                        />
                        <v-list-item
                          v-if="item.type === 'file' && item.permissions.canDownload && (isDiOfficeEditable(item) || isDiDrawioFile(item))"
                          prepend-icon="mdi-open-in-new"
                          :title="t('documentIntelligence.openInEditor')"
                          @click="openLibraryEditor(item)"
                        />
                      </template>
                      <v-list-item
                        v-if="canManageFolder(item)"
                        prepend-icon="mdi-shield-account-outline"
                        :title="t('documentIntelligence.permissions.menuTitle')"
                        @click="openPermissions(item)"
                      />
                      <v-list-item
                        v-if="canBindResource(item)"
                        prepend-icon="mdi-link-plus"
                        :title="t('projectManagement.library.bind.action')"
                        @click="openBind(item)"
                      />
                      <template v-if="item.type === 'folder'">
                        <v-divider class="my-1" />
                        <v-list-subheader>{{ t('documentIntelligence.menuAdvanced') }}</v-list-subheader>
                        <v-list-item
                          prepend-icon="mdi-database-search"
                          :title="t('documentIntelligence.bulkIndex')"
                          @click="openBulkIndex(item)"
                        />
                      </template>
                      <template v-if="isIndexable(item) || item.indexStatus === 'done'">
                        <v-divider class="my-1" />
                        <v-list-subheader>{{ t('documentIntelligence.menuAdvanced') }}</v-list-subheader>
                        <v-list-item
                          v-if="isIndexable(item)"
                          prepend-icon="mdi-database-search"
                          :title="t('documentIntelligence.index')"
                          :disabled="indexingId === item.id"
                          @click="indexDocument(item)"
                        />
                        <v-list-item
                          v-if="item.indexStatus === 'done'"
                          prepend-icon="mdi-creation"
                          :title="t('documentIntelligence.summaryAndDiff')"
                          @click="openAi(item)"
                        />
                      </template>
                      <template v-if="item.permissions.canDelete">
                        <v-divider class="my-1" />
                        <v-list-item
                          prepend-icon="mdi-delete-outline"
                          class="text-error"
                          :title="t('projectManagement.library.remove')"
                          @click="openDelete(item)"
                        />
                      </template>
                    </v-list>
                  </v-menu>
                </div>
              </template>
              <template #no-data>
                <div class="text-medium-emphasis text-body-2 py-8 text-center">
                  {{ searchQuery.trim() ? t('projectManagement.library.emptySearch') : t('projectManagement.library.empty') }}
                </div>
              </template>
            </v-data-table-server>
          </div>
        </div>
        </template>
      </div>
    </div>

    <v-dialog v-model="deleteDialog" max-width="420">
      <v-card v-if="deleteTarget" rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('projectManagement.library.remove') }}</v-card-title>
        <v-card-text>
          {{ t('documentIntelligence.deleteConfirm', { name: resourceLabel(deleteTarget) }) }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="error" variant="flat" class="text-none" :loading="deleting" @click="submitLibraryDelete">
            {{ t('projectManagement.library.remove') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="aiDialog" max-width="1100" :persistent="diffPickerOpen">
      <v-card v-if="aiResource" rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.aiTitle') }}</v-card-title>
        <v-card-subtitle class="pb-0">{{ aiResource.title || aiResource.name }}</v-card-subtitle>
        <v-tabs v-model="aiTab" color="primary" density="comfortable" class="px-2">
          <v-tab value="index">{{ t('documentIntelligence.aiTabIndex') }}</v-tab>
          <v-tab value="summary">{{ t('documentIntelligence.aiTabSummary') }}</v-tab>
          <v-tab value="diff">{{ t('documentIntelligence.aiTabDiff') }}</v-tab>
        </v-tabs>
        <v-divider />
        <v-card-text>
          <div v-show="aiTab === 'index'">
            <div class="text-body-2">{{ t('documentIntelligence.indexSize', { count: (aiResource.indexCharCount ?? 0).toLocaleString('tr-TR') }) }}</div>
            <div v-if="aiResource.indexedAt" class="text-caption text-medium-emphasis mt-1">
              {{ formatDateTime(aiResource.indexedAt) }}
            </div>
          </div>
          <div v-show="aiTab === 'summary'">
            <template v-if="summaryResult && (summaryResult.status === 'running' || summaryResult.log.length)">
              <v-progress-linear
                :indeterminate="summaryResult.status === 'running' && !summaryResult.total"
                :model-value="summaryPercent"
                color="primary"
                height="8"
                rounded
                class="mb-3"
              />
              <div class="d-flex align-center justify-space-between ga-2 mb-2">
                <span class="text-body-2">{{ summaryResult.step }}</span>
                <span v-if="summaryResult.total" class="text-caption text-medium-emphasis">
                  {{ summaryResult.completed }}/{{ summaryResult.total }}
                </span>
              </div>
              <div v-if="summaryElapsedLabel" class="text-caption text-medium-emphasis mb-2">{{ summaryElapsedLabel }}</div>
              <div v-if="summaryResult.log.length" class="summary-log mb-3">
                <div
                  v-for="(line, index) in summaryResult.log"
                  :key="index"
                  :class="summaryResult.status === 'failed' && index === summaryResult.log.length - 1 ? 'text-error' : ''"
                >
                  {{ line }}
                </div>
              </div>
              <div v-if="summaryResult.error && summaryResult.status !== 'running'" class="text-error text-body-2 mb-2">
                {{ summaryResult.error }}
              </div>
              <div v-if="summaryResult.chunked" class="text-caption text-medium-emphasis mb-2">
                {{ t('documentIntelligence.summaryChunked') }}
              </div>
            </template>
            <DiMarkdownEditor
              v-model="summaryDraft"
              compact
              :show-preview="false"
              :current-resource-id="aiResource.id"
              :upload-parent-id="aiResource.parentId || selectedFolderId"
              :can-upload="aiResource.permissions.canUpload"
              @update:model-value="summaryDraftTouched = true"
            />
            <div class="d-flex justify-end ga-2 mt-3">
              <v-btn
                variant="tonal"
                class="text-none"
                :loading="summarizingId === aiResource.id"
                :disabled="summarizingId === aiResource.id"
                @click="summarizeDocument(aiResource)"
              >
                {{ t('documentIntelligence.summary') }}
              </v-btn>
              <v-btn
                color="primary"
                variant="flat"
                class="text-none"
                :loading="savingSummary"
                :disabled="!summaryDraft.trim() || summarizingId === aiResource.id"
                @click="saveSummaryDocument"
              >
                {{ t('documentIntelligence.saveSummary') }}
              </v-btn>
            </div>
          </div>
          <div v-show="aiTab === 'diff'">
            <p class="text-body-2 mb-3">{{ t('documentIntelligence.diffHint') }}</p>
            <v-btn
              variant="tonal"
              class="text-none"
              prepend-icon="mdi-file-search-outline"
              @click="diffPickerOpen = true"
            >
              {{ t('projectManagement.pickDocument.open') }}
            </v-btn>
            <div v-if="diffTargets.length" class="d-flex flex-wrap ga-2 mt-3">
              <v-chip
                v-for="target in diffTargets"
                :key="target.id"
                closable
                size="small"
                @click:close="removeDiffTarget(target.id)"
              >
                {{ target.title || target.name }}
              </v-chip>
            </div>
            <div class="d-flex justify-end mt-3">
              <v-btn
                color="primary"
                variant="flat"
                class="text-none"
                :loading="diffLoading"
                :disabled="!diffTargets.length"
                @click="runDiff"
              >
                {{ t('documentIntelligence.diffCompare') }}
              </v-btn>
              <v-btn
                variant="tonal"
                class="text-none"
                :loading="savingDiff"
                :disabled="!diffResult || diffLoading"
                @click="saveDiffDocument"
              >
                {{ t('documentIntelligence.saveDiff') }}
              </v-btn>
            </div>
            <div v-if="diffResult && activeDiff" class="mt-4">
              <v-tabs
                v-if="diffComparisons.length > 1"
                :model-value="activeDiff.id"
                color="primary"
                density="comfortable"
                show-arrows
                class="mb-2"
                @update:model-value="onDiffPeerTab"
              >
                <v-tab
                  v-for="peer in diffComparisons"
                  :key="peer.id"
                  :value="peer.id"
                  class="text-none"
                >
                  {{ diffDocLabel(peer.name) }}
                </v-tab>
              </v-tabs>
              <div class="text-subtitle-2">
                {{ diffDocLabel(diffResult.baseName) }} → {{ diffDocLabel(activeDiff.name) }}
              </div>
              <div
                v-for="item in [activeDiff]"
                :key="item.id"
                class="mb-4"
              >
                <div v-if="!item.compared" class="text-body-2 text-medium-emphasis mt-1">{{ item.reason }}</div>
                <template v-else>
                  <div class="text-caption text-medium-emphasis mt-1">
                    {{ t('documentIntelligence.diffSame', { count: item.unchangedCount }) }}
                  </div>
                  <div class="d-flex flex-wrap ga-2 mt-3">
                    <v-btn
                      size="small"
                      class="text-none"
                      :color="diffGroup === 'changed' ? 'warning' : undefined"
                      :variant="diffGroup === 'changed' ? 'flat' : 'tonal'"
                      :disabled="!item.changedCount"
                      @click="toggleDiffGroup('changed')"
                    >
                      {{ t('documentIntelligence.diffChanged') }} {{ diffCountText(item.changed.length, item.changedCount) }}
                    </v-btn>
                    <v-btn
                      size="small"
                      class="text-none"
                      :color="diffGroup === 'removed' ? 'error' : undefined"
                      :variant="diffGroup === 'removed' ? 'flat' : 'tonal'"
                      :disabled="!item.removedCount"
                      @click="toggleDiffGroup('removed')"
                    >
                      {{ t('documentIntelligence.diffRemoved') }} {{ diffCountText(item.removed.length, item.removedCount) }}
                    </v-btn>
                    <v-btn
                      size="small"
                      class="text-none"
                      :color="diffGroup === 'added' ? 'success' : undefined"
                      :variant="diffGroup === 'added' ? 'flat' : 'tonal'"
                      :disabled="!item.addedCount"
                      @click="toggleDiffGroup('added')"
                    >
                      {{ t('documentIntelligence.diffAdded') }} {{ diffCountText(item.added.length, item.addedCount) }}
                    </v-btn>
                    <v-btn
                      v-if="diffNoiseCount(item)"
                      size="small"
                      class="text-none"
                      :variant="diffShowNoise ? 'flat' : 'text'"
                      @click="diffShowNoise = !diffShowNoise"
                    >
                      {{ t('documentIntelligence.diffNoise', { count: diffNoiseCount(item) }) }}
                    </v-btn>
                  </div>
                  <div v-if="item.truncated" class="text-caption text-medium-emphasis mt-2">
                    {{ t('documentIntelligence.diffTruncated') }}
                  </div>
                  <div v-if="!diffHasChanges(item)" class="text-body-2 mt-2">
                    {{ t('documentIntelligence.diffNone') }}
                  </div>
                  <div v-if="showDiffGroup('changed') && item.changed.length" class="mt-3">
                    <div v-for="group in groupDiffLines(item.changed)" :key="`c-${group.rows[0].index}`" class="mb-3">
                      <div class="text-caption font-weight-medium text-primary mb-2">
                        {{ group.heading || t('documentIntelligence.diffNoHeading') }}
                      </div>
                      <div v-for="change in group.rows" :key="`c-${change.index}`" class="diff-pair mb-2">
                        <div class="diff-side">
                          <div class="text-caption text-medium-emphasis mb-1">{{ t('documentIntelligence.diffBase') }}</div>
                          <div :class="{ 'diff-clamp': diffLineLong(change.base) && !diffExpanded[`${item.id}:c:${change.index}`] }">
                            <span
                              v-for="(token, tokenIndex) in diffWordMap.get(`${item.id}:c:${change.index}`)?.base"
                              :key="`b-${tokenIndex}`"
                              :class="token.changed ? 'diff-del' : 'diff-same'"
                            >{{ token.text }}</span>
                          </div>
                        </div>
                        <div class="diff-side">
                          <div class="text-caption text-medium-emphasis mb-1">{{ t('documentIntelligence.diffOther') }}</div>
                          <div :class="{ 'diff-clamp': diffLineLong(change.other) && !diffExpanded[`${item.id}:c:${change.index}`] }">
                            <span
                              v-for="(token, tokenIndex) in diffWordMap.get(`${item.id}:c:${change.index}`)?.other"
                              :key="`o-${tokenIndex}`"
                              :class="token.changed ? 'diff-ins' : 'diff-same'"
                            >{{ token.text }}</span>
                          </div>
                        </div>
                        <v-btn
                          v-if="diffLineLong(change.base) || diffLineLong(change.other)"
                          variant="text"
                          size="small"
                          class="text-none diff-pair-span"
                          @click="toggleDiffLine(`${item.id}:c:${change.index}`)"
                        >
                          {{ diffExpanded[`${item.id}:c:${change.index}`] ? t('documentIntelligence.diffCollapse') : t('documentIntelligence.diffExpand') }}
                        </v-btn>
                      </div>
                    </div>
                  </div>
                  <div v-if="showDiffGroup('removed') && item.removed.length" class="mt-3">
                    <div v-for="group in groupDiffLines(item.removed)" :key="`r-${group.rows[0].index}`" class="mb-3">
                      <div class="text-caption font-weight-medium text-primary mb-2">
                        {{ group.heading || t('documentIntelligence.diffNoHeading') }}
                      </div>
                      <div v-for="line in group.rows" :key="`r-${line.index}`" class="diff-side mb-2">
                        <div :class="{ 'diff-clamp': diffLineLong(line.text) && !diffExpanded[`${item.id}:r:${line.index}`] }">
                          <span class="diff-del">{{ line.text }}</span>
                        </div>
                        <v-btn
                          v-if="diffLineLong(line.text)"
                          variant="text"
                          size="small"
                          class="text-none px-0"
                          @click="toggleDiffLine(`${item.id}:r:${line.index}`)"
                        >
                          {{ diffExpanded[`${item.id}:r:${line.index}`] ? t('documentIntelligence.diffCollapse') : t('documentIntelligence.diffExpand') }}
                        </v-btn>
                      </div>
                    </div>
                  </div>
                  <div v-if="showDiffGroup('added') && item.added.length" class="mt-3">
                    <div v-for="group in groupDiffLines(item.added)" :key="`a-${group.rows[0].index}`" class="mb-3">
                      <div class="text-caption font-weight-medium text-primary mb-2">
                        {{ group.heading || t('documentIntelligence.diffNoHeading') }}
                      </div>
                      <div v-for="line in group.rows" :key="`a-${line.index}`" class="diff-side mb-2">
                        <div :class="{ 'diff-clamp': diffLineLong(line.text) && !diffExpanded[`${item.id}:a:${line.index}`] }">
                          <span class="diff-ins">{{ line.text }}</span>
                        </div>
                        <v-btn
                          v-if="diffLineLong(line.text)"
                          variant="text"
                          size="small"
                          class="text-none px-0"
                          @click="toggleDiffLine(`${item.id}:a:${line.index}`)"
                        >
                          {{ diffExpanded[`${item.id}:a:${line.index}`] ? t('documentIntelligence.diffCollapse') : t('documentIntelligence.diffExpand') }}
                        </v-btn>
                      </div>
                    </div>
                  </div>
                  <div v-if="diffShowNoise && showDiffGroup('changed') && item.noiseChanged.length" class="mt-3">
                    <div v-for="group in groupDiffLines(item.noiseChanged)" :key="`nc-${group.rows[0].index}`" class="mb-3">
                      <div class="text-caption font-weight-medium text-medium-emphasis mb-2">
                        {{ group.heading || t('documentIntelligence.diffNoHeading') }}
                      </div>
                      <div v-for="change in group.rows" :key="`nc-${change.index}`" class="diff-pair mb-2">
                        <div class="diff-side">
                          <div class="text-caption text-medium-emphasis mb-1">{{ t('documentIntelligence.diffBase') }}</div>
                          <div :class="{ 'diff-clamp': diffLineLong(change.base) && !diffExpanded[`${item.id}:n:${change.index}`] }">
                            <span
                              v-for="(token, tokenIndex) in diffWordMap.get(`${item.id}:n:${change.index}`)?.base"
                              :key="`nb-${tokenIndex}`"
                              :class="token.changed ? 'diff-del' : 'diff-same'"
                            >{{ token.text }}</span>
                          </div>
                        </div>
                        <div class="diff-side">
                          <div class="text-caption text-medium-emphasis mb-1">{{ t('documentIntelligence.diffOther') }}</div>
                          <div :class="{ 'diff-clamp': diffLineLong(change.other) && !diffExpanded[`${item.id}:n:${change.index}`] }">
                            <span
                              v-for="(token, tokenIndex) in diffWordMap.get(`${item.id}:n:${change.index}`)?.other"
                              :key="`no-${tokenIndex}`"
                              :class="token.changed ? 'diff-ins' : 'diff-same'"
                            >{{ token.text }}</span>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                  <div v-if="diffShowNoise && showDiffGroup('removed') && item.noiseRemoved.length" class="mt-3">
                    <div v-for="group in groupDiffLines(item.noiseRemoved)" :key="`nr-${group.rows[0].index}`" class="mb-3">
                      <div class="text-caption text-medium-emphasis mb-2">
                        {{ group.heading || t('documentIntelligence.diffNoHeading') }}
                      </div>
                      <div v-for="line in group.rows" :key="`nr-${line.index}`" class="diff-side mb-2">
                        <span class="diff-del">{{ line.text }}</span>
                      </div>
                    </div>
                  </div>
                  <div v-if="diffShowNoise && showDiffGroup('added') && item.noiseAdded.length" class="mt-3">
                    <div v-for="group in groupDiffLines(item.noiseAdded)" :key="`na-${group.rows[0].index}`" class="mb-3">
                      <div class="text-caption text-medium-emphasis mb-2">
                        {{ group.heading || t('documentIntelligence.diffNoHeading') }}
                      </div>
                      <div v-for="line in group.rows" :key="`na-${line.index}`" class="diff-side mb-2">
                        <span class="diff-ins">{{ line.text }}</span>
                      </div>
                    </div>
                  </div>
                </template>
              </div>
            </div>
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="aiDialog = false">{{ t('documentIntelligence.close') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="bulkDialog" max-width="720" @update:model-value="onBulkDialogToggle">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">
          {{ t('documentIntelligence.bulkIndexTitle') }}
          <span v-if="bulkJob?.folderName" class="text-medium-emphasis"> — {{ bulkJob.folderName }}</span>
        </v-card-title>
        <v-card-text>
          <div v-if="bulkStarting && !bulkJob" class="text-body-2">{{ t('documentIntelligence.bulkIndexWait') }}</div>
          <template v-else-if="bulkJob">
            <div v-if="bulkJob.status === 'running' && bulkJob.currentName" class="text-body-2 mb-2">
              {{ t('documentIntelligence.bulkIndexCurrent', { name: bulkJob.currentName }) }}
            </div>
            <div v-else-if="bulkJob.status === 'stopping'" class="text-body-2 mb-2">{{ t('documentIntelligence.bulkIndexStopping') }}</div>
            <div v-else-if="bulkJob.status === 'done' && bulkJob.total === 0" class="text-body-2 mb-2">{{ t('documentIntelligence.bulkIndexEmpty') }}</div>
            <div v-else-if="bulkJob.status === 'done'" class="text-body-2 mb-2">{{ t('documentIntelligence.bulkIndexDone') }}</div>
            <div v-else-if="bulkJob.status === 'stopped'" class="text-body-2 mb-2">{{ t('documentIntelligence.bulkIndexStopped') }}</div>
            <v-progress-linear
              :model-value="bulkPercent"
              color="primary"
              height="8"
              rounded
            />
            <div class="text-caption text-medium-emphasis mt-1 mb-3">
              {{ t('documentIntelligence.bulkIndexProgress', { done: bulkDone, total: bulkJob.total }) }}
            </div>
            <v-row dense>
              <v-col cols="12" md="4">
                <div class="text-caption font-weight-bold mb-1">{{ t('documentIntelligence.bulkIndexIndexed') }} ({{ bulkJob.indexedCount }})</div>
                <div class="bulk-index-list">
                  <div v-for="item in bulkJob.indexedItems" :key="item.id" class="text-body-2 text-truncate">{{ item.name }}</div>
                </div>
              </v-col>
              <v-col cols="12" md="4">
                <div class="text-caption font-weight-bold mb-1">{{ t('documentIntelligence.bulkIndexSkipped') }} ({{ bulkJob.skippedCount }})</div>
                <div class="bulk-index-list">
                  <div v-for="item in bulkJob.skippedItems" :key="item.id" class="text-body-2 text-truncate">{{ item.name }}</div>
                </div>
              </v-col>
              <v-col cols="12" md="4">
                <div class="text-caption font-weight-bold mb-1">{{ t('documentIntelligence.bulkIndexFailed') }} ({{ bulkJob.failedCount }})</div>
                <div class="bulk-index-list">
                  <div v-for="item in bulkJob.failedItems" :key="item.id" class="text-body-2">
                    <div class="text-truncate">{{ item.name }}</div>
                    <div v-if="item.error" class="text-caption text-error">{{ item.error }}</div>
                  </div>
                </div>
              </v-col>
            </v-row>
            <div v-if="bulkJob.status === 'running' || bulkJob.status === 'stopping'" class="text-caption text-medium-emphasis mt-3">
              {{ t('documentIntelligence.bulkIndexHint') }}
            </div>
          </template>
        </v-card-text>
        <v-card-actions>
          <v-btn
            v-if="bulkJob && (bulkJob.status === 'running' || bulkJob.status === 'stopping')"
            variant="text"
            color="error"
            class="text-none"
            :disabled="bulkJob.status === 'stopping'"
            @click="stopBulkIndex"
          >
            {{ t('documentIntelligence.bulkIndexStop') }}
          </v-btn>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="closeBulkIndex">{{ t('documentIntelligence.close') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="indexDialog" max-width="480">
      <v-card v-if="indexResult" rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.index') }}</v-card-title>
        <v-card-text>
          <div class="text-body-2">{{ indexResult.steps.join(' · ') }}</div>
          <div v-if="indexResult.status === 'done'" class="mt-2">
            {{ t('documentIntelligence.indexResult', { count: indexResult.charCount }) }}
          </div>
          <div v-else-if="indexResult.error" class="mt-2 text-error">{{ indexResult.error }}</div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="indexDialog = false">{{ t('documentIntelligence.close') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="folderDialog" max-width="420">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.newFolder') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="folderName"
            :label="t('documentIntelligence.folderName')"
            variant="outlined"
            density="comfortable"
            autofocus
            hide-details
            @keydown.enter="submitFolder"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="folderDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="busy" :disabled="!folderName.trim()" @click="submitFolder">
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="pageDialog" max-width="420">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.newPage') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="pageTitle"
            :label="t('documentIntelligence.pageTitle')"
            variant="outlined"
            density="comfortable"
            autofocus
            hide-details
            @keydown.enter="submitPage"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="pageDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="busy" :disabled="!pageTitle.trim()" @click="submitPage">
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="drawingDialog" max-width="420">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.newDrawing') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="drawingName"
            :label="t('documentIntelligence.drawingNameLabel')"
            variant="outlined"
            density="comfortable"
            autofocus
            hide-details
            @keydown.enter="submitDrawing"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="drawingDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="busy" :disabled="!drawingName.trim()" @click="submitDrawing">
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="nativeDocDialog" max-width="520">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.newNativeDocument') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="nativeDocCode"
            :label="t('documentIntelligence.documentNoLabel')"
            :hint="t('documentIntelligence.documentNoHint')"
            variant="outlined"
            density="comfortable"
            persistent-hint
            autofocus
            class="mb-3"
            @keydown.enter="submitNativeDoc"
          />
          <v-text-field
            v-model="nativeDocName"
            :label="t('documentIntelligence.nativeDocumentNameLabel')"
            variant="outlined"
            density="comfortable"
            hide-details
            class="mb-3"
            @keydown.enter="submitNativeDoc"
          />
          <v-select
            v-model="nativeDocLetterheadId"
            :items="letterheadOptions"
            item-title="name"
            item-value="id"
            :label="t('documentIntelligence.letterheadOptionalLabel')"
            variant="outlined"
            density="comfortable"
            hide-details
            clearable
            class="mb-3"
            :no-data-text="t('documentIntelligence.noLetterheads')"
            @update:model-value="onNativeLetterheadChange"
          />
          <template v-if="nativeDocLetterhead">
            <div class="text-caption text-medium-emphasis mb-2">
              {{ t('documentIntelligence.nativeDocHeaderParamsHint') }}
            </div>
            <v-checkbox
              v-if="nativeDocLetterhead.settings.headerFields.documentName"
              v-model="nativeDocHeaderFields.documentName"
              :label="t('documentIntelligence.nativeDocParamDocumentName')"
              density="compact"
              hide-details
              class="mt-0 pt-0"
            />
            <v-checkbox
              v-if="nativeDocLetterhead.settings.headerFields.docNo"
              v-model="nativeDocHeaderFields.docNo"
              :label="t('documentIntelligence.nativeDocParamDocNo')"
              density="compact"
              hide-details
              class="mt-0 pt-0"
            />
            <v-checkbox
              v-if="nativeDocLetterhead.settings.headerFields.generatedAt"
              v-model="nativeDocHeaderFields.generatedAt"
              :label="t('documentIntelligence.nativeDocParamGeneratedAt')"
              density="compact"
              hide-details
              class="mt-0 pt-0"
            />
            <v-checkbox
              v-if="nativeDocLetterhead.settings.headerFields.createPerson"
              v-model="nativeDocHeaderFields.createPerson"
              :label="t('documentIntelligence.nativeDocParamCreatePerson')"
              density="compact"
              hide-details
              class="mt-0 pt-0"
            />
          </template>
          <p v-else class="text-caption text-medium-emphasis mb-0">
            {{ t('documentIntelligence.nativeDocNoLetterheadHint') }}
          </p>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="nativeDocDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="busy"
            :disabled="!nativeDocName.trim() || !nativeDocCode.trim()"
            @click="submitNativeDoc"
          >
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="nativeSheetDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.newNativeSheet') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="nativeSheetCode"
            :label="t('documentIntelligence.documentNoLabel')"
            :hint="t('documentIntelligence.nativeSheetCodeHint')"
            persistent-hint
            density="comfortable"
            variant="outlined"
            class="mb-3"
          />
          <v-text-field
            v-model="nativeSheetName"
            :label="t('documentIntelligence.nativeSheetNameLabel')"
            density="comfortable"
            variant="outlined"
            autofocus
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="nativeSheetDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="busy"
            :disabled="!nativeSheetName.trim()"
            @click="submitNativeSheet"
          >
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="nativePresentationDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.newNativePresentation') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="nativePresentationCode"
            :label="t('documentIntelligence.documentNoLabel')"
            :hint="t('documentIntelligence.nativePresentationCodeHint')"
            persistent-hint
            density="comfortable"
            variant="outlined"
            class="mb-3"
          />
          <v-text-field
            v-model="nativePresentationName"
            :label="t('documentIntelligence.nativePresentationNameLabel')"
            density="comfortable"
            variant="outlined"
            autofocus
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="nativePresentationDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="busy"
            :disabled="!nativePresentationName.trim()"
            @click="submitNativePresentation"
          >
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <PmPickDocumentDialog
      v-model="diffPickerOpen"
      :project-id="projectId"
      :project-code="projectCode"
      :hub-folder-id="hubId"
      indexed-only
      multiple
      :max-selection="4"
      :exclude-resource-ids="aiResource ? [aiResource.id] : []"
      :initial-selection="diffTargets"
      :title="t('documentIntelligence.diffPickTitle')"
      :hint="t('documentIntelligence.diffHint')"
      :confirm-label="t('documentIntelligence.diffPickConfirm')"
      @pick-many="onDiffTargetsPicked"
      @hub-ready="onDiffHubReady"
    />
    <PmLibraryBindDialog
      v-model="bindDialog"
      :project-id="projectId"
      :resource="bindResource"
      :wbs="wbs || []"
      :initial-kind="bindKind"
      @bound="onBound"
    />
    <DiGenerateFromTemplateDialog
      v-model="generateFromTemplateDialog"
      :parent-folder-id="selectedFolderId"
      @created="onGenerateFromTemplateCreated"
    />
    <DiSavePageDialog
      v-model="saveDialog"
      :mode="saveDialogMode"
      :loading="savingDoc"
      @confirm="confirmSaveEdit"
    />
    <v-dialog v-model="renameDialog" max-width="420">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.rename') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="renameName"
            :label="t('documentIntelligence.newName')"
            variant="outlined"
            density="comfortable"
            autofocus
            hide-details
            @keydown.enter="submitRename"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="renameDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="busy" :disabled="!renameName.trim()" @click="submitRename">
            {{ t('documentIntelligence.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="moveDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.move') }}</v-card-title>
        <v-card-text>
          <DiFolderPickerList
            v-model="moveDestId"
            :exclude-subtree-id="moveTarget?.type === 'folder' ? moveTarget.id : null"
            :load-children="loadLibraryFolders"
            :search-folders="searchLibraryFolders"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="moveDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="busy" :disabled="!moveDestId" @click="submitMove">
            {{ t('documentIntelligence.move') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="replaceDialog" max-width="460">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.replaceFile') }}</v-card-title>
        <v-card-text>
          <input ref="replaceInputEl" type="file" class="d-none" @change="onReplacePick" />
          <div
            class="di-dropzone d-flex flex-column align-center justify-center text-center pa-6 rounded-lg"
            @click="replaceInputEl?.click()"
          >
            <v-icon size="40" class="text-medium-emphasis mb-2">mdi-cloud-upload-outline</v-icon>
            <div v-if="!replaceFile" class="text-body-2 text-medium-emphasis">
              {{ t('documentIntelligence.selectFileHint', { max: MAX_FILE_MB }) }}
            </div>
            <div v-else class="text-body-2 font-weight-medium">{{ replaceFile.name }}</div>
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="replaceDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="busy" :disabled="!replaceFile" @click="submitReplace">
            {{ t('documentIntelligence.upload') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <DiCloneResourceDialog
      v-model="cloneDialog"
      :resource="cloneTarget"
      :load-children="loadLibraryFolders"
      :search-folders="searchLibraryFolders"
      :loading="busy"
      @cloned="onCloned"
    />

    <DiResourceInfoDialog v-model="infoDialog" :resource="infoTarget" />
    <DiResourceTagsDialog v-model="tagsDialog" :resource="tagsTarget" @saved="onResourceTagsSaved" />
    <DiPermissionsDialog
      v-model="permissionsDialog"
      :folder-id="permTargetId"
      :folder-name="permTargetName"
      @changed="refreshAll"
      @notify="onPermissionsNotify"
    />
    <DiFileVersionHistoryDialog
      v-model="fileHistoryDialog"
      :resource="fileHistoryTarget"
      :can-restore="fileHistoryTarget?.permissions.canEdit ?? false"
      @restored="refreshAll"
    />

    <DiMarkdownVersionHistoryDialog
      v-model="historyDialog"
      :resource-id="historyResource?.id ?? null"
      :can-restore="historyResource?.permissions.canEdit ?? false"
      @restored="onVersionRestored"
    />
    <DiResourceEditorDialog
      v-model="fileEditorOpen"
      :resource="fileEditorResource"
      @saved="onEditorSaved"
    />
    <DiFilePreviewDialog
      v-model="filePreviewOpen"
      :resource="filePreviewResource"
      @download="downloadFile"
      @updated="onPreviewUpdated"
    />
    <DiDrawioEditorDialog
      v-model="drawingEditorOpen"
      :resource-id="drawingEditorId"
      :title="drawingEditorTitle"
      @saved="refreshAll"
    />
  </div>
  </DiResourcePreviewProvider>
</template>

<style scoped>
.bulk-index-list {
  max-height: 180px;
  overflow: auto;
}
.pm-library__layout {
  min-height: 520px;
  border-top: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
.pm-library__tree {
  width: var(--pm-library-tree-width, 260px);
  display: flex;
  flex-direction: column;
}
.pm-library__resize {
  width: 6px;
  margin-left: -3px;
  cursor: col-resize;
  touch-action: none;
  position: relative;
  z-index: 2;
}
.pm-library__resize::before {
  content: '';
  position: absolute;
  top: 0;
  bottom: 0;
  left: 2px;
  width: 1px;
  background: rgba(var(--v-theme-on-surface), 0.08);
}
.pm-library__resize:hover::before,
.pm-library__resize--active::before {
  left: 1px;
  width: 3px;
  background: rgba(var(--v-theme-primary), 0.45);
}
.pm-library__tree-scroll {
  overflow: auto;
  flex: 1 1 auto;
  max-height: 70vh;
}
.pm-library__content {
  min-width: 0;
  display: flex;
  flex-direction: column;
}
.pm-library__doc-scroll {
  overflow: auto;
  flex: 1 1 auto;
  max-height: 70vh;
}
.pm-library__search {
  max-width: 360px;
  min-width: 160px;
}
.pm-library__crumb {
  cursor: pointer;
  color: rgb(var(--v-theme-primary));
  font-size: 0.875rem;
}
.pm-library__crumb:hover {
  text-decoration: underline;
}
.pm-library__crumb--current {
  color: rgba(var(--v-theme-on-surface), 0.87);
  font-weight: 600;
  cursor: default;
  text-decoration: none;
}
.pm-library-table-wrap {
  min-width: 0;
}
.pm-library-table :deep(tbody tr) {
  cursor: pointer;
}
.pm-library-table :deep(.v-table__wrapper > table > thead > tr > th.pm-library-actions),
.pm-library-table.v-table--hover :deep(.v-table__wrapper > table > tbody > tr > td.pm-library-actions) {
  position: sticky;
  right: 0;
  left: auto;
  z-index: 2;
  background: rgb(var(--v-theme-surface));
  box-shadow: -4px 0 8px -4px rgba(0, 0, 0, 0.12);
  white-space: nowrap;
}
.pm-library-table :deep(.v-table__wrapper > table > thead > tr > th.pm-library-actions) {
  z-index: 3;
}
.library-person {
  max-width: 150px;
}
.di-dropzone {
  border: 2px dashed rgba(var(--v-theme-on-surface), 0.2);
  cursor: pointer;
}
.di-dropzone:hover {
  border-color: rgb(var(--v-theme-primary));
  background-color: rgba(var(--v-theme-primary), 0.04);
}
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.summary-log {
  max-height: 140px;
  overflow: auto;
  padding: 8px 10px;
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.04);
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 12px;
  line-height: 1.5;
}
.diff-pair {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}
.diff-pair-span {
  grid-column: 1 / -1;
  justify-self: start;
}
.diff-side {
  padding: 8px 10px;
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.04);
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  font-size: 13px;
  line-height: 1.45;
}
.diff-del {
  background: rgba(var(--v-theme-error), 0.18);
  border-radius: 2px;
}
.diff-ins {
  background: rgba(var(--v-theme-success), 0.2);
  border-radius: 2px;
}
.diff-same {
  color: rgba(var(--v-theme-on-surface), 0.62);
}
.diff-clamp {
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
@media (max-width: 700px) {
  .diff-pair {
    grid-template-columns: 1fr;
  }
}
.min-width-0 {
  min-width: 0;
}
@media (max-width: 959px) {
  .pm-library__layout {
    flex-direction: column;
  }
  .pm-library__tree {
    width: 100%;
    border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
    max-height: 220px;
  }
  .pm-library__resize {
    display: none;
  }
}
</style>
