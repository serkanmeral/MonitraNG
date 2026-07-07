<script setup lang="ts">
import { ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

const props = defineProps<{
  modelValue: boolean;
  versionNumber: number | null;
  loading?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  confirm: [changeNote: string];
  skip: [];
}>();

const { t } = useAppI18n();
const changeNote = ref('');

watch(
  () => props.modelValue,
  (open) => {
    if (open) changeNote.value = '';
  },
);

function close() {
  emit('update:modelValue', false);
}

function submit() {
  emit('confirm', changeNote.value.trim());
}

function skip() {
  emit('skip');
  close();
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="480" @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="text-h6">
        {{ t('documentIntelligence.saveDialog.titleVersionNote') }}
        <span v-if="versionNumber" class="text-medium-emphasis text-body-2 ml-1">v{{ versionNumber }}</span>
      </v-card-title>
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
          autofocus
          @keydown.ctrl.enter="submit"
          @keydown.meta.enter="submit"
        />
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-btn variant="text" class="text-none" :disabled="loading" @click="skip">
          {{ t('documentIntelligence.saveDialog.skipNote') }}
        </v-btn>
        <v-spacer />
        <v-btn variant="text" class="text-none" :disabled="loading" @click="close">
          {{ t('documentIntelligence.cancel') }}
        </v-btn>
        <v-btn color="primary" variant="flat" class="text-none" :loading="loading" @click="submit">
          {{ t('documentIntelligence.saveDialog.saveNote') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
