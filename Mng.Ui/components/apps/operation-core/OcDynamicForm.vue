<script setup lang="ts">
import { computed, toRef } from 'vue';
import type { OcFormLayoutSectionRuntime, OcFormRuntimeContext } from '@/types/apps/operationCore';
import { fieldColSpanForLayout, sectionColSpanForLayout } from '@/utils/ocFormLayout';
import { useOcDynamicFormLookups } from '@/composables/useOcDynamicFormLookups';
import OcDynamicFormField from '@/components/apps/operation-core/OcDynamicFormField.vue';

const props = defineProps<{
  context: OcFormRuntimeContext;
  readonly?: boolean;
  preview?: boolean;
  /** Alan anahtarı → hata mesajı (submit doğrulaması sonrası). */
  fieldErrors?: Record<string, string>;
  /** Grup id → ad (readonly grup alanlarında ad göstermek için). */
  groupNames?: Record<string, string>;
  /** Alan key → çözülmüş görünen metin (MO profile-view); readonly'de lookup yerine kullanılır. */
  fieldDisplays?: Record<string, string>;
}>();

const model = defineModel<Record<string, unknown>>({ required: true });

const contextRef = toRef(props, 'context');
const workspaceId = computed(() => props.context.workspaceId);
const readonlyRef = computed(() => props.readonly === true);

const { selectItemsForField, isLoadingField, isPersonField, pickerForField, isDatasetPickerFieldForKey, datasetPickerForField, isFieldDisabledByDependsOn, selectPresentationForField } = useOcDynamicFormLookups(
  workspaceId,
  contextRef,
  model,
  { readonly: readonlyRef }
);

const sections = computed<OcFormLayoutSectionRuntime[]>(() => {
  if (props.context.layout?.sections?.length) {
    return props.context.layout.sections.filter((s) => s.fields.length > 0);
  }
  const keys = Object.keys(props.context.fields);
  return keys.length ? [{ key: 'main', title: null, cols: 12, fields: keys }] : [];
});

const formHeading = computed(() => props.context.layout?.formHeading?.trim() ?? '');
const formIntro = computed(() => props.context.layout?.formIntro?.trim() ?? '');

function behaviorFor(key: string) {
  const b = props.context.fieldBehaviors[key];
  return {
    visible: b?.visible !== false,
    required: b?.required === true,
    /** Yalnızca alan düzeyi; form salt okunurluğu `:readonly` ile ayrı iletilir. */
    readonly: b?.readonly === true,
    masked: b?.masked === true,
  };
}

function fieldMeta(key: string) {
  return props.context.fields[key];
}

function sectionSpan(section: OcFormLayoutSectionRuntime): number {
  return sectionColSpanForLayout(section.key, props.context.layout?.sectionCols, section);
}

function fieldMdCols(fieldKey: string): number {
  return fieldColSpanForLayout(fieldKey, props.context.layout?.fieldCols);
}

function setFieldValue(key: string, value: unknown) {
  if (props.readonly) return;
  model.value = { ...model.value, [key]: value };
}
</script>

<template>
  <div class="oc-dynamic-form" :class="{ 'oc-dynamic-form--preview': preview }">
    <header v-if="formHeading || formIntro" class="oc-dynamic-form__hero mb-5">
      <h3 v-if="formHeading" class="text-h6 font-weight-bold mb-1">
        {{ formHeading }}
      </h3>
      <p v-if="formIntro" class="text-body-2 text-medium-emphasis mb-0">
        {{ formIntro }}
      </p>
    </header>

    <div class="oc-dynamic-form__sections">
      <section
        v-for="section in sections"
        :key="section.key"
        class="oc-dynamic-form__section"
        :style="{ gridColumn: `span ${sectionSpan(section)}` }"
      >
        <v-card
          variant="outlined"
          rounded="lg"
          class="oc-dynamic-form__section-card h-100"
          :class="preview ? 'oc-dynamic-form__section-card--preview' : ''"
        >
          <v-card-text class="pa-4 pa-md-5">
            <div v-if="section.title" class="oc-dynamic-form__section-head mb-4">
              <span class="oc-dynamic-form__section-accent" aria-hidden="true" />
              <h4 class="text-subtitle-1 font-weight-semibold mb-0">
                {{ section.title }}
              </h4>
            </div>

            <v-row dense>
              <v-col
                v-for="fieldKey in section.fields"
                :key="fieldKey"
                cols="12"
                :md="fieldMdCols(fieldKey)"
              >
                <OcDynamicFormField
                  v-if="behaviorFor(fieldKey).visible"
                  :model-value="model[fieldKey]"
                  :field-key="fieldKey"
                  :meta="fieldMeta(fieldKey)"
                  :behavior="behaviorFor(fieldKey)"
                  :workspace-id="workspaceId"
                  :select-items="selectItemsForField(fieldKey)"
                  :select-loading="isLoadingField(fieldKey)"
                  :select-presentation="selectPresentationForField(fieldKey)"
                  :select-depends-on-blocked="isFieldDisabledByDependsOn(fieldKey)"
                  :person-picker="isPersonField(fieldKey) ? pickerForField(fieldKey) : undefined"
                  :dataset-picker="isDatasetPickerFieldForKey(fieldKey) ? datasetPickerForField(fieldKey) : undefined"
                  :group-names="groupNames"
                  :field-display="fieldDisplays?.[fieldKey]"
                  :readonly="readonly"
                  :preview="preview"
                  :error-message="fieldErrors?.[fieldKey]"
                  @update:model-value="(v) => setFieldValue(fieldKey, v)"
                />
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>
      </section>
    </div>
  </div>
</template>

<style scoped>
.oc-dynamic-form__sections {
  display: grid;
  grid-template-columns: repeat(12, minmax(0, 1fr));
  gap: 16px;
  align-items: start;
  overflow: visible;
}

.oc-dynamic-form__section {
  min-width: 0;
  overflow: visible;
}

.oc-dynamic-form__section-card {
  background: rgb(var(--v-theme-surface));
  overflow: visible !important;
}

.oc-dynamic-form__section-card :deep(.v-card-text) {
  overflow: visible !important;
}

.oc-dynamic-form :deep(.v-row),
.oc-dynamic-form :deep(.v-col) {
  overflow: visible;
}

.oc-dynamic-form__section-card--preview {
  background: rgb(var(--v-theme-surface));
  border-color: rgba(var(--v-border-color), var(--v-border-opacity));
}

.oc-dynamic-form__section-head {
  display: flex;
  align-items: center;
  gap: 10px;
}

.oc-dynamic-form__section-accent {
  width: 4px;
  height: 1.25rem;
  border-radius: 4px;
  background: rgb(var(--v-theme-primary));
  flex-shrink: 0;
}

.oc-dynamic-form--preview :deep(.v-field--disabled),
.oc-dynamic-form--preview :deep(.v-input--readonly .v-field) {
  opacity: 1;
}

.oc-dynamic-form--preview :deep(.v-input--readonly .v-field__field) {
  background: rgb(var(--v-theme-surface));
}

.font-weight-semibold {
  font-weight: 600;
}
</style>
