<script setup lang="ts">
import { computed } from 'vue';
import { sanitizeOcRichHtml } from '@/utils/ocRichText';

const props = withDefaults(
  defineProps<{
    html?: string | null;
    emptyText?: string;
  }>(),
  {
    html: '',
    emptyText: '—',
  }
);

const safeHtml = computed(() => sanitizeOcRichHtml(props.html));
const isEmpty = computed(() => !safeHtml.value.trim());
</script>

<template>
  <v-card variant="outlined" rounded="lg" class="oc-rich-text-readonly">
    <v-card-text class="pa-3">
      <div v-if="isEmpty" class="text-medium-emphasis">{{ emptyText }}</div>
      <div v-else class="oc-rich-text-content text-body-2" v-html="safeHtml" />
    </v-card-text>
  </v-card>
</template>

<style scoped>
.oc-rich-text-content :deep(p) {
  margin: 0 0 0.4em;
}

.oc-rich-text-content :deep(p:last-child) {
  margin-bottom: 0;
}

.oc-rich-text-content :deep(ul),
.oc-rich-text-content :deep(ol) {
  padding-left: 1.25rem;
  margin: 0.25em 0;
}

.oc-rich-text-content :deep(blockquote) {
  border-left: 3px solid rgba(var(--v-border-color), var(--v-border-opacity));
  margin: 0.35em 0;
  padding-left: 0.75rem;
  opacity: 0.92;
}

.oc-rich-text-content :deep(code) {
  font-size: 0.9em;
  padding: 0.1em 0.35em;
  border-radius: 4px;
  background: rgba(var(--v-theme-on-surface), 0.06);
}
</style>
