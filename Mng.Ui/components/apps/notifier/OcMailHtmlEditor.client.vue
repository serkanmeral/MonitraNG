<script setup lang="ts">
import { ref, watch, onBeforeUnmount } from 'vue';
import { useEditor, EditorContent } from '@tiptap/vue-3';
import StarterKit from '@tiptap/starter-kit';
import { useAppI18n } from '@/composables/useAppI18n';

const props = withDefaults(
  defineProps<{
    modelValue: string;
    label?: string;
  }>(),
  { modelValue: '', label: '' }
);

const emit = defineEmits<{ 'update:modelValue': [string] }>();
const { t } = useAppI18n();

const mode = ref<'visual' | 'source'>('visual');

const PLACEHOLDER_SNIPPETS = [
  '{{workItem.key}}',
  '{{workItem.title}}',
  '{{actor.displayName}}',
  '{{recipient.displayName}}',
  '{{transition.fromState}}',
  '{{transition.toState}}',
  '{{domain.displayName}}',
  '{{event.timestamp}}',
];

function normalizeHtml(html: string): string {
  const t = html.replace(/\s/g, '');
  if (t === '<p></p>' || t === '<p><br></p>' || t === '') return '';
  return html;
}

const editor = useEditor({
  extensions: [StarterKit],
  content: props.modelValue?.trim() ? props.modelValue : '<p></p>',
  editorProps: {
    attributes: {
      class: 'oc-mail-html-editor__prose text-body-2',
    },
  },
  onUpdate: ({ editor: ed }) => {
    if (mode.value === 'visual') {
      emit('update:modelValue', normalizeHtml(ed.getHTML()));
    }
  },
});

watch(
  () => props.modelValue,
  (v) => {
    if (mode.value !== 'visual') return;
    const ed = editor.value;
    if (!ed || ed.isDestroyed) return;
    const next = v?.trim() ? v : '';
    const cur = normalizeHtml(ed.getHTML());
    const normNext = normalizeHtml(next || '<p></p>');
    if (normNext !== cur) {
      ed.commands.setContent(next?.trim() ? next : '<p></p>', false);
    }
  }
);

watch(mode, (m) => {
  if (m === 'visual' && editor.value && !editor.value.isDestroyed) {
    const html = props.modelValue?.trim() ? props.modelValue : '<p></p>';
    editor.value.commands.setContent(html, false);
  }
});

function onSourceInput(value: string) {
  emit('update:modelValue', value);
}

function insertPlaceholder(snippet: string) {
  if (mode.value === 'source') {
    emit('update:modelValue', `${props.modelValue || ''}${snippet}`);
    return;
  }
  editor.value?.chain().focus().insertContent(snippet).run();
}

onBeforeUnmount(() => {
  editor.value?.destroy();
});
</script>

<template>
  <div class="oc-mail-html-editor">
    <div class="d-flex align-center justify-space-between mb-2 flex-wrap ga-2">
      <v-label v-if="label" class="font-weight-medium text-body-2">{{ label }}</v-label>
      <v-btn-toggle v-model="mode" mandatory density="compact" color="primary" variant="outlined" divided>
        <v-btn value="visual" size="small" class="text-none">
          {{ t('notifier.mailTemplates.editorVisual') }}
        </v-btn>
        <v-btn value="source" size="small" class="text-none">
          {{ t('notifier.mailTemplates.editorSource') }}
        </v-btn>
      </v-btn-toggle>
    </div>

    <v-card variant="outlined" rounded="lg" class="overflow-hidden">
      <div class="oc-mail-html-editor__chips d-flex flex-wrap ga-1 pa-2 border-b bg-surface-variant">
        <span class="text-caption text-medium-emphasis me-1">{{ t('notifier.mailTemplates.insertPlaceholder') }}:</span>
        <v-chip
          v-for="ph in PLACEHOLDER_SNIPPETS"
          :key="ph"
          size="x-small"
          variant="tonal"
          class="font-mono-chip"
          @click="insertPlaceholder(ph)"
        >
          {{ ph }}
        </v-chip>
      </div>

      <template v-if="mode === 'visual' && editor">
        <div class="oc-mail-html-editor__toolbar d-flex flex-wrap align-center ga-1 pa-2 border-b">
          <v-btn size="x-small" variant="tonal" @click="editor.chain().focus().toggleBold().run()"><strong>B</strong></v-btn>
          <v-btn size="x-small" variant="tonal" @click="editor.chain().focus().toggleItalic().run()"><em>I</em></v-btn>
          <v-btn size="x-small" variant="tonal" @click="editor.chain().focus().toggleHeading({ level: 1 }).run()">H1</v-btn>
          <v-btn size="x-small" variant="tonal" @click="editor.chain().focus().toggleHeading({ level: 2 }).run()">H2</v-btn>
          <v-btn size="x-small" variant="tonal" @click="editor.chain().focus().toggleBulletList().run()">•</v-btn>
          <v-btn size="x-small" variant="tonal" @click="editor.chain().focus().toggleOrderedList().run()">1.</v-btn>
          <v-btn size="x-small" variant="tonal" @click="editor.chain().focus().toggleBlockquote().run()">❝</v-btn>
          <v-spacer />
          <v-btn size="x-small" variant="text" @click="editor.chain().focus().undo().run()">↺</v-btn>
          <v-btn size="x-small" variant="text" @click="editor.chain().focus().redo().run()">↻</v-btn>
        </div>
        <editor-content :editor="editor" class="oc-mail-html-editor__body" />
      </template>

      <v-textarea
        v-else
        :model-value="modelValue"
        :hint="t('notifier.mailTemplates.fieldBodyHtmlHint')"
        persistent-hint
        rows="12"
        auto-grow
        variant="plain"
        class="oc-mail-html-editor__source pa-3"
        @update:model-value="onSourceInput"
      />
    </v-card>
  </div>
</template>

<style scoped>
.oc-mail-html-editor__body {
  min-height: 200px;
  max-height: 360px;
  overflow: auto;
}
.oc-mail-html-editor :deep(.oc-mail-html-editor__prose) {
  min-height: 180px;
  outline: none;
  padding: 12px 14px;
}
.oc-mail-html-editor :deep(.oc-mail-html-editor__prose h1) {
  font-size: 1.35rem;
  margin: 0 0 0.5em;
}
.oc-mail-html-editor :deep(.oc-mail-html-editor__prose h2) {
  font-size: 1.15rem;
  margin: 0 0 0.5em;
}
.oc-mail-html-editor :deep(.oc-mail-html-editor__prose p) {
  margin: 0 0 0.5em;
}
.oc-mail-html-editor__source :deep(textarea) {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 0.85rem;
  line-height: 1.45;
}
.font-mono-chip {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}
</style>
