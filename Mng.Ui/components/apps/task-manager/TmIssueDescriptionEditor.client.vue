<script setup lang="ts">
import { watch, onBeforeUnmount } from 'vue';
import { useEditor, EditorContent } from '@tiptap/vue-3';
import StarterKit from '@tiptap/starter-kit';

const props = withDefaults(
  defineProps<{
    modelValue: string;
    label?: string;
    readonly?: boolean;
  }>(),
  { modelValue: '', label: '', readonly: false }
);

const emit = defineEmits<{ 'update:modelValue': [string] }>();

function normalizeStored(html: string): string {
  const t = html.replace(/\s/g, '');
  if (t === '<p></p>' || t === '<p><br></p>' || t === '') return '';
  return html;
}

const editor = useEditor({
  extensions: [StarterKit],
  content: props.modelValue?.trim() ? props.modelValue : '<p></p>',
  editable: !props.readonly,
  editorProps: {
    attributes: {
      class: 'tm-issue-desc-editor__prose text-body-2',
    },
  },
  onUpdate: ({ editor: ed }) => {
    emit('update:modelValue', normalizeStored(ed.getHTML()));
  },
});

watch(
  () => props.modelValue,
  (v) => {
    const ed = editor.value;
    if (!ed || ed.isDestroyed) return;
    const next = v?.trim() ? v : '';
    const cur = normalizeStored(ed.getHTML());
    const normNext = normalizeStored(next || '<p></p>');
    if (normNext !== cur) {
      ed.commands.setContent(next?.trim() ? next : '<p></p>', false);
    }
  }
);

watch(
  () => props.readonly,
  (ro) => {
    editor.value?.setEditable(!ro);
  }
);

onBeforeUnmount(() => {
  editor.value?.destroy();
});
</script>

<template>
  <div class="tm-issue-desc-editor">
    <v-label v-if="label" class="font-weight-medium mb-1 d-block text-body-2">{{ label }}</v-label>
    <v-card variant="outlined" rounded="lg" class="tm-issue-desc-editor__card overflow-hidden">
      <template v-if="editor">
        <div
          v-if="!readonly"
          class="tm-issue-desc-editor__toolbar d-flex flex-wrap align-center gap-1 pa-2 border-b"
        >
          <v-btn size="x-small" variant="tonal" class="text-none" @click="editor.chain().focus().toggleBold().run()">
            <strong>B</strong>
          </v-btn>
          <v-btn size="x-small" variant="tonal" class="text-none" @click="editor.chain().focus().toggleItalic().run()">
            <em>I</em>
          </v-btn>
          <v-btn size="x-small" variant="tonal" class="text-none" @click="editor.chain().focus().toggleStrike().run()">
            S
          </v-btn>
          <v-divider vertical inset class="mx-1" />
          <v-btn size="x-small" variant="tonal" class="text-none" @click="editor.chain().focus().toggleBulletList().run()">
            •
          </v-btn>
          <v-btn size="x-small" variant="tonal" class="text-none" @click="editor.chain().focus().toggleOrderedList().run()">
            1.
          </v-btn>
          <v-btn size="x-small" variant="tonal" class="text-none" @click="editor.chain().focus().toggleHeading({ level: 2 }).run()">
            H2
          </v-btn>
          <v-btn size="x-small" variant="tonal" class="text-none" @click="editor.chain().focus().toggleBlockquote().run()">
            ”
          </v-btn>
          <v-spacer />
          <v-btn size="x-small" variant="text" @click="editor.chain().focus().undo().run()">↺</v-btn>
          <v-btn size="x-small" variant="text" @click="editor.chain().focus().redo().run()">↻</v-btn>
        </div>
        <editor-content :editor="editor" class="tm-issue-desc-editor__body" />
      </template>
      <div v-else class="pa-4 text-medium-emphasis text-body-2">…</div>
    </v-card>
  </div>
</template>

<style scoped>
.tm-issue-desc-editor__body {
  min-height: 140px;
  max-height: 360px;
  overflow: auto;
}
.tm-issue-desc-editor :deep(.tm-issue-desc-editor__prose) {
  min-height: 120px;
  outline: none;
  padding: 12px 14px;
}
.tm-issue-desc-editor :deep(.tm-issue-desc-editor__prose p) {
  margin: 0 0 0.5em;
}
.tm-issue-desc-editor :deep(.tm-issue-desc-editor__prose ul),
.tm-issue-desc-editor :deep(.tm-issue-desc-editor__prose ol) {
  padding-left: 1.25rem;
  margin: 0.25em 0;
}
</style>
