<script setup lang="ts">
import { ref, computed, watch, provide } from 'vue';
import DiMarkdownViewer from '@/components/apps/document-intelligence/DiMarkdownViewer.vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiResourceEditorDialog from '@/components/apps/document-intelligence/DiResourceEditorDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  DI_RESOURCE_PREVIEW_KEY,
  type DiResourcePreviewContext,
} from '@/composables/useDiResourcePreview';
import { diGetById, diGetMarkdownContent } from '@/services/documentIntelligenceService';
import { DI_RESOURCE_TYPE, type DiResource } from '@/types/apps/documentIntelligence';
import { isDiDocxEditable, isDiPreviewable } from '@/utils/diFilePreview';
import { buildDiResourceUrl } from '@/utils/diResourceLink';

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
const content = ref('');

/** Modal içi markdown gezinme yığını (kök hariç). */
const navStack = ref<DiResource[]>([]);

const filePreviewOpen = ref(false);
const filePreviewResource = ref<DiResource | null>(null);
const editorOpen = ref(false);
const editorResource = ref<DiResource | null>(null);

const activeResource = computed(() => {
  const stacked = navStack.value[navStack.value.length - 1];
  return stacked ?? props.resource;
});

const canGoBack = computed(() => navStack.value.length > 0);

const title = computed(() => {
  const r = activeResource.value;
  if (!r) return '';
  return r.title?.trim() || r.name?.trim() || '';
});

async function loadContent(resource: DiResource) {
  loading.value = true;
  error.value = null;
  content.value = '';
  try {
    const c = await diGetMarkdownContent(resource.id);
    content.value = c.content ?? '';
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.errors.preview');
  } finally {
    loading.value = false;
  }
}

async function openLinkedResource(resourceId: string, event?: MouseEvent) {
  if (event && (event.metaKey || event.ctrlKey || event.shiftKey)) {
    window.open(buildDiResourceUrl(resourceId), '_blank', 'noopener,noreferrer');
    return;
  }
  event?.preventDefault();

  try {
    const linked = await diGetById(resourceId);
    if (linked.type === DI_RESOURCE_TYPE.markdown) {
      navStack.value.push(linked);
      await loadContent(linked);
      return;
    }
    if (linked.type === DI_RESOURCE_TYPE.file) {
      if (isDiDocxEditable(linked)) {
        editorResource.value = linked;
        editorOpen.value = true;
        return;
      }
      if (isDiPreviewable(linked)) {
        filePreviewResource.value = linked;
        filePreviewOpen.value = true;
        return;
      }
    }
    error.value = t('documentIntelligence.internalLink.previewUnavailable');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.errors.preview');
  }
}

function goBack() {
  if (!navStack.value.length) return;
  navStack.value.pop();
  const target = activeResource.value;
  if (target) void loadContent(target);
}

function resetState() {
  navStack.value = [];
  content.value = '';
  error.value = null;
  filePreviewOpen.value = false;
  filePreviewResource.value = null;
  editorOpen.value = false;
  editorResource.value = null;
}

const modalPreviewContext: DiResourcePreviewContext = {
  openResourceById: openLinkedResource,
};
provide(DI_RESOURCE_PREVIEW_KEY, modalPreviewContext);

watch(
  () => [props.modelValue, props.resource?.id] as const,
  ([isOpen, resourceId]) => {
    if (isOpen && props.resource) {
      resetState();
      void loadContent(props.resource);
    } else if (!isOpen) {
      resetState();
    } else if (isOpen && !resourceId) {
      resetState();
    }
  },
);
</script>

<template>
  <div class="di-markdown-preview-host">
    <v-dialog v-model="open" max-width="900" scrollable>
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center ga-2 py-3">
        <v-btn
          v-if="canGoBack"
          icon="mdi-arrow-left"
          variant="text"
          size="small"
          :title="t('documentIntelligence.internalLink.back')"
          @click="goBack"
        />
        <v-icon icon="mdi-language-markdown-outline" color="primary" size="20" />
        <span class="text-subtitle-1 font-weight-bold text-truncate flex-grow-1 di-min-w-0">
          {{ title }}
        </span>
        <v-btn
          v-if="activeResource"
          icon="mdi-open-in-new"
          variant="text"
          size="small"
          :title="t('documentIntelligence.internalLink.openFullPage')"
          :href="buildDiResourceUrl(activeResource.id)"
          target="_blank"
          rel="noopener noreferrer"
        />
        <v-btn icon="mdi-close" variant="text" size="small" @click="open = false" />
      </v-card-title>
      <v-divider />
      <v-card-text class="di-markdown-preview-body pa-4">
        <div v-if="loading" class="d-flex justify-center py-12">
          <v-progress-circular indeterminate color="primary" size="36" />
        </div>
        <v-alert
          v-else-if="error"
          type="error"
          variant="tonal"
          density="compact"
          class="rounded-lg"
        >
          {{ error }}
        </v-alert>
        <DiMarkdownViewer
          v-else
          :content="content"
          :empty-label="t('documentIntelligence.emptyDoc')"
        />
      </v-card-text>
    </v-card>
  </v-dialog>

  <DiFilePreviewDialog v-model="filePreviewOpen" :resource="filePreviewResource" />
  <DiResourceEditorDialog v-model="editorOpen" :resource="editorResource" />
  </div>
</template>

<style scoped>
.di-min-w-0 {
  min-width: 0;
}

.di-markdown-preview-body {
  max-height: 78vh;
  overflow: auto;
}
</style>
