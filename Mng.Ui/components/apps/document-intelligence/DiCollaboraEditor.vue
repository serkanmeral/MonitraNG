<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  applyCollaboraUiCustomizations,
  handleCollaboraHostMessage,
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
  }
);

const { t } = useAppI18n();
const iframeRef = ref<HTMLIFrameElement | null>(null);
const iframeLoaded = ref(false);

watch(
  () => props.editorUrl,
  () => {
    iframeLoaded.value = false;
  }
);

function onIframeLoad() {
  iframeLoaded.value = true;
  if (props.hideCollaboraChrome) {
    applyCollaboraUiCustomizations(iframeRef.value, props.editorUrl);
  }
}

function onHostMessage(event: MessageEvent) {
  if (!props.hideCollaboraChrome) return;
  handleCollaboraHostMessage(event, props.editorUrl, iframeRef.value);
}

onMounted(() => {
  window.addEventListener('message', onHostMessage);
});

onBeforeUnmount(() => {
  window.removeEventListener('message', onHostMessage);
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
