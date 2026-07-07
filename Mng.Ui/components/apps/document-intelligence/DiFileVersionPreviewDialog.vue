<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useDiEditorSessionCleanup } from '@/composables/useDiEditorSessionCleanup';
import DiCollaboraEditor from '@/components/apps/document-intelligence/DiCollaboraEditor.vue';
import { diGetFileVersionPreviewSession } from '@/services/documentIntelligenceService';
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

const title = computed(() => {
  const name = props.resource?.fileName || props.resource?.name || '';
  const vn = props.versionNumber;
  if (!name && vn == null) return t('documentIntelligence.preview');
  if (vn == null) return name;
  return `${name} · v${vn}`;
});

function reset() {
  editorUrl.value = null;
  error.value = null;
}

async function loadPreview() {
  const id = props.resource?.id?.trim();
  const vn = props.versionNumber;
  if (!id || vn == null) return;

  await releaseEditorSession();
  reset();
  loading.value = true;
  try {
    const session = await diGetFileVersionPreviewSession(id, vn);
    editorUrl.value = session.editorUrl || null;
    trackEditorAccessToken(session.accessToken);
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
          :editor-url="editorUrl"
          :loading="loading"
          :error="error"
          :title="null"
        />
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
</style>
