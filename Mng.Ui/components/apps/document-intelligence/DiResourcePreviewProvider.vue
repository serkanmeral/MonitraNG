<script setup lang="ts">
import { provide } from 'vue';
import DiMarkdownPreviewDialog from '@/components/apps/document-intelligence/DiMarkdownPreviewDialog.vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiResourceEditorDialog from '@/components/apps/document-intelligence/DiResourceEditorDialog.vue';
import {
  DI_RESOURCE_PREVIEW_KEY,
  useDiResourcePreview,
} from '@/composables/useDiResourcePreview';
import type { DiResource } from '@/types/apps/documentIntelligence';

const props = withDefaults(
  defineProps<{
    onDownload?: (resource: DiResource) => void;
    /** Editör önizleme gibi flex düzenlerinde sarmalayıcıyı şeffaf yapar. */
    flat?: boolean;
  }>(),
  { flat: false }
);

const {
  markdownPreviewOpen,
  markdownPreviewResource,
  filePreviewOpen,
  filePreviewResource,
  editorOpen,
  editorResource,
  previewContext,
} = useDiResourcePreview({ onDownload: (r) => props.onDownload?.(r) });

provide(DI_RESOURCE_PREVIEW_KEY, previewContext);
</script>

<template>
  <div class="di-resource-preview-host" :class="{ 'di-resource-preview-host--flat': props.flat }">
    <slot />
    <DiMarkdownPreviewDialog
      v-model="markdownPreviewOpen"
      :resource="markdownPreviewResource"
    />
    <DiFilePreviewDialog
      v-model="filePreviewOpen"
      :resource="filePreviewResource"
      @download="(r) => props.onDownload?.(r)"
    />
    <DiResourceEditorDialog v-model="editorOpen" :resource="editorResource" />
  </div>
</template>

<style scoped>
.di-resource-preview-host--flat {
  display: contents;
}
</style>
