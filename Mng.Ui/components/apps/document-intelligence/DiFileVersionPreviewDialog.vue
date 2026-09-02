<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useDiEditorSessionCleanup } from '@/composables/useDiEditorSessionCleanup';
import DiCollaboraEditor from '@/components/apps/document-intelligence/DiCollaboraEditor.vue';
import DiDrawioViewer from '@/components/apps/document-intelligence/DiDrawioViewer.vue';
import { diDownloadFileVersion, diGetFileVersionPreviewSession } from '@/services/documentIntelligenceService';
import { diPreviewKind, diPreviewMime, isDiOfficeEditable } from '@/utils/diFilePreview';
import type { DiResource } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  resource: DiResource | null;
  versionNumber: number | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const { trackEditorAccessToken, releaseEditorSession } = useDiEditorSessionCleanup();

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const loading = ref(false);
const error = ref<string | null>(null);
const editorUrl = ref<string | null>(null);
const objectUrl = ref<string | null>(null);
const textContent = ref<string | null>(null);
const drawioXml = ref<string | null>(null);

const isOffice = computed(() => Boolean(props.resource && isDiOfficeEditable(props.resource)));
const kind = computed(() => (props.resource ? diPreviewKind(props.resource) : 'none'));

const title = computed(() => {
  const name = props.resource?.fileName || props.resource?.name || '';
  const vn = props.versionNumber;
  if (!name && vn == null) return t('documentIntelligence.preview');
  if (vn == null) return name;
  return `${name} · v${vn}`;
});

function revoke() {
  if (objectUrl.value) {
    URL.revokeObjectURL(objectUrl.value);
    objectUrl.value = null;
  }
  textContent.value = null;
  drawioXml.value = null;
}

function reset() {
  editorUrl.value = null;
  error.value = null;
  revoke();
}

async function loadPreview() {
  const id = props.resource?.id?.trim();
  const vn = props.versionNumber;
  if (!id || vn == null || !props.resource) return;

  await releaseEditorSession();
  reset();
  loading.value = true;
  try {
    if (isOffice.value) {
      const session = await diGetFileVersionPreviewSession(id, vn);
      editorUrl.value = session.editorUrl || null;
      trackEditorAccessToken(session.accessToken);
      return;
    }

    const { blob } = await diDownloadFileVersion(id, vn, props.resource.fileName || props.resource.name);
    const k = kind.value;
    if (k === 'text') {
      textContent.value = await blob.text();
    } else if (k === 'drawio') {
      drawioXml.value = await blob.text();
    } else if (k === 'image' || k === 'pdf') {
      const mime = diPreviewMime(props.resource);
      const typed = mime && blob.type !== mime ? new Blob([blob], { type: mime }) : blob;
      objectUrl.value = URL.createObjectURL(typed);
    } else {
      error.value = t('documentIntelligence.previewUnavailable');
    }
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.errors.preview');
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.modelValue, props.resource?.id, props.versionNumber] as const,
  ([isOpen]) => {
    if (isOpen) void loadPreview();
    else {
      void releaseEditorSession();
      reset();
    }
  },
);
</script>

<template>
  <v-dialog v-model="open" fullscreen transition="dialog-bottom-transition">
    <v-card rounded="0" class="d-flex flex-column h-100">
      <v-toolbar density="comfortable" color="surface" class="border-b">
        <v-icon icon="mdi-file-eye-outline" class="ml-3 mr-2" color="primary" />
        <div class="px-1 py-1 flex-grow-1 min-width-0">
          <div class="text-subtitle-1 font-weight-bold text-truncate">
            {{ title }}
          </div>
          <div class="text-caption text-medium-emphasis">
            {{ t('documentIntelligence.versionPreviewReadOnlyHint') }}
          </div>
        </div>
        <v-chip size="small" variant="tonal" color="warning" label class="ml-2">
          {{ t('documentIntelligence.designer.editorReadOnlyHint') }}
        </v-chip>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" @click="open = false" />
      </v-toolbar>

      <v-card-text class="flex-grow-1 pa-0 di-version-preview-body">
        <DiCollaboraEditor
          v-if="isOffice"
          :editor-url="editorUrl"
          :loading="loading"
          :error="error"
          :title="null"
        />
        <div v-else class="h-100 overflow-auto">
          <div v-if="loading" class="d-flex justify-center align-center py-12">
            <v-progress-circular indeterminate color="primary" size="36" />
          </div>
          <v-alert v-else-if="error" type="error" variant="tonal" class="ma-4" density="compact">
            {{ error }}
          </v-alert>
          <div v-else-if="kind === 'image'" class="d-flex justify-center pa-4">
            <img v-if="objectUrl" :src="objectUrl" :alt="title" class="di-version-image" />
          </div>
          <iframe
            v-else-if="kind === 'pdf' && objectUrl"
            :src="objectUrl"
            class="di-version-pdf"
            :title="title"
          />
          <DiDrawioViewer
            v-else-if="kind === 'drawio' && drawioXml != null"
            :xml="drawioXml"
            :title="title"
          />
          <pre v-else-if="kind === 'text'" class="di-version-text">{{ textContent }}</pre>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.border-b {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}

.di-version-preview-body {
  min-height: 0;
}

.di-version-preview-body :deep(.di-collabora-editor) {
  border: 0;
  border-radius: 0;
  height: 100%;
}

.di-version-preview-body :deep(.di-collabora-editor__frame-wrap) {
  height: calc(100vh - 72px);
  max-height: none;
  min-height: 0;
}

.di-version-image {
  max-width: 100%;
  height: auto;
}

.di-version-pdf {
  width: 100%;
  height: calc(100vh - 72px);
  border: 0;
  display: block;
}

.di-version-text {
  margin: 0;
  padding: 1rem 1.25rem;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: 'Roboto Mono', ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 0.8125rem;
}
</style>
