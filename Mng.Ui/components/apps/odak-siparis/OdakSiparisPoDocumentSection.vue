<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  listPoDocumentEntries,
  type PoDocumentEntry,
} from '@/utils/odakSiparisPoService';
import { DownloadIcon, EyeIcon, TrashIcon, UploadIcon } from 'vue-tabler-icons';

const props = defineProps<{
  title: string;
  hint?: string;
  modelValue: unknown;
  readonly?: boolean;
  packageNo?: string;
  keyPrefix: string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: unknown];
  preview: [entry: PoDocumentEntry];
  download: [entry: PoDocumentEntry];
  addFiles: [files: FileList];
}>();

const { t } = useAppI18n();

const fileEntries = computed(() =>
  listPoDocumentEntries(props.modelValue, props.packageNo, props.keyPrefix)
);

function onFileInput(event: Event) {
  const input = event.target as HTMLInputElement;
  const files = input.files;
  input.value = '';
  if (!files?.length || props.readonly) return;
  emit('addFiles', files);
}

function removeEntry(entry: PoDocumentEntry) {
  if (props.readonly) return;
  const remaining = fileEntries.value.filter((e) => e.key !== entry.key);
  if (!remaining.length) {
    emit('update:modelValue', null);
    return;
  }
  emit(
    'update:modelValue',
    remaining.map((e) => (e.isPending && e.pending ? e.pending : e.raw))
  );
}
</script>

<template>
  <div class="odak-po-section border rounded-md">
    <div class="odak-po-section__header d-flex align-center flex-wrap ga-2 px-3 py-2">
      <span class="text-body-2 font-weight-medium">{{ title }}</span>
      <v-spacer />
      <label v-if="!readonly" class="d-inline-flex">
        <input
          type="file"
          accept=".pdf,application/pdf"
          multiple
          class="d-none"
          @change="onFileInput"
        />
        <v-btn size="x-small" variant="tonal" color="primary" tag="span" role="button">
          <UploadIcon size="14" class="mr-1" />
          {{ t('odakSiparis.po.addFiles') }}
        </v-btn>
      </label>
    </div>

    <div v-if="hint" class="text-caption text-medium-emphasis px-3 pb-2">{{ hint }}</div>

    <v-list v-if="fileEntries.length" density="compact" class="py-0 bg-transparent">
      <v-list-item
        v-for="entry in fileEntries"
        :key="entry.key"
        rounded="0"
        class="odak-po-section__row"
      >
        <template #prepend>
          <v-icon icon="mdi-file-pdf-box" color="error" size="20" />
        </template>
        <v-list-item-title class="text-body-2 text-wrap">{{ entry.fileName }}</v-list-item-title>
        <v-list-item-subtitle class="text-caption">
          {{ entry.isPending ? t('odakSiparis.po.pendingUpload') : t('odakSiparis.po.storedFile') }}
        </v-list-item-subtitle>
        <template #append>
          <div class="d-flex align-center ga-1">
            <v-btn
              icon
              size="x-small"
              variant="text"
              color="primary"
              :title="t('odakSiparis.po.preview')"
              @click="emit('preview', entry)"
            >
              <EyeIcon size="16" />
            </v-btn>
            <v-btn
              icon
              size="x-small"
              variant="text"
              :title="t('odakSiparis.po.download')"
              @click="emit('download', entry)"
            >
              <DownloadIcon size="16" />
            </v-btn>
            <v-btn
              v-if="!readonly"
              icon
              size="x-small"
              variant="text"
              color="error"
              :title="t('odakSiparis.po.remove')"
              @click="removeEntry(entry)"
            >
              <TrashIcon size="16" />
            </v-btn>
          </div>
        </template>
      </v-list-item>
    </v-list>

    <div v-else class="odak-po-section__empty pa-3 text-center">
      <p class="text-caption text-medium-emphasis mb-0">{{ t('odakSiparis.po.noDocument') }}</p>
    </div>
  </div>
</template>

<style scoped>
.odak-po-section {
  background: rgba(var(--v-theme-surface-variant), 0.25);
}

.odak-po-section__header {
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.odak-po-section__row {
  border-bottom: 1px solid rgba(var(--v-border-color), calc(var(--v-border-opacity) * 0.6));
}

.odak-po-section__row:last-child {
  border-bottom: none;
}

.odak-po-section__empty {
  background: rgba(var(--v-theme-on-surface), 0.02);
}
</style>
