<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import DiLinkedWorkItemsPanel from '@/components/apps/document-intelligence/DiLinkedWorkItemsPanel.vue';
import { diFetchFileBlob } from '@/services/documentIntelligenceService';
import { diPreviewKind, diPreviewMime } from '@/utils/diFilePreview';
import type { DiResource } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  resource: DiResource | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  download: [resource: DiResource];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const loading = ref(false);
const error = ref<string | null>(null);
const objectUrl = ref<string | null>(null);
const textContent = ref<string | null>(null);

const kind = computed(() => (props.resource ? diPreviewKind(props.resource) : 'none'));
const fileLabel = computed(() => props.resource?.fileName || props.resource?.name || '');

function revoke() {
  if (objectUrl.value) {
    URL.revokeObjectURL(objectUrl.value);
    objectUrl.value = null;
  }
  textContent.value = null;
}

async function loadPreview(resource: DiResource) {
  revoke();
  error.value = null;
  const k = diPreviewKind(resource);
  if (k === 'none' || !resource.filePath) return;
  loading.value = true;
  try {
    const blob = await diFetchFileBlob(resource.filePath);
    if (k === 'text') {
      textContent.value = await blob.text();
    } else {
      // DG octet-stream döndürürse iframe/img render etmeyip indirir; doğru MIME ile yeniden sar.
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
  () => [props.modelValue, props.resource?.id] as const,
  ([isOpen]) => {
    if (isOpen && props.resource) {
      void loadPreview(props.resource);
    } else if (!isOpen) {
      revoke();
      error.value = null;
    }
  },
);

function triggerDownload() {
  if (props.resource) emit('download', props.resource);
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
          v-if="resource"
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

        <pre v-else-if="kind === 'text'" class="di-preview-text">{{ textContent }}</pre>

        <div v-else class="text-body-2 text-medium-emphasis text-center py-12 px-4">
          {{ t('documentIntelligence.previewUnavailable') }}
        </div>

        <template v-if="resource?.id">
          <v-divider />
          <div class="pa-4">
            <DiLinkedWorkItemsPanel :resource-id="resource.id" />
          </div>
        </template>
      </v-card-text>
    </v-card>
  </v-dialog>
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
