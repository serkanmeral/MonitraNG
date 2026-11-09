<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiMarkdownPreviewDialog from '@/components/apps/document-intelligence/DiMarkdownPreviewDialog.vue';
import DiResourceEditorDialog from '@/components/apps/document-intelligence/DiResourceEditorDialog.vue';
import OcLinkDocumentDialog from '@/components/apps/operation-core/OcLinkDocumentDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  diDeleteResourceLink,
  diDownloadResource,
  diGetById,
  diGetLinkedResourcesForDatasetRow,
  diGetLinkedResourcesForWorkItem,
} from '@/services/documentIntelligenceService';
import {
  DI_RESOURCE_TYPE,
  type DiLinkedResource,
  type DiResource,
} from '@/types/apps/documentIntelligence';
import { isDiOfficeEditable } from '@/utils/diFilePreview';
import { findWorkItemIdByKey } from '@/utils/workItemDocumentLink';

const props = withDefaults(
  defineProps<{
    datasetName: string;
    dataId: string;
    canEdit?: boolean;
    hint?: string;
    relationTypes?: string[];
    /** İş kalemi anahtarı (GIR-0001, ZIM-0001). O tura bağlı belgeler de listelenir. */
    alsoWorkItemKey?: string;
    dense?: boolean;
    rootFolderId?: string | null;
    allowUpload?: boolean;
    allowCreateFolder?: boolean;
    /** İlk belge isteğinde kişi klasörünü açar ve kök olarak döner. */
    prepareRoot?: (() => Promise<string | null>) | null;
  }>(),
  {
    canEdit: false,
    hint: '',
    relationTypes: () => ['reference', 'attachment'],
    alsoWorkItemKey: '',
    dense: false,
    rootFolderId: null,
    allowUpload: true,
    allowCreateFolder: true,
    prepareRoot: null,
  }
);

const resolvedRootId = ref<string | null>(null);
const openingLinker = ref(false);

const dialogRootId = computed(() => resolvedRootId.value || props.rootFolderId);

async function openLinker() {
  if (!props.canEdit || openingLinker.value) return;
  openingLinker.value = true;
  error.value = null;
  try {
    if (props.prepareRoot) {
      const folderId = (await props.prepareRoot())?.trim();
      if (folderId) resolvedRootId.value = folderId;
    }
    linkDialogOpen.value = true;
  } catch (e: unknown) {
    error.value = panelError(e, 'operationCore.profile.documents.linkError');
  } finally {
    openingLinker.value = false;
  }
}

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const error = ref<string | null>(null);
const items = ref<DiLinkedResource[]>([]);
const linkDialogOpen = ref(false);
const deleteBusyId = ref<string | null>(null);
const previewBusyId = ref<string | null>(null);
const downloadingId = ref<string | null>(null);
const filePreviewOpen = ref(false);
const filePreviewResource = ref<DiResource | null>(null);
const editorOpen = ref(false);
const editorResource = ref<DiResource | null>(null);
const markdownPreviewOpen = ref(false);
const markdownPreviewResource = ref<DiResource | null>(null);

const excludedResourceIds = computed(() => items.value.map((item) => item.resourceId));

function resourceLabel(item: DiLinkedResource): string {
  return item.title?.trim() || item.name?.trim() || item.resourceId;
}

function relationLabel(type: string): string {
  const key = `operationCore.profile.documents.relationTypes.${type}`;
  const label = t(key);
  return label === key ? type : label;
}

function resourceIcon(item: DiLinkedResource): string {
  if (item.resourceType === DI_RESOURCE_TYPE.markdown) return 'mdi-language-markdown-outline';
  const ext = (item.extension || '').toLowerCase();
  if (ext === 'pdf' || item.mimeType?.includes('pdf')) return 'mdi-file-pdf-box';
  if (item.mimeType?.startsWith('image/')) return 'mdi-file-image-outline';
  return 'mdi-file-document-outline';
}

function canPreviewLink(item: DiLinkedResource): boolean {
  if (item.permissions && item.permissions.canView === false) return false;
  const type = item.resourceType?.trim().toLowerCase();
  return type === DI_RESOURCE_TYPE.markdown || type === DI_RESOURCE_TYPE.file;
}

function mergeItems(groups: DiLinkedResource[][]): DiLinkedResource[] {
  const seen = new Set<string>();
  const merged: DiLinkedResource[] = [];
  for (const group of groups) {
    for (const item of group) {
      const key = item.resourceId.trim().toLowerCase();
      if (!key || seen.has(key)) continue;
      seen.add(key);
      merged.push(item);
    }
  }
  return merged;
}

async function load() {
  const dataId = props.dataId.trim();
  const datasetName = props.datasetName.trim();
  if (!dataId || !datasetName) {
    items.value = [];
    return;
  }
  loading.value = true;
  error.value = null;
  let rowItems: DiLinkedResource[] = [];
  let rowError: string | null = null;
  try {
    const rowResult = await diGetLinkedResourcesForDatasetRow(datasetName, dataId);
    rowItems = rowResult.items;
  } catch (e: unknown) {
    rowError = panelError(e, 'operationCore.profile.documents.loadError');
  }
  let workItemItems: DiLinkedResource[] = [];
  const workItemKey = props.alsoWorkItemKey.trim();
  if (workItemKey) {
    try {
      const workItemId = await findWorkItemIdByKey(workItemKey);
      if (workItemId) {
        const linked = await diGetLinkedResourcesForWorkItem(workItemId);
        workItemItems = linked.items;
      }
    } catch {
      workItemItems = [];
    }
  }
  items.value = mergeItems([rowItems, workItemItems]);
  if (rowError && items.value.length === 0 && !props.dense) error.value = rowError;
  loading.value = false;
}

async function openPreview(item: DiLinkedResource) {
  const resourceId = item.resourceId?.trim();
  if (!resourceId || !canPreviewLink(item)) return;
  previewBusyId.value = item.linkId;
  error.value = null;
  try {
    const resource = await diGetById(resourceId);
    if (resource.type === DI_RESOURCE_TYPE.markdown) {
      markdownPreviewResource.value = resource;
      markdownPreviewOpen.value = true;
      return;
    }
    if (resource.type === DI_RESOURCE_TYPE.file && isDiOfficeEditable(resource)) {
      editorResource.value = resource;
      editorOpen.value = true;
      return;
    }
    if (resource.type === DI_RESOURCE_TYPE.file) {
      filePreviewResource.value = resource;
      filePreviewOpen.value = true;
      return;
    }
  } catch (e: unknown) {
    error.value = panelError(e, 'operationCore.profile.documents.previewError');
  } finally {
    previewBusyId.value = null;
  }
}

async function downloadLink(item: DiLinkedResource) {
  const resourceId = item.resourceId?.trim();
  if (!resourceId || item.permissions?.canDownload === false) return;
  downloadingId.value = item.linkId;
  error.value = null;
  try {
    const resource = await diGetById(resourceId);
    const { blob, fileName } = await diDownloadResource(resource.id, resource.fileName || resource.name);
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName || resource.fileName || resource.name || 'dosya';
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  } catch (e: unknown) {
    error.value = panelError(e, 'operationCore.profile.documents.downloadError');
  } finally {
    downloadingId.value = null;
  }
}

async function removeLink(item: DiLinkedResource) {
  const linkId = item.linkId?.trim();
  if (!linkId) return;
  deleteBusyId.value = linkId;
  error.value = null;
  try {
    await diDeleteResourceLink(linkId);
    await load();
  } catch (e: unknown) {
    error.value = panelError(e, 'operationCore.profile.documents.deleteError');
  } finally {
    deleteBusyId.value = null;
  }
}

watch(
  () => [props.datasetName, props.dataId, props.alsoWorkItemKey] as const,
  () => {
    void load();
  },
  { immediate: true }
);
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-2">
      <span v-if="hint && !dense" class="text-body-2 text-medium-emphasis">{{ hint }}</span>
      <v-btn
        v-if="canEdit"
        color="primary"
        size="small"
        variant="tonal"
        rounded="lg"
        class="text-none"
        prepend-icon="mdi-link-plus"
        :loading="openingLinker"
        @click="openLinker"
      >
        Belge bağla
      </v-btn>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-2" />
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-2">{{ error }}</v-alert>

    <div v-if="dense && items.length" class="d-flex flex-wrap ga-1 mt-1">
      <v-chip
        v-for="item in items"
        :key="item.linkId"
        size="small"
        variant="tonal"
        :prepend-icon="resourceIcon(item)"
        @click="openPreview(item)"
      >
        {{ resourceLabel(item) }}
      </v-chip>
    </div>

    <div v-else-if="!dense && !loading && !items.length && !error" class="text-body-2 text-medium-emphasis">
      Bu karta bağlı belge yok.
    </div>

    <v-list v-else-if="!dense && items.length" class="py-0" density="comfortable">
      <v-list-item v-for="item in items" :key="item.linkId" class="px-2 rounded-lg">
        <template #prepend>
          <v-icon :icon="resourceIcon(item)" size="22" />
        </template>
        <v-list-item-title class="text-body-2 font-weight-medium" @click="openPreview(item)">
          {{ resourceLabel(item) }}
        </v-list-item-title>
        <v-list-item-subtitle class="text-caption">{{ relationLabel(item.relationType) }}</v-list-item-subtitle>
        <template #append>
          <v-btn
            size="x-small"
            variant="text"
            icon="mdi-eye-outline"
            :loading="previewBusyId === item.linkId"
            @click="openPreview(item)"
          />
          <v-btn
            size="x-small"
            variant="text"
            icon="mdi-download"
            :loading="downloadingId === item.linkId"
            @click="downloadLink(item)"
          />
          <v-btn
            v-if="canEdit"
            size="x-small"
            variant="text"
            icon="mdi-link-variant-off"
            color="error"
            :loading="deleteBusyId === item.linkId"
            @click="removeLink(item)"
          />
        </template>
      </v-list-item>
    </v-list>

    <OcLinkDocumentDialog
      v-model="linkDialogOpen"
      :dataset-name="datasetName"
      :data-id="dataId"
      :relation-types="relationTypes"
      :excluded-resource-ids="excludedResourceIds"
      :root-folder-id="dialogRootId"
      :allow-upload="allowUpload"
      :allow-create-folder="allowCreateFolder"
      @linked="load"
    />
    <DiFilePreviewDialog
      v-model="filePreviewOpen"
      :resource="filePreviewResource"
      @download="downloadResource"
    />
    <DiMarkdownPreviewDialog v-model="markdownPreviewOpen" :resource="markdownPreviewResource" />
    <DiResourceEditorDialog v-model="editorOpen" :resource="editorResource" />
  </div>
</template>
