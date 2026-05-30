<script setup lang="ts">
import { computed } from 'vue';
import type { Ref } from 'vue';
import type { OcFormRuntimeContext } from '@/types/apps/operationCore';
import { useOcDynamicFormLookups } from '@/composables/useOcDynamicFormLookups';
import OcDynamicFormField from '@/components/apps/operation-core/OcDynamicFormField.vue';

const props = defineProps<{
  context: OcFormRuntimeContext;
  /** Toplanacak zorunlu alan anahtarları (transition.requiredFields). */
  fieldKeys: string[];
}>();

const model = defineModel<Record<string, unknown>>({ required: true });

/** Sadece zorunlu alanları içeren daraltılmış context — lookup'lar yalnız bunlar için yüklenir. */
const subContext = computed<OcFormRuntimeContext>(() => {
  const fields: OcFormRuntimeContext['fields'] = {};
  for (const key of props.fieldKeys) {
    const meta = props.context.fields[key];
    if (meta) fields[key] = meta;
  }
  return { ...props.context, layout: null, fields };
});

const renderKeys = computed(() => props.fieldKeys.filter((key) => !!props.context.fields[key]));

const workspaceId = computed(() => props.context.workspaceId);

const { selectItemsForField, isLoadingField, isPersonField, pickerForField } = useOcDynamicFormLookups(
  workspaceId,
  subContext as unknown as Ref<OcFormRuntimeContext | null>,
  model
);

function behaviorFor(key: string) {
  const b = props.context.fieldBehaviors[key];
  return {
    visible: true,
    required: true,
    readonly: false,
    masked: b?.masked === true,
  };
}

function setFieldValue(key: string, value: unknown) {
  model.value = { ...model.value, [key]: value };
}
</script>

<template>
  <v-row dense>
    <v-col v-for="fieldKey in renderKeys" :key="fieldKey" cols="12">
      <OcDynamicFormField
        :model-value="model[fieldKey]"
        :field-key="fieldKey"
        :meta="context.fields[fieldKey]"
        :behavior="behaviorFor(fieldKey)"
        :select-items="selectItemsForField(fieldKey)"
        :select-loading="isLoadingField(fieldKey)"
        :person-picker="isPersonField(fieldKey) ? pickerForField(fieldKey) : undefined"
        @update:model-value="(v) => setFieldValue(fieldKey, v)"
      />
    </v-col>
  </v-row>
</template>
