<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DiMarkdownViewer from '@/components/apps/document-intelligence/DiMarkdownViewer.vue';
import DiMarkdownEditor from '@/components/apps/document-intelligence/DiMarkdownEditor.vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiResourceEditorDialog from '@/components/apps/document-intelligence/DiResourceEditorDialog.vue';
import DiTagPicker from '@/components/apps/document-intelligence/DiTagPicker.vue';
import DiLinkedWorkItemsPanel from '@/components/apps/document-intelligence/DiLinkedWorkItemsPanel.vue';
import DiBacklinksPanel from '@/components/apps/document-intelligence/DiBacklinksPanel.vue';
import DiSavePageDialog from '@/components/apps/document-intelligence/DiSavePageDialog.vue';
import type { DiSavePageMode } from '@/components/apps/document-intelligence/DiSavePageDialog.vue';
import DiMarkdownVersionHistoryDialog from '@/components/apps/document-intelligence/DiMarkdownVersionHistoryDialog.vue';
import DiFileVersionHistoryDialog from '@/components/apps/document-intelligence/DiFileVersionHistoryDialog.vue';
import DiResourcePreviewProvider from '@/components/apps/document-intelligence/DiResourcePreviewProvider.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAppToast } from '@/composables/useAppToast';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { isDiDocxEditable, isDiManagedDocument, isDiPreviewable } from '@/utils/diFilePreview';
import { DI_HOME_PATH, buildDiFolderUrl } from '@/utils/diResourceLink';
import { diPageResourceIcon, diPageResourceLabel } from '@/utils/diPageResource';
import {
  diErrorStatus,
  diFetchFileBlob,
  diGetBreadcrumb,
  diGetById,
  diGetMarkdownContent,
  diUpdateMarkdown,
} from '@/services/documentIntelligenceService';
import type { DiBreadcrumb, DiResource } from '@/types/apps/documentIntelligence';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const { push } = useAppToast();
const panelError = usePanelErrorNotify('errors.dg.generic');
const route = useRoute();

const resourceId = computed(() => String(route.params.id ?? '').trim());

const loading = ref(true);
const errorMessage = ref('');
const resource = ref<DiResource | null>(null);
const folderPath = ref<DiBreadcrumb[]>([]);
const downloadedFallback = ref(false);

const editorOpen = ref(false);
const previewOpen = ref(false);

const markdownContent = ref('');
const markdownLoading = ref(false);
const showMarkdown = ref(false);
const showDocxDetail = ref(false);
const docVersion = ref(0);
const docMode = ref<'view' | 'edit'>('view');
const editContent = ref('');
const editTags = ref<string[]>([]);
const saving = ref(false);

const saveDialog = ref(false);
const saveDialogMode = ref<DiSavePageMode>('save');
const pendingSaveAsDraft = ref(false);
const historyDialog = ref(false);
const fileHistoryDialog = ref(false);

function resourceLabel(r: DiResource | null): string {
  if (!r) return '…';
  return diPageResourceLabel(r);
}

function formatSize(bytes: number | null | undefined): string {
  if (bytes == null || !Number.isFinite(bytes) || bytes <= 0) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '';
  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

const breadcrumbs = computed(() => [
  { title: t('documentIntelligence.menuTitle'), to: DI_HOME_PATH },
  ...folderPath.value.map((b) => ({
    title: b.name,
    to: buildDiFolderUrl(b.id),
  })),
  { title: resourceLabel(resource.value), disabled: true },
]);

function notify(text: string, color: 'success' | 'error' | 'info' = 'info') {
  push({
    title: color === 'success' ? t('documentIntelligence.notify.successTitle') : t('errors.dg.toastTitle'),
    message: text,
    severity: color,
  });
}

async function goToParentFolder() {
  await navigateTo(buildDiFolderUrl(resource.value?.parentId ?? null));
}

async function downloadFile(r: DiResource) {
  if (!r.filePath) {
    errorMessage.value = t('documentIntelligence.errors.download');
    return;
  }
  try {
    const blob = await diFetchFileBlob(r.filePath);
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = r.fileName || r.name || 'dosya';
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
    downloadedFallback.value = true;
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'documentIntelligence.errors.download');
  }
}

async function loadMarkdownContent(r: DiResource) {
  showMarkdown.value = true;
  docMode.value = 'view';
  markdownLoading.value = true;
  try {
    const c = await diGetMarkdownContent(r.id);
    markdownContent.value = c.content;
    docVersion.value = c.currentVersionNumber;
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'documentIntelligence.errors.docLoad');
  } finally {
    markdownLoading.value = false;
  }
}

async function openLoadedResource(r: DiResource) {
  downloadedFallback.value = false;

  if (r.type === 'folder') {
    await navigateTo(buildDiFolderUrl(r.id), { replace: true });
    return;
  }

  if (r.type === 'markdown') {
    await loadMarkdownContent(r);
    return;
  }

  if (isDiDocxEditable(r)) {
    showDocxDetail.value = true;
    docVersion.value = r.currentVersionNumber ?? 0;
    const editQuery = route.query.edit === '1' || route.query.edit === 'true';
    if (editQuery) {
      editorOpen.value = true;
    }
    return;
  }

  if (isDiPreviewable(r)) {
    previewOpen.value = true;
    return;
  }

  await downloadFile(r);
}

async function loadResource() {
  const id = resourceId.value;
  if (!id) {
    await navigateTo(DI_HOME_PATH, { replace: true });
    return;
  }

  loading.value = true;
  errorMessage.value = '';
  showMarkdown.value = false;
  showDocxDetail.value = false;
  editorOpen.value = false;
  previewOpen.value = false;
  downloadedFallback.value = false;
  resource.value = null;
  markdownContent.value = '';
  docMode.value = 'view';

  try {
    const r = await diGetById(id);
    resource.value = r;
    folderPath.value = r.parentId ? await diGetBreadcrumb(r.parentId) : [];
    await openLoadedResource(r);
  } catch (e: unknown) {
    if (diErrorStatus(e) === 404) {
      errorMessage.value = t('documentIntelligence.errors.resourceNotFound');
    } else {
      errorMessage.value = panelError(e, 'documentIntelligence.errors.docLoad');
    }
  } finally {
    loading.value = false;
  }
}

function startEdit() {
  editContent.value = markdownContent.value;
  editTags.value = [...(resource.value?.tags ?? [])];
  docMode.value = 'edit';
}

function cancelEdit() {
  docMode.value = 'view';
}

function openSaveDialog(asDraft: boolean) {
  if (asDraft) {
    saveDialogMode.value = 'draft';
  } else if (resource.value?.status === 'draft') {
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
  if (!resource.value) return false;
  saving.value = true;
  try {
    const updated = await diUpdateMarkdown(resource.value.id, {
      content: editContent.value,
      tags: editTags.value,
      expectedVersionNumber: docVersion.value,
      isDraft: asDraft,
      changeNote: changeNote || null,
    });
    markdownContent.value = editContent.value;
    docVersion.value = updated.currentVersionNumber || docVersion.value + 1;
    resource.value = updated;
    docMode.value = 'view';
    notify(asDraft ? t('documentIntelligence.draftSaved') : t('documentIntelligence.published'), 'success');
    return true;
  } catch (e: unknown) {
    if (diErrorStatus(e) === 409) {
      notify(t('documentIntelligence.errors.conflict'), 'error');
      await loadMarkdownContent(resource.value);
      resource.value = await diGetById(resource.value.id);
    } else {
      notify(panelError(e, 'documentIntelligence.errors.save'), 'error');
    }
    return false;
  } finally {
    saving.value = false;
  }
}

async function onVersionRestored(restored: DiResource) {
  resource.value = restored;
  await loadMarkdownContent(restored);
}

async function onFileVersionRestored(restored: DiResource) {
  resource.value = restored;
  docVersion.value = restored.currentVersionNumber ?? docVersion.value;
}

async function refreshDocxResource() {
  if (!resource.value) return;
  try {
    resource.value = await diGetById(resource.value.id);
    docVersion.value = resource.value.currentVersionNumber ?? docVersion.value;
  } catch {
    // non-fatal
  }
}

function onEditorOpenChange(open: boolean) {
  editorOpen.value = open;
  if (!open && showDocxDetail.value) {
    void refreshDocxResource();
    return;
  }
  if (!open) void goToParentFolder();
}

function onEditorSaved(updated: DiResource) {
  resource.value = updated;
  docVersion.value = updated.currentVersionNumber ?? docVersion.value;
}

function onPreviewOpenChange(open: boolean) {
  previewOpen.value = open;
  if (!open) void goToParentFolder();
}

async function copyShareLink() {
  try {
    await navigator.clipboard.writeText(window.location.href);
    push({
      title: t('documentIntelligence.notify.successTitle'),
      message: t('documentIntelligence.linkCopied'),
      severity: 'success',
    });
  } catch {
    push({
      title: t('errors.dg.toastTitle'),
      message: t('documentIntelligence.linkCopyFailed'),
      severity: 'error',
    });
  }
}

onMounted(() => {
  void loadResource();
});

watch(resourceId, () => {
  void loadResource();
});
</script>

<template>
  <DiResourcePreviewProvider :on-download="downloadFile">
  <div>
    <BaseBreadcrumb :title="resourceLabel(resource)" :breadcrumbs="breadcrumbs" />

    <v-card elevation="10" rounded="lg" class="pa-4">
      <div class="d-flex align-center flex-wrap ga-2 mb-4">
        <v-btn
          variant="text"
          size="small"
          class="text-none"
          prepend-icon="mdi-arrow-left"
          @click="goToParentFolder"
        >
          {{ t('documentIntelligence.backToFolder') }}
        </v-btn>
        <v-spacer />
        <v-btn
          v-if="resource && !loading && !errorMessage"
          variant="tonal"
          size="small"
          class="text-none"
          prepend-icon="mdi-link-variant"
          @click="copyShareLink"
        >
          {{ t('documentIntelligence.copyLink') }}
        </v-btn>
        <v-btn variant="tonal" size="small" class="text-none" :to="DI_HOME_PATH">
          {{ t('documentIntelligence.menuTitle') }}
        </v-btn>
      </div>

      <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

      <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-4">
        {{ errorMessage }}
        <div class="mt-3">
          <v-btn variant="flat" color="primary" size="small" class="text-none" :to="DI_HOME_PATH">
            {{ t('documentIntelligence.menuTitle') }}
          </v-btn>
        </div>
      </v-alert>

      <template v-else-if="showMarkdown && resource">
        <div class="d-flex align-center flex-wrap ga-2 mb-3">
          <v-icon :icon="diPageResourceIcon(resource)" color="primary" class="mr-1" />
          <h3 class="text-h5 font-weight-bold mr-2">{{ resourceLabel(resource) }}</h3>
          <v-chip size="x-small" variant="tonal">v{{ docVersion }}</v-chip>
          <v-chip
            v-if="resource.status === 'draft'"
            size="x-small"
            variant="flat"
            color="warning"
            prepend-icon="mdi-file-document-edit-outline"
          >
            {{ t('documentIntelligence.draft') }}
          </v-chip>
          <v-spacer />
          <template v-if="docMode === 'view'">
            <v-btn
              v-if="resource.permissions.canEdit"
              size="small"
              variant="tonal"
              class="text-none"
              prepend-icon="mdi-pencil"
              @click="startEdit"
            >
              {{ t('documentIntelligence.edit') }}
            </v-btn>
            <v-btn
              size="small"
              variant="text"
              class="text-none"
              prepend-icon="mdi-history"
              @click="historyDialog = true"
            >
              {{ t('documentIntelligence.history') }}
            </v-btn>
          </template>
          <template v-else>
            <v-btn size="small" variant="text" class="text-none" :disabled="saving" @click="cancelEdit">
              {{ t('documentIntelligence.cancel') }}
            </v-btn>
            <v-btn
              size="small"
              variant="text"
              class="text-none"
              :loading="saving"
              prepend-icon="mdi-file-document-edit-outline"
              @click="openSaveDialog(true)"
            >
              {{ t('documentIntelligence.saveAsDraft') }}
            </v-btn>
            <v-btn
              size="small"
              color="primary"
              variant="flat"
              class="text-none"
              :loading="saving"
              prepend-icon="mdi-content-save"
              @click="openSaveDialog(false)"
            >
              {{ resource.status === 'draft' ? t('documentIntelligence.publish') : t('documentIntelligence.save') }}
            </v-btn>
          </template>
        </div>

        <v-alert
          v-if="!resource.permissions.canEdit"
          type="info"
          variant="tonal"
          density="compact"
          class="mb-3 rounded-lg"
        >
          {{ t('documentIntelligence.viewOnlyHint') }}
        </v-alert>

        <div v-if="docMode === 'view'" class="mb-3">
          <DiTagPicker :model-value="resource.tags ?? []" readonly density="compact" />
        </div>
        <div v-else class="mb-3">
          <DiTagPicker v-model="editTags" density="compact" />
        </div>

        <v-progress-linear v-if="markdownLoading" indeterminate color="primary" class="mb-2" />
        <DiMarkdownEditor
          v-else-if="docMode === 'edit'"
          v-model="editContent"
          :current-resource-id="resource.id"
          :upload-parent-id="resource.parentId"
          :can-upload="resource.permissions.canUpload"
        />
        <DiMarkdownViewer
          v-else
          :content="markdownContent"
          :empty-label="t('documentIntelligence.emptyPage')"
        />
        <DiLinkedWorkItemsPanel
          v-if="docMode !== 'edit'"
          :resource-id="resource.id"
          class="mt-4"
        />
        <DiBacklinksPanel
          v-if="docMode !== 'edit'"
          :resource-id="resource.id"
          class="mt-4"
        />
      </template>

      <template v-else-if="showDocxDetail && resource">
        <div class="d-flex align-center flex-wrap ga-2 mb-3">
          <v-icon :icon="diPageResourceIcon(resource)" color="primary" class="mr-1" />
          <h3 class="text-h5 font-weight-bold mr-2">{{ resourceLabel(resource) }}</h3>
          <v-chip v-if="docVersion > 0" size="x-small" variant="tonal">v{{ docVersion }}</v-chip>
          <v-chip
            v-if="isDiManagedDocument(resource) && resource.documentNo"
            size="x-small"
            variant="outlined"
            prepend-icon="mdi-pound"
          >
            {{ resource.documentNo }}
          </v-chip>
          <v-spacer />
          <v-btn
            v-if="resource.permissions.canEdit"
            size="small"
            variant="tonal"
            color="primary"
            class="text-none"
            prepend-icon="mdi-pencil"
            @click="editorOpen = true"
          >
            {{ t('documentIntelligence.edit') }}
          </v-btn>
          <v-btn
            size="small"
            variant="text"
            class="text-none"
            prepend-icon="mdi-history"
            @click="fileHistoryDialog = true"
          >
            {{ t('documentIntelligence.history') }}
          </v-btn>
          <v-btn
            v-if="resource.permissions.canDownload"
            size="small"
            variant="text"
            class="text-none"
            prepend-icon="mdi-download"
            @click="downloadFile(resource)"
          >
            {{ t('documentIntelligence.download') }}
          </v-btn>
        </div>

        <v-alert
          v-if="!resource.permissions.canEdit"
          type="info"
          variant="tonal"
          density="compact"
          class="mb-3 rounded-lg"
        >
          {{ t('documentIntelligence.viewOnlyHint') }}
        </v-alert>

        <v-list density="compact" class="bg-transparent mb-3 rounded-lg border pa-2">
          <v-list-item v-if="resource.description">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('documentIntelligence.resourceInfoDescription') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ resource.description }}</v-list-item-subtitle>
          </v-list-item>
          <v-list-item v-if="formatSize(resource.size)">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('documentIntelligence.resourceInfoSize') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ formatSize(resource.size) }}</v-list-item-subtitle>
          </v-list-item>
          <v-list-item v-if="resource.updatedBy || resource.updatedAt">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('documentIntelligence.metaUpdated') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">
              {{ [resource.updatedBy, resource.updatedAt ? formatDateTime(resource.updatedAt) : null].filter(Boolean).join(' · ') }}
            </v-list-item-subtitle>
          </v-list-item>
        </v-list>

        <div class="mb-3">
          <DiTagPicker :model-value="resource.tags ?? []" readonly density="compact" />
        </div>

        <DiLinkedWorkItemsPanel :resource-id="resource.id" class="mt-2" />
      </template>

      <v-alert
        v-else-if="downloadedFallback && resource"
        type="info"
        variant="tonal"
      >
        {{ t('documentIntelligence.previewUnavailable') }}
        <div class="mt-2">
          <v-btn
            variant="flat"
            color="primary"
            size="small"
            class="text-none"
            prepend-icon="mdi-download"
            @click="downloadFile(resource)"
          >
            {{ t('documentIntelligence.download') }}
          </v-btn>
        </div>
      </v-alert>
    </v-card>

    <DiResourceEditorDialog
      :model-value="editorOpen"
      :resource="resource"
      @update:model-value="onEditorOpenChange"
      @saved="onEditorSaved"
    />

    <DiFilePreviewDialog
      :model-value="previewOpen"
      :resource="resource"
      @update:model-value="onPreviewOpenChange"
    />

    <DiSavePageDialog
      v-model="saveDialog"
      :mode="saveDialogMode"
      :loading="saving"
      @confirm="confirmSaveEdit"
    />

    <DiMarkdownVersionHistoryDialog
      v-model="historyDialog"
      :resource-id="resource?.id ?? null"
      :can-restore="resource?.permissions.canEdit ?? false"
      @restored="onVersionRestored"
    />

    <DiFileVersionHistoryDialog
      v-model="fileHistoryDialog"
      :resource="resource"
      :can-restore="resource?.permissions.canEdit ?? false"
      @restored="onFileVersionRestored"
    />

  </div>
  </DiResourcePreviewProvider>
</template>
