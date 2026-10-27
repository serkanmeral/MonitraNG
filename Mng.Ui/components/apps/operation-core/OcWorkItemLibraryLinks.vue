<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiMarkdownPreviewDialog from '@/components/apps/document-intelligence/DiMarkdownPreviewDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  diCreateFileResource,
  diDownloadResource,
  diGetById,
  diGetChildren,
} from '@/services/documentIntelligenceService';
import { ocUpdateWorkItem } from '@/services/operationCoreService';
import { DI_RESOURCE_TYPE, type DiResource } from '@/types/apps/documentIntelligence';
import { resolveOcWorkspaceLibrary } from '@/utils/ocWorkspaceLibrary';

const props = defineProps<{
  workItemId: string;
  workspaceId: string;
  documentIds: string[];
  canEdit?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
}>();

const MAX_FILE_MB = 20;

interface LinkRow {
  id: string;
  name: string;
  broken: boolean;
  resource: DiResource | null;
}

interface Crumb {
  id: string;
  name: string;
}

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const rows = ref<LinkRow[]>([]);
const loading = ref(false);
const saving = ref(false);
const errorLocal = ref<string | null>(null);
const rootId = ref<string | null>(null);
const rootMissing = ref(false);
const pickerOpen = ref(false);
const crumbs = ref<Crumb[]>([]);
const children = ref<DiResource[]>([]);
const childrenLoading = ref(false);
const selectedId = ref<string | null>(null);
const uploadInput = ref<HTMLInputElement | null>(null);
const pickerUploadInput = ref<HTMLInputElement | null>(null);

const filePreviewOpen = ref(false);
const filePreviewResource = ref<DiResource | null>(null);
const markdownPreviewOpen = ref(false);
const markdownPreviewResource = ref<DiResource | null>(null);

const currentFolderId = computed(() => crumbs.value[crumbs.value.length - 1]?.id ?? rootId.value);
const folders = computed(() => children.value.filter((item) => item.type === DI_RESOURCE_TYPE.folder));
const documents = computed(() =>
  children.value.filter((item) => item.type === DI_RESOURCE_TYPE.file || item.type === DI_RESOURCE_TYPE.markdown)
);

function resourceName(resource: DiResource): string {
  return (resource.title || resource.name || resource.id).trim();
}

async function loadRoot() {
  const workspaceId = props.workspaceId.trim();
  rootId.value = null;
  rootMissing.value = false;
  if (!workspaceId) {
    rootMissing.value = true;
    return;
  }
  try {
    const folderId = await resolveOcWorkspaceLibrary(workspaceId);
    rootId.value = folderId;
    rootMissing.value = !folderId;
  } catch {
    rootMissing.value = true;
  }
}

async function loadRows() {
  const ids = props.documentIds.map((id) => id.trim()).filter(Boolean);
  if (!ids.length) {
    rows.value = [];
    return;
  }
  loading.value = true;
  try {
    rows.value = await Promise.all(
      ids.map(async (id) => {
        try {
          const resource = await diGetById(id);
          return { id, name: resourceName(resource), broken: false, resource };
        } catch {
          return { id, name: id, broken: true, resource: null };
        }
      })
    );
  } finally {
    loading.value = false;
  }
}

async function saveIds(ids: string[]) {
  const workItemId = props.workItemId.trim();
  if (!workItemId) return;
  saving.value = true;
  errorLocal.value = null;
  try {
    await ocUpdateWorkItem(workItemId, { fields: { libraryDocumentIds: ids } });
    emit('changed');
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.libraryLinks.saveError');
  } finally {
    saving.value = false;
  }
}

async function unlink(id: string) {
  const next = props.documentIds.filter((item) => item.trim().toLowerCase() !== id.trim().toLowerCase());
  await saveIds(next);
}

async function linkId(id: string) {
  const trimmed = id.trim();
  if (!trimmed) return;
  const exists = props.documentIds.some((item) => item.trim().toLowerCase() === trimmed.toLowerCase());
  if (exists) {
    pickerOpen.value = false;
    return;
  }
  await saveIds([...props.documentIds, trimmed]);
  pickerOpen.value = false;
}

async function loadFolder(folderId: string) {
  childrenLoading.value = true;
  errorLocal.value = null;
  selectedId.value = null;
  try {
    const page = await diGetChildren(folderId, { limit: 200 });
    children.value = page.items;
  } catch (e: unknown) {
    children.value = [];
    errorLocal.value = panelError(e, 'operationCore.profile.libraryLinks.saveError');
  } finally {
    childrenLoading.value = false;
  }
}

async function openPicker() {
  errorLocal.value = null;
  if (!rootId.value) {
    await loadRoot();
  }
  if (!rootId.value) {
    rootMissing.value = true;
    return;
  }
  crumbs.value = [{ id: rootId.value, name: t('operationCore.profile.libraryLinks.root') }];
  pickerOpen.value = true;
  await loadFolder(rootId.value);
}

async function enterFolder(folder: DiResource) {
  crumbs.value = [...crumbs.value, { id: folder.id, name: resourceName(folder) }];
  await loadFolder(folder.id);
}

async function goToCrumb(index: number) {
  const crumb = crumbs.value[index];
  if (!crumb) return;
  crumbs.value = crumbs.value.slice(0, index + 1);
  await loadFolder(crumb.id);
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
  const index = name.lastIndexOf('.');
  return index >= 0 ? name.slice(index + 1).toLowerCase() : '';
}

async function uploadInto(folderId: string, file: File) {
  if (file.size > MAX_FILE_MB * 1024 * 1024) {
    errorLocal.value = t('documentIntelligence.errors.fileTooLarge', { max: MAX_FILE_MB });
    return;
  }
  saving.value = true;
  errorLocal.value = null;
  try {
    const created = await diCreateFileResource({
      parentId: folderId,
      name: file.name,
      originalFileName: file.name,
      content: await fileToBase64(file),
      mimeType: file.type || null,
      extension: fileExtension(file.name) || null,
      size: file.size,
    });
    await linkId(created.id);
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.attachments.uploadError');
  } finally {
    saving.value = false;
  }
}

async function onRootUpload(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file) return;
  if (!rootId.value) await loadRoot();
  if (!rootId.value) {
    rootMissing.value = true;
    return;
  }
  await uploadInto(rootId.value, file);
}

async function onPickerUpload(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  const folderId = currentFolderId.value;
  if (!file || !folderId) return;
  await uploadInto(folderId, file);
  if (folderId) await loadFolder(folderId);
}

function openRow(row: LinkRow) {
  if (!row.resource || row.broken) return;
  if (row.resource.type === DI_RESOURCE_TYPE.markdown) {
    markdownPreviewResource.value = row.resource;
    markdownPreviewOpen.value = true;
    return;
  }
  if (row.resource.type === DI_RESOURCE_TYPE.file) {
    filePreviewResource.value = row.resource;
    filePreviewOpen.value = true;
  }
}

async function downloadResource(resource: DiResource) {
  const { blob, fileName } = await diDownloadResource(resource.id, resource.fileName || resource.name);
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName || resource.fileName || resource.name || 'dosya';
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

watch(
  () => [props.workspaceId, props.documentIds.join('|')] as const,
  () => {
    void loadRoot();
    void loadRows();
  },
  { immediate: true }
);
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-3">
      <div>
        <div class="text-subtitle-2">{{ t('operationCore.profile.libraryLinks.title') }}</div>
        <div class="text-body-2 text-medium-emphasis">{{ t('operationCore.profile.libraryLinks.hint') }}</div>
      </div>
      <div v-if="canEdit" class="d-flex ga-2">
        <input ref="uploadInput" type="file" class="d-none" @change="onRootUpload" />
        <v-btn
          size="small"
          variant="tonal"
          rounded="lg"
          class="text-none"
          prepend-icon="mdi-file-plus-outline"
          :disabled="rootMissing || saving"
          :loading="saving"
          @click="uploadInput?.click()"
        >
          {{ t('operationCore.profile.libraryLinks.upload') }}
        </v-btn>
        <v-btn
          size="small"
          color="primary"
          variant="flat"
          rounded="lg"
          class="text-none"
          prepend-icon="mdi-link-plus"
          :disabled="rootMissing || saving"
          @click="openPicker"
        >
          {{ t('operationCore.profile.libraryLinks.pick') }}
        </v-btn>
      </div>
    </div>

    <v-alert v-if="rootMissing" type="warning" variant="tonal" density="compact" class="mb-3 rounded-lg">
      {{ t('operationCore.profile.libraryLinks.missingRoot') }}
    </v-alert>
    <v-alert v-if="errorLocal" type="error" variant="tonal" density="compact" class="mb-3 rounded-lg" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

    <div v-if="!loading && !rows.length" class="text-body-2 text-medium-emphasis text-center py-4">
      {{ t('operationCore.profile.libraryLinks.empty') }}
    </div>
    <v-list v-else-if="rows.length" class="py-0" density="comfortable">
      <v-list-item v-for="row in rows" :key="row.id" class="px-2 rounded-lg">
        <template #prepend>
          <v-icon :icon="row.broken ? 'mdi-link-variant-off' : 'mdi-file-document-outline'" :color="row.broken ? 'warning' : 'primary'" />
        </template>
        <v-list-item-title class="text-body-2 font-weight-medium text-truncate">
          {{ row.broken ? row.id : row.name }}
        </v-list-item-title>
        <v-list-item-subtitle v-if="row.broken" class="text-caption">
          {{ t('operationCore.profile.libraryLinks.broken') }}
        </v-list-item-subtitle>
        <template #append>
          <v-btn
            v-if="!row.broken"
            icon="mdi-eye-outline"
            variant="text"
            size="small"
            :title="t('operationCore.profile.libraryLinks.open')"
            @click="openRow(row)"
          />
          <v-btn
            v-if="canEdit"
            icon="mdi-link-off"
            variant="text"
            size="small"
            color="error"
            :loading="saving"
            :title="t('operationCore.profile.libraryLinks.unlink')"
            @click="unlink(row.id)"
          />
        </template>
      </v-list-item>
    </v-list>

    <v-dialog v-model="pickerOpen" max-width="720">
      <v-card class="rounded-lg">
        <v-card-title class="text-subtitle-1">{{ t('operationCore.profile.libraryLinks.pickerTitle') }}</v-card-title>
        <v-card-text>
          <p class="text-body-2 text-medium-emphasis mb-3">{{ t('operationCore.profile.libraryLinks.pickerHint') }}</p>
          <div class="d-flex align-center flex-wrap ga-1 mb-3">
            <v-btn
              v-for="(crumb, index) in crumbs"
              :key="crumb.id"
              size="small"
              variant="text"
              class="text-none px-1"
              @click="goToCrumb(index)"
            >
              {{ crumb.name }}
            </v-btn>
            <v-spacer />
            <input ref="pickerUploadInput" type="file" class="d-none" @change="onPickerUpload" />
            <v-btn
              size="small"
              variant="tonal"
              class="text-none"
              prepend-icon="mdi-upload"
              :loading="saving"
              @click="pickerUploadInput?.click()"
            >
              {{ t('operationCore.profile.libraryLinks.upload') }}
            </v-btn>
          </div>
          <v-progress-linear v-if="childrenLoading" indeterminate color="primary" class="mb-2" />
          <div v-else-if="!folders.length && !documents.length" class="text-body-2 text-medium-emphasis py-4 text-center">
            {{ t('operationCore.profile.libraryLinks.folderEmpty') }}
          </div>
          <v-list v-else density="comfortable" class="py-0">
            <v-list-item v-for="folder in folders" :key="folder.id" prepend-icon="mdi-folder-outline" @click="enterFolder(folder)">
              <v-list-item-title>{{ resourceName(folder) }}</v-list-item-title>
            </v-list-item>
            <v-list-item
              v-for="doc in documents"
              :key="doc.id"
              :active="selectedId === doc.id"
              prepend-icon="mdi-file-document-outline"
              @click="selectedId = doc.id"
            >
              <v-list-item-title>{{ resourceName(doc) }}</v-list-item-title>
            </v-list-item>
          </v-list>
        </v-card-text>
        <v-card-actions class="px-4 pb-4">
          <v-btn variant="text" class="text-none" @click="pickerOpen = false">
            {{ t('operationCore.profile.libraryLinks.cancel') }}
          </v-btn>
          <v-spacer />
          <v-btn color="primary" class="text-none" :disabled="!selectedId || saving" :loading="saving" @click="selectedId && linkId(selectedId)">
            {{ t('operationCore.profile.libraryLinks.use') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <DiFilePreviewDialog v-model="filePreviewOpen" :resource="filePreviewResource" @download="downloadResource" />
    <DiMarkdownPreviewDialog v-model="markdownPreviewOpen" :resource="markdownPreviewResource" />
  </div>
</template>
