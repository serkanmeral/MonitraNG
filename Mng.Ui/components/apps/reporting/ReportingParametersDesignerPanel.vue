<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { FieldDefinition } from '@/stores/apps/dataset';
import type {
  ReportingParameterFieldFromParameter,
  ReportingReportParameter,
} from '@/types/apps/reporting';
import {
  REPORTING_PARAM_SEARCH_FIELD,
  bindingModesForField,
  buildReportingParameter,
  choiceGroupParameters,
  defaultBindingModeForField,
  inferReportingParameterFieldName,
  isReportingParameterSearchField,
  reportableFieldsForParameters,
  reportingFieldTypeLabel,
  reportingParameterBindingDisplayLabel,
  type ReportingParameterBindingModeId,
  type ReportingParameterDatePart,
} from '@/utils/reportingParameterGenerator';
import { reportingFieldLabel } from '@/utils/reportingListConfig';
import { normalizeReportingParameter } from '@/utils/reportingParameterModel';
import { ChevronDownIcon, ChevronUpIcon, PencilIcon, PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  parameters: ReportingReportParameter[];
  fields: FieldDefinition[];
  disabled?: boolean;
}>();

const emit = defineEmits<{
  'update:parameters': [ReportingReportParameter[]];
  reset: [];
}>();

const { t } = useAppI18n();

const dialogOpen = ref(false);
const editingIndex = ref<number | null>(null);
const advancedPanels = ref<number[]>([]);

const formFieldKey = ref('');
const formBindingMode = ref<ReportingParameterBindingModeId>('fieldEq');
const formDatePart = ref<ReportingParameterDatePart>('year');
const formLabel = ref('');
const formRequired = ref(false);
const formDefaultValue = ref('');
const formYearMin = ref(2017);
const formYearMaxCurrent = ref(true);
const formYearMax = ref(new Date().getFullYear());
const formYearIncludeAll = ref(true);
const formIncludeAllOption = ref(true);
const formUseFieldFromParam = ref(false);
const formDependsOnParamId = ref('');
const formDefaultDateField = ref('');
const formSkipAllChoice = ref(true);
const formFieldMapEntries = ref<{ choice: string; field: string }[]>([]);

const fieldMap = computed(() => new Map(props.fields.map((f) => [f.name, f])));

const selectedField = computed(() => {
  if (isReportingParameterSearchField(formFieldKey.value)) return null;
  return fieldMap.value.get(formFieldKey.value) ?? null;
});

const formFieldItems = computed(() => {
  const items = reportableFieldsForParameters(props.fields).map((f) => ({
    value: f.name,
    title: reportingFieldLabel(f, f.name),
    subtitle: reportingFieldTypeLabel(f.fieldType, t),
  }));
  return [
    {
      value: REPORTING_PARAM_SEARCH_FIELD,
      title: t('reporting.parameters.searchFieldOption'),
      subtitle: t('reporting.parameters.fieldTypes.search'),
    },
    ...items,
  ];
});

const bindingModeItems = computed(() =>
  bindingModesForField(selectedField.value).map((mode) => ({
    value: mode,
    title: t(`reporting.parameters.bindings.${mode}`),
  }))
);

const showBindingModePicker = computed(() => bindingModeItems.value.length > 1);

const dependsOnParamItems = computed(() => {
  const currentId =
    editingIndex.value != null ? props.parameters[editingIndex.value]?.id : undefined;
  return choiceGroupParameters(props.parameters)
    .filter((p) => p.id !== currentId)
    .map((p) => ({ value: p.id, title: p.label }));
});

const dependsOnChoices = computed(() => {
  const param = props.parameters.find((p) => p.id === formDependsOnParamId.value);
  if (!param) return [];
  const normalized = normalizeReportingParameter(param);
  return (normalized.binding.choices ?? []).map((c) => ({
    value: c.value,
    title: c.title,
  }));
});

const datetimeFieldItems = computed(() =>
  props.fields
    .filter((f) => f.fieldType === 'datetime' && f.name?.trim())
    .map((f) => ({ value: f.name, title: reportingFieldLabel(f, f.name) }))
);

const datePartItems = computed(() =>
  (['year', 'month', 'quarter'] as ReportingParameterDatePart[]).map((part) => ({
    value: part,
    title: t(`reporting.parameters.dateParts.${part}`),
  }))
);

const showYearOptions = computed(
  () =>
    formBindingMode.value === 'datePart' &&
    (formDatePart.value === 'year' || formDatePart.value === 'quarter')
);
const showDatePartPicker = computed(() => formBindingMode.value === 'datePart');
const showIncludeAllOption = computed(
  () =>
    formBindingMode.value === 'fieldEq' ||
    formBindingMode.value === 'choiceGroup' ||
    (formBindingMode.value === 'datePart' && formDatePart.value === 'year')
);
const showAdvancedDateMapping = computed(() => false);

function resetForm() {
  formFieldKey.value = '';
  formBindingMode.value = 'fieldEq';
  formDatePart.value = 'year';
  formLabel.value = '';
  formRequired.value = false;
  formDefaultValue.value = '';
  formYearMin.value = 2017;
  formYearMaxCurrent.value = true;
  formYearMax.value = new Date().getFullYear();
  formYearIncludeAll.value = true;
  formIncludeAllOption.value = true;
  formUseFieldFromParam.value = false;
  formDependsOnParamId.value = '';
  formDefaultDateField.value = '';
  formSkipAllChoice.value = true;
  formFieldMapEntries.value = [];
  advancedPanels.value = [];
}

function openAddDialog() {
  editingIndex.value = null;
  resetForm();
  dialogOpen.value = true;
}

function loadFormFromParameter(param: ReportingReportParameter) {
  resetForm();
  formFieldKey.value = inferReportingParameterFieldName(param);
  formBindingMode.value = inferReportingParameterBindingMode(param);
  formLabel.value = param.label;
  formRequired.value = param.required;
  formDefaultValue.value = param.defaultValue ?? '';

  const binding = param.binding ?? normalizeReportingParameter(param).binding;

  if (binding.kind === 'datePartRange') {
    formDatePart.value = binding.part ?? 'year';
    const opts = param.options;
    if (opts?.kind === 'yearRange' || opts?.kind === 'quarterRange') {
      formYearMin.value = opts.min ?? 2017;
      formYearMaxCurrent.value = opts.max === 'currentYear' || opts.max == null;
      formYearMax.value =
        typeof opts.max === 'number' ? opts.max : new Date().getFullYear();
      if (opts.kind === 'yearRange') {
        formYearIncludeAll.value = opts.includeAll !== false;
      }
    }
    const fp = binding.fieldFromParameter;
    if (fp) {
      formUseFieldFromParam.value = true;
      advancedPanels.value = [0];
      formDependsOnParamId.value = fp.parameterId;
      formDefaultDateField.value = fp.defaultField ?? binding.field ?? '';
      formSkipAllChoice.value = fp.skipChoiceValues?.includes('all') ?? true;
      formFieldMapEntries.value = Object.entries(fp.map ?? {}).map(([choice, field]) => ({
        choice,
        field: String(field),
      }));
    }
  } else if (binding.kind === 'dateRange') {
    const fp = binding.fieldFromParameter;
    if (fp) {
      formUseFieldFromParam.value = true;
      advancedPanels.value = [0];
      formDependsOnParamId.value = fp.parameterId;
      formDefaultDateField.value = fp.defaultField ?? binding.field ?? '';
      formSkipAllChoice.value = fp.skipChoiceValues?.includes('all') ?? true;
      formFieldMapEntries.value = Object.entries(fp.map ?? {}).map(([choice, field]) => ({
        choice,
        field: String(field),
      }));
    }
  } else if (binding.kind === 'choiceFilters') {
    formIncludeAllOption.value = binding.choices?.some((c) => c.value === 'all') ?? false;
  }

  if (param.options?.kind === 'static') {
    formIncludeAllOption.value = param.options.includeAll !== false;
  }
}

function openEditDialog(index: number) {
  editingIndex.value = index;
  loadFormFromParameter(props.parameters[index]!);
  dialogOpen.value = true;
}

function onFieldChange() {
  const field = selectedField.value;
  if (isReportingParameterSearchField(formFieldKey.value)) {
    formBindingMode.value = 'search';
    if (!formLabel.value.trim()) {
      formLabel.value = t('reporting.parameters.searchFieldOption');
    }
    return;
  }

  formBindingMode.value = defaultBindingModeForField(field);
  if (field && !formLabel.value.trim()) {
    formLabel.value = reportingFieldLabel(field, field.name);
  }
}

function buildFieldFromParameter(): ReportingParameterFieldFromParameter | undefined {
  if (!formUseFieldFromParam.value || !formDependsOnParamId.value) return undefined;
  const map: Record<string, string> = {};
  for (const entry of formFieldMapEntries.value) {
    const choice = entry.choice.trim();
    const field = entry.field.trim();
    if (choice && field) map[choice] = field;
  }
  const primaryField = isReportingParameterSearchField(formFieldKey.value)
    ? ''
    : formFieldKey.value.trim();
  return {
    parameterId: formDependsOnParamId.value,
    map,
    defaultField: formDefaultDateField.value.trim() || primaryField || undefined,
    skipChoiceValues: formSkipAllChoice.value ? ['all'] : undefined,
  };
}

function syncFieldMapFromDependsOn() {
  const choices = dependsOnChoices.value.filter((c) => c.value !== 'all');
  const existing = new Map(formFieldMapEntries.value.map((e) => [e.choice, e.field]));
  formFieldMapEntries.value = choices.map((c) => ({
    choice: c.value,
    field: existing.get(c.value) ?? '',
  }));
}

watch(formDependsOnParamId, () => {
  if (formUseFieldFromParam.value) syncFieldMapFromDependsOn();
});

watch(formUseFieldFromParam, (on) => {
  if (on) {
    advancedPanels.value = [0];
    syncFieldMapFromDependsOn();
  }
});

watch(formFieldKey, () => {
  if (!bindingModeItems.value.some((m) => m.value === formBindingMode.value)) {
    formBindingMode.value = defaultBindingModeForField(selectedField.value);
  }
});

function saveDialog() {
  const existingIds = props.parameters
    .map((p) => p.id)
    .filter((id) => editingIndex.value == null || props.parameters[editingIndex.value!]?.id !== id);

  const field = selectedField.value;
  const built = buildReportingParameter({
    bindingMode: formBindingMode.value,
    field,
    fieldName: formFieldKey.value,
    label: formLabel.value,
    required: formRequired.value,
    defaultValue: formDefaultValue.value,
    existingIds,
    parameterId:
      editingIndex.value != null ? props.parameters[editingIndex.value]?.id : undefined,
    datePart: formDatePart.value,
    yearMin: formYearMin.value,
    yearMax: formYearMaxCurrent.value ? 'currentYear' : formYearMax.value,
    yearIncludeAll: formYearIncludeAll.value,
    includeAllOption: formIncludeAllOption.value,
    allChoiceTitle: t('reporting.parameters.allChoice'),
    boolTrueLabel: t('reporting.bool.true'),
    boolFalseLabel: t('reporting.bool.false'),
  });

  const next = [...props.parameters];
  if (editingIndex.value != null) {
    next[editingIndex.value] = built;
  } else {
    next.push(built);
  }
  emit('update:parameters', next);
  dialogOpen.value = false;
}

function removeAt(index: number) {
  emit('update:parameters', props.parameters.filter((_, i) => i !== index));
}

function moveUp(index: number) {
  if (index <= 0) return;
  const next = [...props.parameters];
  const tmp = next[index - 1]!;
  next[index - 1] = next[index]!;
  next[index] = tmp;
  emit('update:parameters', next);
}

function moveDown(index: number) {
  if (index >= props.parameters.length - 1) return;
  const next = [...props.parameters];
  const tmp = next[index + 1]!;
  next[index + 1] = next[index]!;
  next[index] = tmp;
  emit('update:parameters', next);
}

function clearAll() {
  emit('update:parameters', []);
}

function updateFieldMapEntry(index: number, field: string | null) {
  const row = formFieldMapEntries.value[index];
  if (!row) return;
  row.field = field ?? '';
}

function dialogValid(): boolean {
  if (!formLabel.value.trim()) return false;
  if (
    !isReportingParameterSearchField(formFieldKey.value) &&
    formBindingMode.value !== 'search' &&
    !formFieldKey.value.trim()
  ) {
    return false;
  }
  if (formUseFieldFromParam.value && !formDependsOnParamId.value) return false;
  if (
    !isReportingParameterSearchField(formFieldKey.value) &&
    bindingModesForField(selectedField.value).length === 0
  ) {
    return false;
  }
  return true;
}
</script>

<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      {{ t('reporting.parameters.hint') }}
    </v-alert>

    <div v-if="!disabled" class="d-flex flex-wrap justify-end ga-2 mb-3">
      <v-btn v-if="parameters.length" size="small" variant="text" @click="clearAll">
        {{ t('reporting.parameters.clear') }}
      </v-btn>
      <v-btn size="small" variant="text" @click="emit('reset')">
        {{ t('reporting.parameters.reset') }}
      </v-btn>
      <v-btn color="primary" size="small" variant="tonal" @click="openAddDialog">
        <PlusIcon size="16" class="mr-1" />
        {{ t('reporting.parameters.add') }}
      </v-btn>
    </div>

    <v-alert v-if="disabled" type="warning" variant="tonal" density="compact">
      {{ t('reporting.parameters.noSchema') }}
    </v-alert>

    <v-table v-else-if="parameters.length" density="compact" class="border rounded">
      <thead>
        <tr>
          <th class="text-caption">{{ t('reporting.parameters.colLabel') }}</th>
          <th class="text-caption">{{ t('reporting.parameters.colField') }}</th>
          <th class="text-caption">{{ t('reporting.parameters.colBinding') }}</th>
          <th class="text-caption text-center">{{ t('reporting.parameters.colRequired') }}</th>
          <th class="text-caption text-end">{{ t('reporting.parameters.colActions') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(param, index) in parameters" :key="param.id">
          <td class="text-body-2">{{ param.label }}</td>
          <td class="text-body-2 text-medium-emphasis">
            {{
              isReportingParameterSearchField(inferReportingParameterFieldName(param))
                ? t('reporting.parameters.searchFieldOption')
                : reportingFieldLabel(
                    fieldMap.get(inferReportingParameterFieldName(param)),
                    inferReportingParameterFieldName(param)
                  )
            }}
          </td>
          <td class="text-body-2 text-medium-emphasis">
            {{ reportingParameterBindingDisplayLabel(param, t) }}
          </td>
          <td class="text-center">
            <v-icon v-if="param.required" icon="mdi-check" size="18" color="primary" />
            <span v-else class="text-medium-emphasis">—</span>
          </td>
          <td class="text-end">
            <v-btn icon variant="text" size="x-small" :disabled="index === 0" @click="moveUp(index)">
              <ChevronUpIcon size="16" />
            </v-btn>
            <v-btn
              icon
              variant="text"
              size="x-small"
              :disabled="index === parameters.length - 1"
              @click="moveDown(index)"
            >
              <ChevronDownIcon size="16" />
            </v-btn>
            <v-btn icon variant="text" size="x-small" @click="openEditDialog(index)">
              <PencilIcon size="16" />
            </v-btn>
            <v-btn icon variant="text" size="x-small" color="error" @click="removeAt(index)">
              <TrashIcon size="16" />
            </v-btn>
          </td>
        </tr>
      </tbody>
    </v-table>

    <v-alert v-else-if="!disabled" type="info" variant="tonal" density="compact">
      {{ t('reporting.parameters.empty') }}
    </v-alert>

    <v-dialog v-model="dialogOpen" max-width="560" persistent>
      <v-card>
        <v-card-title class="text-subtitle-1">
          {{
            editingIndex != null
              ? t('reporting.parameters.editTitle')
              : t('reporting.parameters.addTitle')
          }}
        </v-card-title>
        <v-card-text>
          <v-autocomplete
            v-model="formFieldKey"
            :items="formFieldItems"
            item-title="title"
            item-value="value"
            :label="t('reporting.parameters.formField')"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-1"
            @update:model-value="onFieldChange"
          >
            <template #item="{ props: itemProps, item }">
              <v-list-item v-bind="itemProps" :subtitle="item.raw.subtitle" />
            </template>
          </v-autocomplete>
          <p class="text-caption text-medium-emphasis mb-3">
            {{ t('reporting.parameters.formFieldHint') }}
          </p>

          <v-select
            v-if="showBindingModePicker"
            v-model="formBindingMode"
            :items="bindingModeItems"
            item-title="title"
            item-value="value"
            :label="t('reporting.parameters.formBinding')"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-3"
          />

          <v-alert
            v-else-if="selectedField && bindingModeItems.length === 1"
            type="info"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{
              t('reporting.parameters.autoBindingHint', {
                binding: t(`reporting.parameters.bindings.${formBindingMode}`),
              })
            }}
          </v-alert>

          <v-alert
            v-else-if="selectedField && bindingModeItems.length === 0"
            type="warning"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{ t('reporting.parameters.unsupportedFieldType') }}
          </v-alert>

          <v-select
            v-if="showDatePartPicker"
            v-model="formDatePart"
            :items="datePartItems"
            item-title="title"
            item-value="value"
            :label="t('reporting.parameters.formDatePart')"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-3"
          />

          <v-text-field
            v-model="formLabel"
            :label="t('reporting.parameters.formLabel')"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-3"
          />

          <v-checkbox
            v-model="formRequired"
            :label="t('reporting.parameters.formRequired')"
            density="compact"
            hide-details
            class="mb-2"
          />

          <v-text-field
            v-if="formBindingMode !== 'search' && formBindingMode !== 'dateRange'"
            v-model="formDefaultValue"
            :label="t('reporting.parameters.formDefaultValue')"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            class="mb-3"
          />

          <template v-if="showYearOptions">
            <v-text-field
              v-model.number="formYearMin"
              type="number"
              :label="t('reporting.parameters.formYearMin')"
              density="compact"
              variant="outlined"
              hide-details
              class="mb-3"
            />
            <v-checkbox
              v-model="formYearMaxCurrent"
              :label="t('reporting.parameters.formYearMaxCurrent')"
              density="compact"
              hide-details
              class="mb-2"
            />
            <v-text-field
              v-if="!formYearMaxCurrent"
              v-model.number="formYearMax"
              type="number"
              :label="t('reporting.parameters.formYearMax')"
              density="compact"
              variant="outlined"
              hide-details
              class="mb-3"
            />
          </template>

          <v-checkbox
            v-if="showIncludeAllOption && formBindingMode === 'datePart' && formDatePart === 'year'"
            v-model="formYearIncludeAll"
            :label="t('reporting.parameters.formYearIncludeAll')"
            density="compact"
            hide-details
            class="mb-3"
          />

          <v-checkbox
            v-if="showIncludeAllOption && formBindingMode !== 'datePart'"
            v-model="formIncludeAllOption"
            :label="t('reporting.parameters.formIncludeAll')"
            density="compact"
            hide-details
            class="mb-3"
          />

          <v-expansion-panels v-if="showAdvancedDateMapping" v-model="advancedPanels" class="mb-2">
            <v-expansion-panel>
              <v-expansion-panel-title class="text-body-2">
                {{ t('reporting.parameters.advancedTitle') }}
              </v-expansion-panel-title>
              <v-expansion-panel-text>
                <p class="text-caption text-medium-emphasis mb-3">
                  {{ t('reporting.parameters.fieldFromParamHint') }}
                </p>
                <v-checkbox
                  v-model="formUseFieldFromParam"
                  :label="t('reporting.parameters.formUseFieldFromParam')"
                  density="compact"
                  hide-details
                  class="mb-2"
                />
                <template v-if="formUseFieldFromParam">
                  <v-select
                    v-model="formDependsOnParamId"
                    :items="dependsOnParamItems"
                    item-title="title"
                    item-value="value"
                    :label="t('reporting.parameters.formDependsOn')"
                    density="compact"
                    variant="outlined"
                    hide-details
                    class="mb-3"
                  />
                  <v-select
                    v-model="formDefaultDateField"
                    :items="datetimeFieldItems"
                    item-title="title"
                    item-value="value"
                    :label="t('reporting.parameters.formDefaultDateField')"
                    density="compact"
                    variant="outlined"
                    hide-details
                    clearable
                    class="mb-3"
                  />
                  <v-checkbox
                    v-model="formSkipAllChoice"
                    :label="t('reporting.parameters.formSkipAllChoice')"
                    density="compact"
                    hide-details
                    class="mb-3"
                  />
                  <div
                    v-for="(entry, idx) in formFieldMapEntries"
                    :key="entry.choice"
                    class="d-flex ga-2 mb-2 align-center"
                  >
                    <v-text-field
                      :model-value="entry.choice"
                      :label="t('reporting.parameters.formChoiceValue')"
                      density="compact"
                      variant="outlined"
                      hide-details
                      readonly
                      style="max-width: 140px"
                    />
                    <v-select
                      :model-value="entry.field"
                      :items="datetimeFieldItems"
                      item-title="title"
                      item-value="value"
                      :label="t('reporting.parameters.formDateField')"
                      density="compact"
                      variant="outlined"
                      hide-details
                      @update:model-value="(v) => updateFieldMapEntry(idx, v)"
                    />
                  </div>
                </template>
              </v-expansion-panel-text>
            </v-expansion-panel>
          </v-expansion-panels>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialogOpen = false">
            {{ t('reporting.parameters.cancel') }}
          </v-btn>
          <v-btn color="primary" variant="flat" :disabled="!dialogValid()" @click="saveDialog">
            {{ t('reporting.parameters.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
