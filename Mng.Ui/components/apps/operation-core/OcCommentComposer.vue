<script setup lang="ts">
import { computed, nextTick, ref } from 'vue';
import { useOcPersonPicker } from '@/composables/useOcPersonPicker';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcPersonPickerItem } from '@/utils/ocPersonPicker';

const props = defineProps<{
  placeholder: string;
  sendLabel: string;
  sending?: boolean;
}>();

const emit = defineEmits<{
  (e: 'submit', payload: { body: string; mentions: string[]; files: File[] }): void;
}>();

const { t } = useAppI18n();
const picker = useOcPersonPicker();
const { loading } = picker;

const text = ref('');
const pendingFiles = ref<File[]>([]);
const fileInputRef = ref<HTMLInputElement | null>(null);
// VTextarea bileşen ref'i (inner <textarea> için $el üzerinden erişiriz).
const textareaRef = ref<{ $el?: HTMLElement } | null>(null);
const menuOpen = ref(false);
const mentionStart = ref(-1);
/** display ad (lowercase) -> kişi id. Gönderimde gövdede hâlâ duran token'lar mention sayılır. */
const mentionMap = ref(new Map<string, string>());

function innerTextarea(): HTMLTextAreaElement | null {
  const root = textareaRef.value?.$el;
  return root?.querySelector('textarea') ?? null;
}

function detectMention() {
  const el = innerTextarea();
  const caret = el ? el.selectionStart ?? text.value.length : text.value.length;
  const upto = text.value.slice(0, caret);
  const m = /(?:^|\s)@([\p{L}\p{N}._-]*)$/u.exec(upto);
  if (m) {
    mentionStart.value = caret - m[1].length - 1;
    menuOpen.value = true;
    void picker.resetAndFetch(m[1]);
  } else {
    menuOpen.value = false;
    mentionStart.value = -1;
  }
}

function onInput(val: string) {
  text.value = val;
  void nextTick(detectMention);
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

async function pickMention(item: OcPersonPickerItem) {
  if (picker.isLoadMoreValue(item.value)) {
    await picker.loadMore();
    return;
  }
  const el = innerTextarea();
  const caret = el ? el.selectionStart ?? text.value.length : text.value.length;
  const before = text.value.slice(0, Math.max(0, mentionStart.value));
  const after = text.value.slice(caret);
  const token = `@${item.title}`;
  text.value = `${before}${token} ${after}`;
  mentionMap.value.set(item.title.toLowerCase(), item.value);
  menuOpen.value = false;
  mentionStart.value = -1;
  await nextTick();
  const pos = (before + token + ' ').length;
  el?.focus();
  el?.setSelectionRange(pos, pos);
}

function resolveMentions(): string[] {
  const body = text.value.toLowerCase();
  const ids: string[] = [];
  for (const [name, id] of mentionMap.value.entries()) {
    if (body.includes(`@${name}`)) ids.push(id);
  }
  return [...new Set(ids)];
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

function doSubmit() {
  const body = text.value.trim();
  if ((!body && pendingFiles.value.length === 0) || props.sending) return;
  emit('submit', { body, mentions: resolveMentions(), files: [...pendingFiles.value] });
}

function reset() {
  text.value = '';
  pendingFiles.value = [];
  mentionMap.value = new Map();
  menuOpen.value = false;
  mentionStart.value = -1;
}

defineExpose({ reset });

const menuItems = computed<OcPersonPickerItem[]>(() =>
  picker.itemsWithLoadMoreRow(t('operationCore.profile.comments.loadMore'), '')
);
</script>

<template>
  <div class="oc-comment-composer">
    <div class="oc-comment-composer__field">
      <v-textarea
        ref="textareaRef"
        :model-value="text"
        :label="placeholder"
        variant="outlined"
        rows="2"
        auto-grow
        hide-details
        density="comfortable"
        @update:model-value="onInput"
        @keydown="onKeydown"
      />
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
          color="primary"
          size="small"
          variant="flat"
          rounded="lg"
          class="text-none"
          :loading="sending"
          :disabled="!text.trim() && !pendingFiles.length"
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
</style>
