<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { useEditor, EditorContent } from '@tiptap/vue-3';
import StarterKit from '@tiptap/starter-kit';
import { useAppI18n } from '@/composables/useAppI18n';
import { isOcRichTextEmpty, normalizeOcRichTextHtml, OC_RICH_TEXT_EMOJIS } from '@/utils/ocRichText';

const props = withDefaults(
  defineProps<{
    modelValue?: string | null;
    disabled?: boolean;
    placeholder?: string;
    minHeight?: number;
  }>(),
  {
    modelValue: '',
    disabled: false,
    placeholder: '',
    minHeight: 96,
  }
);

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void;
}>();

const { t } = useAppI18n();

const emojiMenu = ref(false);
const emojis = OC_RICH_TEXT_EMOJIS;

function insertEmoji(emoji: string) {
  editor.value?.chain().focus().insertContent(emoji).run();
  emojiMenu.value = false;
}

function initialEditorContent(value?: string | null): string {
  const normalized = normalizeOcRichTextHtml(value);
  return normalized && !isOcRichTextEmpty(normalized) ? normalized : '<p></p>';
}

const editor = useEditor({
  immediatelyRender: false,
  extensions: [StarterKit],
  content: initialEditorContent(props.modelValue),
  editable: !props.disabled,
  editorProps: {
    attributes: {
      class: 'oc-rich-text-editor__prose text-body-2',
      'data-placeholder': props.placeholder,
    },
  },
  onUpdate: ({ editor: ed }) => {
    const html = ed.getHTML();
    emit('update:modelValue', isOcRichTextEmpty(html) ? '' : html);
  },
});

watch(
  () => props.disabled,
  (disabled) => {
    editor.value?.setEditable(!disabled);
  }
);

watch(
  () => props.modelValue,
  (value) => {
    const ed = editor.value;
    if (!ed || ed.isDestroyed) return;
    const incoming = normalizeOcRichTextHtml(value);
    const current = ed.getHTML();
    const currentNorm = isOcRichTextEmpty(current) ? '' : current;
    const incomingNorm = isOcRichTextEmpty(incoming) ? '' : incoming;
    if (incomingNorm === currentNorm) return;
    ed.commands.setContent(incomingNorm || '<p></p>', false);
  }
);

onBeforeUnmount(() => {
  editor.value?.destroy();
});

const proseStyle = computed(() => ({ minHeight: `${props.minHeight}px` }));
</script>

<template>
  <v-card variant="outlined" rounded="lg" class="oc-rich-text-editor overflow-visible">
    <div
      v-if="!disabled"
      class="oc-rich-text-editor__toolbar d-flex flex-wrap align-center ga-1 px-2 py-1 border-b"
    >
      <v-btn
        size="x-small"
        variant="text"
        :disabled="!editor"
        :color="editor?.isActive('bold') ? 'primary' : undefined"
        :title="t('operationCore.profile.comments.format.bold')"
        @click="editor?.chain().focus().toggleBold().run()"
      >
        <strong>B</strong>
      </v-btn>
      <v-btn
        size="x-small"
        variant="text"
        :disabled="!editor"
        :color="editor?.isActive('italic') ? 'primary' : undefined"
        :title="t('operationCore.profile.comments.format.italic')"
        @click="editor?.chain().focus().toggleItalic().run()"
      >
        <em>I</em>
      </v-btn>
      <v-btn
        size="x-small"
        variant="text"
        :disabled="!editor"
        :color="editor?.isActive('strike') ? 'primary' : undefined"
        :title="t('operationCore.profile.comments.format.strike')"
        @click="editor?.chain().focus().toggleStrike().run()"
      >
        <span style="text-decoration: line-through">S</span>
      </v-btn>
      <v-divider vertical inset class="mx-1" />
      <v-btn
        size="x-small"
        variant="text"
        icon="mdi-format-list-bulleted"
        :disabled="!editor"
        :color="editor?.isActive('bulletList') ? 'primary' : undefined"
        :title="t('operationCore.profile.comments.format.bulletList')"
        @click="editor?.chain().focus().toggleBulletList().run()"
      />
      <v-btn
        size="x-small"
        variant="text"
        icon="mdi-format-list-numbered"
        :disabled="!editor"
        :color="editor?.isActive('orderedList') ? 'primary' : undefined"
        :title="t('operationCore.profile.comments.format.orderedList')"
        @click="editor?.chain().focus().toggleOrderedList().run()"
      />
      <v-divider vertical inset class="mx-1" />
      <v-menu v-model="emojiMenu" :close-on-content-click="false" location="top">
        <template #activator="{ props: menuProps }">
          <v-btn
            size="x-small"
            variant="text"
            icon="mdi-emoticon-happy-outline"
            :disabled="!editor"
            :title="t('operationCore.profile.comments.format.emoji')"
            v-bind="menuProps"
          />
        </template>
        <v-card rounded="lg" class="pa-2 oc-rich-text-editor__emoji-grid" elevation="6">
          <button
            v-for="emoji in emojis"
            :key="emoji"
            type="button"
            class="oc-rich-text-editor__emoji-btn"
            @click="insertEmoji(emoji)"
          >
            {{ emoji }}
          </button>
        </v-card>
      </v-menu>
    </div>

    <div class="oc-rich-text-editor__field" :style="proseStyle">
      <editor-content v-if="editor" :editor="editor" />
      <div v-else class="pa-3 text-medium-emphasis text-body-2">{{ placeholder }}</div>
    </div>
  </v-card>
</template>

<style scoped>
.oc-rich-text-editor__field {
  position: relative;
}

.oc-rich-text-editor :deep(.oc-rich-text-editor__prose) {
  max-height: 320px;
  overflow: auto;
  outline: none;
  padding: 10px 12px;
}

.oc-rich-text-editor :deep(.oc-rich-text-editor__prose p) {
  margin: 0 0 0.4em;
}

.oc-rich-text-editor :deep(.oc-rich-text-editor__prose ul),
.oc-rich-text-editor :deep(.oc-rich-text-editor__prose ol) {
  padding-left: 1.25rem;
  margin: 0.25em 0;
}

.oc-rich-text-editor :deep(.ProseMirror p.is-editor-empty:first-child::before) {
  color: rgba(var(--v-theme-on-surface), 0.45);
  content: attr(data-placeholder);
  float: left;
  height: 0;
  pointer-events: none;
}

.oc-rich-text-editor__emoji-grid {
  display: grid;
  grid-template-columns: repeat(8, 1fr);
  gap: 2px;
  max-width: 280px;
}

.oc-rich-text-editor__emoji-btn {
  font-size: 1.15rem;
  line-height: 1;
  padding: 4px;
  border-radius: 6px;
  cursor: pointer;
  background: transparent;
  border: none;
}

.oc-rich-text-editor__emoji-btn:hover {
  background: rgba(var(--v-theme-primary), 0.12);
}
</style>
