<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import DiMarkdownViewer from '@/components/apps/document-intelligence/DiMarkdownViewer.vue';
import DiResourcePreviewProvider from '@/components/apps/document-intelligence/DiResourcePreviewProvider.vue';
import DiPickResourceDialog from '@/components/apps/document-intelligence/DiPickResourceDialog.vue';
import { buildDiResourceMarkdownHref, buildDiFilePathMarkdownHref } from '@/utils/diResourceLink';
import {
  DI_PAGE_TEMPLATE_DEFINITIONS,
  getDiPageTemplateContent,
  type DiPageTemplateId,
} from '@/utils/diPageTemplates';
import { diCreateFileResource } from '@/services/documentIntelligenceService';
import { DI_RESOURCE_TYPE, type DiResource } from '@/types/apps/documentIntelligence';

const MAX_IMAGE_MB = 8;

const props = withDefaults(
  defineProps<{
    modelValue: string;
    showPreview?: boolean;
    /** Düzenlenen dokümanın kendisine link vermeyi engelle. */
    currentResourceId?: string | null;
    /** Görsel yükleme hedef klasörü (markdown parentId). */
    uploadParentId?: string | null;
    canUpload?: boolean;
  }>(),
  { modelValue: '', showPreview: true, currentResourceId: null, uploadParentId: null, canUpload: false }
);

const emit = defineEmits<{ 'update:modelValue': [string] }>();

const { t } = useAppI18n();
const textareaRef = ref<HTMLTextAreaElement | null>(null);
const localPreview = ref(props.showPreview);
const pickDialogOpen = ref(false);
const fullscreen = ref(false);
const imageInputRef = ref<HTMLInputElement | null>(null);
const imageUploading = ref(false);
const imageUploadError = ref<string | null>(null);
const templateMenuOpen = ref(false);
const pendingTemplateId = ref<DiPageTemplateId | null>(null);
const templateReplaceDialog = ref(false);

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

watch(fullscreen, (v) => {
  if (v) {
    localPreview.value = true;
    document.body.style.overflow = 'hidden';
  } else {
    document.body.style.overflow = '';
  }
});

function resourceLabel(r: DiResource): string {
  return r.type === DI_RESOURCE_TYPE.markdown ? r.title || r.name : r.name;
}

function getSelectionContext() {
  const el = textareaRef.value;
  const value = content.value;
  if (!el) {
    const pos = value.length;
    return { value, start: pos, end: pos, el: null as HTMLTextAreaElement | null };
  }
  return {
    value,
    start: el.selectionStart ?? value.length,
    end: el.selectionEnd ?? value.length,
    el,
  };
}

function applyEdit(next: string, cursorStart: number, cursorEnd: number) {
  content.value = next;
  void nextTick(() => {
    const el = textareaRef.value;
    if (!el) return;
    el.focus();
    el.setSelectionRange(cursorStart, cursorEnd);
  });
}

function surround(before: string, after = before) {
  const { value, start, end } = getSelectionContext();
  const selected = value.slice(start, end);
  const next = `${value.slice(0, start)}${before}${selected}${after}${value.slice(end)}`;
  const pos = start + before.length + selected.length + after.length;
  applyEdit(next, pos, pos);
}

function insertMarkdownLink(label: string, href: string) {
  const { value, start, end } = getSelectionContext();
  const selected = value.slice(start, end);
  const text = selected || label;
  const snippet = `[${text}](${href})`;
  const next = `${value.slice(0, start)}${snippet}${value.slice(end)}`;
  applyEdit(next, start + snippet.length, start + snippet.length);
}

function insertBlock(snippet: string, selectStart?: number, selectEnd?: number) {
  const { value, start, end } = getSelectionContext();
  const next = `${value.slice(0, start)}${snippet}${value.slice(end)}`;
  if (selectStart != null && selectEnd != null) {
    applyEdit(next, selectStart, selectEnd);
  } else {
    const pos = start + snippet.length;
    applyEdit(next, pos, pos);
  }
}

function setHeading(level: 1 | 2 | 3 | 4) {
  const prefix = `${'#'.repeat(level)} `;
  const { value, start, end } = getSelectionContext();
  const lineStart = value.lastIndexOf('\n', start - 1) + 1;
  const lineEndIdx = value.indexOf('\n', end);
  const lineEnd = lineEndIdx === -1 ? value.length : lineEndIdx;
  const line = value.slice(lineStart, lineEnd);
  const stripped = line.replace(/^#{1,6}\s*/, '');
  const newLine = stripped.length ? `${prefix}${stripped}` : prefix.trimEnd();
  const next = `${value.slice(0, lineStart)}${newLine}${value.slice(lineEnd)}`;
  applyEdit(next, lineStart + prefix.length, lineStart + prefix.length);
}

function prefixSelectedLines(prefix: string) {
  const { value, start, end } = getSelectionContext();
  const blockStart = value.lastIndexOf('\n', start - 1) + 1;
  const blockEndIdx = value.indexOf('\n', end);
  const blockEnd = blockEndIdx === -1 ? value.length : blockEndIdx;
  const block = value.slice(blockStart, blockEnd);

  if (!block) {
    const next = `${value.slice(0, start)}${prefix}${value.slice(start)}`;
    applyEdit(next, start + prefix.length, start + prefix.length);
    return;
  }

  const lines = block.split('\n');
  const prefixed = lines.map((line) => (line.length ? `${prefix}${line}` : line)).join('\n');
  const next = `${value.slice(0, blockStart)}${prefixed}${value.slice(blockEnd)}`;
  applyEdit(next, blockStart + prefixed.length, blockStart + prefixed.length);
}

function insertCodeBlock() {
  const { value, start, end } = getSelectionContext();
  const selected = value.slice(start, end);
  const body = selected || t('documentIntelligence.editor.codeBlockPlaceholder');
  const lead = value.slice(0, start).endsWith('\n') || start === 0 ? '' : '\n';
  const trail = value.slice(end).startsWith('\n') ? '' : '\n';
  const snippet = `${lead}\`\`\`\n${body}\n\`\`\`${trail}`;
  const bodyStart = start + lead.length + 4;
  applyEdit(`${value.slice(0, start)}${snippet}${value.slice(end)}`, bodyStart, bodyStart + body.length);
}

function insertTable(cols = 3, rows = 3) {
  const headerLabel = t('documentIntelligence.editor.tableHeader');
  const cellLabel = t('documentIntelligence.editor.tableCell');
  const headers = Array.from({ length: cols }, (_, i) =>
    i === 0 ? headerLabel : `${cellLabel} ${i + 1}`
  ).join(' | ');
  const separator = Array.from({ length: cols }, () => '---').join(' | ');
  const dataRows = Array.from({ length: Math.max(rows - 1, 1) }, (_, rowIdx) =>
    Array.from({ length: cols }, (_, colIdx) =>
      colIdx === 0 ? `${cellLabel} ${rowIdx + 1}` : ''
    ).join(' | ')
  );
  const snippet = `\n| ${headers} |\n| ${separator} |\n${dataRows.map((r) => `| ${r} |`).join('\n')}\n`;
  insertBlock(snippet);
}

function insertHorizontalRule() {
  insertBlock('\n\n---\n\n');
}

function insertImageMarkdown(alt: string, filePath: string) {
  const href = buildDiFilePathMarkdownHref(filePath);
  const snippet = `\n![${alt}](${href})\n`;
  insertBlock(snippet);
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result || '');
      resolve(result.includes(',') ? result.split(',')[1] : result);
    };
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}

function fileExtension(name: string): string {
  const i = name.lastIndexOf('.');
  return i >= 0 ? name.slice(i + 1).toLowerCase() : '';
}

function triggerImagePick() {
  imageUploadError.value = null;
  imageInputRef.value?.click();
}

async function onImagePick(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0] ?? null;
  if (input.value) input.value = '';
  if (!file) return;

  if (!file.type.startsWith('image/')) {
    imageUploadError.value = t('documentIntelligence.editor.imageTypeError');
    return;
  }
  if (file.size > MAX_IMAGE_MB * 1024 * 1024) {
    imageUploadError.value = t('documentIntelligence.editor.imageTooLarge', { max: MAX_IMAGE_MB });
    return;
  }

  imageUploading.value = true;
  imageUploadError.value = null;
  try {
    const fileContent = await fileToBase64(file);
    const created = await diCreateFileResource({
      parentId: props.uploadParentId ?? null,
      name: file.name,
      originalFileName: file.name,
      content: fileContent,
      mimeType: file.type || null,
      extension: fileExtension(file.name) || null,
      size: file.size,
    });
    if (!created.filePath) {
      throw new Error('filePath missing');
    }
    const alt = file.name.replace(/\.[^.]+$/, '') || file.name;
    insertImageMarkdown(alt, created.filePath);
  } catch {
    imageUploadError.value = t('documentIntelligence.editor.imageUploadFailed');
  } finally {
    imageUploading.value = false;
  }
}

const templateItems = computed(() =>
  DI_PAGE_TEMPLATE_DEFINITIONS.filter((item) => item.id !== 'blank').map((item) => ({
    id: item.id,
    title: t(item.labelKey),
  }))
);

function requestTemplate(id: DiPageTemplateId) {
  templateMenuOpen.value = false;
  if (id === 'blank') return;
  if (content.value.trim()) {
    pendingTemplateId.value = id;
    templateReplaceDialog.value = true;
    return;
  }
  applyTemplate(id);
}

function applyTemplate(id: DiPageTemplateId) {
  content.value = getDiPageTemplateContent(id);
  templateReplaceDialog.value = false;
  pendingTemplateId.value = null;
  void nextTick(() => textareaRef.value?.focus());
}

function confirmTemplateReplace() {
  if (pendingTemplateId.value) applyTemplate(pendingTemplateId.value);
}

function toggleFullscreen() {
  fullscreen.value = !fullscreen.value;
}

function onGlobalKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && fullscreen.value) {
    fullscreen.value = false;
  }
}

function onResourcePicked(resource: DiResource) {
  insertMarkdownLink(resourceLabel(resource), buildDiResourceMarkdownHref(resource.id));
}

const excludeResourceIds = computed(() => {
  const id = props.currentResourceId?.trim();
  return id ? [id] : [];
});

onMounted(() => {
  document.addEventListener('keydown', onGlobalKeydown);
});

onUnmounted(() => {
  document.removeEventListener('keydown', onGlobalKeydown);
  document.body.style.overflow = '';
});

defineExpose({
  focus: () => textareaRef.value?.focus(),
});
</script>

<template>
  <Teleport to="body" :disabled="!fullscreen">
    <div :class="['di-md-editor', { 'di-md-editor--fullscreen': fullscreen }]">
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
        <v-btn
          size="x-small"
          variant="text"
          icon="mdi-code-braces"
          :title="t('documentIntelligence.editor.codeBlock')"
          @click="insertCodeBlock"
        />
        <v-divider vertical inset class="mx-1" />
        <v-btn size="x-small" variant="text" :title="t('documentIntelligence.editor.h1')" @click="setHeading(1)">H1</v-btn>
        <v-btn size="x-small" variant="text" :title="t('documentIntelligence.editor.h2')" @click="setHeading(2)">H2</v-btn>
        <v-btn size="x-small" variant="text" :title="t('documentIntelligence.editor.h3')" @click="setHeading(3)">H3</v-btn>
        <v-btn size="x-small" variant="text" :title="t('documentIntelligence.editor.h4')" @click="setHeading(4)">H4</v-btn>
        <v-divider vertical inset class="mx-1" />
        <v-btn
          size="x-small"
          variant="text"
          icon="mdi-format-list-bulleted"
          :title="t('documentIntelligence.editor.bulletList')"
          @click="prefixSelectedLines('- ')"
        />
        <v-btn
          size="x-small"
          variant="text"
          icon="mdi-format-list-numbered"
          :title="t('documentIntelligence.editor.orderedList')"
          @click="prefixSelectedLines('1. ')"
        />
        <v-btn
          size="x-small"
          variant="text"
          icon="mdi-checkbox-marked-outline"
          :title="t('documentIntelligence.editor.checklist')"
          @click="prefixSelectedLines('- [ ] ')"
        />
        <v-btn
          size="x-small"
          variant="text"
          icon="mdi-format-quote-close"
          :title="t('documentIntelligence.editor.quote')"
          @click="prefixSelectedLines('> ')"
        />
        <v-btn
          size="x-small"
          variant="text"
          icon="mdi-table"
          :title="t('documentIntelligence.editor.table')"
          @click="insertTable()"
        />
        <v-btn
          size="x-small"
          variant="text"
          icon="mdi-minus"
          :title="t('documentIntelligence.editor.horizontalRule')"
          @click="insertHorizontalRule"
        />
        <v-btn size="x-small" variant="text" icon="mdi-link" :title="t('documentIntelligence.editor.link')" @click="surround('[', '](https://)')" />
        <v-btn
          size="x-small"
          variant="text"
          icon="mdi-file-link"
          :title="t('documentIntelligence.internalLink.linkToDocument')"
          @click="pickDialogOpen = true"
        />
        <v-btn
          v-if="canUpload"
          size="x-small"
          variant="text"
          icon="mdi-image-plus-outline"
          :title="t('documentIntelligence.editor.insertImage')"
          :loading="imageUploading"
          @click="triggerImagePick"
        />
        <input
          ref="imageInputRef"
          type="file"
          accept="image/*"
          class="d-none"
          @change="onImagePick"
        />
        <v-divider vertical inset class="mx-1" />
        <v-menu v-model="templateMenuOpen" location="bottom">
          <template #activator="{ props: menuProps }">
            <v-btn
              v-bind="menuProps"
              size="x-small"
              variant="text"
              icon="mdi-file-document-outline"
              :title="t('documentIntelligence.editor.insertTemplate')"
            />
          </template>
          <v-list density="compact" min-width="220">
            <v-list-item
              v-for="item in templateItems"
              :key="item.id"
              :title="item.title"
              @click="requestTemplate(item.id)"
            />
          </v-list>
        </v-menu>
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
        <v-btn
          size="x-small"
          variant="text"
          :color="fullscreen ? 'primary' : undefined"
          :icon="fullscreen ? 'mdi-fullscreen-exit' : 'mdi-fullscreen'"
          :title="fullscreen ? t('documentIntelligence.editor.exitFullscreen') : t('documentIntelligence.editor.fullscreen')"
          @click="toggleFullscreen"
        />
      </div>

      <v-alert
        v-if="imageUploadError"
        type="error"
        variant="tonal"
        density="compact"
        class="mx-2 mt-2 rounded-lg text-body-2"
        closable
        @click:close="imageUploadError = null"
      >
        {{ imageUploadError }}
      </v-alert>

      <div :class="['di-md-editor__body d-flex', { 'di-md-editor__body--split': localPreview }]">
        <textarea
          ref="textareaRef"
          v-model="content"
          class="di-md-editor__textarea"
          spellcheck="false"
          :placeholder="t('documentIntelligence.editor.placeholder')"
        />
        <DiResourcePreviewProvider v-if="localPreview" flat class="di-md-editor__preview">
          <DiMarkdownViewer :content="content" :empty-label="t('documentIntelligence.editor.previewEmpty')" />
        </DiResourcePreviewProvider>
      </div>

      <DiPickResourceDialog
        v-model="pickDialogOpen"
        :exclude-resource-ids="excludeResourceIds"
        @pick="onResourcePicked"
      />

      <v-dialog v-model="templateReplaceDialog" max-width="420">
        <v-card rounded="lg">
          <v-card-title class="text-subtitle-1 font-weight-bold">
            {{ t('documentIntelligence.editor.templateReplaceTitle') }}
          </v-card-title>
          <v-card-text class="text-body-2">
            {{ t('documentIntelligence.editor.templateReplaceHint') }}
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" class="text-none" @click="templateReplaceDialog = false">
              {{ t('documentIntelligence.cancel') }}
            </v-btn>
            <v-btn color="primary" variant="flat" class="text-none" @click="confirmTemplateReplace">
              {{ t('documentIntelligence.editor.templateReplaceConfirm') }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>
    </div>
  </Teleport>
</template>

<style scoped>
.di-md-editor {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.18);
  border-radius: 10px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  background: rgb(var(--v-theme-surface));
}
.di-md-editor--fullscreen {
  position: fixed;
  inset: 0;
  z-index: 2400;
  border-radius: 0;
  border: none;
}
.di-md-editor__body {
  flex: 1 1 auto;
  min-height: 320px;
}
.di-md-editor--fullscreen .di-md-editor__body {
  min-height: 0;
  max-height: none;
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
.di-md-editor--fullscreen .di-md-editor__textarea {
  min-height: 0;
  resize: none;
}
.di-md-editor__body--split .di-md-editor__textarea {
  border-right: 1px solid rgba(var(--v-theme-on-surface), 0.12);
}
.di-md-editor__preview {
  flex: 1 1 50%;
  padding: 14px 18px;
  overflow: auto;
  max-height: 600px;
  display: block;
}
.di-md-editor--fullscreen .di-md-editor__preview {
  max-height: none;
}
</style>
