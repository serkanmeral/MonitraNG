<script setup lang="ts">
import { computed, nextTick, toRef, watch } from 'vue';
import type { OcFormLayoutSectionRuntime, OcFormRuntimeContext } from '@/types/apps/operationCore';
import { fieldColSpanForLayout, sectionColSpanForLayout } from '@/utils/ocFormLayout';
import { fieldComputeSignature, runFieldComputes } from '@/utils/ocFieldCompute';
import { applyOcLookupFieldCopies } from '@/utils/ocLookupCopy';
import { useOcDynamicFormLookups } from '@/composables/useOcDynamicFormLookups';
import OcDynamicFormField from '@/components/apps/operation-core/OcDynamicFormField.vue';

const props = defineProps<{
  context: OcFormRuntimeContext;
  readonly?: boolean;
  preview?: boolean;
  /** Salt okunur profil görünümünde değeri olmayan alan/bölümleri gizle. */
  hideEmptySections?: boolean;
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

const { selectItemsForField, isLoadingField, isPersonField, isGroupField, userDirectoryPickerForField, groupDirectoryPickerForField, isDatasetPickerFieldForKey, datasetPickerForField, isFieldDisabledByDependsOn, selectPresentationForField } = useOcDynamicFormLookups(
  workspaceId,
  contextRef,
  model,
  { readonly: readonlyRef }
);

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

function isFieldValueEmpty(value: unknown): boolean {
  if (value == null) return true;
  if (typeof value === 'string') return value.trim().length === 0;
  if (Array.isArray(value)) return value.length === 0;
  return false;
}

function fieldHasReadonlyContent(fieldKey: string): boolean {
  if (!behaviorFor(fieldKey).visible) return false;
  // Açıklama işin metnidir; boş olsa da Kimlik bölümünde yerini korur.
  if (fieldKey.toLowerCase() === 'description') return true;
  const display = props.fieldDisplays?.[fieldKey];
  if (display != null && String(display).trim().length > 0) return true;
  return !isFieldValueEmpty(model.value[fieldKey]);
}

const sections = computed<OcFormLayoutSectionRuntime[]>(() => {
  let base: OcFormLayoutSectionRuntime[];
  if (props.context.layout?.sections?.length) {
    base = props.context.layout.sections.filter((s) => s.fields.length > 0);
  } else {
    const keys = Object.keys(props.context.fields);
    base = keys.length ? [{ key: 'main', title: null, cols: 12, fields: keys }] : [];
  }

  if (!props.hideEmptySections || !props.readonly) return base;

  return base
    .map((section) => ({
      ...section,
      fields: section.fields.filter((fieldKey) => fieldHasReadonlyContent(fieldKey)),
    }))
    .filter((section) => section.fields.length > 0);
});

const formHeading = computed(() => props.context.layout?.formHeading?.trim() ?? '');
const formIntro = computed(() => props.context.layout?.formIntro?.trim() ?? '');

function fieldMeta(key: string) {
  return props.context.fields[key];
}

function sectionSpan(section: OcFormLayoutSectionRuntime): number {
  return sectionColSpanForLayout(section.key, props.context.layout?.sectionCols, section);
}

function fieldMdCols(fieldKey: string): number {
  return fieldColSpanForLayout(fieldKey, props.context.layout?.fieldCols);
}

const lookupCopyGeneration = new Map<string, number>();
let pendingLookupCopy: Promise<void> = Promise.resolve();
/** Son yazılan model. Üst form prop'u dönene kadar yerel okuma bunu kullanır. */
let draft: Record<string, unknown> | null = null;

function currentModel(): Record<string, unknown> {
  if (draft && model.value !== draft) return draft;
  draft = null;
  return model.value;
}

function writeModel(next: Record<string, unknown>) {
  draft = next;
  model.value = next;
}

function setFieldValue(key: string, value: unknown) {
  if (props.readonly) return;
  writeModel({ ...currentModel(), [key]: value });
  queueComputes();
  const generation = (lookupCopyGeneration.get(key) ?? 0) + 1;
  lookupCopyGeneration.set(key, generation);
  const meta = fieldMeta(key);
  pendingLookupCopy = pendingLookupCopy.then(() =>
    applyOcLookupFieldCopies(key, value, meta).then((patch) => {
      if (lookupCopyGeneration.get(key) !== generation) return;
      if (!Object.keys(patch).length) return;
      writeModel({ ...currentModel(), ...patch });
    })
  );
}

let computeTimer: ReturnType<typeof setTimeout> | null = null;
let computeChain: Promise<void> = Promise.resolve();
let computeGeneration = 0;
let lastComputeSignature = '';

function queueComputes() {
  void nextTick(() => scheduleComputes());
}

function scheduleComputes() {
  if (props.readonly) return;
  const signature = fieldComputeSignature(currentModel(), props.context.fields);
  if (signature === lastComputeSignature) return;
  lastComputeSignature = signature;
  if (computeTimer) clearTimeout(computeTimer);
  computeTimer = setTimeout(() => {
    computeTimer = null;
    computeChain = computeChain.then(() => refreshComputes());
  }, 350);
}

async function refreshComputes() {
  if (props.readonly) return;
  const generation = ++computeGeneration;
  const source = currentModel();
  const patch = await runFieldComputes(source, props.context.fields);
  if (generation !== computeGeneration) return;
  const changed = Object.entries(patch).filter(([key, value]) => source[key] !== value);
  if (!changed.length) return;
  writeModel({ ...currentModel(), ...Object.fromEntries(changed) });
}

watch(model, () => queueComputes(), { deep: true, flush: 'post' });
queueComputes();

async function flushLookupCopies() {
  if (computeTimer) {
    clearTimeout(computeTimer);
    computeTimer = null;
    lastComputeSignature = '';
    computeChain = computeChain.then(() => refreshComputes());
  }
  await computeChain;
  await pendingLookupCopy;
}

defineExpose({ flushLookupCopies });
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
                  :user-directory-picker="isPersonField(fieldKey) ? userDirectoryPickerForField(fieldKey) : undefined"
                  :group-directory-picker="isGroupField(fieldKey) ? groupDirectoryPickerForField(fieldKey) : undefined"
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
