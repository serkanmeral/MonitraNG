<script setup lang="ts">
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcFormFieldRuntimeDto } from '@/types/apps/operationCore';

defineProps<{
  fieldKey: string;
  meta?: OcFormFieldRuntimeDto | null;
  readonly?: boolean;
  disabled?: boolean;
  errorMessage?: string | null;
}>();

defineModel<unknown>({ required: true });

const { t } = useAppI18n();
</script>

<template>
  <p class="text-body-2 text-medium-emphasis mb-0">
    {{ t('operationCore.formUi.fileUpload.libraryInstead') }}
  </p>
</template>
