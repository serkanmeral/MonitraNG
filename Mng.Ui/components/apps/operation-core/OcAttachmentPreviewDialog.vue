<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocFetchAttachmentBlob, ocExtractDgErrorMessage } from '@/services/operationCoreService';
import { previewKind, previewMime, typedBlobForPreview } from '@/utils/ocAttachmentPreview';
import type { OcAttachment } from '@/types/apps/operationCore';

const props = defineProps<{
  modelValue: boolean;
  attachment: OcAttachment | null;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void;
  (e: 'download', att: OcAttachment): void;
}>();

const { t } = useAppI18n();

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const loading = ref(false);
const error = ref<string | null>(null);
const objectUrl = ref<string | null>(null);
const textContent = ref<string | null>(null);

const kind = computed(() => (props.attachment ? previewKind(props.attachment) : 'none'));

function revoke() {
  if (objectUrl.value) {
    URL.revokeObjectURL(objectUrl.value);
    objectUrl.value = null;
  }
  textContent.value = null;
}

async function loadPreview(att: OcAttachment) {
  revoke();
  error.value = null;
  const k = previewKind(att);
  if (k === 'none') return;
  loading.value = true;
  try {
    const blob = await ocFetchAttachmentBlob(att);
    if (k === 'text') {
      textContent.value = await blob.text();
    } else {
      // DG octet-stream döndürürse iframe/img render etmeyip indirir; doğru MIME ile yeniden sar.
      const mime = previewMime(att);
      const typed = mime ? typedBlobForPreview(blob, att.fileName ?? 'file') : blob;
      objectUrl.value = URL.createObjectURL(typed);
    }
  } catch (e: unknown) {
    error.value = ocExtractDgErrorMessage(e, t('operationCore.profile.attachments.previewError'));
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.modelValue, props.attachment?.path] as const,
  ([isOpen]) => {
    if (isOpen && props.attachment) {
      void loadPreview(props.attachment);
    } else if (!isOpen) {
      revoke();
      error.value = null;
    }
  }
);

function triggerDownload() {
  if (props.attachment) emit('download', props.attachment);
}
</script>

<template>
  <v-dialog v-model="open" max-width="980" scrollable>
    <v-card rounded="lg" class="oc-attach-preview-card">
      <v-card-title class="d-flex align-center ga-2 py-3">
        <v-icon icon="mdi-file-eye-outline" color="primary" size="20" />
        <span class="text-subtitle-1 font-weight-bold text-truncate flex-grow-1 min-width-0">
          {{ attachment?.fileName }}
        </span>
        <v-btn
          v-if="attachment"
          variant="text"
          size="small"
          class="text-none"
          prepend-icon="mdi-download"
          @click="triggerDownload"
        >
          {{ t('operationCore.profile.attachments.download') }}
        </v-btn>
        <v-btn icon="mdi-close" variant="text" size="small" @click="open = false" />
      </v-card-title>
      <v-divider />
      <v-card-text class="oc-attach-preview-body pa-0">
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
          <img v-if="objectUrl" :src="objectUrl" :alt="attachment?.fileName ?? ''" class="oc-preview-image" />
        </div>

        <iframe
          v-else-if="kind === 'pdf' && objectUrl"
          :src="objectUrl"
          class="oc-preview-pdf"
          :title="attachment?.fileName ?? 'PDF'"
        />

        <pre v-else-if="kind === 'text'" class="oc-preview-text">{{ textContent }}</pre>

        <div v-else class="text-body-2 text-medium-emphasis text-center py-12 px-4">
          {{ t('operationCore.profile.attachments.previewUnavailable') }}
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.min-width-0 {
  min-width: 0;
}

.oc-attach-preview-body {
  max-height: 78vh;
  overflow: auto;
}

.oc-preview-image {
  max-width: 100%;
  height: auto;
  border-radius: 8px;
}

.oc-preview-pdf {
  width: 100%;
  height: 78vh;
  border: 0;
  display: block;
}

.oc-preview-text {
  margin: 0;
  padding: 1rem 1.25rem;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: 'Roboto Mono', ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 0.8125rem;
  line-height: 1.5;
}
</style>
