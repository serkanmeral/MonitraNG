<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import DiCollaboraEditor from '@/components/apps/document-intelligence/DiCollaboraEditor.vue';
import { diGetResourceEditorSession } from '@/services/documentIntelligenceService';
import { isDiManagedDocument } from '@/utils/diFilePreview';
import type { DiResource } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  resource: DiResource | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const loading = ref(false);
const error = ref<string | null>(null);
const editorUrl = ref<string | null>(null);
const editorReadOnly = ref(false);
const viaTemplateFallback = ref(false);

const fileLabel = computed(() => props.resource?.fileName || props.resource?.name || '');

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '';
  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

const toolbarMeta = computed(() => {
  const r = props.resource;
  if (!r) return '';
  const parts: string[] = [];
  if (isDiManagedDocument(r) && r.documentNo) parts.push(r.documentNo);
  if (r.updatedBy || r.updatedAt) {
    const audit = [r.updatedBy, r.updatedAt ? formatDateTime(r.updatedAt) : null].filter(Boolean).join(' · ');
    if (audit) parts.push(audit);
  } else if (r.createdBy || r.createdAt) {
    const audit = [r.createdBy, r.createdAt ? formatDateTime(r.createdAt) : null].filter(Boolean).join(' · ');
    if (audit) parts.push(audit);
  }
  return parts.join(' · ');
});

function reset() {
  editorUrl.value = null;
  editorReadOnly.value = false;
  viaTemplateFallback.value = false;
  error.value = null;
}

async function loadEditor(resource: DiResource) {
  reset();
  loading.value = true;
  try {
    const session = await diGetResourceEditorSession(
      resource.id,
      resource.fileName ?? resource.name
    );
    editorReadOnly.value = session.readOnly;
    viaTemplateFallback.value = Boolean(session.viaTemplateFallback);
    editorUrl.value = session.editorUrl || null;
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.errors.editorSession');
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.modelValue, props.resource?.id] as const,
  ([isOpen]) => {
    if (isOpen && props.resource) {
      void loadEditor(props.resource);
    } else if (!isOpen) {
      reset();
    }
  },
);
</script>

<template>
  <v-dialog v-model="open" fullscreen transition="dialog-bottom-transition">
    <v-card rounded="0" class="d-flex flex-column h-100">
      <v-toolbar density="comfortable" color="surface" class="border-b">
        <div class="px-4 py-1 flex-grow-1 min-width-0">
          <div class="text-subtitle-1 font-weight-bold text-truncate">
            {{ fileLabel }}
          </div>
          <div v-if="toolbarMeta" class="text-caption text-medium-emphasis text-truncate">
            {{ toolbarMeta }}
          </div>
        </div>
        <v-chip
          v-if="editorReadOnly"
          size="small"
          variant="tonal"
          color="warning"
          label
          class="ml-2"
        >
          {{ t('documentIntelligence.designer.editorReadOnlyHint') }}
        </v-chip>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" @click="open = false" />
      </v-toolbar>

      <v-card-text class="flex-grow-1 pa-0 di-resource-editor-body">
        <v-alert
          v-if="viaTemplateFallback && !loading && !error"
          type="info"
          variant="tonal"
          density="compact"
          class="ma-3 mb-0 rounded-lg"
        >
          {{ t('documentIntelligence.editorTemplateFallbackHint') }}
        </v-alert>

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

.di-resource-editor-body {
  min-height: 0;
}

.di-resource-editor-body :deep(.di-collabora-editor) {
  border: 0;
  border-radius: 0;
  height: 100%;
}

.di-resource-editor-body :deep(.di-collabora-editor__frame-wrap) {
  height: calc(100vh - 72px);
  max-height: none;
  min-height: 0;
}
</style>
