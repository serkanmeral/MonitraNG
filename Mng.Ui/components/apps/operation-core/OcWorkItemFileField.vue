<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { diCreateFileResource, diGetById } from '@/services/documentIntelligenceService';
import type { OcFormFieldRuntimeDto } from '@/types/apps/operationCore';

const props = defineProps<{
  fieldKey: string;
  meta?: OcFormFieldRuntimeDto | null;
  readonly?: boolean;
  disabled?: boolean;
  errorMessage?: string | null;
}>();

const model = defineModel<unknown>({ required: true });

const { t } = useAppI18n();
const inputRef = ref<HTMLInputElement | null>(null);
const uploading = ref(false);
const localError = ref<string | null>(null);
const fileLabel = ref('');

const folderId = computed(() => {
  const raw = props.meta?.options?.folderId;
  return typeof raw === 'string' ? raw.trim() : '';
});

const locked = computed(() => props.readonly === true || props.disabled === true);
const shownError = computed(() => localError.value || props.errorMessage || '');

const maxBytes = computed(() => {
  const raw = props.meta?.options?.maxSizeBytes;
  return typeof raw === 'number' && raw > 0 ? raw : 10 * 1024 * 1024;
});

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const value = String(reader.result || '');
      const comma = value.indexOf(',');
      resolve(comma >= 0 ? value.slice(comma + 1) : value);
    };
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}

async function loadLabel(id: string) {
  try {
    const resource = await diGetById(id);
    fileLabel.value = resource.fileName || resource.name || resource.title || id;
  } catch {
    fileLabel.value = id;
  }
}

watch(
  () => (typeof model.value === 'string' ? model.value.trim() : ''),
  (id) => {
    fileLabel.value = '';
    if (id) void loadLabel(id);
  },
  { immediate: true }
);

async function onFile(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file || locked.value || !folderId.value) return;
  if (file.size > maxBytes.value) {
    localError.value = t('operationCore.formUi.fileUpload.tooLarge', {
      max: Math.round(maxBytes.value / (1024 * 1024)),
    });
    return;
  }
  uploading.value = true;
  localError.value = null;
  try {
    const content = await fileToBase64(file);
    const dot = file.name.lastIndexOf('.');
    const created = await diCreateFileResource({
      parentId: folderId.value,
      name: file.name,
      originalFileName: file.name,
      content,
      mimeType: file.type || null,
      extension: dot >= 0 ? file.name.slice(dot + 1).toLowerCase() : null,
      size: file.size,
    });
    if (!created.id) throw new Error('empty');
    fileLabel.value = created.fileName || created.name || file.name;
    model.value = created.id;
  } catch {
    localError.value = t('operationCore.formUi.fileUpload.readError');
  } finally {
    uploading.value = false;
  }
}

function clearFile() {
  if (locked.value) return;
  model.value = null;
  fileLabel.value = '';
  localError.value = null;
}
</script>

<template>
  <p v-if="!folderId" class="text-body-2 text-medium-emphasis mb-0">
    {{ t('operationCore.formUi.fileUpload.libraryInstead') }}
  </p>
  <div v-else>
    <div class="d-flex flex-wrap align-center ga-2">
      <v-btn
        v-if="!locked"
        size="small"
        variant="tonal"
        class="text-none"
        :loading="uploading"
        @click="inputRef?.click()"
      >
        {{ t('documentIntelligence.uploadFile') }}
      </v-btn>
      <input ref="inputRef" type="file" class="d-none" :disabled="locked" @change="onFile" />
      <span v-if="fileLabel" class="text-body-2">{{ fileLabel }}</span>
      <v-btn
        v-if="fileLabel && !locked"
        size="small"
        variant="text"
        class="text-none"
        @click="clearFile"
      >
        {{ t('operationCore.formUi.fileUpload.remove') }}
      </v-btn>
    </div>
    <div v-if="shownError" class="text-caption text-error mt-1">{{ shownError }}</div>
  </div>
</template>
