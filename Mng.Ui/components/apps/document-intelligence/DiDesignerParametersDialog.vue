<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import DiDesignerPlaceholderPanel from '@/components/apps/document-intelligence/DiDesignerPlaceholderPanel.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {

  diGetTemplate,
  diGetTemplateDocxStructure,
  diListDocumentContextTypes,
  diPreviewDocumentGeneration,
  diUpdateTemplateParameters,
} from '@/services/documentIntelligenceService';
import type {
  DiDocxPlaceholder,
  DiDocumentContextType,
  DiTemplateParameter,
  DiTemplateSummary,
} from '@/types/apps/documentIntelligence';
import {
  buildParameterFromPlaceholder,
  importMissingPlaceholders,
  isPlaceholderDefined,
} from '@/utils/diDesignerPlaceholders';

const SYSTEM_PARAM_KEYS = new Set(['documentname', 'docno', 'generatedat']);

const props = defineProps<{
  modelValue: boolean;
  template: DiTemplateSummary | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  saved: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const saving = ref(false);
const error = ref<string | null>(null);
const notify = ref<string | null>(null);

const placeholders = ref<DiDocxPlaceholder[]>([]);
const placeholderWarnings = ref<string[]>([]);
const sourceFileName = ref<string | null>(null);
const parameters = ref<DiTemplateParameter[]>([]);
const primaryContextType = ref<string | null>(null);
const contextTypes = ref<DiDocumentContextType[]>([]);
const previewContextId = ref('');
const previewLoading = ref(false);
const previewValues = ref<Record<string, string> | null>(null);
const previewMissing = ref<string[]>([]);
const selectedKey = ref<string | null>(null);

const dataTypeOptions = computed(() => [
  { title: t('documentIntelligence.designer.dataTypeText'), value: 'text' },
  { title: t('documentIntelligence.designer.dataTypeDate'), value: 'date' },
  { title: t('documentIntelligence.designer.dataTypeNumber'), value: 'number' },
]);

const valueSourceOptions = computed(() => [
  { title: t('documentIntelligence.designer.modeManual'), value: 'manual' },
  { title: t('documentIntelligence.designer.modeIncremental'), value: 'incremental' },
  { title: t('documentIntelligence.designer.modeContext'), value: 'context' },
  { title: t('documentIntelligence.designer.modeStatic'), value: 'static' },
  { title: t('documentIntelligence.designer.modeGenerated'), value: 'generated' },
]);

const contextTypeOptions = computed(() =>
  contextTypes.value.map((ct) => ({ title: `${ct.displayName} (${ct.type})`, value: ct.type }))
);

const contextFieldOptions = computed(() => {
  const ct = contextTypes.value.find((x) => x.type === primaryContextType.value);
  return (ct?.fields ?? []).map((f) => ({ title: `${f.label} — ${f.path}`, value: f.path }));
});

const selectedParameter = computed(() =>
  parameters.value.find((p) => p.key === selectedKey.value) ?? null
);

const isDraft = computed(
  () => (props.template?.status ?? '').toLowerCase() === 'draft'
);

function isSystemKey(key: string): boolean {
  return SYSTEM_PARAM_KEYS.has(key.trim().toLowerCase());
}

function ensureIncremental(param: DiTemplateParameter): DiTemplateParameter {
  if (param.valueSourceMode !== 'incremental') {
    return { ...param, incremental: null };
  }
  const inc = param.incremental ?? {};
  return {
    ...param,
    incremental: {
      format: inc.format ?? (param.key.toLowerCase() === 'docno' ? 'ODK-COC-{yyyy}-{0:D3}' : '{0:D3}'),
      startValue: inc.startValue ?? 1,
      incrementStep: inc.incrementStep ?? 1,
      scopeKey: inc.scopeKey ?? param.key,
      resetPolicy: inc.resetPolicy ?? 'yearly',
    },
  };
}

function cloneParameters(list: DiTemplateParameter[]): DiTemplateParameter[] {
  return list.map((p) => ensureIncremental({ ...p, incremental: p.incremental ? { ...p.incremental } : null }));
}

async function loadDialogData() {
  const tpl = props.template;
  if (!tpl) return;

  loading.value = true;
  error.value = null;
  notify.value = null;
  selectedKey.value = null;
  placeholders.value = [];
  placeholderWarnings.value = [];
  sourceFileName.value = null;

  try {
    const [detail, structure] = await Promise.all([
      diGetTemplate(tpl.id),
      diGetTemplateDocxStructure(tpl.id),
    ]);
    parameters.value = cloneParameters(detail.parameters ?? []);
    primaryContextType.value = detail.primaryContextType ?? null;
    placeholders.value = structure.placeholders ?? [];
    placeholderWarnings.value = structure.placeholderWarnings ?? [];
    sourceFileName.value = structure.fileName ?? detail.sourceFileName ?? null;
    try {
      contextTypes.value = await diListDocumentContextTypes();
    } catch {
      // Generate API may be absent on older deployments; placeholders still load.
      contextTypes.value = [];
    }
    if (parameters.value.length) {
      selectedKey.value = parameters.value[0]!.key;
    } else if (placeholders.value.length) {
      selectedKey.value = placeholders.value[0]!.key;
    }
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.load');
    parameters.value = [];
  } finally {
    loading.value = false;
  }
}

function closeDialog() {
  emit('update:modelValue', false);
}

function onImportAllPlaceholders() {
  parameters.value = cloneParameters(importMissingPlaceholders(placeholders.value, parameters.value));
  notify.value = t('documentIntelligence.designer.placeholdersImported');
}

function onSelectPlaceholder(key: string) {
  selectedKey.value = key;
  if (!isPlaceholderDefined(key, parameters.value)) {
    parameters.value = cloneParameters([
      ...parameters.value,
      buildParameterFromPlaceholder(
        placeholders.value.find((p) => p.key === key) ?? { key, token: `{{${key}}}`, occurrenceCount: 1 },
        key.toLowerCase() === 'docno' ? 'incremental' : 'manual'
      ),
    ]);
  }
}

function updateSelected(field: keyof DiTemplateParameter, value: unknown) {
  const key = selectedKey.value;
  if (!key) return;
  parameters.value = parameters.value.map((p) => {
    if (p.key !== key) return p;
    const next = { ...p, [field]: value } as DiTemplateParameter;
    return ensureIncremental(next);
  });
}

function updateIncremental(field: string, value: unknown) {
  const key = selectedKey.value;
  if (!key) return;
  parameters.value = parameters.value.map((p) => {
    if (p.key !== key) return p;
    const inc = { ...(p.incremental ?? {}) };
    (inc as Record<string, unknown>)[field] = value;
    return ensureIncremental({ ...p, incremental: inc });
  });
}

function removeSelectedParameter() {
  const key = selectedKey.value;
  if (!key || isSystemKey(key)) return;
  parameters.value = parameters.value.filter((p) => p.key !== key);
  selectedKey.value = parameters.value[0]?.key ?? placeholders.value[0]?.key ?? null;
}

function updateContextBinding(field: string, value: unknown) {
  const key = selectedKey.value;
  if (!key) return;
  parameters.value = parameters.value.map((p) => {
    if (p.key !== key) return p;
    const ctx = { ...(p.contextBinding ?? { path: '' }) };
    (ctx as Record<string, unknown>)[field] = value;
    return ensureIncremental({ ...p, contextBinding: ctx });
  });
}

async function runPreview() {
  if (!previewContextId.value.trim()) return;
  previewLoading.value = true;
  previewValues.value = null;
  previewMissing.value = [];
  try {
    const res = await diPreviewDocumentGeneration('odak.coc.fromLine', previewContextId.value.trim());
    previewValues.value = res.values;
    previewMissing.value = res.missingKeys;
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.load');
  } finally {
    previewLoading.value = false;
  }
}

async function saveParameters() {
  const tpl = props.template;
  if (!tpl) return;

  saving.value = true;
  error.value = null;
  notify.value = null;
  try {
    const payload = parameters.value.map((p) => ensureIncremental(p));
    await diUpdateTemplateParameters(tpl.id, {
      primaryContextType: primaryContextType.value,
      parameters: payload,
    });
    notify.value = t('documentIntelligence.designer.saved');
    emit('saved');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.save');
  } finally {
    saving.value = false;
  }
}

watch(
  () => props.modelValue,
  (open) => {
    if (open) void loadDialogData();
  }
);
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="1100"
    scrollable
    @update:model-value="emit('update:modelValue', $event)"
  >
    <v-card rounded="lg">
      <v-card-title class="text-h6 font-weight-bold d-flex align-center flex-wrap ga-2">
        <span>{{ t('documentIntelligence.designer.advancedParameters') }}</span>
        <v-chip v-if="template?.code" size="small" variant="tonal">{{ template.code }}</v-chip>
      </v-card-title>
      <v-divider />

      <v-card-text class="pa-4">
        <p v-if="template?.name" class="text-body-2 text-medium-emphasis mb-3">
          {{ template.name }}
        </p>

        <v-alert v-if="error" type="error" variant="tonal" class="mb-3 rounded-lg" density="compact">
          {{ error }}
        </v-alert>
        <v-alert
          v-if="notify"
          type="success"
          variant="tonal"
          class="mb-3 rounded-lg"
          density="compact"
          closable
          @click:close="notify = null"
        >
          {{ notify }}
        </v-alert>

        <v-alert
          v-if="!isDraft"
          type="info"
          variant="tonal"
          class="mb-3 rounded-lg"
          density="compact"
        >
          {{ t('documentIntelligence.designer.publishedParametersHint') }}
        </v-alert>

        <v-row dense>
          <v-col cols="12" md="5">
            <DiDesignerPlaceholderPanel
              :placeholders="placeholders"
              :warnings="placeholderWarnings"
              :parameters="parameters"
              :loading="loading"
              :file-name="sourceFileName"
              @import-all="onImportAllPlaceholders"
              @select="onSelectPlaceholder"
            />
          </v-col>

          <v-col cols="12" md="7">
            <v-card variant="outlined" rounded="lg" min-height="420">
              <v-card-title class="text-subtitle-2 font-weight-bold">
                {{ t('documentIntelligence.designer.paramList') }}
                <v-chip size="x-small" variant="tonal" class="ms-2">{{ parameters.length }}</v-chip>
              </v-card-title>
              <v-divider />

              <v-progress-linear v-if="loading" indeterminate color="primary" />

              <template v-else>
                <v-select
                  :model-value="primaryContextType"
                  :items="contextTypeOptions"
                  :label="t('documentIntelligence.designer.primaryContextType')"
                  density="compact"
                  variant="outlined"
                  hide-details
                  clearable
                  class="pa-3 border-b"
                  @update:model-value="primaryContextType = $event"
                />

                <div class="pa-3 border-b">
                  <v-select
                    :model-value="selectedKey"
                    :items="parameters.map((p) => ({ title: `${p.label} (${p.key})`, value: p.key }))"
                    :label="t('documentIntelligence.designer.paramKey')"
                    density="compact"
                    variant="outlined"
                    hide-details
                    @update:model-value="selectedKey = $event"
                  />
                </div>

                <div v-if="!selectedParameter" class="pa-6 text-body-2 text-medium-emphasis text-center">
                  {{ t('documentIntelligence.designer.noParams') }}
                </div>

                <div v-else class="pa-4">
                  <div class="d-flex align-center ga-2 mb-3 flex-wrap">
                    <code>{{ selectedParameter.key }}</code>
                    <v-chip v-if="isSystemKey(selectedParameter.key)" size="x-small" color="info" variant="tonal">
                      {{ t('documentIntelligence.designer.systemParameter') }}
                    </v-chip>
                  </div>

                  <v-text-field
                    :model-value="selectedParameter.label"
                    :label="t('documentIntelligence.designer.paramLabel')"
                    density="compact"
                    variant="outlined"
                    hide-details
                    class="mb-3"
                    @update:model-value="updateSelected('label', $event)"
                  />

                  <v-select
                    :model-value="selectedParameter.dataType"
                    :items="dataTypeOptions"
                    :label="t('documentIntelligence.designer.paramDataType')"
                    density="compact"
                    variant="outlined"
                    hide-details
                    class="mb-3"
                    @update:model-value="updateSelected('dataType', $event)"
                  />

                  <v-select
                    :model-value="selectedParameter.valueSourceMode"
                    :items="valueSourceOptions"
                    :label="t('documentIntelligence.designer.valueSource')"
                    density="compact"
                    variant="outlined"
                    hide-details
                    class="mb-3"
                    @update:model-value="updateSelected('valueSourceMode', $event)"
                  />

                  <template v-if="selectedParameter.valueSourceMode === 'incremental'">
                    <v-text-field
                      :model-value="selectedParameter.incremental?.format ?? ''"
                      :label="t('documentIntelligence.designer.incrementalFormat')"
                      density="compact"
                      variant="outlined"
                      hide-details
                      class="mb-3"
                      hint="ODK-COC-{yy}-{0}"
                      @update:model-value="updateIncremental('format', $event)"
                    />
                    <v-row dense>
                      <v-col cols="6">
                        <v-text-field
                          :model-value="selectedParameter.incremental?.startValue ?? 1"
                          :label="t('documentIntelligence.designer.incrementalStart')"
                          type="number"
                          density="compact"
                          variant="outlined"
                          hide-details
                          @update:model-value="updateIncremental('startValue', Number($event))"
                        />
                      </v-col>
                      <v-col cols="6">
                        <v-text-field
                          :model-value="selectedParameter.incremental?.incrementStep ?? 1"
                          :label="t('documentIntelligence.designer.incrementalStep')"
                          type="number"
                          density="compact"
                          variant="outlined"
                          hide-details
                          @update:model-value="updateIncremental('incrementStep', Number($event))"
                        />
                      </v-col>
                    </v-row>
                  </template>

                  <template v-else-if="selectedParameter.valueSourceMode === 'context'">
                    <v-select
                      :model-value="selectedParameter.contextBinding?.path ?? ''"
                      :items="contextFieldOptions"
                      :label="t('documentIntelligence.designer.contextFieldPath')"
                      density="compact"
                      variant="outlined"
                      hide-details
                      class="mb-3"
                      @update:model-value="updateContextBinding('path', $event)"
                    />
                    <v-select
                      :model-value="selectedParameter.contextBinding?.fallbackPath ?? ''"
                      :items="contextFieldOptions"
                      :label="t('documentIntelligence.designer.contextFallbackPath')"
                      density="compact"
                      variant="outlined"
                      hide-details
                      clearable
                      class="mb-3"
                      @update:model-value="updateContextBinding('fallbackPath', $event)"
                    />
                    <v-text-field
                      :model-value="selectedParameter.contextBinding?.format ?? ''"
                      :label="t('documentIntelligence.designer.contextFormat')"
                      density="compact"
                      variant="outlined"
                      hide-details
                      class="mb-3"
                      placeholder="{quantity} {unit}"
                      @update:model-value="updateContextBinding('format', $event)"
                    />
                    <v-text-field
                      :model-value="selectedParameter.contextBinding?.defaultValue ?? ''"
                      :label="t('documentIntelligence.designer.contextDefaultValue')"
                      density="compact"
                      variant="outlined"
                      hide-details
                      @update:model-value="updateContextBinding('defaultValue', $event)"
                    />
                  </template>

                  <template v-else-if="selectedParameter.valueSourceMode === 'static'">
                    <v-text-field
                      :model-value="selectedParameter.defaultValue ?? ''"
                      :label="t('documentIntelligence.designer.contextDefaultValue')"
                      density="compact"
                      variant="outlined"
                      hide-details
                      @update:model-value="updateSelected('defaultValue', $event)"
                    />
                  </template>

                  <template v-else-if="selectedParameter.valueSourceMode === 'generated'">
                    <v-text-field
                      :model-value="selectedParameter.format ?? 'dd.MM.yyyy'"
                      :label="t('documentIntelligence.designer.contextFormat')"
                      density="compact"
                      variant="outlined"
                      hide-details
                      @update:model-value="updateSelected('format', $event)"
                    />
                  </template>

                  <v-divider class="my-4" />
                  <div class="text-caption text-medium-emphasis mb-2">
                    {{ t('documentIntelligence.designer.previewValues') }}
                  </div>
                  <v-text-field
                    v-model="previewContextId"
                    :label="t('documentIntelligence.designer.previewContextId')"
                    density="compact"
                    variant="outlined"
                    hide-details
                    class="mb-2"
                  />
                  <v-btn
                    size="small"
                    variant="tonal"
                    class="text-none mb-3"
                    :loading="previewLoading"
                    :disabled="!previewContextId.trim()"
                    @click="runPreview"
                  >
                    {{ t('documentIntelligence.designer.runPreview') }}
                  </v-btn>
                  <div v-if="previewValues && selectedParameter" class="text-body-2 mb-1">
                    <strong>{{ selectedParameter.key }}:</strong>
                    {{ previewValues[selectedParameter.key] || '—' }}
                  </div>
                  <div v-if="previewMissing.length" class="text-caption text-warning">
                    {{ t('documentIntelligence.designer.previewMissing') }}:
                    {{ previewMissing.join(', ') }}
                  </div>

                  <v-btn
                    v-if="!isSystemKey(selectedParameter.key)"
                    size="small"
                    color="error"
                    variant="text"
                    class="text-none mt-2"
                    prepend-icon="mdi-delete-outline"
                    @click="removeSelectedParameter"
                  >
                    {{ t('documentIntelligence.designer.removeParameter') }}
                  </v-btn>
                </div>
              </template>
            </v-card>
          </v-col>
        </v-row>
      </v-card-text>

      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="closeDialog">
          {{ t('documentIntelligence.cancel') }}
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          class="text-none"
          :loading="saving"
          :disabled="loading"
          @click="saveParameters"
        >
          {{ t('documentIntelligence.designer.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.border-b {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
</style>
