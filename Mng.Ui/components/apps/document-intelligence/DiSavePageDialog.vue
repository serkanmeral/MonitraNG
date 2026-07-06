<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

export type DiSavePageMode = 'save' | 'publish' | 'draft';

const props = defineProps<{
  modelValue: boolean;
  mode: DiSavePageMode;
  loading?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  confirm: [changeNote: string];
}>();

const { t } = useAppI18n();
const changeNote = ref('');

const titleKey = computed(() => {
  if (props.mode === 'publish') return 'documentIntelligence.saveDialog.titlePublish';
  if (props.mode === 'draft') return 'documentIntelligence.saveDialog.titleDraft';
  return 'documentIntelligence.saveDialog.titleSave';
});

const confirmKey = computed(() => {
  if (props.mode === 'publish') return 'documentIntelligence.publish';
  if (props.mode === 'draft') return 'documentIntelligence.saveAsDraft';
  return 'documentIntelligence.save';
});

watch(
  () => props.modelValue,
  (open) => {
    if (open) changeNote.value = '';
  }
);

function close() {
  emit('update:modelValue', false);
}

function submit() {
  emit('confirm', changeNote.value.trim());
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="480" @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="text-h6">{{ t(titleKey) }}</v-card-title>
      <v-card-text>
        <v-textarea
          v-model="changeNote"
          :label="t('documentIntelligence.saveDialog.changeNoteLabel')"
          :hint="t('documentIntelligence.saveDialog.changeNoteHint')"
          persistent-hint
          rows="3"
          auto-grow
          maxlength="500"
          counter
          variant="outlined"
          density="comfortable"
          hide-details="auto"
          class="mt-1"
          @keydown.ctrl.enter="submit"
          @keydown.meta.enter="submit"
        />
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" class="text-none" :disabled="loading" @click="close">
          {{ t('documentIntelligence.cancel') }}
        </v-btn>
        <v-btn color="primary" variant="flat" class="text-none" :loading="loading" @click="submit">
          {{ t(confirmKey) }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
