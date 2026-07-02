<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  ODAK_FAI_STATUS_OPTIONS,
  ODAK_NCR_STATUS_OPTIONS,
  type OdakNcrRow,
} from '@/utils/odakSiparisConfig';
import { formatLineSelectLabel, listLinesForPackage, lineDataId } from '@/utils/odakSiparisLineService';
import {
  createOdakNcr,
  emptyNcrFormModel,
  fetchOdakNcrById,
  ncrDisplayNo,
  ncrRowToFormModel,
  updateOdakNcr,
  type OdakNcrDialogMode,
  type OdakNcrFormModel,
} from '@/utils/odakSiparisNcrService';

const props = defineProps<{
  modelValue: boolean;
  mode: OdakNcrDialogMode;
  packageId?: string;
  packageNo?: string;
  ncrId?: string;
  seedRow?: OdakNcrRow | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  saved: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const internalMode = ref<OdakNcrDialogMode>('view');
const loadedRow = ref<OdakNcrRow | null>(null);
const form = reactive<OdakNcrFormModel>(emptyNcrFormModel());
const lineItems = ref<{ value: string; title: string }[]>([]);

const readonly = computed(() => internalMode.value === 'view');
const isGeneralMode = computed(() => !props.packageId?.trim());
const statusItems = computed(() =>
  ODAK_NCR_STATUS_OPTIONS.map((o) => ({ value: o.value, title: o.title }))
);
const faiItems = computed(() =>
  ODAK_FAI_STATUS_OPTIONS.map((o) => ({ value: o.value, title: o.title }))
);

const dialogTitle = computed(() => {
  const pkg = props.packageNo ? ` · ${props.packageNo}` : '';
  if (internalMode.value === 'create') return t('odakSiparis.quality.ncr.dialog.createTitle') + pkg;
  if (internalMode.value === 'edit') return t('odakSiparis.quality.ncr.dialog.editTitle') + pkg;
  return t('odakSiparis.quality.ncr.dialog.viewTitle', { ncrNo: ncrDisplayNo(loadedRow.value ?? {}) }) + pkg;
});

async function loadLineOptions() {
  if (!props.packageId) {
    lineItems.value = [];
    return;
  }
  try {
    const lines = await listLinesForPackage(props.packageId);
    const seen = new Set<string>();
    lineItems.value = lines
      .map((line) => {
        const id = lineDataId(line);
        if (!id || seen.has(id)) return null;
        seen.add(id);
        return { value: id, title: formatLineSelectLabel(line) };
      })
      .filter((x): x is { value: string; title: string } => !!x);
  } catch {
    lineItems.value = [];
  }
}

async function loadDialogData() {
  if (!props.modelValue) return;
  internalMode.value = props.mode;
  errorMessage.value = '';
  loading.value = true;
  loadedRow.value = null;
  try {
    await loadLineOptions();
    if (internalMode.value === 'create') {
      Object.assign(form, emptyNcrFormModel());
      return;
    }
    const id = props.ncrId;
    if (!id) throw new Error(t('odakSiparis.quality.ncr.dialog.missingId'));
    if (props.seedRow && props.mode === 'view') {
      Object.assign(form, ncrRowToFormModel(props.seedRow));
      loadedRow.value = props.seedRow;
    }
    const full = await fetchOdakNcrById(id);
    if (!full) throw new Error(t('odakSiparis.quality.ncr.dialog.notFound'));
    loadedRow.value = full;
    Object.assign(form, ncrRowToFormModel(full));
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    loading.value = false;
  }
}

function closeDialog() {
  emit('update:modelValue', false);
}

function switchToEdit() {
  internalMode.value = 'edit';
}

async function saveNcr() {
  errorMessage.value = '';
  if (!form.descriptor.trim()) {
    errorMessage.value = t('odakSiparis.quality.ncr.validation.descriptorRequired');
    return;
  }
  saving.value = true;
  try {
    if (internalMode.value === 'create') {
      await createOdakNcr(form, props.packageId || undefined);
    } else if (props.ncrId) {
      await updateOdakNcr(props.ncrId, form, props.packageId || undefined);
    }
    emit('saved');
    closeDialog();
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    saving.value = false;
  }
}

watch(
  () => [props.modelValue, props.mode, props.ncrId] as const,
  () => {
    if (props.modelValue) void loadDialogData();
  }
);
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="860"
    scrollable
    @update:model-value="emit('update:modelValue', $event)"
  >
    <v-card>
      <v-card-title class="d-flex align-center py-4 px-5">
        <div>
          <div class="text-h6">{{ dialogTitle }}</div>
          <div v-if="internalMode === 'view'" class="text-caption text-medium-emphasis">
            {{ t('odakSiparis.quality.ncr.dialog.viewHint') }}
          </div>
        </div>
        <v-spacer />
        <v-btn icon variant="text" size="small" @click="closeDialog">
          <v-icon>mdi-close</v-icon>
        </v-btn>
      </v-card-title>
      <v-divider />
      <v-card-text class="px-5 py-4">
        <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-4">
          {{ errorMessage }}
        </v-alert>
        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />
        <v-row dense>
          <v-col v-if="internalMode !== 'create'" cols="12" sm="6">
            <v-text-field
              :model-value="ncrDisplayNo(loadedRow ?? {})"
              :label="t('odakSiparis.quality.ncr.fields.ncrNo')"
              readonly
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-select
              v-model="form.ncStatus"
              :items="statusItems"
              item-title="title"
              item-value="value"
              :label="t('odakSiparis.quality.ncr.fields.ncStatus')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.ncDate"
              type="date"
              :label="t('odakSiparis.quality.ncr.fields.ncDate')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.controlType"
              :label="t('odakSiparis.quality.ncr.fields.controlType')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-text-field
              v-model="form.descriptor"
              :label="t('odakSiparis.quality.ncr.fields.descriptor')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-textarea
              v-model="form.explanation"
              :label="t('odakSiparis.quality.ncr.fields.explanation')"
              :readonly="readonly"
              rows="3"
              auto-grow
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.productCode"
              :label="t('odakSiparis.quality.ncr.fields.productCode')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.jobNo"
              :label="t('odakSiparis.quality.ncr.fields.jobNo')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col v-if="!isGeneralMode" cols="12" sm="6">
            <v-select
              v-model="form.parentLineId"
              :items="lineItems"
              item-title="title"
              item-value="value"
              :label="t('odakSiparis.quality.ncr.fields.parentLineId')"
              :readonly="readonly"
              clearable
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="3">
            <v-text-field
              v-model.number="form.partCount"
              type="number"
              min="0"
              :label="t('odakSiparis.quality.ncr.fields.partCount')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="3">
            <v-text-field
              v-model.number="form.scrapCount"
              type="number"
              min="0"
              :label="t('odakSiparis.quality.ncr.fields.scrapCount')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="3">
            <v-text-field
              v-model.number="form.reworkCount"
              type="number"
              min="0"
              :label="t('odakSiparis.quality.ncr.fields.reworkCount')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-select
              v-model="form.faiStatus"
              :items="faiItems"
              item-title="title"
              item-value="value"
              :label="t('odakSiparis.quality.ncr.fields.faiStatus')"
              :readonly="readonly"
              clearable
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.errorCode"
              :label="t('odakSiparis.quality.ncr.fields.errorCode')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-textarea
              v-model="form.ncAction"
              :label="t('odakSiparis.quality.ncr.fields.ncAction')"
              :readonly="readonly"
              rows="2"
              auto-grow
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.responsible"
              :label="t('odakSiparis.quality.ncr.fields.responsible')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col v-if="isGeneralMode" cols="12" sm="6">
            <v-text-field
              v-model="form.supplierRef"
              :label="t('odakSiparis.quality.ncr.fields.supplierRef')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.closureDate"
              type="date"
              :label="t('odakSiparis.quality.ncr.fields.closureDate')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-textarea
              v-model="form.notes"
              :label="t('odakSiparis.quality.ncr.fields.notes')"
              :readonly="readonly"
              rows="2"
              auto-grow
              variant="outlined"
              density="compact"
            />
          </v-col>
        </v-row>
      </v-card-text>
      <v-divider />
      <v-card-actions class="px-5 py-3">
        <v-spacer />
        <template v-if="internalMode === 'view'">
          <v-btn variant="text" @click="closeDialog">{{ t('odakSiparis.lines.dialog.close') }}</v-btn>
          <v-btn color="primary" variant="flat" @click="switchToEdit">
            {{ t('odakSiparis.lines.dialog.edit') }}
          </v-btn>
        </template>
        <template v-else>
          <v-btn variant="text" @click="closeDialog">{{ t('odakSiparis.lines.dialog.cancel') }}</v-btn>
          <v-btn color="primary" variant="flat" :loading="saving" @click="saveNcr">
            {{ internalMode === 'create' ? t('odakSiparis.lines.dialog.create') : t('odakSiparis.lines.dialog.save') }}
          </v-btn>
        </template>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
