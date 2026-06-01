<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import DiMarkdownViewer from '@/components/apps/document-intelligence/DiMarkdownViewer.vue';

const props = withDefaults(
  defineProps<{
    modelValue: string;
    showPreview?: boolean;
  }>(),
  { modelValue: '', showPreview: true }
);

const emit = defineEmits<{ 'update:modelValue': [string] }>();

const { t } = useAppI18n();
const textareaRef = ref<HTMLTextAreaElement | null>(null);
const localPreview = ref(props.showPreview);

const content = computed({
  get: () => props.modelValue,
  set: (v: string) => emit('update:modelValue', v),
});

watch(
  () => props.showPreview,
  (v) => {
    localPreview.value = v;
  }
);

// Seçili metnin etrafına / başına markdown söz dizimi ekleyen yardımcılar.
function surround(before: string, after = before) {
  const el = textareaRef.value;
  if (!el) {
    content.value = `${content.value}${before}${after}`;
    return;
  }
  const start = el.selectionStart ?? content.value.length;
  const end = el.selectionEnd ?? content.value.length;
  const value = content.value;
  const selected = value.slice(start, end);
  const next = `${value.slice(0, start)}${before}${selected}${after}${value.slice(end)}`;
  content.value = next;
  void nextTick(() => {
    el.focus();
    const pos = start + before.length + selected.length + after.length;
    el.setSelectionRange(pos, pos);
  });
}

function prefixLine(prefix: string) {
  const el = textareaRef.value;
  const value = content.value;
  if (!el) {
    content.value = `${value}\n${prefix}`;
    return;
  }
  const start = el.selectionStart ?? value.length;
  const lineStart = value.lastIndexOf('\n', start - 1) + 1;
  const next = `${value.slice(0, lineStart)}${prefix}${value.slice(lineStart)}`;
  content.value = next;
  void nextTick(() => {
    el.focus();
    const pos = start + prefix.length;
    el.setSelectionRange(pos, pos);
  });
}

defineExpose({ focus: () => textareaRef.value?.focus() });
</script>

<template>
  <div class="di-md-editor">
    <div class="di-md-editor__toolbar d-flex flex-wrap align-center ga-1 px-2 py-1 border-b">
      <v-btn size="x-small" variant="text" :title="t('documentIntelligence.editor.bold')" @click="surround('**')">
        <strong>B</strong>
      </v-btn>
      <v-btn size="x-small" variant="text" :title="t('documentIntelligence.editor.italic')" @click="surround('*')">
        <em>I</em>
      </v-btn>
      <v-btn size="x-small" variant="text" :title="t('documentIntelligence.editor.strike')" @click="surround('~~')">
        <span style="text-decoration: line-through">S</span>
      </v-btn>
      <v-btn size="x-small" variant="text" :title="t('documentIntelligence.editor.code')" @click="surround('`')">
        &lt;/&gt;
      </v-btn>
      <v-divider vertical inset class="mx-1" />
      <v-btn size="x-small" variant="text" :title="t('documentIntelligence.editor.h1')" @click="prefixLine('# ')">H1</v-btn>
      <v-btn size="x-small" variant="text" :title="t('documentIntelligence.editor.h2')" @click="prefixLine('## ')">H2</v-btn>
      <v-btn size="x-small" variant="text" :title="t('documentIntelligence.editor.h3')" @click="prefixLine('### ')">H3</v-btn>
      <v-divider vertical inset class="mx-1" />
      <v-btn size="x-small" variant="text" icon="mdi-format-list-bulleted" :title="t('documentIntelligence.editor.bulletList')" @click="prefixLine('- ')" />
      <v-btn size="x-small" variant="text" icon="mdi-format-list-numbered" :title="t('documentIntelligence.editor.orderedList')" @click="prefixLine('1. ')" />
      <v-btn size="x-small" variant="text" icon="mdi-format-quote-close" :title="t('documentIntelligence.editor.quote')" @click="prefixLine('> ')" />
      <v-btn size="x-small" variant="text" icon="mdi-link" :title="t('documentIntelligence.editor.link')" @click="surround('[', '](https://)')" />
      <v-spacer />
      <v-btn
        size="x-small"
        variant="text"
        :color="localPreview ? 'primary' : undefined"
        :title="t('documentIntelligence.editor.togglePreview')"
        @click="localPreview = !localPreview"
      >
        <v-icon size="16" class="mr-1">mdi-eye</v-icon>
        {{ t('documentIntelligence.editor.preview') }}
      </v-btn>
    </div>

    <div :class="['di-md-editor__body d-flex', { 'di-md-editor__body--split': localPreview }]">
      <textarea
        ref="textareaRef"
        v-model="content"
        class="di-md-editor__textarea"
        spellcheck="false"
        :placeholder="t('documentIntelligence.editor.placeholder')"
      />
      <div v-if="localPreview" class="di-md-editor__preview">
        <DiMarkdownViewer :content="content" :empty-label="t('documentIntelligence.editor.previewEmpty')" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.di-md-editor {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.18);
  border-radius: 10px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}
.di-md-editor__body {
  min-height: 320px;
}
.di-md-editor__textarea {
  flex: 1 1 50%;
  min-height: 320px;
  width: 100%;
  resize: vertical;
  border: none;
  outline: none;
  padding: 14px 16px;
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 0.875rem;
  line-height: 1.6;
  background: transparent;
  color: rgb(var(--v-theme-on-surface));
}
.di-md-editor__body--split .di-md-editor__textarea {
  border-right: 1px solid rgba(var(--v-theme-on-surface), 0.12);
}
.di-md-editor__preview {
  flex: 1 1 50%;
  padding: 14px 18px;
  overflow: auto;
  max-height: 600px;
}
</style>
