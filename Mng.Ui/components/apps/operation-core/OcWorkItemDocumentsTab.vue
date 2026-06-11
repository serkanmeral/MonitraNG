<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiMarkdownPreviewDialog from '@/components/apps/document-intelligence/DiMarkdownPreviewDialog.vue';
import OcLinkDocumentDialog from '@/components/apps/operation-core/OcLinkDocumentDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  diDeleteResourceLink,
  diExtractMessage,
  diFetchFileBlob,
  diGetById,
  diGetLinkedResourcesForWorkItem,
} from '@/services/documentIntelligenceService';
import {
  DI_RESOURCE_TYPE,
  type DiLinkedResource,
  type DiResource,
} from '@/types/apps/documentIntelligence';

const props = defineProps<{
  workItemId: string;
  canEdit?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
}>();

const { t } = useAppI18n();

const loading = ref(false);
const error = ref<string | null>(null);
const items = ref<DiLinkedResource[]>([]);
const linkDialogOpen = ref(false);
const deleteBusyId = ref<string | null>(null);
const previewBusyId = ref<string | null>(null);
const downloadingId = ref<string | null>(null);

const filePreviewOpen = ref(false);
const filePreviewResource = ref<DiResource | null>(null);
const markdownPreviewOpen = ref(false);
const markdownPreviewResource = ref<DiResource | null>(null);

const excludedResourceIds = ref<string[]>([]);

async function load() {
  const id = props.workItemId.trim();
  if (!id) {
    items.value = [];
    return;
  }
  loading.value = true;
  error.value = null;
  try {
    const res = await diGetLinkedResourcesForWorkItem(id);
    items.value = res.items;
    excludedResourceIds.value = res.items.map((x) => x.resourceId);
  } catch (e: unknown) {
    error.value = diExtractMessage(e, t('operationCore.profile.documents.loadError'));
    items.value = [];
  } finally {
    loading.value = false;
  }
}

function resourceLabel(item: DiLinkedResource): string {
  if (item.title?.trim()) return item.title.trim();
  if (item.name?.trim()) return item.name.trim();
  return item.resourceId;
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
  return 'mdi-file-document-outline';
}

function canPreviewLink(item: DiLinkedResource): boolean {
  if (!item.permissions?.canView) return false;
  const type = item.resourceType?.trim().toLowerCase();
  return type === DI_RESOURCE_TYPE.markdown || type === DI_RESOURCE_TYPE.file;
}

function canDownloadLink(item: DiLinkedResource): boolean {
  return item.permissions?.canDownload === true && item.resourceType === DI_RESOURCE_TYPE.file;
}

function openInDi() {
  navigateTo('/apps/document-intelligence');
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
    if (resource.type === DI_RESOURCE_TYPE.file) {
      filePreviewResource.value = resource;
      filePreviewOpen.value = true;
      return;
    }
    error.value = t('operationCore.profile.documents.previewUnsupported');
  } catch (e: unknown) {
    error.value = diExtractMessage(e, t('operationCore.profile.documents.previewError'));
  } finally {
    previewBusyId.value = null;
  }
}

async function downloadResource(resource: DiResource) {
  if (!resource.filePath) {
    error.value = t('operationCore.profile.documents.downloadError');
    return;
  }
  downloadingId.value = resource.id;
  error.value = null;
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
  } catch (e: unknown) {
    error.value = diExtractMessage(e, t('operationCore.profile.documents.downloadError'));
  } finally {
    downloadingId.value = null;
  }
}

async function downloadLink(item: DiLinkedResource) {
  const resourceId = item.resourceId?.trim();
  if (!resourceId || !canDownloadLink(item)) return;

  downloadingId.value = item.linkId;
  error.value = null;
  try {
    const resource = await diGetById(resourceId);
    await downloadResource(resource);
  } catch (e: unknown) {
    error.value = diExtractMessage(e, t('operationCore.profile.documents.downloadError'));
  } finally {
    downloadingId.value = null;
  }
}

function showFilePreviewButton(item: DiLinkedResource): boolean {
  if (!canPreviewLink(item) || item.resourceType !== DI_RESOURCE_TYPE.file) return false;
  // Özet listede boyut yok; butonu göster, dialog desteklenmeyen türde mesaj verir.
  return true;
}

function showMarkdownPreviewButton(item: DiLinkedResource): boolean {
  return canPreviewLink(item) && item.resourceType === DI_RESOURCE_TYPE.markdown;
}

async function removeLink(item: DiLinkedResource) {
  const linkId = item.linkId?.trim();
  if (!linkId) return;
  deleteBusyId.value = linkId;
  error.value = null;
  try {
    await diDeleteResourceLink(linkId);
    await load();
    emit('changed');
  } catch (e: unknown) {
    error.value = diExtractMessage(e, t('operationCore.profile.documents.deleteError'));
  } finally {
    deleteBusyId.value = null;
  }
}

function onLinked() {
  load();
  emit('changed');
}

watch(() => props.workItemId, () => load());
onMounted(() => load());
</script>

<template>
  <v-card-text class="pa-4">
    <div class="d-flex align-center justify-space-between mb-3">
      <span class="text-body-2 text-medium-emphasis">
        {{ t('operationCore.profile.documents.hint') }}
      </span>
      <v-btn
        v-if="canEdit"
        color="primary"
        size="small"
        variant="flat"
        rounded="lg"
        class="text-none"
        prepend-icon="mdi-link-plus"
        @click="linkDialogOpen = true"
      >
        {{ t('operationCore.profile.documents.linkAction') }}
      </v-btn>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-3 rounded-lg" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <div v-if="!loading && !items.length && !error" class="text-body-2 text-medium-emphasis text-center py-6">
      {{ t('operationCore.profile.documents.empty') }}
    </div>

    <v-list v-else-if="items.length" class="py-0" density="comfortable">
      <v-list-item
        v-for="item in items"
        :key="item.linkId"
        class="px-2 rounded-lg oc-doc-row"
      >
        <template #prepend>
          <v-icon :icon="resourceIcon(item)" size="22" />
        </template>
        <v-list-item-title
          class="text-body-2 font-weight-medium"
          :class="{ 'oc-doc-row--clickable': canPreviewLink(item) }"
          @click="canPreviewLink(item) && openPreview(item)"
        >
          {{ resourceLabel(item) }}
        </v-list-item-title>
        <v-list-item-subtitle class="text-caption">
          {{ relationLabel(item.relationType) }}
          <span v-if="item.resourceType" class="ml-1">· {{ item.resourceType }}</span>
        </v-list-item-subtitle>
        <template #append>
          <v-btn
            v-if="showMarkdownPreviewButton(item) || showFilePreviewButton(item)"
            size="x-small"
            variant="text"
            icon="mdi-eye-outline"
            :loading="previewBusyId === item.linkId"
            :title="t('operationCore.profile.documents.preview')"
            @click="openPreview(item)"
          />
          <v-btn
            v-if="canDownloadLink(item)"
            size="x-small"
            variant="text"
            icon="mdi-download"
            :loading="downloadingId === item.linkId"
            :title="t('operationCore.profile.documents.download')"
            @click="downloadLink(item)"
          />
          <v-btn
            size="x-small"
            variant="text"
            icon="mdi-open-in-new"
            :title="t('operationCore.profile.documents.openDi')"
            @click="openInDi"
          />
          <v-btn
            v-if="canEdit"
            size="x-small"
            variant="text"
            color="error"
            icon="mdi-link-off"
            :loading="deleteBusyId === item.linkId"
            :title="t('operationCore.profile.documents.unlink')"
            @click="removeLink(item)"
          />
        </template>
      </v-list-item>
    </v-list>

    <OcLinkDocumentDialog
      v-model="linkDialogOpen"
      :work-item-id="workItemId"
      :excluded-resource-ids="excludedResourceIds"
      @linked="onLinked"
    />

    <DiFilePreviewDialog
      v-model="filePreviewOpen"
      :resource="filePreviewResource"
      @download="downloadResource"
    />

    <DiMarkdownPreviewDialog
      v-model="markdownPreviewOpen"
      :resource="markdownPreviewResource"
    />
  </v-card-text>
</template>

<style scoped>
.oc-doc-row:hover {
  background: rgba(var(--v-theme-on-surface), 0.04);
}

.oc-doc-row--clickable {
  cursor: pointer;
}

.oc-doc-row--clickable:hover {
  color: rgb(var(--v-theme-primary));
}
</style>
