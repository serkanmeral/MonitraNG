<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcFormFieldRuntimeDto } from '@/types/apps/operationCore';
import { isMultiCardinality } from '@/utils/ocDynamicFormField';
import { parseOcFileFieldOptions } from '@/utils/ocFileFieldOptions';
import {
  collectOcFileUploadPayloads,
  type OcFileUploadPayload,
} from '@/utils/ocWorkItemFileFields';

const props = defineProps<{
  fieldKey: string;
  meta?: OcFormFieldRuntimeDto | null;
  readonly?: boolean;
  disabled?: boolean;
  errorMessage?: string | null;
}>();

const model = defineModel<unknown>({ required: true });

const { t } = useAppI18n();

const fileInput = ref<HTMLInputElement | null>(null);
const localError = ref<string | null>(null);
const busy = ref(false);

const fileOptions = computed(() => parseOcFileFieldOptions(props.meta?.options));
const isMulti = computed(() => isMultiCardinality(props.fieldKey, props.meta));
const payloads = computed(() => collectOcFileUploadPayloads(model.value));
const fieldDisabled = computed(() => props.readonly === true || props.disabled === true);

const acceptAttr = computed(() => {
  if (!fileOptions.value.allowedExtensions.length) return undefined;
  return fileOptions.value.allowedExtensions.join(',');
});

const hintText = computed(() => {
  const maxMb = (fileOptions.value.maxSizeBytes / (1024 * 1024)).toFixed(0);
  if (fileOptions.value.allowedExtensions.length) {
    return t('operationCore.formUi.fileUpload.hintWithTypes', {
      max: maxMb,
      types: fileOptions.value.allowedExtensions.join(', '),
    });
  }
  return t('operationCore.formUi.fileUpload.hintAnyType', { max: maxMb });
});

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result ?? '');
      const comma = result.indexOf(',');
      resolve(comma >= 0 ? result.slice(comma + 1) : result);
    };
    reader.onerror = () => reject(reader.error ?? new Error('read failed'));
    reader.readAsDataURL(file);
  });
}

function validateFile(file: File): string | null {
  if (file.size > fileOptions.value.maxSizeBytes) {
    return t('operationCore.formUi.fileUpload.tooLarge', {
      max: (fileOptions.value.maxSizeBytes / (1024 * 1024)).toFixed(0),
    });
  }
  if (fileOptions.value.allowedExtensions.length) {
    const ext = `.${file.name.split('.').pop()?.toLowerCase() ?? ''}`;
    if (!fileOptions.value.allowedExtensions.includes(ext)) {
      return t('operationCore.formUi.fileUpload.typeNotAllowed', {
        types: fileOptions.value.allowedExtensions.join(', '),
      });
    }
  }
  return null;
}

async function ingestFiles(files: FileList | File[]) {
  if (fieldDisabled.value) return;
  localError.value = null;
  const list = Array.from(files as ArrayLike<File>);
  if (!list.length) return;

  busy.value = true;
  try {
    const next: OcFileUploadPayload[] = isMulti.value ? [...payloads.value] : [];
    for (const file of list) {
      const validationError = validateFile(file);
      if (validationError) {
        localError.value = validationError;
        return;
      }
      next.push({
        content: await fileToBase64(file),
        originalFileName: file.name,
      });
      if (!isMulti.value) break;
    }
    model.value = isMulti.value ? next : (next[0] ?? null);
  } catch {
    localError.value = t('operationCore.formUi.fileUpload.readError');
  } finally {
    busy.value = false;
    if (fileInput.value) fileInput.value.value = '';
  }
}

function openPicker() {
  if (fieldDisabled.value) return;
  fileInput.value?.click();
}

function onInputChange(event: Event) {
  const input = event.target as HTMLInputElement;
  if (input.files?.length) void ingestFiles(input.files);
}

function onDrop(event: DragEvent) {
  event.preventDefault();
  if (fieldDisabled.value) return;
  const files = event.dataTransfer?.files;
  if (files?.length) void ingestFiles(files);
}

function onDragOver(event: DragEvent) {
  event.preventDefault();
}

function removeAt(index: number) {
  if (fieldDisabled.value) return;
  if (isMulti.value) {
    const next = [...payloads.value];
    next.splice(index, 1);
    model.value = next.length ? next : null;
  } else {
    model.value = null;
  }
}

const showError = computed(() => Boolean(props.errorMessage || localError.value));
const errorText = computed(() => props.errorMessage || localError.value || '');
</script>

<template>
  <div class="oc-work-item-file-field">
    <input
      ref="fileInput"
      type="file"
      class="d-none"
      :accept="acceptAttr"
      :multiple="isMulti"
      :disabled="fieldDisabled || busy"
      @change="onInputChange"
    />

    <div
      v-if="!fieldDisabled"
      class="oc-work-item-file-field__drop rounded-lg pa-4 text-center"
      :class="{ 'oc-work-item-file-field__drop--busy': busy }"
      @click="openPicker"
      @drop="onDrop"
      @dragover="onDragOver"
    >
      <v-icon icon="mdi-cloud-upload-outline" size="28" color="primary" class="mb-2" />
      <p class="text-body-2 mb-1">{{ t('operationCore.formUi.fileUpload.dropHint') }}</p>
      <p class="text-caption text-medium-emphasis mb-0">{{ hintText }}</p>
      <v-progress-linear v-if="busy" indeterminate color="primary" class="mt-3" />
    </div>

    <v-list v-if="payloads.length" density="compact" class="oc-work-item-file-field__list mt-2 rounded-lg">
      <v-list-item v-for="(item, index) in payloads" :key="`${item.originalFileName}-${index}`">
        <template #prepend>
          <v-icon icon="mdi-paperclip" size="20" />
        </template>
        <v-list-item-title class="text-body-2">{{ item.originalFileName }}</v-list-item-title>
        <template v-if="!fieldDisabled" #append>
          <v-btn
            icon
            variant="text"
            size="small"
            :aria-label="t('operationCore.formUi.fileUpload.remove')"
            @click.stop="removeAt(index)"
          >
            <v-icon icon="mdi-close" size="18" />
          </v-btn>
        </template>
      </v-list-item>
    </v-list>

    <p v-else-if="fieldDisabled" class="text-body-2 text-medium-emphasis mb-0">—</p>

    <p v-if="showError" class="text-caption text-error mt-1 mb-0">{{ errorText }}</p>
  </div>
</template>

<style scoped>
.oc-work-item-file-field__drop {
  border: 1px dashed rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgba(var(--v-theme-on-surface), 0.02);
  cursor: pointer;
}

.oc-work-item-file-field__drop--busy {
  pointer-events: none;
  opacity: 0.85;
}

.oc-work-item-file-field__list {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
