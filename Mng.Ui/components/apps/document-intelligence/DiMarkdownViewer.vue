<script setup lang="ts">
import { computed, inject } from 'vue';
import { renderMarkdown } from '@/utils/markdown';
import { DI_RESOURCE_PREVIEW_KEY } from '@/composables/useDiResourcePreview';
import {
  parseDiResourceIdFromAnchor,
  rewriteDiInternalLinksInHtml,
  rewriteDiFileImagesInHtml,
} from '@/utils/diResourceLink';

const props = withDefaults(
  defineProps<{
    content: string;
    emptyLabel?: string;
    /** false ise iç link tıklaması yakalanmaz (salt metin önizleme). */
    interactiveInternalLinks?: boolean;
  }>(),
  { interactiveInternalLinks: true }
);

const preview = inject(DI_RESOURCE_PREVIEW_KEY, null);

const html = computed(() => {
  const base = renderMarkdown(props.content);
  if (!base) return base;
  let next = rewriteDiFileImagesInHtml(base);
  if (props.interactiveInternalLinks === false || !preview) return next;
  return rewriteDiInternalLinksInHtml(next);
});

const isEmpty = computed(() => !props.content || !props.content.trim());

function onBodyClick(event: MouseEvent) {
  if (props.interactiveInternalLinks === false || !preview) return;

  const anchor = (event.target as HTMLElement | null)?.closest('a');
  if (!anchor || !(anchor instanceof HTMLAnchorElement)) return;

  const resourceId = parseDiResourceIdFromAnchor(anchor);
  if (!resourceId) return;

  event.preventDefault();
  event.stopPropagation();

  void preview.openResourceById(resourceId, event);
}
</script>

<template>
  <div class="di-md-viewer">
    <div v-if="isEmpty" class="text-medium-emphasis text-body-2 py-4">
      {{ emptyLabel || '—' }}
    </div>
    <!-- renderMarkdown DOMPurify ile sanitize edilmiş HTML döner -->
    <div
      v-else
      class="di-md-viewer__body"
      v-html="html"
      @click.capture="onBodyClick"
    />
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
.di-md-viewer__body :deep(h4) { font-size: 1.05rem; }
.di-md-viewer__body :deep(p) { margin: 0.5em 0; }
.di-md-viewer__body :deep(ul),
.di-md-viewer__body :deep(ol) { padding-left: 1.5rem; margin: 0.5em 0; }
.di-md-viewer__body :deep(ul.contains-task-list) {
  list-style: none;
  padding-left: 0.25rem;
}
.di-md-viewer__body :deep(li.task-list-item) {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  list-style: none;
}
.di-md-viewer__body :deep(li.task-list-item input[type='checkbox']) {
  margin-top: 0.35em;
  flex-shrink: 0;
  pointer-events: none;
}
.di-md-viewer__body :deep(li) { margin: 0.2em 0; }
.di-md-viewer__body :deep(a) {
  color: rgb(var(--v-theme-primary));
  text-decoration: underline;
  cursor: pointer;
}
.di-md-viewer__body :deep(a.di-internal-resource-link),
.di-md-viewer__body :deep(a[data-di-resource-id]) {
  text-decoration-style: dotted;
}
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
