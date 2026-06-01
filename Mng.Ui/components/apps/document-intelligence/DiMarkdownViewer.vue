<script setup lang="ts">
import { computed } from 'vue';
import { renderMarkdown } from '@/utils/markdown';

const props = defineProps<{
  content: string;
  emptyLabel?: string;
}>();

const html = computed(() => renderMarkdown(props.content));
const isEmpty = computed(() => !props.content || !props.content.trim());
</script>

<template>
  <div class="di-md-viewer">
    <div v-if="isEmpty" class="text-medium-emphasis text-body-2 py-4">
      {{ emptyLabel || '—' }}
    </div>
    <!-- renderMarkdown DOMPurify ile sanitize edilmiş HTML döner -->
    <div v-else class="di-md-viewer__body" v-html="html" />
  </div>
</template>

<style scoped>
.di-md-viewer__body {
  line-height: 1.7;
  word-break: break-word;
}
.di-md-viewer__body :deep(h1),
.di-md-viewer__body :deep(h2),
.di-md-viewer__body :deep(h3),
.di-md-viewer__body :deep(h4) {
  margin: 1.2em 0 0.5em;
  font-weight: 600;
  line-height: 1.3;
}
.di-md-viewer__body :deep(h1) { font-size: 1.7rem; }
.di-md-viewer__body :deep(h2) { font-size: 1.4rem; }
.di-md-viewer__body :deep(h3) { font-size: 1.2rem; }
.di-md-viewer__body :deep(p) { margin: 0.5em 0; }
.di-md-viewer__body :deep(ul),
.di-md-viewer__body :deep(ol) { padding-left: 1.5rem; margin: 0.5em 0; }
.di-md-viewer__body :deep(li) { margin: 0.2em 0; }
.di-md-viewer__body :deep(a) { color: rgb(var(--v-theme-primary)); text-decoration: underline; }
.di-md-viewer__body :deep(code) {
  background: rgba(var(--v-theme-on-surface), 0.08);
  padding: 0.1em 0.35em;
  border-radius: 4px;
  font-size: 0.9em;
}
.di-md-viewer__body :deep(pre) {
  background: rgba(var(--v-theme-on-surface), 0.06);
  padding: 12px 14px;
  border-radius: 8px;
  overflow: auto;
  margin: 0.8em 0;
}
.di-md-viewer__body :deep(pre code) {
  background: transparent;
  padding: 0;
}
.di-md-viewer__body :deep(blockquote) {
  border-left: 3px solid rgba(var(--v-theme-primary), 0.4);
  padding: 0.2em 1em;
  margin: 0.8em 0;
  color: rgba(var(--v-theme-on-surface), 0.75);
}
.di-md-viewer__body :deep(table) {
  border-collapse: collapse;
  margin: 0.8em 0;
  width: 100%;
}
.di-md-viewer__body :deep(th),
.di-md-viewer__body :deep(td) {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.15);
  padding: 6px 10px;
  text-align: left;
}
.di-md-viewer__body :deep(img) {
  max-width: 100%;
  border-radius: 6px;
}
.di-md-viewer__body :deep(hr) {
  border: none;
  border-top: 1px solid rgba(var(--v-theme-on-surface), 0.15);
  margin: 1.2em 0;
}
</style>
