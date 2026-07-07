<script setup lang="ts">
import { useAppI18n } from '@/composables/useAppI18n';

defineProps<{
  modelValue: boolean;
  saving?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  save: [];
  discard: [];
}>();

const { t } = useAppI18n();

function close() {
  emit('update:modelValue', false);
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="440" persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center ga-2 text-subtitle-1 font-weight-bold">
        <v-icon icon="mdi-content-save-alert-outline" color="warning" />
        {{ t('documentIntelligence.editorClose.title') }}
      </v-card-title>
      <v-card-text class="text-body-2">
        {{ t('documentIntelligence.editorClose.message') }}
      </v-card-text>
      <v-card-actions class="px-4 pb-4 flex-wrap ga-1">
        <v-spacer />
        <v-btn variant="text" class="text-none" :disabled="saving" @click="close">
          {{ t('documentIntelligence.cancel') }}
        </v-btn>
        <v-btn variant="tonal" color="warning" class="text-none" :disabled="saving" @click="emit('discard')">
          {{ t('documentIntelligence.editorClose.discard') }}
        </v-btn>
        <v-btn color="primary" variant="flat" class="text-none" :loading="saving" @click="emit('save')">
          {{ t('documentIntelligence.editorClose.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
