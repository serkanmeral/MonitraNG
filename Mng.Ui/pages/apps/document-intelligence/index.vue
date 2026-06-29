<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DiResourceTree from '@/components/apps/document-intelligence/DiResourceTree.vue';
import DiMarkdownViewer from '@/components/apps/document-intelligence/DiMarkdownViewer.vue';
import DiMarkdownEditor from '@/components/apps/document-intelligence/DiMarkdownEditor.vue';
import DiPermissionsDialog from '@/components/apps/document-intelligence/DiPermissionsDialog.vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiResourceEditorDialog from '@/components/apps/document-intelligence/DiResourceEditorDialog.vue';
import DiLinkedWorkItemsPanel from '@/components/apps/document-intelligence/DiLinkedWorkItemsPanel.vue';
import { isDiPreviewable, isDiDocxEditable } from '@/utils/diFilePreview';
import {
  DI_HOME_PATH,
  buildDiResourceUrl,
  parseFolderIdQuery,
  parseLegacyResourceIdQuery,
} from '@/utils/diResourceLink';
import { useResizableTreePanel } from '@/composables/useResizableTreePanel';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';
import {
  LayoutSidebarLeftCollapseIcon,
  LayoutSidebarLeftExpandIcon,
} from 'vue-tabler-icons';
import {
  diGetBootstrap,
  diGetBrowseContext,
  diGetBreadcrumb,
  diGetMarkdownContent,
  diCreateFolder,
  diCreateMarkdown,
  diUpdateMarkdown,
  diRename,
  diMove,
  diDelete,
  diSearch,
  diCreateFileResource,
  diFetchFileBlob,
  diGetMarkdownVersions,
  diGetMarkdownVersionContent,
  diRestoreMarkdownVersion,
  diErrorStatus,
  diExtractMessage,
} from '@/services/documentIntelligenceService';
import {
  diFullPermission,
  type DiTreeNode,
  type DiResource,
  type DiBreadcrumb,
  type DiResourceBrowseContext,
  type DiResourceBootstrap,
  type DiMarkdownVersion,
  type DiEffectivePermission,
} from '@/types/apps/documentIntelligence';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const authStore = useAuthStore();
const route = useRoute();
const router = useRouter();

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
const tree = ref<DiTreeNode[]>([]);
const treeLoading = ref(false);
const selectedFolderId = ref<string | null>(null);
const selectedFolder = ref<DiResource | null>(null);
const children = ref<DiResource[]>([]);
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
const saving = ref(false);

// Arama
const searchQuery = ref('');
const searchActive = ref(false);
const searching = ref(false);
const searchResults = ref<DiResource[]>([]);

// Snackbar
const snackbar = ref(false);
const snackbarText = ref('');
const snackbarColor = ref<'success' | 'error' | 'info'>('info');

function notify(text: string, color: 'success' | 'error' | 'info' = 'info') {
  snackbarText.value = text;
  snackbarColor.value = color;
  snackbar.value = true;
}

// --- Diyaloglar ---
const folderDialog = ref(false);
const folderName = ref('');
const docDialog = ref(false);
const docTitle = ref('');
const renameDialog = ref(false);
const renameTarget = ref<DiResource | null>(null);
const renameName = ref('');
const moveDialog = ref(false);
const moveTarget = ref<DiResource | null>(null);
const moveDestId = ref<string | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<DiResource | null>(null);
const deleteForce = ref(false);
const busy = ref(false);

// Sürüm geçmişi
const historyDialog = ref(false);
const versions = ref<DiMarkdownVersion[]>([]);
const versionsLoading = ref(false);
const selectedVersion = ref<number | null>(null);
const versionContent = ref('');
const versionContentLoading = ref(false);
const restoringVersion = ref<number | null>(null);

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

// Taşıma hedefleri için düz klasör listesi (kendisi + alt ağacı hariç).
const flatFolders = computed(() => {
  const out: { id: string; name: string; depth: number }[] = [];
  const excludeId = moveTarget.value?.id;
  function walk(nodes: DiTreeNode[], depth: number) {
    for (const n of nodes) {
      if (n.id === excludeId) continue; // kendi alt ağacına taşımayı engelle
      out.push({ id: n.id, name: n.name, depth });
      if (n.children.length) walk(n.children, depth + 1);
    }
  }
  walk(tree.value, 0);
  return out;
});

const folderChildren = computed(() => children.value.filter((c) => c.type === 'folder'));
const docChildren = computed(() => children.value.filter((c) => c.type !== 'folder'));

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
  folderPath.value = ctx.breadcrumb;
  selectedFolder.value = ctx.selectedFolder;
}

function applyBootstrap(boot: DiResourceBootstrap) {
  tree.value = boot.tree;
  applyBrowseContext(boot);
}

/** Ağaç + geçerli klasör içeriği (mutasyon / yetki değişimi sonrası). */
async function refreshWorkspace() {
  treeLoading.value = true;
  childrenLoading.value = true;
  try {
    applyBootstrap(await diGetBootstrap(selectedFolderId.value));
  } catch (e) {
    notify(diExtractMessage(e, t('documentIntelligence.errors.treeLoad')), 'error');
  } finally {
    treeLoading.value = false;
    childrenLoading.value = false;
  }
}

/** Yalnızca liste/breadcrumb (ağaç değişmediyse). */
async function refreshListing() {
  childrenLoading.value = true;
  try {
    applyBrowseContext(await diGetBrowseContext(selectedFolderId.value));
  } catch (e) {
    notify(diExtractMessage(e, t('documentIntelligence.errors.childrenLoad')), 'error');
    children.value = [];
  } finally {
    childrenLoading.value = false;
  }
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
  mainMode.value = 'browse';
  selectedFolderId.value = folderId;
  openDoc.value = null;
  childrenLoading.value = true;
  try {
    applyBrowseContext(await diGetBrowseContext(folderId));
  } catch (e) {
    notify(diExtractMessage(e, t('documentIntelligence.errors.childrenLoad')), 'error');
    children.value = [];
    folderPath.value = [];
    selectedFolder.value = null;
  } finally {
    childrenLoading.value = false;
  }

  if (options?.syncUrl !== false && route.path === DI_HOME_PATH) {
    await router.replace({
      path: DI_HOME_PATH,
      query: folderId ? { folderId } : {},
    });
  }
}

async function openResource(resource: DiResource) {
  if (resource.type === 'folder') {
    await selectFolder(resource.id);
    return;
  }
  await navigateTo(buildDiResourceUrl(resource.id));
}

function openFilePreview(resource: DiResource) {
  void navigateTo(buildDiResourceUrl(resource.id));
}

function openFileEditor(resource: DiResource) {
  void navigateTo(buildDiResourceUrl(resource.id));
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
    notify(diExtractMessage(e, t('documentIntelligence.errors.docLoad')), 'error');
    mainMode.value = 'browse';
  } finally {
    docLoading.value = false;
  }
}

function backToFolder() {
  selectFolder(openDoc.value?.parentId ?? null);
}

// --- Sürüm geçmişi ---
async function openHistory() {
  if (!openDoc.value) return;
  historyDialog.value = true;
  selectedVersion.value = null;
  versionContent.value = '';
  versionsLoading.value = true;
  try {
    versions.value = await diGetMarkdownVersions(openDoc.value.id);
  } catch (e) {
    notify(diExtractMessage(e, t('documentIntelligence.errors.versionsLoad')), 'error');
    versions.value = [];
  } finally {
    versionsLoading.value = false;
  }
}

async function previewVersion(v: DiMarkdownVersion) {
  if (!openDoc.value) return;
  selectedVersion.value = v.versionNumber;
  versionContentLoading.value = true;
  try {
    const c = await diGetMarkdownVersionContent(openDoc.value.id, v.versionNumber);
    versionContent.value = c.content;
  } catch (e) {
    notify(diExtractMessage(e, t('documentIntelligence.errors.versionLoad')), 'error');
    versionContent.value = '';
  } finally {
    versionContentLoading.value = false;
  }
}

async function restoreVersion(v: DiMarkdownVersion) {
  if (!openDoc.value || v.isCurrent) return;
  restoringVersion.value = v.versionNumber;
  try {
    const restored = await diRestoreMarkdownVersion(openDoc.value.id, v.versionNumber);
    historyDialog.value = false;
    notify(t('documentIntelligence.versionRestored', { n: v.versionNumber }), 'success');
    await openMarkdown(restored);
  } catch (e) {
    notify(diExtractMessage(e, t('documentIntelligence.errors.versionRestore')), 'error');
  } finally {
    restoringVersion.value = null;
  }
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
  docMode.value = 'edit';
}

function cancelEdit() {
  docMode.value = 'view';
}

async function saveEdit(asDraft = false) {
  if (!openDoc.value) return;
  saving.value = true;
  try {
    const updated = await diUpdateMarkdown(openDoc.value.id, {
      content: editContent.value,
      expectedVersionNumber: docVersion.value,
      isDraft: asDraft,
    });
    docContent.value = editContent.value;
    docVersion.value = updated.currentVersionNumber || docVersion.value + 1;
    openDoc.value = updated;
    docMode.value = 'view';
    notify(asDraft ? t('documentIntelligence.draftSaved') : t('documentIntelligence.published'), 'success');
  } catch (e) {
    if (diErrorStatus(e) === 409) {
      notify(t('documentIntelligence.errors.conflict'), 'error');
      // Güncel sürümü tekrar yükle
      await openMarkdown(openDoc.value);
    } else {
      notify(diExtractMessage(e, t('documentIntelligence.errors.save')), 'error');
    }
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
    notify(diExtractMessage(e, t('documentIntelligence.errors.create')), 'error');
  } finally {
    busy.value = false;
  }
}

function openDocDialog() {
  docTitle.value = '';
  docDialog.value = true;
}

async function submitDoc(asDraft = false) {
  const title = docTitle.value.trim();
  if (!title) return;
  busy.value = true;
  try {
    const created = await diCreateMarkdown({ title, content: '', parentId: selectedFolderId.value, isDraft: asDraft });
    docDialog.value = false;
    notify(t('documentIntelligence.docCreated'), 'success');
    await refreshListing();
    await openMarkdown(created);
    startEdit();
  } catch (e) {
    notify(diExtractMessage(e, t('documentIntelligence.errors.create')), 'error');
  } finally {
    busy.value = false;
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
    notify(diExtractMessage(e, t('documentIntelligence.errors.rename')), 'error');
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
    notify(diExtractMessage(e, t('documentIntelligence.errors.move')), 'error');
  } finally {
    busy.value = false;
  }
}

// --- Sil ---
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
      notify(diExtractMessage(e, t('documentIntelligence.errors.delete')), 'error');
    }
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
  // Yetki değişince görünürlük değişebilir: ağaç + içerik + seçili klasör tazelenir.
  await refreshWorkspace();
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
    notify(diExtractMessage(e, t('documentIntelligence.errors.upload')), 'error');
  } finally {
    busy.value = false;
  }
}

async function downloadFile(resource: DiResource) {
  if (!resource.filePath) {
    notify(diExtractMessage(null, t('documentIntelligence.errors.download')), 'error');
    return;
  }
  downloadingId.value = resource.id;
  try {
    const blob = await diFetchFileBlob(resource.filePath);
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = resource.fileName || resource.name || 'dosya';
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  } catch (e) {
    notify(diExtractMessage(e, t('documentIntelligence.errors.download')), 'error');
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
    notify(diExtractMessage(e, t('documentIntelligence.errors.search')), 'error');
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

function resourceIcon(resource: DiResource): string {
  if (resource.type === 'folder') return 'mdi-folder';
  if (resource.type === 'markdown') return 'mdi-language-markdown-outline';
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
  return resource.type === 'markdown' ? resource.title || resource.name : resource.name;
}

function formatSize(bytes: number | null): string {
  if (!bytes || bytes <= 0) return '';
  const units = ['B', 'KB', 'MB', 'GB'];
  const i = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));
  return `${Math.round((bytes / Math.pow(1024, i)) * 10) / 10} ${units[i]}`;
}

onMounted(async () => {
  const legacyId = parseLegacyResourceIdQuery(route.query as Record<string, unknown>);
  if (legacyId) {
    await navigateTo(buildDiResourceUrl(legacyId), { replace: true });
    return;
  }

  treeLoading.value = true;
  childrenLoading.value = true;
  try {
    const folderId = parseFolderIdQuery(route.query as Record<string, unknown>);
    const initialFolder = folderId === undefined ? null : folderId;
    applyBootstrap(await diGetBootstrap(initialFolder));
    selectedFolderId.value = initialFolder;
  } catch (e) {
    notify(diExtractMessage(e, t('documentIntelligence.errors.treeLoad')), 'error');
  } finally {
    treeLoading.value = false;
    childrenLoading.value = false;
  }
});

watch(
  () => route.query.folderId,
  async () => {
    if (route.path !== DI_HOME_PATH) return;
    const folderId = parseFolderIdQuery(route.query as Record<string, unknown>);
    if (folderId === undefined) return;
    if (selectedFolderId.value === folderId) return;
    await selectFolder(folderId, { syncUrl: false });
  }
);
</script>

<template>
  <div>
    <BaseBreadcrumb :title="t('documentIntelligence.title')" :breadcrumbs="breadcrumbs" />

    <div class="d-flex justify-end mb-3">
      <v-btn
        to="/apps/document-intelligence/designer"
        variant="tonal"
        color="primary"
        size="small"
        class="text-none"
        prepend-icon="mdi-file-document-edit-outline"
      >
        {{ t('documentIntelligence.designer.openDesigner') }}
      </v-btn>
    </div>

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
              :nodes="tree"
              :selected-id="selectedFolderId"
              :root-label="t('documentIntelligence.allDocuments')"
              :empty-label="t('documentIntelligence.noFolders')"
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
          <!-- Üst bar -->
          <div class="d-flex align-center ga-2 px-4 py-2 border-b flex-wrap">
            <v-btn
              v-if="treeCollapsed"
              icon
              size="small"
              variant="text"
              :title="t('documentIntelligence.expand')"
              @click="toggleTreeCollapse"
            >
              <LayoutSidebarLeftExpandIcon size="18" />
            </v-btn>

            <v-text-field
              v-model="searchQuery"
              density="compact"
              variant="solo-filled"
              flat
              hide-details
              clearable
              prepend-inner-icon="mdi-magnify"
              :placeholder="t('documentIntelligence.searchPlaceholder')"
              class="di-search flex-grow-1"
              style="max-width: 420px"
              @update:model-value="onSearchInput"
              @keydown.enter="runSearch"
              @click:clear="clearSearch"
            />

            <v-spacer />

            <v-btn
              v-if="selectedFolder && canManage(selectedFolder)"
              color="primary"
              variant="text"
              size="small"
              class="text-none"
              prepend-icon="mdi-shield-account-outline"
              :title="t('documentIntelligence.permissions.title')"
              @click="openPermissions(selectedFolder)"
            >
              {{ t('documentIntelligence.permissions.menuTitle') }}
            </v-btn>
            <v-btn
              v-if="currentPerm.canCreate"
              color="primary"
              variant="tonal"
              size="small"
              class="text-none"
              prepend-icon="mdi-folder-plus-outline"
              @click="openFolderDialog"
            >
              {{ t('documentIntelligence.newFolder') }}
            </v-btn>
            <v-btn
              v-if="currentPerm.canCreate"
              color="primary"
              variant="flat"
              size="small"
              class="text-none"
              prepend-icon="mdi-file-document-plus-outline"
              @click="openDocDialog"
            >
              {{ t('documentIntelligence.newDocument') }}
            </v-btn>
            <v-btn
              v-if="currentPerm.canUpload"
              color="primary"
              variant="tonal"
              size="small"
              class="text-none"
              prepend-icon="mdi-upload"
              @click="openFileDialog"
            >
              {{ t('documentIntelligence.uploadFile') }}
            </v-btn>
          </div>

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
                <v-icon icon="mdi-language-markdown-outline" color="primary" class="mr-1" />
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
                  <v-btn v-if="openDoc.permissions.canEdit" size="small" variant="text" class="text-none" icon="mdi-pencil-box-outline" :title="t('documentIntelligence.rename')" @click="openRename(openDoc)" />
                  <v-btn v-if="openDoc.permissions.canMove" size="small" variant="text" class="text-none" icon="mdi-folder-move-outline" :title="t('documentIntelligence.move')" @click="openMove(openDoc)" />
                  <v-btn v-if="openDoc.permissions.canDelete" size="small" variant="text" color="error" class="text-none" icon="mdi-delete-outline" :title="t('documentIntelligence.delete')" @click="openDelete(openDoc)" />
                </template>
                <template v-else>
                  <v-btn size="small" variant="text" class="text-none" :disabled="saving" @click="cancelEdit">
                    {{ t('documentIntelligence.cancel') }}
                  </v-btn>
                  <v-btn size="small" variant="text" class="text-none" :loading="saving" prepend-icon="mdi-file-document-edit-outline" @click="saveEdit(true)">
                    {{ t('documentIntelligence.saveAsDraft') }}
                  </v-btn>
                  <v-btn size="small" color="primary" variant="flat" class="text-none" :loading="saving" prepend-icon="mdi-content-save" @click="saveEdit(false)">
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

              <v-progress-linear v-if="docLoading" indeterminate color="primary" class="mb-2" />

              <DiMarkdownEditor v-if="docMode === 'edit'" v-model="editContent" />
              <DiMarkdownViewer v-else :content="docContent" :empty-label="t('documentIntelligence.emptyDoc')" />

              <DiLinkedWorkItemsPanel
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

                  <div v-if="!children.length && !childrenLoading" class="text-center py-12">
                    <v-icon icon="mdi-folder-open-outline" size="56" class="text-medium-emphasis mb-2" />
                    <div class="text-body-1 text-medium-emphasis">{{ t('documentIntelligence.emptyFolder') }}</div>
                  </div>

                  <template v-else-if="children.length">
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

                <!-- Dokümanlar / dosyalar -->
                <div v-if="docChildren.length">
                  <div class="text-caption text-medium-emphasis mb-2">{{ t('documentIntelligence.documents') }}</div>
                  <v-list class="py-0">
                    <v-list-item
                      v-for="d in docChildren"
                      :key="d.id"
                      rounded="lg"
                      class="mb-1 border di-doc-row"
                      @click="openResource(d)"
                    >
                      <template #prepend>
                        <v-icon :icon="resourceIcon(d)" color="primary" />
                      </template>
                      <v-list-item-title>
                        {{ resourceLabel(d) }}
                        <v-chip v-if="d.type === 'markdown' && d.status === 'draft'" size="x-small" variant="flat" color="warning" class="ml-1">
                          {{ t('documentIntelligence.draft') }}
                        </v-chip>
                      </v-list-item-title>
                      <v-list-item-subtitle v-if="d.description || (d.type === 'file' && d.size)">
                        <span v-if="d.description">{{ d.description }}</span>
                        <span v-if="d.type === 'file' && d.size" class="text-medium-emphasis">
                          {{ d.description ? ' · ' : '' }}{{ formatSize(d.size) }}
                        </span>
                      </v-list-item-subtitle>
                      <template #append>
                        <v-btn
                          v-if="d.type === 'file' && d.permissions.canDownload && isDiDocxEditable(d)"
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
                            <v-list-item v-if="d.type === 'file' && d.permissions.canDownload && isDiDocxEditable(d)" prepend-icon="mdi-file-document-edit-outline" :title="t('documentIntelligence.openInEditor')" @click="openFileEditor(d)" />
                            <v-list-item v-if="d.type === 'file' && d.permissions.canDownload && isDiPreviewable(d)" prepend-icon="mdi-file-eye-outline" :title="t('documentIntelligence.preview')" @click="openFilePreview(d)" />
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

    <!-- Yeni doküman -->
    <v-dialog v-model="docDialog" max-width="420">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.newDocument') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="docTitle"
            :label="t('documentIntelligence.docTitle')"
            variant="outlined"
            density="comfortable"
            autofocus
            hide-details
            @keydown.enter="submitDoc(false)"
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
          <v-list density="compact" class="di-move-list border rounded-lg">
            <v-list-item
              :active="moveDestId === null"
              prepend-icon="mdi-folder-home-outline"
              :title="t('documentIntelligence.allDocuments')"
              @click="moveDestId = null"
            />
            <v-list-item
              v-for="f in flatFolders"
              :key="f.id"
              :active="moveDestId === f.id"
              :title="f.name"
              @click="moveDestId = f.id"
            >
              <template #prepend>
                <span :style="{ display: 'inline-block', width: f.depth * 14 + 'px' }" />
                <v-icon size="18" class="mr-2">mdi-folder-outline</v-icon>
              </template>
            </v-list-item>
          </v-list>
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

    <v-dialog v-model="historyDialog" max-width="980" scrollable>
      <v-card rounded="lg">
        <v-card-title class="d-flex align-center text-subtitle-1 font-weight-bold">
          <v-icon size="20" class="mr-2">mdi-history</v-icon>
          {{ t('documentIntelligence.versionHistory') }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="historyDialog = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-0">
          <div class="d-flex di-history">
            <div class="di-history-list">
              <div v-if="versionsLoading" class="d-flex justify-center pa-6">
                <v-progress-circular indeterminate size="28" color="primary" />
              </div>
              <div v-else-if="!versions.length" class="text-medium-emphasis text-body-2 pa-4 text-center">
                {{ t('documentIntelligence.noVersions') }}
              </div>
              <v-list v-else density="compact" nav>
                <v-list-item
                  v-for="v in versions"
                  :key="v.versionNumber"
                  :active="selectedVersion === v.versionNumber"
                  rounded="lg"
                  @click="previewVersion(v)"
                >
                  <template #prepend>
                    <v-avatar size="28" :color="v.isCurrent ? 'primary' : 'grey-lighten-1'" class="text-caption">
                      v{{ v.versionNumber }}
                    </v-avatar>
                  </template>
                  <v-list-item-title class="text-body-2">
                    {{ formatDateTime(v.createdAt) || ('v' + v.versionNumber) }}
                    <v-chip v-if="v.isCurrent" size="x-small" color="primary" variant="tonal" class="ml-1">
                      {{ t('documentIntelligence.currentVersion') }}
                    </v-chip>
                  </v-list-item-title>
                  <v-list-item-subtitle class="text-caption">
                    <span v-if="v.createdBy">{{ v.createdBy }}</span>
                    <span v-if="v.changeNote"> · {{ v.changeNote }}</span>
                  </v-list-item-subtitle>
                  <template #append>
                    <v-btn
                      v-if="!v.isCurrent"
                      size="x-small"
                      variant="tonal"
                      color="primary"
                      class="text-none"
                      :loading="restoringVersion === v.versionNumber"
                      @click.stop="restoreVersion(v)"
                    >
                      {{ t('documentIntelligence.restore') }}
                    </v-btn>
                  </template>
                </v-list-item>
              </v-list>
            </div>
            <v-divider vertical />
            <div class="di-history-preview pa-4">
              <div v-if="versionContentLoading" class="d-flex justify-center pa-6">
                <v-progress-circular indeterminate size="28" color="primary" />
              </div>
              <div v-else-if="selectedVersion === null" class="text-medium-emphasis text-body-2 d-flex align-center justify-center fill-height">
                {{ t('documentIntelligence.selectVersionHint') }}
              </div>
              <DiMarkdownViewer v-else :content="versionContent" :empty-label="t('documentIntelligence.emptyDoc')" />
            </div>
          </div>
        </v-card-text>
      </v-card>
    </v-dialog>

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

    <v-snackbar v-model="snackbar" :color="snackbarColor" location="top right" :timeout="3500">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<style scoped>
.di-layout {
  min-height: 600px;
}
.di-history {
  height: 65vh;
}
.di-history-list {
  width: 340px;
  flex-shrink: 0;
  overflow: auto;
}
.di-history-preview {
  flex: 1 1 auto;
  overflow: auto;
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
