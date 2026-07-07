<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  applyCollaboraUiCustomizations,
  handleCollaboraHostMessage,
  requestCollaboraSave,
} from '@/utils/diCollaboraPostMessage';

const props = withDefaults(
  defineProps<{
    editorUrl: string | null;
    loading?: boolean;
    error?: string | null;
    title?: string | null;
    hideCollaboraChrome?: boolean;
  }>(),
  {
    hideCollaboraChrome: true,
  },
);

const emit = defineEmits<{
  documentSaved: [];
}>();

const { t } = useAppI18n();
const iframeRef = ref<HTMLIFrameElement | null>(null);
const iframeLoaded = ref(false);
const documentModified = ref(false);
const collaboraMessagingActive = ref(false);

let saveWaitResolve: (() => void) | null = null;
let saveWaitTimeout: ReturnType<typeof setTimeout> | null = null;

function clearSaveWait() {
  if (saveWaitTimeout) {
    clearTimeout(saveWaitTimeout);
    saveWaitTimeout = null;
  }
  saveWaitResolve = null;
}

function resolveSaveWait() {
  const resolve = saveWaitResolve;
  clearSaveWait();
  resolve?.();
}

function onModifiedStatus(modified: boolean) {
  const wasModified = documentModified.value;
  documentModified.value = modified;
  if (!modified) {
    resolveSaveWait();
    if (wasModified) emit('documentSaved');
  }
}

function onSaveComplete() {
  documentModified.value = false;
  resolveSaveWait();
  emit('documentSaved');
}

watch(
  () => props.editorUrl,
  () => {
    iframeLoaded.value = false;
    documentModified.value = false;
    collaboraMessagingActive.value = false;
    clearSaveWait();
  },
);

function onIframeLoad() {
  iframeLoaded.value = true;
  if (props.hideCollaboraChrome) {
    applyCollaboraUiCustomizations(iframeRef.value, props.editorUrl);
  }
}

function onHostMessage(event: MessageEvent) {
  const handled = handleCollaboraHostMessage(event, props.editorUrl, iframeRef.value, {
    onDocumentLoaded: () => {
      collaboraMessagingActive.value = true;
      if (props.hideCollaboraChrome) {
        applyCollaboraUiCustomizations(iframeRef.value, props.editorUrl);
      }
    },
    onModifiedStatus: (modified) => {
      collaboraMessagingActive.value = true;
      onModifiedStatus(modified);
    },
    onSaveComplete: () => {
      collaboraMessagingActive.value = true;
      onSaveComplete();
    },
  });
  if (handled) collaboraMessagingActive.value = true;
}

function isModified(): boolean {
  return documentModified.value;
}

function requestSave(): Promise<void> {
  if (!documentModified.value || !iframeRef.value || !props.editorUrl) {
    return Promise.resolve();
  }

  return new Promise((resolve) => {
    clearSaveWait();
    saveWaitResolve = resolve;
    saveWaitTimeout = setTimeout(() => {
      resolveSaveWait();
    }, 15000);
    requestCollaboraSave(iframeRef.value, props.editorUrl, { dontTerminateEdit: true });
  });
}

onMounted(() => {
  window.addEventListener('message', onHostMessage);
});

onBeforeUnmount(() => {
  window.removeEventListener('message', onHostMessage);
  clearSaveWait();
});

defineExpose({
  isModified,
  requestSave,
  hasCollaboraMessaging: () => collaboraMessagingActive.value,
});
</script>

<template>
  <v-card variant="outlined" rounded="lg" class="di-collabora-editor">
    <v-card-title
      v-if="title"
      class="text-subtitle-2 font-weight-bold py-2 px-4 border-b d-flex align-center ga-2"
    >
      <v-icon icon="mdi-file-document-edit-outline" size="20" />
      <span class="text-truncate">{{ title }}</span>
    </v-card-title>

    <div class="di-collabora-editor__frame-wrap">
      <v-progress-linear
        v-if="loading || (editorUrl && !iframeLoaded)"
        indeterminate
        color="primary"
        class="di-collabora-editor__progress"
      />

      <v-alert v-if="error" type="error" variant="tonal" class="ma-4 rounded-lg">
        {{ error }}
      </v-alert>

      <div v-else-if="!editorUrl && !loading" class="pa-8 text-center text-body-2 text-medium-emphasis">
        {{ t('documentIntelligence.designer.editorNotReady') }}
      </div>

      <iframe
        v-else-if="editorUrl"
        ref="iframeRef"
        :src="editorUrl"
        class="di-collabora-editor__iframe"
        :title="title ?? t('documentIntelligence.designer.editorFrameTitle')"
        allow="clipboard-read; clipboard-write"
        @load="onIframeLoad"
      />
    </div>
  </v-card>
</template>

<style scoped>
.di-collabora-editor {
  overflow: hidden;
}

.di-collabora-editor__frame-wrap {
  position: relative;
  min-height: 720px;
  height: calc(100vh - 260px);
  max-height: 960px;
  background: rgb(var(--v-theme-surface-variant), 0.3);
}

.di-collabora-editor__progress {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  z-index: 2;
}

.di-collabora-editor__iframe {
  width: 100%;
  height: 100%;
  border: 0;
  display: block;
}

.border-b {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
</style>
