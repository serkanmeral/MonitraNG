<script setup lang="ts">
import { ref, computed, onMounted, onActivated, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DiResourceTree from '@/components/apps/document-intelligence/DiResourceTree.vue';
import DiBrowseToolbar from '@/components/apps/document-intelligence/DiBrowseToolbar.vue';
import DiDiscoveryHome from '@/components/apps/document-intelligence/DiDiscoveryHome.vue';
import DiAreaIndexBanner from '@/components/apps/document-intelligence/DiAreaIndexBanner.vue';
import DiTagPicker from '@/components/apps/document-intelligence/DiTagPicker.vue';
import DiClassificationField from '@/components/apps/document-intelligence/DiClassificationField.vue';
import DiResourceTagsDialog from '@/components/apps/document-intelligence/DiResourceTagsDialog.vue';
import DiMarkdownViewer from '@/components/apps/document-intelligence/DiMarkdownViewer.vue';
import DiMarkdownEditor from '@/components/apps/document-intelligence/DiMarkdownEditor.vue';
import DiPermissionsDialog from '@/components/apps/document-intelligence/DiPermissionsDialog.vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiResourceEditorDialog from '@/components/apps/document-intelligence/DiResourceEditorDialog.vue';
import DiLinkedWorkItemsPanel from '@/components/apps/document-intelligence/DiLinkedWorkItemsPanel.vue';
import DiBacklinksPanel from '@/components/apps/document-intelligence/DiBacklinksPanel.vue';
import DiSavePageDialog from '@/components/apps/document-intelligence/DiSavePageDialog.vue';
import type { DiSavePageMode } from '@/components/apps/document-intelligence/DiSavePageDialog.vue';
import DiMarkdownVersionHistoryDialog from '@/components/apps/document-intelligence/DiMarkdownVersionHistoryDialog.vue';
import DiFileVersionHistoryDialog from '@/components/apps/document-intelligence/DiFileVersionHistoryDialog.vue';
import DiResourceInfoDialog from '@/components/apps/document-intelligence/DiResourceInfoDialog.vue';
import DiCloneResourceDialog from '@/components/apps/document-intelligence/DiCloneResourceDialog.vue';
import DiGenerateFromTemplateDialog from '@/components/apps/document-intelligence/DiGenerateFromTemplateDialog.vue';
import DiEditorLockDialog from '@/components/apps/document-intelligence/DiEditorLockDialog.vue';
import DiFolderPickerList from '@/components/apps/document-intelligence/DiFolderPickerList.vue';
import DiResourcePreviewProvider from '@/components/apps/document-intelligence/DiResourcePreviewProvider.vue';
import { isDiPreviewable, isDiOfficePdfExportable, isDiOfficeEditable, isDiManagedDocument, isDiCloneable, isDiSheet, isDiPresentation } from '@/utils/diFilePreview';
import { useDiLazyFolderTree } from '@/composables/useDiLazyFolderTree';
import {
  DI_HOME_PATH,
  DI_LAST_FOLDER_STORAGE_KEY,
  buildDiResourceUrl,
  buildDiResourceEditorUrl,
  parseFolderIdQuery,
  parseLegacyResourceIdQuery,
} from '@/utils/diResourceLink';
import { useResizableTreePanel } from '@/composables/useResizableTreePanel';
import { useAppI18n } from '@/composables/useAppI18n';
import { useDiEditorLockGate } from '@/composables/useDiEditorLockGate';
import { useAppToast } from '@/composables/useAppToast';
import { useApiErrorNotify } from '@/composables/useApiErrorNotify';
import { useAuthStore } from '@/stores/auth';
import {
  LayoutSidebarLeftCollapseIcon,
} from 'vue-tabler-icons';
import {
  DI_CHILDREN_PAGE_SIZE,
  DI_CHILDREN_PAGE_SIZE_OPTIONS,
  diGetBootstrap,
  diGetBrowseContext,
  diGetBreadcrumb,
  diGetById,
  diGetMarkdownContent,
  diCreateFolder,
  diCreateMarkdown,
  diUpdateMarkdown,
  diRename,
  diMove,
  diDelete,
  diSearch,
  diCreateFileResource,
  diCreateNativeDocument,
  diCreateNativeSheet,
  diCreateNativePresentation,
  diListLetterheads,
  diGetLetterhead,
  diDownloadResource,
  diFetchResourceExportPdf,
  diErrorStatus,
} from '@/services/documentIntelligenceService';
import {
  diFullPermission,
  type DiGenerateDocumentResult,
  type DiResource,
  type DiBreadcrumb,
  type DiResourceBrowseContext,
  type DiResourceBootstrap,
  type DiEffectivePermission,
  type DiLetterhead,
  type DiLetterheadHeaderFields,
} from '@/types/apps/documentIntelligence';
import {
  DI_PAGE_TEMPLATE_DEFINITIONS,
  getDiPageTemplateContent,
  type DiPageTemplateId,
} from '@/utils/diPageTemplates';
import {
  diPageResourceIcon,
  diPageResourceLabel,
  findDiAreaIndexPage,
  isDiTopAreaFolder,
} from '@/utils/diPageResource';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const { push } = useAppToast();
const { notifyApiError } = useApiErrorNotify();
const authStore = useAuthStore();
const route = useRoute();
const router = useRouter();
const {
  dialogOpen: editorLockDialogOpen,
  lockStatus: editorLockStatus,
  gateResourceEditor,
  onDialogChoose: onEditorLockChoose,
  onDialogUpdate: onEditorLockDialogUpdate,
} = useDiEditorLockGate();

const canViewEditorSessions = computed(
  () => authStore.isAdmin || authStore.isManager
);

const {
  treeWidth,
  treeCollapsed,
  resizeActive,
  startResize,
  toggleTreeCollapse,
} = useResizableTreePanel('document-intelligence-tree', {
  minWidth: 220,
  maxWidth: 480,
  defaultWidth: 300,
});

// --- Durum ---
const {
  treeRoots,
  setRoots,
  invalidateAll,
  loadChildren: loadLazyTreeChildren,
  hydrateTreePath,
  isParentLoading,
} = useDiLazyFolderTree();
const treeExpandedIds = ref<string[]>([]);
const treeLoading = ref(false);

function readFolderIdFromRoute(): string | null {
  const parsed = parseFolderIdQuery(route.query as Record<string, unknown>);
  return parsed === undefined ? null : parsed;
}

const selectedFolderId = ref<string | null>(readFolderIdFromRoute());
const selectedFolder = ref<DiResource | null>(null);
const children = ref<DiResource[]>([]);
const childrenTotal = ref(0);
const childrenPage = ref(1);
const childrenPageSize = ref(DI_CHILDREN_PAGE_SIZE);
const childrenPageSizeOptions = DI_CHILDREN_PAGE_SIZE_OPTIONS.map((v) => ({
  title: String(v),
  value: v,
}));
const childrenLoading = ref(false);
const folderPath = ref<DiBreadcrumb[]>([]);

type MainMode = 'browse' | 'doc';
const mainMode = ref<MainMode>('browse');

// Açık doküman
const openDoc = ref<DiResource | null>(null);
const docContent = ref('');
const docVersion = ref(0);
const docMode = ref<'view' | 'edit'>('view');
const docLoading = ref(false);
const editContent = ref('');
const editTags = ref<string[]>([]);
const editClassificationId = ref<string | null>(null);
const saving = ref(false);

// Etiket filtresi (klasör listesi)
const activeTagFilter = ref<string | null>(null);

// Arama
const searchQuery = ref('');
const searchActive = ref(false);
const searching = ref(false);
const searchResults = ref<DiResource[]>([]);

function notify(text: string, color: 'success' | 'error' | 'info' = 'info') {
  push({
    title: color === 'success' ? t('documentIntelligence.notify.successTitle') : t('errors.dg.toastTitle'),
    message: text,
    severity: color,
  });
}

function notifyError(error: unknown, fallbackKey: string) {
  notifyApiError(error, {
    fallbackKey,
    title: t('errors.dg.toastTitle'),
  });
}

// --- Diyaloglar ---
const folderDialog = ref(false);
const folderName = ref('');
const docDialog = ref(false);
const docTitle = ref('');
const docTemplate = ref<DiPageTemplateId>('blank');
const nativeDocDialog = ref(false);
const nativeSheetDialog = ref(false);
const nativePresentationDialog = ref(false);
const generateFromTemplateDialog = ref(false);
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
const renameDialog = ref(false);
const renameTarget = ref<DiResource | null>(null);
const renameName = ref('');
const moveDialog = ref(false);
const moveTarget = ref<DiResource | null>(null);
const moveDestId = ref<string | null>(null);
const cloneDialog = ref(false);
const cloneTarget = ref<DiResource | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<DiResource | null>(null);
const deleteForce = ref(false);
const selectedResourceIds = ref<string[]>([]);
const bulkDeleteDialog = ref(false);
const busy = ref(false);

// Sürüm geçmişi
const historyDialog = ref(false);
const fileHistoryDialog = ref(false);
const fileHistoryTarget = ref<DiResource | null>(null);
const infoDialog = ref(false);
const infoTarget = ref<DiResource | null>(null);
const tagsDialog = ref(false);
const tagsTarget = ref<DiResource | null>(null);

// Kaydet diyaloğu (changeNote)
const saveDialog = ref(false);
const saveDialogMode = ref<DiSavePageMode>('save');
const pendingSaveAsDraft = ref(false);

// Yetkiler (izin diyaloğu)
const permissionsDialog = ref(false);
const permTargetId = ref<string | null>(null);
const permTargetName = ref('');

// Dosya yükleme
const MAX_FILE_MB = 20;
const fileDialog = ref(false);
const fileInputEl = ref<HTMLInputElement | null>(null);
const pickedFile = ref<File | null>(null);
const fileDisplayName = ref('');
const downloadingId = ref<string | null>(null);

// Dosya inline önizleme
const filePreviewOpen = ref(false);
const filePreviewResource = ref<DiResource | null>(null);

// DOCX Collabora editör
const fileEditorOpen = ref(false);
const fileEditorResource = ref<DiResource | null>(null);

const breadcrumbs = computed(() => {
  const base = [{ title: t('documentIntelligence.menuTitle'), disabled: folderPath.value.length === 0 }];
  return [...base, ...folderPath.value.map((b, i) => ({ title: b.name, disabled: i === folderPath.value.length - 1 }))];
});

const folderChildren = computed(() => filteredChildren.value.filter((c) => c.type === 'folder'));

const areaIndexPage = computed(() => {
  if (!selectedFolder.value || !isDiTopAreaFolder(selectedFolder.value.name)) return null;
  return findDiAreaIndexPage(children.value);
});

const pageChildren = computed(() => {
  const pages = filteredChildren.value.filter((c) => c.type === 'markdown');
  const indexPage = areaIndexPage.value;
  if (!indexPage) return pages;
  return pages.filter((p) => p.id !== indexPage.id);
});
const formalDocumentChildren = computed(() =>
  filteredChildren.value.filter((c) => c.type === 'file' && isDiManagedDocument(c))
);
const otherFileChildren = computed(() =>
  filteredChildren.value.filter((c) => c.type === 'file' && !isDiManagedDocument(c))
);

function resourceMatchesTagFilter(resource: DiResource): boolean {
  if (!activeTagFilter.value) return true;
  const needle = activeTagFilter.value.toLowerCase();
  return (resource.tags ?? []).some((tag) => tag.toLowerCase() === needle);
}

const filteredChildren = computed(() => {
  if (!activeTagFilter.value) return children.value;
  return children.value.filter((item) => item.type === 'folder' || resourceMatchesTagFilter(item));
});

const folderTagOptions = computed(() => {
  const set = new Set<string>();
  for (const item of children.value) {
    if (item.type === 'folder') continue;
    for (const tag of item.tags ?? []) {
      if (tag.trim()) set.add(tag.trim());
    }
  }
  return [...set].sort((a, b) => a.localeCompare(b, 'tr'));
});

type BrowseContentSection = {
  key: string;
  labelKey: string;
  items: DiResource[];
};

const contentSections = computed<BrowseContentSection[]>(() => {
  const sections: BrowseContentSection[] = [];
  if (pageChildren.value.length) {
    sections.push({
      key: 'pages',
      labelKey: 'documentIntelligence.pagesSection',
      items: pageChildren.value,
    });
  }
  if (formalDocumentChildren.value.length) {
    sections.push({
      key: 'formalDocuments',
      labelKey: 'documentIntelligence.formalDocumentsSection',
      items: formalDocumentChildren.value,
    });
  }
  if (otherFileChildren.value.length) {
    sections.push({
      key: 'files',
      labelKey: 'documentIntelligence.filesSection',
      items: otherFileChildren.value,
    });
  }
  return sections;
});

const selectableResources = computed(() =>
  filteredChildren.value.filter((c) => c.type !== 'folder' && c.permissions.canDelete)
);

const selectedResourceCount = computed(() => selectedResourceIds.value.length);

const allSelectableSelected = computed(() => {
  const selectable = selectableResources.value;
  if (!selectable.length) return false;
  return selectable.every((r) => selectedResourceIds.value.includes(r.id));
});

const someSelectableSelected = computed(() =>
  selectableResources.value.some((r) => selectedResourceIds.value.includes(r.id))
);

const urlFolderId = computed(() => readFolderIdFromRoute());

const showDiscovery = computed(
  () =>
    !searchActive.value
    && mainMode.value === 'browse'
    && selectedFolderId.value === null
    && urlFolderId.value === null,
);

const childrenSkip = computed(() => (childrenPage.value - 1) * childrenPageSize.value);

const childrenPageCount = computed(() =>
  Math.max(1, Math.ceil(childrenTotal.value / childrenPageSize.value)),
);

/** Klasör gezinme alt çubuğu: sayaç + sayfa boyutu + sayfalama. */
const showChildrenListingFooter = computed(
  () =>
    !activeTagFilter.value
    && !searchActive.value
    && !showDiscovery.value
    && childrenTotal.value > 0,
);

const childrenRangeLabel = computed(() => {
  if (!childrenTotal.value) return '';
  const from = childrenSkip.value + 1;
  const to = Math.min(childrenSkip.value + children.value.length, childrenTotal.value);
  return t('documentIntelligence.childrenRange', { from, to, total: childrenTotal.value });
});

function resolveChildrenListingOptions(): { skip: number; limit?: number } {
  if (activeTagFilter.value) {
    return { skip: 0 };
  }
  return { skip: childrenSkip.value, limit: childrenPageSize.value };
}

const pageTemplateOptions = computed(() =>
  DI_PAGE_TEMPLATE_DEFINITIONS.map((item) => ({
    value: item.id,
    title: t(item.labelKey),
  }))
);

// --- Yetki gating ---
// Seçili klasörün (kök ise açık varsayılan) etkin yetkisi — üst bar buton kontrolü.
const currentPerm = computed<DiEffectivePermission>(() =>
  selectedFolderId.value ? (selectedFolder.value?.permissions ?? diFullPermission()) : diFullPermission(),
);

/** İzin yönetimi: admin ya da kaynak üzerinde share yetkisi. */
function canManage(resource: DiResource | null): boolean {
  if (!resource) return false;
  return authStore.isAdmin || resource.permissions.canShare;
}

function applyBrowseContext(ctx: DiResourceBrowseContext) {
  children.value = ctx.children.items;
  childrenTotal.value = ctx.children.total;
  folderPath.value = ctx.breadcrumb;
  selectedFolder.value = ctx.selectedFolder;
}

function applyBootstrap(boot: DiResourceBootstrap) {
  setRoots(boot.treeRoots);
  applyBrowseContext(boot);
}

async function hydrateDeepLinkTree(folderId: string) {
  const path = await hydrateTreePath(folderId);
  treeExpandedIds.value = path.breadcrumb.map((b) => b.id);
}

function loadTreeChildren(parentId: string) {
  return loadLazyTreeChildren(parentId);
}

/** Ağaç + geçerli klasör içeriği (mutasyon / yetki değişimi sonrası). */
async function refreshWorkspace() {
  treeLoading.value = true;
  childrenLoading.value = true;
  childrenPage.value = 1;
  try {
    invalidateAll();
    treeExpandedIds.value = [];
    const boot = await diGetBootstrap(selectedFolderId.value, resolveChildrenListingOptions());
    applyBootstrap(boot);
    if (selectedFolderId.value) {
      await hydrateDeepLinkTree(selectedFolderId.value);
    }
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.treeLoad');
  } finally {
    treeLoading.value = false;
    childrenLoading.value = false;
  }
}

/** Yalnızca liste/breadcrumb (ağaç değişmediyse). */
async function refreshListing() {
  childrenLoading.value = true;
  try {
    applyBrowseContext(await diGetBrowseContext(selectedFolderId.value, resolveChildrenListingOptions()));
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.childrenLoad');
    children.value = [];
    childrenTotal.value = 0;
  } finally {
    childrenLoading.value = false;
  }
}

async function onChildrenPageChange(page: number) {
  childrenPage.value = page;
  clearResourceSelection();
  await refreshListing();
}

async function onChildrenPageSizeChange(size: number) {
  if (!size || size === childrenPageSize.value) return;
  childrenPageSize.value = size;
  childrenPage.value = 1;
  clearResourceSelection();
  await refreshListing();
}

async function loadFolderPath(folderId: string | null) {
  if (!folderId) {
    folderPath.value = [];
    return;
  }
  try {
    folderPath.value = await diGetBreadcrumb(folderId);
  } catch {
    folderPath.value = [];
  }
}

async function selectFolder(folderId: string | null, options?: { syncUrl?: boolean }) {
  searchActive.value = false;
  activeTagFilter.value = null;
  mainMode.value = 'browse';
  clearResourceSelection();
  selectedFolderId.value = folderId;
  openDoc.value = null;
  rememberBrowseFolder(folderId);
  childrenPage.value = 1;
  childrenLoading.value = true;
  try {
    applyBrowseContext(await diGetBrowseContext(folderId, resolveChildrenListingOptions()));
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.childrenLoad');
    children.value = [];
    childrenTotal.value = 0;
    folderPath.value = [];
    selectedFolder.value = null;
  } finally {
    childrenLoading.value = false;
  }

  if (options?.syncUrl !== false && route.path === DI_HOME_PATH) {
    const nextQuery = folderId ? { folderId } : {};
    const currentFolderId = parseFolderIdQuery(route.query as Record<string, unknown>);
    if (currentFolderId !== folderId) {
      await router.push({
        path: DI_HOME_PATH,
        query: nextQuery,
      });
    }
  }
}

function rememberBrowseFolder(folderId: string | null) {
  if (!import.meta.client) return;
  if (folderId) {
    sessionStorage.setItem(DI_LAST_FOLDER_STORAGE_KEY, folderId);
  } else {
    sessionStorage.removeItem(DI_LAST_FOLDER_STORAGE_KEY);
  }
}

function buildResourceNavUrl(resourceId: string): string {
  const fromFolder = selectedFolderId.value;
  return buildDiResourceUrl(resourceId, fromFolder ? { fromFolderId: fromFolder } : undefined);
}

async function openResource(resource: DiResource) {
  if (resource.type === 'folder') {
    await selectFolder(resource.id);
    return;
  }
  const fromFolder = selectedFolderId.value;
  if (fromFolder) {
    rememberBrowseFolder(fromFolder);
  }
  await router.push(buildResourceNavUrl(resource.id));
}

function openFilePreview(resource: DiResource) {
  void openResource(resource);
}

function openFileEditor(resource: DiResource) {
  void openResource(resource);
}

async function openFileEditorInNewTab(resource: DiResource) {
  const gate = await gateResourceEditor(resource.id);
  if (!gate.proceed) return;

  const url = buildDiResourceEditorUrl(resource.id, {
    readOnly: gate.options?.readOnly,
    bypassLock: gate.options?.bypassLock,
  });

  if (typeof window === 'undefined') {
    void navigateTo(url);
    return;
  }
  window.open(url, '_blank', 'noopener,noreferrer');
}

async function openMarkdown(resource: DiResource) {
  mainMode.value = 'doc';
  openDoc.value = resource;
  docMode.value = 'view';
  docLoading.value = true;
  try {
    const c = await diGetMarkdownContent(resource.id);
    docContent.value = c.content;
    docVersion.value = c.currentVersionNumber;
    if (!folderPath.value.length || selectedFolderId.value !== resource.parentId) {
      await loadFolderPath(resource.parentId);
    }
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.docLoad');
    mainMode.value = 'browse';
  } finally {
    docLoading.value = false;
  }
}

function backToFolder() {
  selectFolder(openDoc.value?.parentId ?? null);
}

function onDocTagClick(tag: string) {
  activeTagFilter.value = tag;
  void backToFolder();
}

function openHistory() {
  if (!openDoc.value) return;
  historyDialog.value = true;
}

function openFileHistory(resource: DiResource) {
  fileHistoryTarget.value = resource;
  fileHistoryDialog.value = true;
}

async function onFileVersionRestored(_restored: DiResource) {
  await refreshListing();
}

function openResourceInfo(resource: DiResource) {
  infoTarget.value = resource;
  infoDialog.value = true;
}

function openResourceTags(resource: DiResource) {
  tagsTarget.value = resource;
  tagsDialog.value = true;
}

async function onResourceTagsSaved(updated: DiResource) {
  notify(t('documentIntelligence.saved'), 'success');
  if (openDoc.value?.id === updated.id) {
    openDoc.value = {
      ...openDoc.value,
      tags: updated.tags,
      classificationTagId: updated.classificationTagId,
    };
  }
  await refreshListing();
}

async function onVersionRestored(restored: DiResource) {
  await openMarkdown(restored);
}

function formatDateTime(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('tr-TR', {
    year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit',
  });
}

function startEdit() {
  editContent.value = docContent.value;
  editTags.value = [...(openDoc.value?.tags ?? [])];
  editClassificationId.value = openDoc.value?.classificationTagId ?? null;
  docMode.value = 'edit';
}

function cancelEdit() {
  docMode.value = 'view';
}

function openSaveDialog(asDraft: boolean) {
  if (asDraft) {
    saveDialogMode.value = 'draft';
  } else if (openDoc.value?.status === 'draft') {
    saveDialogMode.value = 'publish';
  } else {
    saveDialogMode.value = 'save';
  }
  pendingSaveAsDraft.value = asDraft;
  saveDialog.value = true;
}

async function confirmSaveEdit(changeNote: string) {
  const ok = await saveEdit(pendingSaveAsDraft.value, changeNote);
  if (ok) saveDialog.value = false;
}

async function saveEdit(asDraft = false, changeNote = ''): Promise<boolean> {
  if (!openDoc.value) return false;
  saving.value = true;
  try {
    const updated = await diUpdateMarkdown(openDoc.value.id, {
      content: editContent.value,
      tags: editTags.value,
      classificationTagId: editClassificationId.value ?? '',
      expectedVersionNumber: docVersion.value,
      isDraft: asDraft,
      changeNote: changeNote || null,
    });
    docContent.value = editContent.value;
    docVersion.value = updated.currentVersionNumber || docVersion.value + 1;
    openDoc.value = updated;
    docMode.value = 'view';
    notify(asDraft ? t('documentIntelligence.draftSaved') : t('documentIntelligence.published'), 'success');
    return true;
  } catch (e) {
    if (diErrorStatus(e) === 409) {
      notify(t('documentIntelligence.errors.conflict'), 'error');
      // Güncel sürümü tekrar yükle
      await openMarkdown(openDoc.value);
    } else {
      notifyError(e, 'documentIntelligence.errors.save');
    }
    return false;
  } finally {
    saving.value = false;
  }
}

// --- Oluşturma ---
function openFolderDialog() {
  folderName.value = '';
  folderDialog.value = true;
}

async function submitFolder() {
  const name = folderName.value.trim();
  if (!name) return;
  busy.value = true;
  try {
    await diCreateFolder({ name, parentId: selectedFolderId.value });
    folderDialog.value = false;
    notify(t('documentIntelligence.folderCreated'), 'success');
    await refreshWorkspace();
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.create');
  } finally {
    busy.value = false;
  }
}

function openDocDialog() {
  docTitle.value = '';
  docTemplate.value = 'blank';
  docDialog.value = true;
}

async function submitDoc(asDraft = false) {
  const title = docTitle.value.trim();
  if (!title) return;
  busy.value = true;
  try {
    const created = await diCreateMarkdown({
      title,
      content: getDiPageTemplateContent(docTemplate.value),
      parentId: selectedFolderId.value,
      isDraft: asDraft,
    });
    docDialog.value = false;
    notify(t('documentIntelligence.pageCreated'), 'success');
    await refreshListing();
    await openMarkdown(created);
    startEdit();
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.create');
  } finally {
    busy.value = false;
  }
}

function openNativeDocDialog() {
  nativeDocName.value = '';
  nativeDocCode.value = '';
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

async function loadLetterheadOptions() {
  try {
    const res = await diListLetterheads(true);
    letterheadOptions.value = res.items;
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.letterheadsLoad');
  }
}

async function onNativeLetterheadChange(letterheadId: string | null) {
  nativeDocLetterhead.value = null;
  if (!letterheadId) return;
  try {
    const lh = await diGetLetterhead(letterheadId);
    nativeDocLetterhead.value = lh;
    nativeDocHeaderFields.value = { ...lh.settings.headerFields };
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.letterheadsLoad');
  }
}

async function submitNativeDoc() {
  const name = nativeDocName.value.trim();
  const documentNo = nativeDocCode.value.trim();
  if (!name || !documentNo) return;
  const letterheadId = nativeDocLetterheadId.value?.trim() || null;
  busy.value = true;
  try {
    const created = await diCreateNativeDocument({
      parentId: selectedFolderId.value,
      name,
      documentNo,
      letterheadId,
      selectedHeaderFields: letterheadId ? { ...nativeDocHeaderFields.value } : null,
    });
    nativeDocDialog.value = false;
    notify(t('documentIntelligence.nativeDocumentCreated'), 'success');
    await refreshListing();
    await navigateTo(buildDiResourceUrl(created.id));
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.create');
  } finally {
    busy.value = false;
  }
}

function openNativeSheetDialog() {
  nativeSheetName.value = '';
  nativeSheetCode.value = '';
  nativeSheetDialog.value = true;
}

async function submitNativeSheet() {
  const name = nativeSheetName.value.trim();
  if (!name) return;
  const documentNo = nativeSheetCode.value.trim() || null;
  busy.value = true;
  try {
    const created = await diCreateNativeSheet({
      parentId: selectedFolderId.value,
      name,
      documentNo,
    });
    nativeSheetDialog.value = false;
    notify(t('documentIntelligence.nativeSheetCreated'), 'success');
    await refreshListing();
    await navigateTo(buildDiResourceUrl(created.id));
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.create');
  } finally {
    busy.value = false;
  }
}

function openNativePresentationDialog() {
  nativePresentationName.value = '';
  nativePresentationCode.value = '';
  nativePresentationDialog.value = true;
}

async function submitNativePresentation() {
  const name = nativePresentationName.value.trim();
  if (!name) return;
  const documentNo = nativePresentationCode.value.trim() || null;
  busy.value = true;
  try {
    const created = await diCreateNativePresentation({
      parentId: selectedFolderId.value,
      name,
      documentNo,
    });
    nativePresentationDialog.value = false;
    notify(t('documentIntelligence.nativePresentationCreated'), 'success');
    await refreshListing();
    await navigateTo(buildDiResourceUrl(created.id));
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.create');
  } finally {
    busy.value = false;
  }
}

function openGenerateFromTemplateDialog() {
  if (!selectedFolderId.value) {
    notify(t('documentIntelligence.generateFromTemplate.selectFolderHint'), 'info');
    return;
  }
  generateFromTemplateDialog.value = true;
}

async function onGenerateFromTemplateCreated(result: DiGenerateDocumentResult) {
  generateFromTemplateDialog.value = false;
  await refreshListing();
  if (result.resourceId) {
    await navigateTo(buildDiResourceUrl(result.resourceId));
  }
}

async function downloadPdfExport(resource: DiResource) {
  if (!resource.permissions.canDownload) return;
  downloadingId.value = resource.id;
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
  } catch (e) {
    notifyError(e, 'documentIntelligence.generateFromTemplate.errors.exportPdf');
  } finally {
    downloadingId.value = null;
  }
}

// --- Yeniden adlandır ---
function openRename(resource: DiResource) {
  renameTarget.value = resource;
  renameName.value = resource.type === 'markdown' ? resource.title || resource.name : resource.name;
  renameDialog.value = true;
}

async function submitRename() {
  const target = renameTarget.value;
  const name = renameName.value.trim();
  if (!target || !name) return;
  busy.value = true;
  try {
    await diRename(target.id, { name });
    renameDialog.value = false;
    notify(t('documentIntelligence.renamed'), 'success');
    await refreshWorkspace();
    if (openDoc.value?.id === target.id) {
      openDoc.value = { ...openDoc.value, name, title: name };
    }
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.rename');
  } finally {
    busy.value = false;
  }
}

// --- Taşı ---
function openMove(resource: DiResource) {
  moveTarget.value = resource;
  moveDestId.value = resource.parentId;
  moveDialog.value = true;
}

async function submitMove() {
  const target = moveTarget.value;
  if (!target) return;
  busy.value = true;
  try {
    await diMove(target.id, { newParentId: moveDestId.value });
    moveDialog.value = false;
    notify(t('documentIntelligence.moved'), 'success');
    await refreshWorkspace();
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.move');
  } finally {
    busy.value = false;
  }
}

function openClone(resource: DiResource) {
  cloneTarget.value = resource;
  cloneDialog.value = true;
}

async function onCloned(created: DiResource) {
  notify(t('documentIntelligence.cloned'), 'success');
  await refreshWorkspace();
  if (created.type === 'markdown') {
    await openMarkdown(created);
  }
}

// --- Sil ---
function clearResourceSelection() {
  selectedResourceIds.value = [];
}

function isResourceSelected(id: string): boolean {
  return selectedResourceIds.value.includes(id);
}

function toggleResourceSelection(id: string, selected: boolean | null) {
  const set = new Set(selectedResourceIds.value);
  if (selected) set.add(id);
  else set.delete(id);
  selectedResourceIds.value = [...set];
}

function toggleSelectAllOnPage() {
  if (allSelectableSelected.value) {
    clearResourceSelection();
    return;
  }
  selectedResourceIds.value = selectableResources.value.map((r) => r.id);
}

function openDelete(resource: DiResource) {
  deleteTarget.value = resource;
  deleteForce.value = false;
  deleteDialog.value = true;
}

async function submitDelete() {
  const target = deleteTarget.value;
  if (!target) return;
  busy.value = true;
  try {
    await diDelete(target.id, deleteForce.value);
    deleteDialog.value = false;
    notify(t('documentIntelligence.deleted'), 'success');
    if (openDoc.value?.id === target.id) {
      openDoc.value = null;
      mainMode.value = 'browse';
    }
    await refreshWorkspace();
  } catch (e) {
    if (diErrorStatus(e) === 409 && !deleteForce.value) {
      // Dolu klasör guard'ı: force seçeneği sun
      deleteForce.value = true;
      notify(t('documentIntelligence.errors.notEmpty'), 'error');
    } else {
      notifyError(e, 'documentIntelligence.errors.delete');
    }
  } finally {
    busy.value = false;
  }
}

function openBulkDelete() {
  if (!selectedResourceIds.value.length) return;
  bulkDeleteDialog.value = true;
}

async function submitBulkDelete() {
  const ids = [...selectedResourceIds.value];
  if (!ids.length) return;
  busy.value = true;
  let ok = 0;
  let fail = 0;
  try {
    for (const id of ids) {
      try {
        await diDelete(id, false);
        ok += 1;
        if (openDoc.value?.id === id) {
          openDoc.value = null;
          mainMode.value = 'browse';
        }
      } catch {
        fail += 1;
      }
    }
    bulkDeleteDialog.value = false;
    clearResourceSelection();
    if (fail === 0) {
      notify(t('documentIntelligence.bulkDeleted', { count: ok }), 'success');
    } else if (ok > 0) {
      notify(t('documentIntelligence.bulkDeletePartial', { ok, fail }), 'warning');
    } else {
      notify(t('documentIntelligence.errors.delete'), 'error');
    }
    await refreshWorkspace();
  } finally {
    busy.value = false;
  }
}

// --- Yetkiler ---
function openPermissions(resource: DiResource) {
  if (resource.type !== 'folder') return;
  permTargetId.value = resource.id;
  permTargetName.value = resource.name;
  permissionsDialog.value = true;
}

async function onPermissionsChanged() {
  await refreshWorkspace();
  if (openDoc.value?.id) {
    try {
      openDoc.value = await diGetById(openDoc.value.id);
    } catch {
      /* açık doküman artık görünmüyor olabilir */
    }
  }
}

function onToolbarPermissions() {
  if (selectedFolder.value) openPermissions(selectedFolder.value);
}

// --- Dosya yükleme ---
function openFileDialog() {
  pickedFile.value = null;
  fileDisplayName.value = '';
  fileDialog.value = true;
}

function triggerFilePick() {
  fileInputEl.value?.click();
}

function onFilePick(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files && input.files.length ? input.files[0] : null;
  if (file) {
    pickedFile.value = file;
    if (!fileDisplayName.value.trim()) fileDisplayName.value = file.name;
  }
  if (fileInputEl.value) fileInputEl.value.value = '';
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

async function submitFile() {
  const file = pickedFile.value;
  if (!file) return;
  if (file.size > MAX_FILE_MB * 1024 * 1024) {
    notify(t('documentIntelligence.errors.fileTooLarge', { max: MAX_FILE_MB }), 'error');
    return;
  }
  const name = (fileDisplayName.value.trim() || file.name).trim();
  busy.value = true;
  try {
    const content = await fileToBase64(file);
    await diCreateFileResource({
      parentId: selectedFolderId.value,
      name,
      originalFileName: file.name,
      content,
      mimeType: file.type || null,
      extension: fileExtension(file.name) || null,
      size: file.size,
    });
    fileDialog.value = false;
    notify(t('documentIntelligence.fileUploaded'), 'success');
    await refreshListing();
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.upload');
  } finally {
    busy.value = false;
  }
}

async function downloadFile(resource: DiResource) {
  if (!resource.filePath) {
    notifyError(null, 'documentIntelligence.errors.download');
    return;
  }
  downloadingId.value = resource.id;
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
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.download');
  } finally {
    downloadingId.value = null;
  }
}

// --- Arama ---
let searchTimer: ReturnType<typeof setTimeout> | null = null;
function onSearchInput() {
  if (searchTimer) clearTimeout(searchTimer);
  const q = searchQuery.value.trim();
  if (!q) {
    searchActive.value = false;
    searchResults.value = [];
    return;
  }
  searchTimer = setTimeout(() => runSearch(), 350);
}

async function runSearch() {
  const q = searchQuery.value.trim();
  if (!q) return;
  searching.value = true;
  searchActive.value = true;
  try {
    const res = await diSearch(q, 0, 50);
    searchResults.value = res.items;
  } catch (e) {
    notifyError(e, 'documentIntelligence.errors.search');
    searchResults.value = [];
  } finally {
    searching.value = false;
  }
}

function clearSearch() {
  searchQuery.value = '';
  searchActive.value = false;
  searchResults.value = [];
}

function onDiscoverySearch(q: string) {
  searchQuery.value = q;
  void runSearch();
}

function resourceTypeLabel(resource: DiResource): string | null {
  if (resource.type === 'markdown') return t('documentIntelligence.typePage');
  if (resource.type === 'file' && isDiManagedDocument(resource)) {
    if (isDiSheet(resource)) return t('documentIntelligence.typeSpreadsheet');
    if (isDiPresentation(resource)) return t('documentIntelligence.typePresentation');
    return t('documentIntelligence.typeDocument');
  }
  if (resource.type === 'file') return t('documentIntelligence.typeFile');
  return null;
}

function resourceIcon(resource: DiResource): string {
  if (resource.type === 'folder') return 'mdi-folder';
  if (resource.type === 'markdown') return diPageResourceIcon(resource);
  const mime = resource.mimeType || '';
  const ext = (resource.extension || '').toLowerCase();
  if (mime.startsWith('image/')) return 'mdi-file-image-outline';
  if (mime.includes('pdf') || ext === 'pdf') return 'mdi-file-pdf-box';
  if (mime.includes('word') || ['doc', 'docx'].includes(ext)) return 'mdi-file-word-box';
  if (mime.includes('sheet') || mime.includes('excel') || ['xls', 'xlsx', 'csv'].includes(ext)) return 'mdi-file-excel-box';
  if (mime.includes('presentation') || ['ppt', 'pptx'].includes(ext)) return 'mdi-file-powerpoint-box';
  if (mime.includes('zip') || mime.includes('compressed') || ['zip', 'rar', '7z', 'gz'].includes(ext)) return 'mdi-folder-zip-outline';
  return 'mdi-file-outline';
}

function resourceLabel(resource: DiResource): string {
  return diPageResourceLabel(resource);
}

function formatSize(bytes: number | null): string {
  if (!bytes || bytes <= 0) return '';
  const units = ['B', 'KB', 'MB', 'GB'];
  const i = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));
  return `${Math.round((bytes / Math.pow(1024, i)) * 10) / 10} ${units[i]}`;
}

function buildResourceSubtitle(resource: DiResource): string {
  const parts: string[] = [];
  const typeLabel = resourceTypeLabel(resource);
  if (typeLabel) parts.push(typeLabel);
  if (resource.description) parts.push(resource.description);
  if (resource.type === 'file' && resource.size) parts.push(formatSize(resource.size));
  if (resource.tags?.length) parts.push(resource.tags.join(', '));
  const auditParts: string[] = [];
  if (resource.updatedBy) auditParts.push(resource.updatedBy);
  if (resource.updatedAt) auditParts.push(formatDateTime(resource.updatedAt));
  if (auditParts.length) parts.push(auditParts.join(' · '));
  return parts.join(' · ');
}

function hasResourceSubtitle(resource: DiResource): boolean {
  return buildResourceSubtitle(resource).length > 0;
}

onMounted(async () => {
  const legacyId = parseLegacyResourceIdQuery(route.query as Record<string, unknown>);
  if (legacyId) {
    await navigateTo(buildDiResourceUrl(legacyId), { replace: true });
    return;
  }

  await syncBrowseRouteContext({ initial: true });
});

async function syncBrowseRouteContext(options?: { initial?: boolean; forceRefresh?: boolean }) {
  if (route.path !== DI_HOME_PATH) return;

  const folderId = parseFolderIdQuery(route.query as Record<string, unknown>);
  if (folderId === undefined) return;

  const needsRefresh = options?.forceRefresh || route.query.refresh === '1';
  const folderChanged = selectedFolderId.value !== folderId;

  if (folderChanged) {
    selectedFolderId.value = folderId;
  }
  if (folderId) {
    rememberBrowseFolder(folderId);
  }

  if (options?.initial) {
    treeLoading.value = true;
    childrenLoading.value = true;
    try {
      childrenPage.value = 1;
      const boot = await diGetBootstrap(folderId, { skip: 0, limit: childrenPageSize.value });
      applyBootstrap(boot);
      selectedFolderId.value = folderId;
      if (folderId) {
        await hydrateDeepLinkTree(folderId);
      }
    } catch (e) {
      notifyError(e, 'documentIntelligence.errors.treeLoad');
    } finally {
      treeLoading.value = false;
      childrenLoading.value = false;
    }
    return;
  }

  if (folderChanged) {
    await selectFolder(folderId, { syncUrl: false });
    if (folderId) {
      await hydrateDeepLinkTree(folderId);
    }
  } else if (needsRefresh) {
    await refreshListing();
  }

  if (route.query.refresh === '1') {
    const q = { ...route.query };
    delete q.refresh;
    await router.replace({ path: DI_HOME_PATH, query: q });
  }
}

onActivated(() => {
  void syncBrowseRouteContext();
});

watch(activeTagFilter, async () => {
  if (searchActive.value || mainMode.value !== 'browse') return;
  childrenPage.value = 1;
  await refreshListing();
});

watch(
  () => [route.query.folderId, route.query.refresh] as const,
  async () => {
    await syncBrowseRouteContext();
  },
);
</script>

<template>
  <DiResourcePreviewProvider :on-download="downloadFile">
  <div>
    <BaseBreadcrumb :title="t('documentIntelligence.title')" :breadcrumbs="breadcrumbs" />

    <v-card elevation="10" rounded="lg" class="overflow-hidden">
      <div class="d-flex di-layout">
        <!-- Sol panel: klasör ağacı -->
        <div
          v-if="!treeCollapsed"
          class="di-tree-panel flex-shrink-0"
          :style="{ width: treeWidth + 'px' }"
        >
          <div class="d-flex align-center justify-space-between px-3 py-2 border-b">
            <span class="text-subtitle-2 font-weight-bold">{{ t('documentIntelligence.explorer') }}</span>
            <v-btn icon size="x-small" variant="text" :title="t('documentIntelligence.collapse')" @click="toggleTreeCollapse">
              <LayoutSidebarLeftCollapseIcon size="18" />
            </v-btn>
          </div>
          <div class="pa-2 di-tree-scroll">
            <v-progress-linear v-if="treeLoading" indeterminate color="primary" class="mb-2" />
            <DiResourceTree
              :nodes="treeRoots"
              :selected-id="selectedFolderId"
              :root-label="t('documentIntelligence.allDocuments')"
              :empty-label="t('documentIntelligence.noFolders')"
              :load-children="loadTreeChildren"
              :is-loading="isParentLoading"
              :initial-expanded-ids="treeExpandedIds"
              @select="selectFolder"
            />
          </div>
        </div>

        <!-- Resize handle -->
        <div
          v-if="!treeCollapsed"
          :class="['di-resize-handle', { 'di-resize-handle--active': resizeActive }]"
          @mousedown.prevent="startResize"
        />

        <!-- Sağ panel: içerik -->
        <div class="di-content-panel flex-grow-1">
          <DiBrowseToolbar
            v-model:search-query="searchQuery"
            :tree-collapsed="treeCollapsed"
            :show-refresh="!searchActive && mainMode === 'browse'"
            :refresh-loading="childrenLoading || treeLoading"
            :can-view-editor-sessions="canViewEditorSessions"
            :show-permissions="!!(selectedFolder && canManage(selectedFolder))"
            :can-create="currentPerm.canCreate"
            :can-upload="currentPerm.canUpload"
            @toggle-tree="toggleTreeCollapse"
            @search-input="onSearchInput"
            @search-enter="runSearch"
            @search-clear="clearSearch"
            @refresh="refreshWorkspace"
            @permissions="onToolbarPermissions"
            @new-folder="openFolderDialog"
            @new-page="openDocDialog"
            @new-native-document="openNativeDocDialog"
            @new-native-sheet="openNativeSheetDialog"
            @new-native-presentation="openNativePresentationDialog"
            @generate-from-template="openGenerateFromTemplateDialog"
            @upload="openFileDialog"
          />

          <div class="di-content-scroll pa-4">
            <!-- Arama sonuçları -->
            <template v-if="searchActive">
              <div class="d-flex align-center mb-3">
                <h4 class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.searchResults') }}</h4>
                <v-chip size="small" class="ml-2" variant="tonal">{{ searchResults.length }}</v-chip>
                <v-spacer />
                <v-btn size="small" variant="text" class="text-none" @click="clearSearch">
                  {{ t('documentIntelligence.clearSearch') }}
                </v-btn>
              </div>
              <p class="text-caption text-medium-emphasis mb-2">{{ t('documentIntelligence.searchPublishedHint') }}</p>
              <v-progress-linear v-if="searching" indeterminate color="primary" class="mb-2" />
              <v-list v-if="searchResults.length" lines="two" class="py-0">
                <v-list-item
                  v-for="r in searchResults"
                  :key="r.id"
                  rounded="lg"
                  class="mb-1 border"
                  @click="openResource(r)"
                >
                  <template #prepend>
                    <v-icon :icon="resourceIcon(r)" color="primary" />
                  </template>
                  <v-list-item-title>{{ resourceLabel(r) }}</v-list-item-title>
                  <v-list-item-subtitle v-if="r.description">{{ r.description }}</v-list-item-subtitle>
                </v-list-item>
              </v-list>
              <div v-else-if="!searching" class="text-medium-emphasis text-body-2 py-6 text-center">
                {{ t('documentIntelligence.noResults') }}
              </div>
            </template>

            <!-- Doküman görünümü -->
            <template v-else-if="mainMode === 'doc' && openDoc">
              <!-- Klasöre dön + tıklanabilir klasör yolu -->
              <div class="d-flex align-center flex-wrap ga-1 mb-2">
                <v-btn size="small" variant="text" class="text-none px-2" prepend-icon="mdi-arrow-left" @click="backToFolder">
                  {{ t('documentIntelligence.backToFolder') }}
                </v-btn>
                <v-divider vertical class="mx-1 my-1" />
                <a class="di-crumb" @click="selectFolder(null)">{{ t('documentIntelligence.allDocuments') }}</a>
                <template v-for="b in folderPath" :key="b.id">
                  <v-icon size="14" class="text-medium-emphasis">mdi-chevron-right</v-icon>
                  <a class="di-crumb" @click="selectFolder(b.id)">{{ b.name }}</a>
                </template>
              </div>

              <div class="d-flex align-center mb-3 flex-wrap ga-2">
                <v-icon :icon="diPageResourceIcon(openDoc)" color="primary" class="mr-1" />
                <h3 class="text-h5 font-weight-bold mr-2">{{ resourceLabel(openDoc) }}</h3>
                <v-chip size="x-small" variant="tonal">v{{ docVersion }}</v-chip>
                <v-chip v-if="openDoc.status === 'draft'" size="x-small" variant="flat" color="warning" class="ml-1" prepend-icon="mdi-file-document-edit-outline">
                  {{ t('documentIntelligence.draft') }}
                </v-chip>
                <v-spacer />
                <template v-if="docMode === 'view'">
                  <v-btn v-if="openDoc.permissions.canEdit" size="small" variant="tonal" class="text-none" prepend-icon="mdi-pencil" @click="startEdit">
                    {{ t('documentIntelligence.edit') }}
                  </v-btn>
                  <v-btn size="small" variant="text" class="text-none" prepend-icon="mdi-history" @click="openHistory">
                    {{ t('documentIntelligence.history') }}
                  </v-btn>
                  <v-btn
                    v-if="isDiCloneable(openDoc)"
                    size="small"
                    variant="text"
                    class="text-none"
                    prepend-icon="mdi-content-copy"
                    @click="openClone(openDoc)"
                  >
                    {{ t('documentIntelligence.clone') }}
                  </v-btn>
                  <v-btn v-if="openDoc.permissions.canEdit" size="small" variant="text" class="text-none" icon="mdi-pencil-box-outline" :title="t('documentIntelligence.rename')" @click="openRename(openDoc)" />
                  <v-btn v-if="openDoc.permissions.canMove" size="small" variant="text" class="text-none" icon="mdi-folder-move-outline" :title="t('documentIntelligence.move')" @click="openMove(openDoc)" />
                  <v-btn v-if="openDoc.permissions.canDelete" size="small" variant="text" color="error" class="text-none" icon="mdi-delete-outline" :title="t('documentIntelligence.delete')" @click="openDelete(openDoc)" />
                </template>
                <template v-else>
                  <v-btn size="small" variant="text" class="text-none" :disabled="saving" @click="cancelEdit">
                    {{ t('documentIntelligence.cancel') }}
                  </v-btn>
                  <v-btn size="small" variant="text" class="text-none" :loading="saving" prepend-icon="mdi-file-document-edit-outline" @click="openSaveDialog(true)">
                    {{ t('documentIntelligence.saveAsDraft') }}
                  </v-btn>
                  <v-btn size="small" color="primary" variant="flat" class="text-none" :loading="saving" prepend-icon="mdi-content-save" @click="openSaveDialog(false)">
                    {{ openDoc.status === 'draft' ? t('documentIntelligence.publish') : t('documentIntelligence.save') }}
                  </v-btn>
                </template>
              </div>

              <div class="d-flex align-center flex-wrap ga-3 mb-3 text-caption text-medium-emphasis">
                <span v-if="openDoc.createdBy || openDoc.createdAt" class="d-flex align-center">
                  <v-icon size="14" class="mr-1">mdi-account-plus-outline</v-icon>
                  {{ t('documentIntelligence.metaCreated') }}:
                  <strong class="font-weight-medium ml-1">{{ openDoc.createdBy || '—' }}</strong>
                  <span v-if="openDoc.createdAt" class="ml-1">· {{ formatDateTime(openDoc.createdAt) }}</span>
                </span>
                <span v-if="openDoc.updatedBy || openDoc.updatedAt" class="d-flex align-center">
                  <v-icon size="14" class="mr-1">mdi-update</v-icon>
                  {{ t('documentIntelligence.metaUpdated') }}:
                  <strong class="font-weight-medium ml-1">{{ openDoc.updatedBy || '—' }}</strong>
                  <span v-if="openDoc.updatedAt" class="ml-1">· {{ formatDateTime(openDoc.updatedAt) }}</span>
                </span>
              </div>

              <div v-if="docMode === 'view'" class="mb-3 d-flex flex-wrap align-center ga-2">
                <DiClassificationField :model-value="openDoc.classificationTagId" readonly />
                <DiTagPicker
                  :model-value="openDoc.tags ?? []"
                  readonly
                  clickable
                  density="compact"
                  @tag-click="onDocTagClick"
                />
              </div>
              <div v-else class="mb-3">
                <DiClassificationField v-model="editClassificationId" density="compact" />
                <DiTagPicker v-model="editTags" density="compact" />
              </div>

              <v-progress-linear v-if="docLoading" indeterminate color="primary" class="mb-2" />

              <DiMarkdownEditor
                v-if="docMode === 'edit'"
                v-model="editContent"
                :current-resource-id="openDoc?.id ?? null"
                :upload-parent-id="openDoc?.parentId ?? selectedFolderId"
                :can-upload="openDoc?.permissions.canUpload ?? currentPerm.canUpload"
              />
              <DiMarkdownViewer v-else :content="docContent" :empty-label="t('documentIntelligence.emptyPage')" />

              <DiLinkedWorkItemsPanel
                v-if="openDoc && docMode !== 'edit'"
                :resource-id="openDoc.id"
                class="mt-4"
              />
              <DiBacklinksPanel
                v-if="openDoc && docMode !== 'edit'"
                :resource-id="openDoc.id"
                class="mt-4"
              />
            </template>

            <!-- Klasör tarayıcı -->
            <template v-else>
              <div class="di-browse-panel">
                <nav
                  class="di-content-breadcrumb d-flex align-center flex-wrap ga-1 py-2 mb-3"
                  :aria-label="t('documentIntelligence.explorer')"
                >
                  <template v-if="folderPath.length">
                    <a class="di-crumb" @click="selectFolder(null)">{{ t('documentIntelligence.allDocuments') }}</a>
                    <template v-for="(b, idx) in folderPath" :key="b.id">
                      <v-icon size="14" class="text-medium-emphasis flex-shrink-0">mdi-chevron-right</v-icon>
                      <a
                        v-if="idx < folderPath.length - 1"
                        class="di-crumb"
                        @click="selectFolder(b.id)"
                      >{{ b.name }}</a>
                      <span v-else class="di-crumb di-crumb--current">{{ b.name }}</span>
                    </template>
                  </template>
                  <span v-else class="di-crumb di-crumb--current">{{ t('documentIntelligence.allDocuments') }}</span>
                </nav>

                <DiDiscoveryHome
                  v-if="showDiscovery"
                  :tree="treeRoots"
                  @select-folder="selectFolder"
                  @open-resource="openResource"
                  @search="onDiscoverySearch"
                />

                <DiAreaIndexBanner
                  v-if="areaIndexPage && selectedFolder"
                  :index-page="areaIndexPage"
                  :area-name="selectedFolder.name"
                  @open="openResource"
                />

                <div v-if="folderTagOptions.length" class="d-flex align-center flex-wrap ga-2 mb-3">
                  <span class="text-caption text-medium-emphasis">{{ t('documentIntelligence.tags.filterLabel') }}</span>
                  <v-chip
                    size="small"
                    :variant="activeTagFilter === null ? 'flat' : 'outlined'"
                    :color="activeTagFilter === null ? 'primary' : undefined"
                    @click="activeTagFilter = null"
                  >
                    {{ t('documentIntelligence.tags.all') }}
                  </v-chip>
                  <v-chip
                    v-for="tag in folderTagOptions"
                    :key="tag"
                    size="small"
                    :variant="activeTagFilter === tag ? 'flat' : 'outlined'"
                    :color="activeTagFilter === tag ? 'primary' : undefined"
                    @click="activeTagFilter = activeTagFilter === tag ? null : tag"
                  >
                    {{ tag }}
                  </v-chip>
                </div>

                <div
                  class="di-browse-body position-relative"
                  :class="{ 'di-browse-body--loading': childrenLoading }"
                >
                  <v-overlay
                    :model-value="childrenLoading"
                    contained
                    persistent
                    scrim="rgba(var(--v-theme-surface), 0.78)"
                    class="align-center justify-center"
                  >
                    <div class="d-flex flex-column align-center text-center px-4">
                      <v-progress-circular indeterminate color="primary" size="48" width="4" />
                      <span class="text-body-2 text-medium-emphasis mt-3">{{ t('documentIntelligence.loadingContents') }}</span>
                    </div>
                  </v-overlay>

                  <div v-if="!filteredChildren.length && !childrenLoading" class="text-center py-12">
                    <v-icon icon="mdi-folder-open-outline" size="56" class="text-medium-emphasis mb-2" />
                    <div class="text-body-1 text-medium-emphasis">
                      {{ activeTagFilter ? t('documentIntelligence.tags.noMatches') : t('documentIntelligence.emptyFolder') }}
                    </div>
                  </div>

                  <template v-else-if="filteredChildren.length">
                <div
                  v-if="selectableResources.length"
                  class="d-flex flex-wrap align-center ga-2 mb-3"
                >
                  <v-checkbox-btn
                    :model-value="allSelectableSelected"
                    :indeterminate="someSelectableSelected && !allSelectableSelected"
                    density="compact"
                    hide-details
                    @update:model-value="toggleSelectAllOnPage"
                  />
                  <span class="text-body-2 text-medium-emphasis">
                    {{ t('documentIntelligence.selectAllOnPage') }}
                  </span>
                  <template v-if="selectedResourceCount">
                    <v-chip size="small" variant="tonal" color="primary">
                      {{ t('documentIntelligence.selectedCount', { count: selectedResourceCount }) }}
                    </v-chip>
                    <v-btn
                      size="small"
                      variant="text"
                      class="text-none"
                      @click="clearResourceSelection"
                    >
                      {{ t('documentIntelligence.clearSelection') }}
                    </v-btn>
                    <v-btn
                      size="small"
                      variant="flat"
                      color="error"
                      class="text-none"
                      prepend-icon="mdi-delete-outline"
                      @click="openBulkDelete"
                    >
                      {{ t('documentIntelligence.bulkDelete') }}
                    </v-btn>
                  </template>
                </div>

                <!-- Klasörler -->
                <div v-if="folderChildren.length" class="mb-4">
                  <div class="text-caption text-medium-emphasis mb-2">{{ t('documentIntelligence.folders') }}</div>
                  <v-row dense>
                    <v-col v-for="f in folderChildren" :key="f.id" cols="12" sm="6" md="4" lg="3">
                      <v-card variant="outlined" rounded="lg" class="di-folder-card pa-3 d-flex align-center" @click="selectFolder(f.id)">
                        <v-icon icon="mdi-folder" color="primary" size="28" class="mr-3 flex-shrink-0" />
                        <span class="text-body-2 font-weight-medium text-truncate flex-grow-1">{{ f.name }}</span>
                        <v-menu location="bottom end">
                          <template #activator="{ props: menuProps }">
                            <v-btn icon size="x-small" variant="text" v-bind="menuProps" @click.stop>
                              <v-icon size="18">mdi-dots-vertical</v-icon>
                            </v-btn>
                          </template>
                          <v-list density="compact">
                            <v-list-item v-if="canManage(f)" prepend-icon="mdi-shield-account-outline" :title="t('documentIntelligence.permissions.menuTitle')" @click="openPermissions(f)" />
                            <v-list-item v-if="f.permissions.canEdit" prepend-icon="mdi-pencil-box-outline" :title="t('documentIntelligence.rename')" @click="openRename(f)" />
                            <v-list-item v-if="f.permissions.canMove" prepend-icon="mdi-folder-move-outline" :title="t('documentIntelligence.move')" @click="openMove(f)" />
                            <v-list-item v-if="f.permissions.canDelete" prepend-icon="mdi-delete-outline" :title="t('documentIntelligence.delete')" base-color="error" @click="openDelete(f)" />
                          </v-list>
                        </v-menu>
                      </v-card>
                    </v-col>
                  </v-row>
                </div>

                <!-- Sayfalar / dökümanlar / dosyalar -->
                <template v-for="section in contentSections">
                  <div v-if="section.items.length" :key="section.key" class="mb-4">
                    <div class="text-caption text-medium-emphasis mb-2">{{ t(section.labelKey) }}</div>
                    <v-list class="py-0">
                      <v-list-item
                        v-for="d in section.items"
                        :key="d.id"
                        rounded="lg"
                        class="mb-1 border di-doc-row"
                        @click="openResource(d)"
                      >
                        <template #prepend>
                          <v-checkbox-btn
                            v-if="d.permissions.canDelete"
                            :model-value="isResourceSelected(d.id)"
                            density="compact"
                            hide-details
                            class="mr-1 flex-shrink-0"
                            @click.stop
                            @update:model-value="(value) => toggleResourceSelection(d.id, value)"
                          />
                          <v-icon :icon="resourceIcon(d)" color="primary" />
                        </template>
                        <v-list-item-title>
                          {{ resourceLabel(d) }}
                          <v-chip v-if="isDiManagedDocument(d) && d.documentNo" size="x-small" variant="tonal" color="primary" class="ml-1">
                            {{ d.documentNo }}
                          </v-chip>
                          <v-chip v-if="d.type === 'markdown' && d.status === 'draft'" size="x-small" variant="flat" color="warning" class="ml-1">
                            {{ t('documentIntelligence.draft') }}
                          </v-chip>
                        </v-list-item-title>
                        <v-list-item-subtitle v-if="hasResourceSubtitle(d)">
                          {{ buildResourceSubtitle(d) }}
                        </v-list-item-subtitle>
                        <template #append>
                          <v-btn
                            v-if="d.type === 'file' && d.permissions.canDownload && isDiOfficeEditable(d)"
                            icon
                            size="x-small"
                            variant="text"
                            :title="t('documentIntelligence.openInEditor')"
                            @click.stop="openFileEditor(d)"
                          >
                            <v-icon size="18">mdi-file-document-edit-outline</v-icon>
                          </v-btn>
                          <v-btn
                            v-if="d.type === 'file' && d.permissions.canDownload && isDiPreviewable(d)"
                            icon
                            size="x-small"
                            variant="text"
                            :title="t('documentIntelligence.preview')"
                            @click.stop="openFilePreview(d)"
                          >
                            <v-icon size="18">mdi-file-eye-outline</v-icon>
                          </v-btn>
                          <v-btn
                            v-if="d.type === 'file' && d.permissions.canDownload"
                            icon
                            size="x-small"
                            variant="text"
                            :loading="downloadingId === d.id"
                            :title="t('documentIntelligence.download')"
                            @click.stop="downloadFile(d)"
                          >
                            <v-icon size="18">mdi-download</v-icon>
                          </v-btn>
                          <v-menu location="bottom end">
                            <template #activator="{ props: menuProps }">
                              <v-btn icon size="x-small" variant="text" v-bind="menuProps" @click.stop>
                                <v-icon size="18">mdi-dots-vertical</v-icon>
                              </v-btn>
                            </template>
                            <v-list density="compact">
                              <v-list-item prepend-icon="mdi-information-outline" :title="t('documentIntelligence.resourceInfoTitle')" @click="openResourceInfo(d)" />
                              <v-list-item v-if="d.permissions.canEdit" prepend-icon="mdi-tag-outline" :title="t('documentIntelligence.tags.editTitle')" @click="openResourceTags(d)" />
                              <v-list-item v-if="isDiCloneable(d)" prepend-icon="mdi-content-copy" :title="t('documentIntelligence.clone')" @click="openClone(d)" />
                              <v-list-item v-if="d.type === 'file' && d.permissions.canDownload && isDiOfficeEditable(d)" prepend-icon="mdi-history" :title="t('documentIntelligence.versionHistory')" @click="openFileHistory(d)" />
                              <v-list-item v-if="d.type === 'file' && d.permissions.canDownload && isDiOfficeEditable(d)" prepend-icon="mdi-open-in-new" :title="t('documentIntelligence.openInEditor')" @click="openFileEditorInNewTab(d)" />
                              <v-list-item v-if="d.type === 'file' && d.permissions.canDownload && isDiPreviewable(d)" prepend-icon="mdi-file-eye-outline" :title="t('documentIntelligence.preview')" @click="openFilePreview(d)" />
                              <v-list-item v-if="d.type === 'file' && d.permissions.canDownload && isDiOfficePdfExportable(d)" prepend-icon="mdi-file-pdf-box" :title="t('documentIntelligence.exportPdf')" @click="downloadPdfExport(d)" />
                              <v-list-item v-if="d.type === 'file' && d.permissions.canDownload" prepend-icon="mdi-download" :title="t('documentIntelligence.download')" @click="downloadFile(d)" />
                              <v-list-item v-if="d.permissions.canEdit" prepend-icon="mdi-pencil-box-outline" :title="t('documentIntelligence.rename')" @click="openRename(d)" />
                              <v-list-item v-if="d.permissions.canMove" prepend-icon="mdi-folder-move-outline" :title="t('documentIntelligence.move')" @click="openMove(d)" />
                              <v-list-item v-if="d.permissions.canDelete" prepend-icon="mdi-delete-outline" :title="t('documentIntelligence.delete')" base-color="error" @click="openDelete(d)" />
                            </v-list>
                          </v-menu>
                        </template>
                      </v-list-item>
                    </v-list>
                  </div>
                </template>
                  </template>

                  <div
                    v-if="showChildrenListingFooter"
                    class="di-children-footer d-flex flex-column flex-sm-row align-center justify-space-between gap-3 mt-4 pt-3"
                  >
                    <span class="text-caption text-medium-emphasis">{{ childrenRangeLabel }}</span>
                    <div class="d-flex flex-wrap align-center justify-end gap-3">
                      <v-select
                        :model-value="childrenPageSize"
                        :items="childrenPageSizeOptions"
                        :label="t('documentIntelligence.childrenPageSize')"
                        density="compact"
                        variant="outlined"
                        hide-details
                        style="min-width: 120px; max-width: 140px"
                        @update:model-value="onChildrenPageSizeChange"
                      />
                      <v-pagination
                        v-if="childrenPageCount > 1"
                        :model-value="childrenPage"
                        :length="childrenPageCount"
                        :total-visible="7"
                        density="compact"
                        @update:model-value="onChildrenPageChange"
                      />
                    </div>
                  </div>
                </div>
              </div>
            </template>
          </div>
        </div>
      </div>
    </v-card>

    <!-- Yeni klasör -->
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

    <!-- Yeni sayfa -->
    <v-dialog v-model="docDialog" max-width="420">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.newPage') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="docTitle"
            :label="t('documentIntelligence.pageTitle')"
            variant="outlined"
            density="comfortable"
            autofocus
            hide-details
            class="mb-3"
            @keydown.enter="submitDoc(false)"
          />
          <v-select
            v-model="docTemplate"
            :items="pageTemplateOptions"
            item-title="title"
            item-value="value"
            :label="t('documentIntelligence.templates.label')"
            variant="outlined"
            density="comfortable"
            hide-details
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="docDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn variant="text" class="text-none" :loading="busy" :disabled="!docTitle.trim()" prepend-icon="mdi-file-document-edit-outline" @click="submitDoc(true)">
            {{ t('documentIntelligence.saveAsDraft') }}
          </v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="busy" :disabled="!docTitle.trim()" @click="submitDoc(false)">
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Yeni döküman (native DOCX) -->
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
            <p
              v-if="nativeDocHeaderFields.documentName && nativeDocLetterhead.settings.headerFields.documentName"
              class="text-caption text-medium-emphasis mb-2 ms-8"
            >
              {{ t('documentIntelligence.nativeDocDocumentNameAutoHint') }}
            </p>
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

    <!-- Yeni elektronik tablo (native XLSX) -->
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

    <!-- Yeni sunum (native PPTX) -->
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

    <!-- Dosya yükle -->
    <v-dialog v-model="fileDialog" max-width="460">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.uploadFile') }}</v-card-title>
        <v-card-text>
          <input ref="fileInputEl" type="file" class="d-none" @change="onFilePick" />
          <div
            class="di-dropzone d-flex flex-column align-center justify-center text-center pa-6 mb-3 rounded-lg"
            @click="triggerFilePick"
          >
            <v-icon size="40" class="text-medium-emphasis mb-2">mdi-cloud-upload-outline</v-icon>
            <div v-if="!pickedFile" class="text-body-2 text-medium-emphasis">
              {{ t('documentIntelligence.selectFileHint', { max: MAX_FILE_MB }) }}
            </div>
            <div v-else class="d-flex align-center ga-2">
              <v-icon size="20" color="primary">mdi-file-outline</v-icon>
              <span class="text-body-2 font-weight-medium">{{ pickedFile.name }}</span>
              <span class="text-caption text-medium-emphasis">{{ formatSize(pickedFile.size) }}</span>
            </div>
          </div>
          <v-text-field
            v-model="fileDisplayName"
            :label="t('documentIntelligence.fileNameLabel')"
            variant="outlined"
            density="comfortable"
            hide-details
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="fileDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="busy" :disabled="!pickedFile" @click="submitFile">
            {{ t('documentIntelligence.upload') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Yeniden adlandır -->
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

    <!-- Taşı -->
    <v-dialog v-model="moveDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.move') }}</v-card-title>
        <v-card-text>
          <DiFolderPickerList
            v-model="moveDestId"
            :exclude-subtree-id="moveTarget?.type === 'folder' ? moveTarget.id : null"
            :load-children="loadLazyTreeChildren"
            :is-loading="isParentLoading"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="moveDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="busy" @click="submitMove">
            {{ t('documentIntelligence.move') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <DiCloneResourceDialog
      v-model="cloneDialog"
      :resource="cloneTarget"
      :load-children="loadLazyTreeChildren"
      :is-loading="isParentLoading"
      :loading="busy"
      @cloned="onCloned"
    />

    <!-- Sil -->
    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.delete') }}</v-card-title>
        <v-card-text>
          <p class="text-body-2 mb-2">
            {{ t('documentIntelligence.deleteConfirm', { name: deleteTarget ? resourceLabel(deleteTarget) : '' }) }}
          </p>
          <v-alert v-if="deleteForce" type="warning" variant="tonal" density="compact" class="text-body-2">
            {{ t('documentIntelligence.deleteForceHint') }}
          </v-alert>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="error" variant="flat" class="text-none" :loading="busy" @click="submitDelete">
            {{ deleteForce ? t('documentIntelligence.deleteAnyway') : t('documentIntelligence.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Toplu sil -->
    <v-dialog v-model="bulkDeleteDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.bulkDelete') }}</v-card-title>
        <v-card-text>
          <p class="text-body-2 mb-0">
            {{ t('documentIntelligence.bulkDeleteConfirm', { count: selectedResourceCount }) }}
          </p>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="bulkDeleteDialog = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="error" variant="flat" class="text-none" :loading="busy" @click="submitBulkDelete">
            {{ t('documentIntelligence.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <DiSavePageDialog
      v-model="saveDialog"
      :mode="saveDialogMode"
      :loading="saving"
      @confirm="confirmSaveEdit"
    />

    <DiMarkdownVersionHistoryDialog
      v-model="historyDialog"
      :resource-id="openDoc?.id ?? null"
      :can-restore="openDoc?.permissions.canEdit ?? false"
      @restored="onVersionRestored"
    />

    <DiFileVersionHistoryDialog
      v-model="fileHistoryDialog"
      :resource="fileHistoryTarget"
      :can-restore="fileHistoryTarget?.permissions.canEdit ?? false"
      @restored="onFileVersionRestored"
    />

    <DiResourceInfoDialog v-model="infoDialog" :resource="infoTarget" />

    <DiResourceTagsDialog v-model="tagsDialog" :resource="tagsTarget" @saved="onResourceTagsSaved" />

    <!-- Klasör yetkileri -->
    <DiPermissionsDialog
      v-model="permissionsDialog"
      :folder-id="permTargetId"
      :folder-name="permTargetName"
      @changed="onPermissionsChanged"
      @notify="notify"
    />

    <DiFilePreviewDialog
      v-model="filePreviewOpen"
      :resource="filePreviewResource"
      @download="downloadFile"
    />

    <DiResourceEditorDialog
      v-model="fileEditorOpen"
      :resource="fileEditorResource"
    />

    <DiEditorLockDialog
      :model-value="editorLockDialogOpen"
      :status="editorLockStatus"
      @update:model-value="onEditorLockDialogUpdate"
      @choose="onEditorLockChoose"
    />

    <DiGenerateFromTemplateDialog
      v-model="generateFromTemplateDialog"
      :parent-folder-id="selectedFolderId"
      @created="onGenerateFromTemplateCreated"
    />

  </div>
  </DiResourcePreviewProvider>
</template>

<style scoped>
.di-layout {
  min-height: 600px;
}
.di-tree-panel {
  border-right: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  display: flex;
  flex-direction: column;
}
.di-tree-scroll {
  overflow: auto;
  flex: 1 1 auto;
  max-height: 70vh;
}
.di-resize-handle {
  width: 5px;
  cursor: col-resize;
  background: transparent;
  flex-shrink: 0;
}
.di-resize-handle:hover,
.di-resize-handle--active {
  background: rgba(var(--v-theme-primary), 0.3);
}
.di-content-panel {
  display: flex;
  flex-direction: column;
  min-width: 0;
}
.di-content-scroll {
  overflow: auto;
  flex: 1 1 auto;
  max-height: 75vh;
}
.di-folder-card {
  cursor: pointer;
  transition: border-color 0.15s ease, background-color 0.15s ease;
}
.di-folder-card:hover {
  border-color: rgb(var(--v-theme-primary));
  background-color: rgba(var(--v-theme-primary), 0.04);
}
.di-doc-row {
  cursor: pointer;
}
.di-crumb {
  cursor: pointer;
  color: rgb(var(--v-theme-primary));
  font-size: 0.875rem;
}
.di-crumb:hover {
  text-decoration: underline;
}
.di-crumb--current {
  color: rgba(var(--v-theme-on-surface), 0.87);
  font-weight: 600;
  cursor: default;
}
.di-content-breadcrumb {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
.di-browse-body {
  min-height: 200px;
}
.di-children-footer {
  border-top: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
.di-browse-body--loading {
  min-height: 320px;
}
.di-move-list {
  max-height: 320px;
  overflow: auto;
}
.di-dropzone {
  border: 2px dashed rgba(var(--v-theme-on-surface), 0.2);
  cursor: pointer;
  transition: border-color 0.15s ease, background-color 0.15s ease;
}
.di-dropzone:hover {
  border-color: rgb(var(--v-theme-primary));
  background-color: rgba(var(--v-theme-primary), 0.04);
}
</style>
