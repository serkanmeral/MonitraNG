<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import DiLinkedWorkItemsPanel from '@/components/apps/document-intelligence/DiLinkedWorkItemsPanel.vue';
import DiRelatedResourcesPanel from '@/components/apps/document-intelligence/DiRelatedResourcesPanel.vue';
import DiLifecycleBar from '@/components/apps/document-intelligence/DiLifecycleBar.vue';
import DiDrawioViewer from '@/components/apps/document-intelligence/DiDrawioViewer.vue';
import DiFileVersionHistoryDialog from '@/components/apps/document-intelligence/DiFileVersionHistoryDialog.vue';
import {
  diFetchFileBlob,
  diFetchResourcePreviewPdf,
  diReplaceFileContent,
} from '@/services/documentIntelligenceService';
import {
  diPreviewKind,
  diPreviewMime,
  isDiServerRenderedPdfPreview,
  isDiUploadedFile,
} from '@/utils/diFilePreview';
import type { DiResource } from '@/types/apps/documentIntelligence';

const MAX_FILE_MB = 20;

const props = defineProps<{
  modelValue: boolean;
  resource: DiResource | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  download: [resource: DiResource];
  updated: [resource: DiResource];
}>();

const { t } = useAppI18n();
const { push } = useAppToast();
const panelError = usePanelErrorNotify('errors.dg.generic');

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const current = ref<DiResource | null>(null);
const loading = ref(false);
const replacing = ref(false);
const error = ref<string | null>(null);
const objectUrl = ref<string | null>(null);
const textContent = ref<string | null>(null);
const drawioXml = ref<string | null>(null);
const historyOpen = ref(false);
const replaceInputEl = ref<HTMLInputElement | null>(null);

const kind = computed(() => (current.value ? diPreviewKind(current.value) : 'none'));
const fileLabel = computed(() => current.value?.fileName || current.value?.name || '');
const canReplace = computed(
  () => Boolean(current.value && isDiUploadedFile(current.value) && current.value.permissions.canEdit),
);

function revoke() {
  if (objectUrl.value) {
    URL.revokeObjectURL(objectUrl.value);
    objectUrl.value = null;
  }
  textContent.value = null;
  drawioXml.value = null;
}

async function loadPreview(resource: DiResource) {
  revoke();
  error.value = null;
  const k = diPreviewKind(resource);
  if (k === 'none') return;
  loading.value = true;
  try {
    if (isDiServerRenderedPdfPreview(resource)) {
      const blob = await diFetchResourcePreviewPdf(resource.id);
      const typed = blob.type !== 'application/pdf' ? new Blob([blob], { type: 'application/pdf' }) : blob;
      objectUrl.value = URL.createObjectURL(typed);
      return;
    }
    if (!resource.filePath) return;
    const blob = await diFetchFileBlob(resource.filePath);
    if (k === 'text') {
      textContent.value = await blob.text();
    } else if (k === 'drawio') {
      drawioXml.value = await blob.text();
    } else {
      const mime = diPreviewMime(resource);
      const typed = mime && blob.type !== mime ? new Blob([blob], { type: mime }) : blob;
      objectUrl.value = URL.createObjectURL(typed);
    }
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.errors.preview');
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.modelValue, props.resource] as const,
  ([isOpen, resource]) => {
    if (isOpen && resource) {
      current.value = resource;
      void loadPreview(resource);
    } else if (!isOpen) {
      revoke();
      error.value = null;
      historyOpen.value = false;
    }
  },
);

function triggerDownload() {
  if (current.value) emit('download', current.value);
}

function applyUpdated(updated: DiResource) {
  current.value = updated;
  emit('updated', updated);
  void loadPreview(updated);
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
  const lower = name.toLowerCase();
  if (lower.endsWith('.drawio.xml')) return 'drawio';
  const i = name.lastIndexOf('.');
  return i >= 0 ? name.slice(i + 1).toLowerCase() : '';
}

async function onReplacePick(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files && input.files.length ? input.files[0] : null;
  if (input) input.value = '';
  if (!file || !current.value) return;
  if (file.size > MAX_FILE_MB * 1024 * 1024) {
    push({
      title: t('errors.dg.toastTitle'),
      message: t('documentIntelligence.errors.fileTooLarge', { max: MAX_FILE_MB }),
      severity: 'error',
    });
    return;
  }
  replacing.value = true;
  try {
    const content = await fileToBase64(file);
    const updated = await diReplaceFileContent(current.value.id, {
      content,
      originalFileName: file.name,
      mimeType: file.type || null,
      extension: fileExtension(file.name) || null,
      changeNote: 'replace',
    });
    applyUpdated(updated);
    push({
      title: t('documentIntelligence.notify.successTitle'),
      message: t('documentIntelligence.fileReplaced'),
      severity: 'success',
    });
  } catch (e: unknown) {
    push({
      title: t('errors.dg.toastTitle'),
      message: panelError(e, 'documentIntelligence.errors.replace'),
      severity: 'error',
    });
  } finally {
    replacing.value = false;
  }
}
</script>

<template>
  <v-dialog v-model="open" max-width="980" scrollable>
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center ga-2 py-3">
        <v-icon icon="mdi-file-eye-outline" color="primary" size="20" />
        <span class="text-subtitle-1 font-weight-bold text-truncate flex-grow-1 di-min-w-0">
          {{ fileLabel }}
        </span>
        <v-btn
          v-if="current"
          variant="text"
          size="small"
          class="text-none"
          prepend-icon="mdi-history"
          @click="historyOpen = true"
        >
          {{ t('documentIntelligence.versionHistory') }}
        </v-btn>
        <v-btn
          v-if="canReplace"
          variant="text"
          size="small"
          class="text-none"
          prepend-icon="mdi-file-replace-outline"
          :loading="replacing"
          @click="replaceInputEl?.click()"
        >
          {{ t('documentIntelligence.replaceFile') }}
        </v-btn>
        <v-btn
          v-if="current"
          variant="text"
          size="small"
          class="text-none"
          prepend-icon="mdi-download"
          @click="triggerDownload"
        >
          {{ t('documentIntelligence.download') }}
        </v-btn>
        <v-btn icon="mdi-close" variant="text" size="small" @click="open = false" />
      </v-card-title>
      <v-divider />
      <v-card-text class="di-file-preview-body pa-0">
        <input ref="replaceInputEl" type="file" class="d-none" @change="onReplacePick" />
        <div v-if="loading" class="d-flex justify-center align-center py-12">
          <v-progress-circular indeterminate color="primary" size="36" />
        </div>

        <v-alert
          v-else-if="error"
          type="error"
          variant="tonal"
          density="compact"
          class="ma-4 rounded-lg"
        >
          {{ error }}
        </v-alert>

        <div v-else-if="kind === 'image'" class="d-flex justify-center pa-4">
          <img v-if="objectUrl" :src="objectUrl" :alt="fileLabel" class="di-preview-image" />
        </div>

        <iframe
          v-else-if="kind === 'pdf' && objectUrl"
          :src="objectUrl"
          class="di-preview-pdf"
          :title="fileLabel || 'PDF'"
        />

        <DiDrawioViewer
          v-else-if="kind === 'drawio' && drawioXml != null"
          :xml="drawioXml"
          :title="fileLabel"
        />

        <pre v-else-if="kind === 'text'" class="di-preview-text">{{ textContent }}</pre>

        <div v-else class="text-body-2 text-medium-emphasis text-center py-12 px-4">
          {{ t('documentIntelligence.previewUnavailable') }}
        </div>

        <template v-if="current?.id">
          <v-divider />
          <div class="pa-4">
            <DiLifecycleBar
              class="mb-3"
              :resource="current"
              :can-edit="current.permissions.canEdit"
              @updated="applyUpdated"
            />
            <DiLinkedWorkItemsPanel :resource-id="current.id" />
            <DiRelatedResourcesPanel
              :resource-id="current.id"
              :can-edit="current.permissions.canEdit"
              class="mt-3"
            />
          </div>
        </template>
      </v-card-text>
    </v-card>
  </v-dialog>

  <DiFileVersionHistoryDialog
    v-model="historyOpen"
    :resource="current"
    :can-restore="current?.permissions.canEdit ?? false"
    @restored="applyUpdated"
  />
</template>

<style scoped>
.di-min-w-0 {
  min-width: 0;
}

.di-file-preview-body {
  max-height: 78vh;
  overflow: auto;
}

.di-preview-image {
  max-width: 100%;
  height: auto;
  border-radius: 8px;
}

.di-preview-pdf {
  width: 100%;
  height: 78vh;
  border: 0;
  display: block;
}

.di-preview-text {
  margin: 0;
  padding: 1rem 1.25rem;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: 'Roboto Mono', ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 0.8125rem;
  line-height: 1.5;
}
</style>
