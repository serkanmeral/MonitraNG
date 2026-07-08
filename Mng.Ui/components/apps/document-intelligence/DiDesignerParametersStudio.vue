<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import DiDesignerContextPathPicker from '@/components/apps/document-intelligence/DiDesignerContextPathPicker.vue';
import DiDesignerParameterTablePanel from '@/components/apps/document-intelligence/DiDesignerParameterTablePanel.vue';
import DiDesignerPlaceholderPanel from '@/components/apps/document-intelligence/DiDesignerPlaceholderPanel.vue';
import DiDesignerProducerMetaBand from '@/components/apps/document-intelligence/DiDesignerProducerMetaBand.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  diGetDocumentDataSource,
  diGetDocumentProducer,
  diGetTemplate,
  diGetTemplateDocxStructure,
  diListDocumentContextTypes,
  diListDocumentDataSources,
  diPreviewDocumentGeneration,
  diUpdateTemplateParameters,
} from '@/services/documentIntelligenceService';
import type {
  DiDocxPlaceholder,
  DiDocumentContextType,
  DiDocumentDataSourceDetail,
  DiDocumentDataSourceSummary,
  DiDocumentProducerDetail,
  DiTemplateDetail,
  DiTemplateDocBinding,
  DiTemplateParameter,
} from '@/types/apps/documentIntelligence';
import { isTableParameterKind, isImageParameterKind } from '@/utils/diDesignerContextPaths';
import {
  buildParameterFromPlaceholder,
  importMissingPlaceholders,
  isPlaceholderDefined,
} from '@/utils/diDesignerPlaceholders';

const SYSTEM_PARAM_KEYS = new Set(['documentname', 'docno', 'generatedat']);

const props = defineProps<{
  templateId: string;
}>();

const emit = defineEmits<{
  saved: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const saving = ref(false);
const error = ref<string | null>(null);
const notify = ref<string | null>(null);

const template = ref<DiTemplateDetail | null>(null);
const placeholders = ref<DiDocxPlaceholder[]>([]);
const placeholderWarnings = ref<string[]>([]);
const sourceFileName = ref<string | null>(null);
const structureUnavailable = ref(false);
const parameters = ref<DiTemplateParameter[]>([]);
const primaryContextType = ref<string | null>(null);
const contextTypes = ref<DiDocumentContextType[]>([]);
const producer = ref<DiDocumentProducerDetail | null>(null);
const producerLoading = ref(false);
const previewContextId = ref('');
const previewLoading = ref(false);
const previewValues = ref<Record<string, string> | null>(null);
const previewMissing = ref<string[]>([]);
const selectedKey = ref<string | null>(null);
const detailTab = ref('general');
const tableDataSource = ref<DiDocumentDataSourceDetail | null>(null);
const tableDataSourceLoading = ref(false);
const dataSourcesCatalog = ref<DiDocumentDataSourceSummary[]>([]);
const dataSourcesCatalogLoading = ref(false);

const dataTypeOptions = computed(() => [
  { title: t('documentIntelligence.designer.dataTypeText'), value: 'text' },
  { title: t('documentIntelligence.designer.dataTypeDate'), value: 'date' },
  { title: t('documentIntelligence.designer.dataTypeNumber'), value: 'number' },
  { title: t('documentIntelligence.designer.dataTypeImage'), value: 'image' },
]);

const valueSourceOptions = computed(() => {
  const base = [
    { title: t('documentIntelligence.designer.modeManual'), value: 'manual' },
    { title: t('documentIntelligence.designer.modeIncremental'), value: 'incremental' },
    { title: t('documentIntelligence.designer.modeContext'), value: 'context' },
    { title: t('documentIntelligence.designer.modeStatic'), value: 'static' },
    { title: t('documentIntelligence.designer.modeGenerated'), value: 'generated' },
  ];
  if (isImageParameterKind(selectedParameter.value?.kind)) {
    return [
      { title: t('documentIntelligence.designer.modeDomain'), value: 'domain' },
      ...base.filter((item) => item.value === 'static' || item.value === 'manual'),
    ];
  }
  return base;
});

const contextTypeOptions = computed(() =>
  contextTypes.value.map((ct) => ({ title: `${ct.displayName} (${ct.type})`, value: ct.type }))
);

const contextFields = computed(() => {
  const ct = contextTypes.value.find((x) => x.type === primaryContextType.value);
  return ct?.fields ?? [];
});

const selectedParameter = computed(() =>
  parameters.value.find((p) => p.key === selectedKey.value) ?? null
);

const isTableSelected = computed(() => isTableParameterKind(selectedParameter.value?.kind));

const previewProfileCode = computed(() => template.value?.generationProfile?.trim() ?? '');

const isDraft = computed(() => (template.value?.status ?? '').toLowerCase() === 'draft');

const parameterTabs = computed(() => {
  if (isTableSelected.value) {
    return [
      { value: 'data', title: t('documentIntelligence.designer.parameterStudio.tabData') },
      { value: 'binding', title: t('documentIntelligence.designer.parameterStudio.tabBinding') },
    ];
  }
  return [
    { value: 'general', title: t('documentIntelligence.designer.parameterStudio.tabGeneral') },
    { value: 'binding', title: t('documentIntelligence.designer.parameterStudio.tabBinding') },
  ];
});

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

async function loadProducerMeta(profileCode: string) {
  producerLoading.value = true;
  producer.value = null;
  try {
    producer.value = await diGetDocumentProducer(profileCode);
  } finally {
    producerLoading.value = false;
  }
}

async function loadDataSourcesCatalog() {
  dataSourcesCatalogLoading.value = true;
  try {
    dataSourcesCatalog.value = await diListDocumentDataSources();
  } catch {
    dataSourcesCatalog.value = [];
  } finally {
    dataSourcesCatalogLoading.value = false;
  }
}

async function loadTableDataSource(refCode: string | null | undefined) {
  tableDataSource.value = null;
  if (!refCode?.trim()) return;
  tableDataSourceLoading.value = true;
  try {
    tableDataSource.value = await diGetDocumentDataSource(refCode.trim());
  } finally {
    tableDataSourceLoading.value = false;
  }
}

async function loadStudioData() {
  loading.value = true;
  error.value = null;
  notify.value = null;
  selectedKey.value = null;
  placeholders.value = [];
  placeholderWarnings.value = [];
  sourceFileName.value = null;
  structureUnavailable.value = false;

  try {
    const detail = await diGetTemplate(props.templateId);
    template.value = detail;
    parameters.value = cloneParameters(detail.parameters ?? []);
    primaryContextType.value = detail.primaryContextType ?? null;

    try {
      const structure = await diGetTemplateDocxStructure(props.templateId);
      placeholders.value = structure.placeholders ?? [];
      placeholderWarnings.value = structure.placeholderWarnings ?? [];
      sourceFileName.value = structure.fileName ?? detail.sourceFileName ?? null;
    } catch {
      structureUnavailable.value = true;
      sourceFileName.value = detail.sourceFileName ?? null;
    }

    try {
      contextTypes.value = await diListDocumentContextTypes();
    } catch {
      contextTypes.value = [];
    }

    if (previewProfileCode.value) {
      void loadProducerMeta(previewProfileCode.value);
    }

    void loadDataSourcesCatalog();

    if (parameters.value.length) {
      selectedKey.value = parameters.value[0]!.key;
    } else if (placeholders.value.length) {
      selectedKey.value = placeholders.value[0]!.key;
    }
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.load');
    template.value = null;
    parameters.value = [];
  } finally {
    loading.value = false;
  }
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
  const profile = previewProfileCode.value;
  if (!profile || !previewContextId.value.trim()) return;
  previewLoading.value = true;
  previewValues.value = null;
  previewMissing.value = [];
  try {
    const res = await diPreviewDocumentGeneration(profile, previewContextId.value.trim());
    previewValues.value = res.values;
    previewMissing.value = res.missingKeys;
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.load');
  } finally {
    previewLoading.value = false;
  }
}

function updateTableDocBinding(field: keyof DiTemplateDocBinding, value: unknown) {
  const key = selectedKey.value;
  if (!key) return;
  parameters.value = parameters.value.map((p) => {
    if (p.key !== key) return p;
    const binding: DiTemplateDocBinding = {
      regionKind: 'table',
      paragraphIndex: 0,
      ...(p.docBinding ?? {}),
      [field]: value,
    };
    return ensureIncremental({ ...p, docBinding: binding });
  });
}

function updateTableColumns(
  columns: Array<{ sourceField: string; header?: string | null; format?: string | null }>
) {
  const key = selectedKey.value;
  if (!key) return;
  parameters.value = parameters.value.map((p) => {
    if (p.key !== key) return p;
    return ensureIncremental({
      ...p,
      valueSource: {
        ...(p.valueSource ?? {}),
        columns,
      },
    });
  });
}

async function updateTableDataSourceRef(refCode: string | null) {
  updateSelected('dataSourceRef', refCode);
  if (refCode?.trim()) {
    tableDataSourceLoading.value = true;
    try {
      const ds = await diGetDocumentDataSource(refCode.trim());
      tableDataSource.value = ds;
      if (ds?.columns.length && selectedKey.value) {
        updateTableColumns(ds.columns.map((col) => ({ ...col })));
      }
    } finally {
      tableDataSourceLoading.value = false;
    }
  } else {
    tableDataSource.value = null;
  }
}

async function saveParameters() {
  if (!template.value) return;

  saving.value = true;
  error.value = null;
  notify.value = null;
  try {
    const payload = parameters.value.map((p) => ensureIncremental(p));
    await diUpdateTemplateParameters(template.value.id, {
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
  () => props.templateId,
  () => {
    void loadStudioData();
  },
  { immediate: true }
);

watch(selectedKey, async (key) => {
  const param = parameters.value.find((p) => p.key === key);
  detailTab.value = isTableParameterKind(param?.kind) ? 'data' : 'general';
  if (isTableParameterKind(param?.kind)) {
    await loadTableDataSource(param?.dataSourceRef);
    if (param?.dataSourceRef && !param.valueSource?.columns?.length && tableDataSource.value?.columns.length) {
      updateTableColumns(tableDataSource.value.columns.map((col) => ({ ...col })));
    }
  } else {
    tableDataSource.value = null;
  }
});

watch(previewProfileCode, (code) => {
  if (code) void loadProducerMeta(code);
  else producer.value = null;
});
</script>

<template>
  <div>
    <DiDesignerProducerMetaBand
      :profile-code="previewProfileCode || null"
      :producer="producer"
      :loading="producerLoading"
    />

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

    <v-alert
      v-if="structureUnavailable"
      type="info"
      variant="tonal"
      class="mb-3 rounded-lg"
      density="compact"
    >
      {{ t('documentIntelligence.designer.parameterStudio.structureUnavailable') }}
    </v-alert>

    <v-row dense>
      <v-col cols="12" lg="5">
        <DiDesignerPlaceholderPanel
          v-if="!structureUnavailable"
          :placeholders="placeholders"
          :warnings="placeholderWarnings"
          :parameters="parameters"
          :loading="loading"
          :file-name="sourceFileName"
          @import-all="onImportAllPlaceholders"
          @select="onSelectPlaceholder"
        />
        <v-card v-else variant="outlined" rounded="lg" min-height="420">
          <v-card-title class="text-subtitle-2 font-weight-bold">
            {{ t('documentIntelligence.designer.paramList') }}
            <v-chip size="x-small" variant="tonal" class="ms-2">{{ parameters.length }}</v-chip>
          </v-card-title>
          <v-divider />
          <v-list density="compact" class="pa-0">
            <v-list-item
              v-for="param in parameters"
              :key="param.key"
              :active="selectedKey === param.key"
              @click="selectedKey = param.key"
            >
              <v-list-item-title>{{ param.label }}</v-list-item-title>
              <v-list-item-subtitle>
                <code>{{ param.key }}</code>
                <v-chip v-if="isTableParameterKind(param.kind)" size="x-small" class="ms-2">table</v-chip>
                <v-chip v-else-if="isImageParameterKind(param.kind)" size="x-small" class="ms-2">image</v-chip>
              </v-list-item-subtitle>
            </v-list-item>
          </v-list>
        </v-card>
      </v-col>

      <v-col cols="12" lg="7">
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
                :items="parameters.map((p) => ({
                  title: `${p.label} (${p.key})${isTableParameterKind(p.kind) ? ' · table' : isImageParameterKind(p.kind) ? ' · image' : ''}`,
                  value: p.key,
                }))"
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
                <v-chip v-if="isTableParameterKind(selectedParameter.kind)" size="x-small" color="secondary" variant="tonal">
                  table
                </v-chip>
                <v-chip v-else-if="isImageParameterKind(selectedParameter.kind)" size="x-small" color="secondary" variant="tonal">
                  image
                </v-chip>
                <v-chip v-if="isSystemKey(selectedParameter.key)" size="x-small" color="info" variant="tonal">
                  {{ t('documentIntelligence.designer.systemParameter') }}
                </v-chip>
              </div>

              <v-tabs v-model="detailTab" density="compact" class="mb-4">
                <v-tab v-for="tab in parameterTabs" :key="tab.value" :value="tab.value" class="text-none">
                  {{ tab.title }}
                </v-tab>
              </v-tabs>

              <v-window v-model="detailTab">
                <v-window-item value="general">
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
                    <DiDesignerContextPathPicker
                      :model-value="selectedParameter.contextBinding?.path ?? ''"
                      :fields="contextFields"
                      :label="t('documentIntelligence.designer.contextFieldPath')"
                      class="mb-3"
                      @update:model-value="updateContextBinding('path', $event)"
                    />
                    <DiDesignerContextPathPicker
                      :model-value="selectedParameter.contextBinding?.fallbackPath ?? ''"
                      :fields="contextFields"
                      :label="t('documentIntelligence.designer.contextFallbackPath')"
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
                    :hint="previewProfileCode ? previewProfileCode : t('documentIntelligence.designer.parameterStudio.previewProfileMissing')"
                    persistent-hint
                  />
                  <v-btn
                    size="small"
                    variant="tonal"
                    class="text-none mb-3"
                    :loading="previewLoading"
                    :disabled="!previewContextId.trim() || !previewProfileCode"
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
                </v-window-item>

                <v-window-item value="data">
                  <DiDesignerParameterTablePanel
                    v-if="selectedParameter"
                    :parameter="selectedParameter"
                    :data-source="tableDataSource"
                    :data-sources="dataSourcesCatalog"
                    :data-sources-loading="dataSourcesCatalogLoading"
                    :data-source-loading="tableDataSourceLoading"
                    section="data"
                    @update:label="updateSelected('label', $event)"
                    @update:data-source-ref="updateTableDataSourceRef"
                    @update:columns="updateTableColumns"
                  />
                </v-window-item>

                <v-window-item value="binding">
                  <template v-if="isTableSelected && selectedParameter">
                    <DiDesignerParameterTablePanel
                      :parameter="selectedParameter"
                      :data-source="tableDataSource"
                      :data-sources="dataSourcesCatalog"
                      :data-sources-loading="dataSourcesCatalogLoading"
                      :data-source-loading="tableDataSourceLoading"
                      section="binding"
                      @update:doc-binding="updateTableDocBinding"
                    />
                  </template>
                  <template v-else>
                    <v-row dense>
                      <v-col cols="12" md="4">
                        <div class="text-caption text-medium-emphasis">{{ t('documentIntelligence.designer.parameterStudio.bindingRegion') }}</div>
                        <div>{{ selectedParameter.docBinding?.regionKind ?? '—' }}</div>
                      </v-col>
                      <v-col cols="12" md="4">
                        <div class="text-caption text-medium-emphasis">{{ t('documentIntelligence.designer.parameterStudio.bindingParagraph') }}</div>
                        <div>{{ selectedParameter.docBinding?.paragraphIndex ?? '—' }}</div>
                      </v-col>
                      <v-col cols="12">
                        <div class="text-caption text-medium-emphasis">{{ t('documentIntelligence.designer.parameterStudio.bindingOriginalText') }}</div>
                        <div class="text-body-2">{{ selectedParameter.docBinding?.originalText ?? '—' }}</div>
                      </v-col>
                    </v-row>
                  </template>
                </v-window-item>
              </v-window>
            </div>
          </template>
        </v-card>
      </v-col>
    </v-row>

    <div class="d-flex justify-end ga-2 mt-4">
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
    </div>
  </div>
</template>

<style scoped>
.border-b {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
</style>
