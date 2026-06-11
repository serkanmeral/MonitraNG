<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import DiMarkdownViewer from '@/components/apps/document-intelligence/DiMarkdownViewer.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { diExtractMessage, diGetMarkdownContent } from '@/services/documentIntelligenceService';
import type { DiResource } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  resource: DiResource | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
}>();

const { t } = useAppI18n();

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const loading = ref(false);
const error = ref<string | null>(null);
const content = ref('');

const title = computed(() => {
  const r = props.resource;
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
    error.value = diExtractMessage(e, t('documentIntelligence.errors.preview'));
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.modelValue, props.resource?.id] as const,
  ([isOpen]) => {
    if (isOpen && props.resource) {
      void loadContent(props.resource);
    } else if (!isOpen) {
      content.value = '';
      error.value = null;
    }
  },
);
</script>

<template>
  <v-dialog v-model="open" max-width="900" scrollable>
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center ga-2 py-3">
        <v-icon icon="mdi-language-markdown-outline" color="primary" size="20" />
        <span class="text-subtitle-1 font-weight-bold text-truncate flex-grow-1 di-min-w-0">
          {{ title }}
        </span>
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
