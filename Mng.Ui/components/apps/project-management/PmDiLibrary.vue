<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiGenerateFromTemplateDialog from '@/components/apps/document-intelligence/DiGenerateFromTemplateDialog.vue';
import DiLifecycleBar from '@/components/apps/document-intelligence/DiLifecycleBar.vue';
import DiMarkdownEditor from '@/components/apps/document-intelligence/DiMarkdownEditor.vue';
import DiMarkdownViewer from '@/components/apps/document-intelligence/DiMarkdownViewer.vue';
import DiMarkdownVersionHistoryDialog from '@/components/apps/document-intelligence/DiMarkdownVersionHistoryDialog.vue';
import DiResourceEditorDialog from '@/components/apps/document-intelligence/DiResourceEditorDialog.vue';
import DiResourcePreviewProvider from '@/components/apps/document-intelligence/DiResourcePreviewProvider.vue';
import DiResourceTree from '@/components/apps/document-intelligence/DiResourceTree.vue';
import DiSavePageDialog from '@/components/apps/document-intelligence/DiSavePageDialog.vue';
import type { DiSavePageMode } from '@/components/apps/document-intelligence/DiSavePageDialog.vue';
import PmLibraryBindDialog from '@/components/apps/project-management/PmLibraryBindDialog.vue';
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
  diDownloadResource,
  diErrorStatus,
  diGetBrowseContext,
  diGetById,
  diGetLetterhead,
  diGetMarkdownContent,
  diGetTreeChildren,
  diListLetterheads,
  diSearch,
  diUpdateMarkdown,
  diUpdateResourceMetadata,
} from '@/services/documentIntelligenceService';
import type {
  DiBreadcrumb,
  DiGenerateDocumentResult,
  DiLetterhead,
  DiLetterheadHeaderFields,
  DiResource,
  DiTreeNode,
} from '@/types/apps/documentIntelligence';
import { diFullPermission } from '@/types/apps/documentIntelligence';
import { isDiManagedDocument, isDiOfficeEditable, isDiPreviewable, isDiPresentation, isDiSheet } from '@/utils/diFilePreview';
import { diPageResourceIcon, diPageResourceLabel } from '@/utils/diPageResource';
import { buildDiFolderUrl } from '@/utils/diResourceLink';
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
  wbs?: PmWbsItem[];
}>();

const emit = defineEmits<{
  'hub-ready': [id: string];
  bound: [];
}>();

const MAX_FILE_MB = 20;

const { t } = useAppI18n();
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
const tagColorTick = ref(0);
const busy = ref(false);
const folderDialog = ref(false);
const pageDialog = ref(false);
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
  { title: t('projectManagement.library.colTags'), key: 'tags', sortable: false, minWidth: 180 },
  { title: t('projectManagement.library.colStatus'), key: 'status', width: 130 },
  { title: t('projectManagement.library.colUpdated'), key: 'updatedAt', width: 160 },
  {
    title: t('projectManagement.actions'),
    key: 'actions',
    width: 96,
    sortable: false,
    align: 'end' as const,
  },
]);
const diHref = computed(() => buildDiFolderUrl(selectedFolderId.value || hubId.value));

function resourceIcon(resource: DiResource): string {
  if (resource.type === 'folder') return 'mdi-folder-outline';
  if (resource.type === 'markdown') return diPageResourceIcon(resource);
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
      const filtered = (result.items || []).filter((row) => isInSelectedFolder(row, folderId));
      listingTotal.value = filtered.length;
      const pageCount = Math.max(1, Math.ceil(filtered.length / listingPageSize.value) || 1);
      if (listingPage.value > pageCount) {
        suppressListingWatch.value = true;
        listingPage.value = pageCount;
        suppressListingWatch.value = false;
      }
      const skip = (listingPage.value - 1) * listingPageSize.value;
      children.value = filtered.slice(skip, skip + listingPageSize.value);
      return;
    }

    const skip = (listingPage.value - 1) * listingPageSize.value;
    const context = await diGetBrowseContext(folderId, { skip, limit: listingPageSize.value });
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
    children.value = context.children.items || [];
    listingTotal.value = context.children.total ?? children.value.length;
    const pageCount = Math.max(1, Math.ceil(listingTotal.value / listingPageSize.value) || 1);
    if (listingTotal.value > 0 && listingPage.value > pageCount) {
      suppressListingWatch.value = true;
      listingPage.value = pageCount;
      suppressListingWatch.value = false;
      await loadListing(folderId);
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

function onBound() {
  emit('bound');
}

function onListingRowClick(_event: Event, ctx: { item: DiResource }) {
  void openResource(ctx.item);
}

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

    <div v-else-if="hubId" class="pm-library__layout d-flex">
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
              <v-btn size="small" variant="text" class="text-none" prepend-icon="mdi-history" @click="historyDialog = true">
                {{ t('documentIntelligence.history') }}
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
              <template #item.updatedAt="{ item }">
                <span class="text-body-2">{{ formatDateTime(item.updatedAt) || '—' }}</span>
              </template>
              <template #item.actions="{ item }">
                <div class="d-flex justify-end" @click.stop>
                  <v-btn
                    v-if="canBindResource(item)"
                    icon
                    size="small"
                    variant="text"
                    :title="t('projectManagement.library.bind.action')"
                    @click="openBind(item)"
                  >
                    <v-icon size="18">mdi-link-plus</v-icon>
                  </v-btn>
                  <v-btn icon size="small" variant="text" @click="openResource(item)">
                    <v-icon size="18">mdi-chevron-right</v-icon>
                  </v-btn>
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
    <DiMarkdownVersionHistoryDialog
      v-model="historyDialog"
      :resource-id="openDoc?.id ?? null"
      :can-restore="openDoc?.permissions.canEdit ?? false"
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
  </div>
  </DiResourcePreviewProvider>
</template>

<style scoped>
.pm-library__layout {
  min-height: 520px;
  border-top: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
.pm-library__tree {
  width: 260px;
  border-right: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  display: flex;
  flex-direction: column;
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
  overflow-x: auto;
}
.pm-library-table :deep(tbody tr) {
  cursor: pointer;
}
.pm-library-table :deep(thead th:last-child),
.pm-library-table :deep(tbody td:last-child) {
  position: sticky;
  right: 0;
  z-index: 2;
  background: rgb(var(--v-theme-surface));
  box-shadow: -4px 0 8px -4px rgba(0, 0, 0, 0.12);
  white-space: nowrap;
}
.pm-library-table :deep(thead th:last-child) {
  z-index: 3;
}
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
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
    border-right: 0;
    border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
    max-height: 220px;
  }
}
</style>
