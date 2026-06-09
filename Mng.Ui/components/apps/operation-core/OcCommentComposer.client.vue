<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref } from 'vue';
import { useEditor, EditorContent } from '@tiptap/vue-3';
import StarterKit from '@tiptap/starter-kit';
import { useOcPersonPicker } from '@/composables/useOcPersonPicker';
import { useAppI18n } from '@/composables/useAppI18n';
import { OC_RICH_TEXT_EMOJIS } from '@/utils/ocRichText';
import type { OcPersonPickerItem } from '@/utils/ocPersonPicker';

const props = withDefaults(
  defineProps<{
    placeholder: string;
    sendLabel: string;
    sending?: boolean;
    /** Düzenleme modu için başlangıç gövdesi (HTML). */
    initialHtml?: string;
    /** İptal butonunu göster (düzenleme modu). */
    showCancel?: boolean;
    /** Ek (dosya) butonunu göster — düzenlemede kapatılır. Varsayılan: true. */
    allowAttachments?: boolean;
  }>(),
  {
    // Vue boolean prop'ları yokken false'a düşer; ana composer'da ek butonu açık kalsın.
    allowAttachments: true,
  }
);

const emit = defineEmits<{
  (e: 'submit', payload: { html: string; mentions: string[]; files: File[] }): void;
  (e: 'cancel'): void;
}>();

const attachmentsEnabled = computed(() => props.allowAttachments !== false);

const { t } = useAppI18n();
const picker = useOcPersonPicker();
const { loading } = picker;

const pendingFiles = ref<File[]>([]);
const fileInputRef = ref<HTMLInputElement | null>(null);
const emojiMenu = ref(false);

const menuOpen = ref(false);
const mentionLen = ref(0);
/** display ad (lowercase) -> kişi id. Gönderimde gövdedeki @token'lar mention sayılır. */
const mentionMap = ref(new Map<string, string>());

// Hafif emoji paleti (yeni ağır bağımlılık olmadan).
const EMOJIS = OC_RICH_TEXT_EMOJIS;

const editor = useEditor({
  // Statik deploy / client-only sarmalayıcıda TipTap'ın sessizce çökmesini önler.
  immediatelyRender: false,
  extensions: [StarterKit],
  content: props.initialHtml && props.initialHtml.trim() ? props.initialHtml : '<p></p>',
  editable: true,
  editorProps: {
    attributes: { class: 'oc-comment-editor__prose text-body-2' },
  },
  onUpdate: () => {
    void nextTick(detectMention);
  },
  onSelectionUpdate: () => {
    void nextTick(detectMention);
  },
});

function plainText(): string {
  return editor.value?.getText() ?? '';
}

function isEmpty(): boolean {
  const ed = editor.value;
  if (!ed) return true;
  return ed.isEmpty || plainText().trim().length === 0;
}

function detectMention() {
  const ed = editor.value;
  if (!ed || ed.isDestroyed) {
    menuOpen.value = false;
    return;
  }
  const from = ed.state.selection.from;
  const textBefore = ed.state.doc.textBetween(0, from, '\n', '\n');
  const m = /(?:^|\s)@([\p{L}\p{N}._-]*)$/u.exec(textBefore);
  if (m) {
    mentionLen.value = m[1].length + 1; // @ + kelime
    menuOpen.value = true;
    void picker.resetAndFetch(m[1]);
  } else {
    menuOpen.value = false;
    mentionLen.value = 0;
  }
}

async function pickMention(item: OcPersonPickerItem) {
  if (picker.isLoadMoreValue(item.value)) {
    await picker.loadMore();
    return;
  }
  const ed = editor.value;
  if (!ed) return;
  const to = ed.state.selection.from;
  const fromPos = Math.max(0, to - mentionLen.value);
  const token = `@${item.title}`;
  ed.chain()
    .focus()
    .deleteRange({ from: fromPos, to })
    .insertContent(`${token} `)
    .run();
  mentionMap.value.set(item.title.toLowerCase(), item.value);
  menuOpen.value = false;
  mentionLen.value = 0;
}

function resolveMentions(): string[] {
  const body = plainText().toLowerCase();
  const ids: string[] = [];
  for (const [name, id] of mentionMap.value.entries()) {
    if (body.includes(`@${name}`)) ids.push(id);
  }
  return [...new Set(ids)];
}

function insertEmoji(emoji: string) {
  editor.value?.chain().focus().insertContent(emoji).run();
  emojiMenu.value = false;
}

function onKeydown(e: KeyboardEvent) {
  if (menuOpen.value && e.key === 'Escape') {
    menuOpen.value = false;
    e.stopPropagation();
    return;
  }
  if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
    e.preventDefault();
    doSubmit();
  }
}

function triggerFilePick() {
  fileInputRef.value?.click();
}

function onFilesPicked(e: Event) {
  const input = e.target as HTMLInputElement;
  const picked = Array.from(input.files ?? []);
  if (picked.length) pendingFiles.value = [...pendingFiles.value, ...picked];
  input.value = '';
}

function removeFile(index: number) {
  pendingFiles.value = pendingFiles.value.filter((_, i) => i !== index);
}

function normalizeHtml(html: string): string {
  const stripped = html.replace(/\s/g, '');
  if (stripped === '<p></p>' || stripped === '<p><br></p>' || stripped === '') return '';
  return html;
}

function doSubmit() {
  const empty = isEmpty();
  if ((empty && pendingFiles.value.length === 0) || props.sending) return;
  const html = empty ? '' : normalizeHtml(editor.value?.getHTML() ?? '');
  emit('submit', { html, mentions: resolveMentions(), files: [...pendingFiles.value] });
}

function reset() {
  editor.value?.commands.clearContent(true);
  pendingFiles.value = [];
  mentionMap.value = new Map();
  menuOpen.value = false;
  mentionLen.value = 0;
}

defineExpose({ reset });

onBeforeUnmount(() => {
  editor.value?.destroy();
});

const menuItems = computed<OcPersonPickerItem[]>(() =>
  picker.itemsWithLoadMoreRow(t('operationCore.profile.comments.loadMore'), '')
);

const canSend = computed(() => !isEmpty() || pendingFiles.value.length > 0);
</script>

<template>
  <div class="oc-comment-composer">
    <v-card variant="outlined" rounded="lg" class="oc-comment-composer__card overflow-visible">
      <div v-if="editor" class="oc-comment-composer__toolbar d-flex flex-wrap align-center ga-1 px-2 py-1 border-b">
        <v-btn
          size="x-small"
          variant="text"
          :color="editor.isActive('bold') ? 'primary' : undefined"
          :title="t('operationCore.profile.comments.format.bold')"
          @click="editor.chain().focus().toggleBold().run()"
        >
          <strong>B</strong>
        </v-btn>
        <v-btn
          size="x-small"
          variant="text"
          :color="editor.isActive('italic') ? 'primary' : undefined"
          :title="t('operationCore.profile.comments.format.italic')"
          @click="editor.chain().focus().toggleItalic().run()"
        >
          <em>I</em>
        </v-btn>
        <v-btn
          size="x-small"
          variant="text"
          :color="editor.isActive('strike') ? 'primary' : undefined"
          :title="t('operationCore.profile.comments.format.strike')"
          @click="editor.chain().focus().toggleStrike().run()"
        >
          <span style="text-decoration: line-through">S</span>
        </v-btn>
        <v-divider vertical inset class="mx-1" />
        <v-btn
          size="x-small"
          variant="text"
          icon="mdi-format-list-bulleted"
          :color="editor.isActive('bulletList') ? 'primary' : undefined"
          :title="t('operationCore.profile.comments.format.bulletList')"
          @click="editor.chain().focus().toggleBulletList().run()"
        />
        <v-btn
          size="x-small"
          variant="text"
          icon="mdi-format-list-numbered"
          :color="editor.isActive('orderedList') ? 'primary' : undefined"
          :title="t('operationCore.profile.comments.format.orderedList')"
          @click="editor.chain().focus().toggleOrderedList().run()"
        />
        <v-divider vertical inset class="mx-1" />
        <v-menu v-model="emojiMenu" :close-on-content-click="false" location="top">
          <template #activator="{ props: menuProps }">
            <v-btn
              size="x-small"
              variant="text"
              icon="mdi-emoticon-happy-outline"
              :title="t('operationCore.profile.comments.format.emoji')"
              v-bind="menuProps"
            />
          </template>
          <v-card rounded="lg" class="pa-2 oc-emoji-grid" elevation="6">
            <button
              v-for="emoji in EMOJIS"
              :key="emoji"
              type="button"
              class="oc-emoji-btn"
              @click="insertEmoji(emoji)"
            >
              {{ emoji }}
            </button>
          </v-card>
        </v-menu>
      </div>

      <div class="oc-comment-composer__field" @keydown="onKeydown">
        <editor-content v-if="editor" :editor="editor" />
        <div v-else class="pa-3 text-medium-emphasis text-body-2">{{ placeholder }}</div>

        <v-card v-if="menuOpen" class="oc-mention-menu" elevation="6" rounded="lg">
          <v-list density="compact" class="py-1" max-height="240">
            <template v-if="menuItems.length">
              <v-list-item
                v-for="item in menuItems"
                :key="item.value"
                :title="item.title"
                :subtitle="item.subtitle"
                class="oc-mention-item"
                @mousedown.prevent="pickMention(item)"
              >
                <template #prepend>
                  <v-icon icon="mdi-account" size="18" class="mr-1" />
                </template>
              </v-list-item>
            </template>
            <v-list-item
              v-else
              :title="loading ? t('operationCore.profile.comments.searching') : t('operationCore.profile.comments.noPeople')"
              class="text-medium-emphasis"
            />
          </v-list>
        </v-card>
      </div>
    </v-card>

    <input
      ref="fileInputRef"
      type="file"
      multiple
      class="d-none"
      @change="onFilesPicked"
    >

    <div v-if="pendingFiles.length" class="d-flex flex-wrap ga-2 mt-2">
      <v-chip
        v-for="(file, idx) in pendingFiles"
        :key="idx"
        size="small"
        variant="tonal"
        rounded="lg"
        closable
        prepend-icon="mdi-paperclip"
        @click:close="removeFile(idx)"
      >
        {{ file.name }}
      </v-chip>
    </div>

    <div class="d-flex align-center justify-space-between mt-2 ga-2">
      <span class="text-caption text-medium-emphasis">
        {{ t('operationCore.profile.comments.mentionHint') }}
      </span>
      <div class="d-flex align-center ga-2">
        <v-btn
          v-if="attachmentsEnabled"
          variant="text"
          size="small"
          rounded="lg"
          class="text-none"
          prepend-icon="mdi-paperclip"
          :disabled="sending"
          @click="triggerFilePick"
        >
          {{ t('operationCore.profile.comments.attach') }}
        </v-btn>
        <v-btn
          v-if="showCancel"
          variant="text"
          size="small"
          rounded="lg"
          class="text-none"
          :disabled="sending"
          @click="emit('cancel')"
        >
          {{ t('operationCore.definitions.cancel') }}
        </v-btn>
        <v-btn
          color="primary"
          size="small"
          variant="flat"
          rounded="lg"
          class="text-none"
          :loading="sending"
          :disabled="!canSend"
          prepend-icon="mdi-send"
          @click="doSubmit"
        >
          {{ sendLabel }}
        </v-btn>
      </div>
    </div>
  </div>
</template>

<style scoped>
.oc-comment-composer__field {
  position: relative;
}

.oc-comment-composer :deep(.oc-comment-editor__prose) {
  min-height: 72px;
  max-height: 280px;
  overflow: auto;
  outline: none;
  padding: 10px 12px;
}

.oc-comment-composer :deep(.oc-comment-editor__prose p) {
  margin: 0 0 0.4em;
}

.oc-comment-composer :deep(.oc-comment-editor__prose ul),
.oc-comment-composer :deep(.oc-comment-editor__prose ol) {
  padding-left: 1.25rem;
  margin: 0.25em 0;
}

.oc-mention-menu {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  margin-top: 4px;
  z-index: 20;
  overflow: hidden;
}

.oc-mention-item {
  cursor: pointer;
}

.oc-emoji-grid {
  display: grid;
  grid-template-columns: repeat(8, 1fr);
  gap: 2px;
  max-width: 280px;
}

.oc-emoji-btn {
  font-size: 1.15rem;
  line-height: 1;
  padding: 4px;
  border-radius: 6px;
  cursor: pointer;
  background: transparent;
  border: none;
}

.oc-emoji-btn:hover {
  background: rgba(var(--v-theme-primary), 0.12);
}
</style>
