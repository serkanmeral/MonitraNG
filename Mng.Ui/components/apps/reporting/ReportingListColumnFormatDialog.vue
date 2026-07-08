<script setup lang="ts">
import { ref, watch } from 'vue';
import AfListColumnFormatEditor from '@/components/apps/automated-forms/AfListColumnFormatEditor.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  cloneAfListColumnFormat,
  emptyAfListColumnFormat,
  type AfListColumnFormat,
} from '@/utils/afListColumnFormat';
import type { OdakHubListColumnConfig } from '@/utils/odakSiparisHubListConfig';

const props = defineProps<{
  modelValue: boolean;
  column: OdakHubListColumnConfig | null;
  columnLabel: string;
  conditionFields: { value: string; title: string }[];
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  save: [format: AfListColumnFormat | undefined];
}>();

const { t } = useAppI18n();
const draft = ref<AfListColumnFormat>(emptyAfListColumnFormat());

watch(
  () => [props.modelValue, props.column?.fieldName] as const,
  ([open]) => {
    if (!open || !props.column) return;
    draft.value = cloneAfListColumnFormat(props.column.format);
  }
);

function close() {
  emit('update:modelValue', false);
}

function save() {
  const type = draft.value.type ?? 'none';
  emit('save', type === 'none' ? undefined : { ...draft.value, type });
  close();
}

function clearFormat() {
  draft.value = emptyAfListColumnFormat();
  emit('save', undefined);
  close();
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="720" scrollable @update:model-value="emit('update:modelValue', $event)">
    <v-card>
      <v-card-title class="d-flex align-center py-4 px-5">
        <span class="text-h6">
          {{ t('reporting.columns.formatTitle', { column: columnLabel }) }}
        </span>
        <v-spacer />
        <v-btn icon variant="text" size="small" @click="close">
          <v-icon>mdi-close</v-icon>
        </v-btn>
      </v-card-title>
      <v-divider />
      <v-card-text class="px-5 py-4">
        <AfListColumnFormatEditor
          v-if="column"
          v-model="draft"
          i18n-prefix="odakSiparis.packages.settings.formatting"
          :field-name="column.fieldName"
          :default-condition-field="column.fieldName"
          :condition-fields="conditionFields"
        />
      </v-card-text>
      <v-divider />
      <v-card-actions class="px-5 py-3">
        <v-btn variant="text" color="error" @click="clearFormat">
          {{ t('reporting.columns.clearFormat') }}
        </v-btn>
        <v-spacer />
        <v-btn variant="text" @click="close">{{ t('reporting.actions.cancel') }}</v-btn>
        <v-btn color="primary" variant="flat" @click="save">{{ t('reporting.actions.applyFormat') }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
