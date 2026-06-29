<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useOdakFieldAccess } from '@/composables/useOdakFieldAccess';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ODAK_QCF_STATUS_OPTIONS,
  ODAK_SHIPMENT_STATUS_OPTIONS,
  type OdakShipmentRow,
} from '@/utils/odakSiparisConfig';
import type { OdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import { shipmentRecordForPolicyEval } from '@/utils/odakSiparisFieldPolicies';
import { ODAK_SHIPMENT_LIST_KEY_TO_FIELD } from '@/utils/odakSiparisShipmentListSettings';
import { loadOdakShipmentFieldPoliciesOnly } from '@/utils/odakSiparisHubSettingsService';
import { formatLineSelectLabel, lineDataId, listLinesForPackage } from '@/utils/odakSiparisLineService';
import {
  createOdakShipment,
  emptyShipmentFormModel,
  fetchOdakShipmentById,
  loadShipmentFormModel,
  shipmentHeaderToFormModel,
  updateOdakShipment,
  type OdakShipmentDialogMode,
  type OdakShipmentFormModel,
} from '@/utils/odakSiparisShipmentService';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  modelValue: boolean;
  mode: OdakShipmentDialogMode;
  packageId: string;
  packageNo?: string;
  shipmentId?: string;
  seedRow?: OdakShipmentRow | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  saved: [];
}>();

const { t } = useAppI18n();

const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const internalMode = ref<OdakShipmentDialogMode>('view');
const loadedRow = ref<OdakShipmentRow | null>(null);
const form = reactive<OdakShipmentFormModel>(emptyShipmentFormModel());
const fieldPolicies = ref<OdakFieldPoliciesBlob>({ policiesByField: {} });
const { canViewField, canEditField } = useOdakFieldAccess(fieldPolicies, ODAK_SHIPMENT_LIST_KEY_TO_FIELD);
const lineOptions = ref<{ value: string; title: string; quantity: number; shippedQuantity: number }[]>([]);

const readonly = computed(() => internalMode.value === 'view');

const policyRecord = computed(() =>
  loadedRow.value ? shipmentRecordForPolicyEval(loadedRow.value) : shipmentRecordForPolicyEval(form)
);

function fieldVisible(fieldKey: string): boolean {
  return canViewField(fieldKey, policyRecord.value);
}

function isFieldReadonly(fieldKey: string): boolean {
  return readonly.value || !canEditField(fieldKey, policyRecord.value);
}

const showQcfGroup = computed(
  () =>
    fieldVisible('qcfStatus') ||
    fieldVisible('qcfReferenceNo') ||
    fieldVisible('qcfNotes')
);

const statusItems = computed(() =>
  ODAK_SHIPMENT_STATUS_OPTIONS.map((o) => ({ value: o.value, title: o.title }))
);
const qcfItems = computed(() =>
  ODAK_QCF_STATUS_OPTIONS.map((o) => ({ value: o.value, title: o.title }))
);

const dialogTitle = computed(() => {
  const pkg = props.packageNo ? ` · ${props.packageNo}` : '';
  if (internalMode.value === 'create') return t('odakSiparis.shipments.dialog.createTitle') + pkg;
  if (internalMode.value === 'edit') return t('odakSiparis.shipments.dialog.editTitle') + pkg;
  const no = loadedRow.value?.waybillNo || props.seedRow?.waybillNo || '—';
  return t('odakSiparis.shipments.dialog.viewTitle', { waybillNo: no }) + pkg;
});

async function loadLineOptions() {
  if (!props.packageId) {
    lineOptions.value = [];
    return;
  }
  try {
    const lines = await listLinesForPackage(props.packageId);
    const seen = new Set<string>();
    lineOptions.value = lines
      .map((line) => {
        const id = lineDataId(line);
        if (!id || seen.has(id)) return null;
        seen.add(id);
        return {
          value: id,
          title: formatLineSelectLabel(line),
          quantity: Number(line.quantity) || 0,
          shippedQuantity: Number(line.shippedQuantity) || 0,
        };
      })
      .filter((x): x is { value: string; title: string; quantity: number; shippedQuantity: number } => !!x);
  } catch {
    lineOptions.value = [];
  }
}

function resetFormFromRow(row: OdakShipmentRow, lines?: OdakShipmentFormModel['lines']) {
  Object.assign(form, emptyShipmentFormModel({ ...shipmentHeaderToFormModel(row), lines }));
}

async function loadShipment() {
  errorMessage.value = '';
  fieldPolicies.value = await loadOdakShipmentFieldPoliciesOnly();
  if (internalMode.value === 'create') {
    Object.assign(form, emptyShipmentFormModel());
    loadedRow.value = null;
    return;
  }
  const id = props.shipmentId;
  if (!id) {
    if (props.seedRow) resetFormFromRow(props.seedRow);
    return;
  }
  loading.value = true;
  try {
    const model = await loadShipmentFormModel(id, props.packageId);
    if (!model) {
      errorMessage.value = t('odakSiparis.shipments.dialog.notFound');
      return;
    }
    Object.assign(form, model);
    loadedRow.value = (await fetchOdakShipmentById(id)) ?? props.seedRow ?? null;
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

function addLineRow() {
  form.lines.push({ parentLineId: '', shippedQuantity: 1 });
}

function removeLineRow(index: number) {
  if (form.lines.length <= 1) {
    form.lines[0] = { parentLineId: '', shippedQuantity: 1 };
    return;
  }
  form.lines.splice(index, 1);
}

function lineHint(lineId: string): string {
  const opt = lineOptions.value.find((o) => o.value === lineId);
  if (!opt) return '';
  const remaining = Math.max(0, opt.quantity - opt.shippedQuantity);
  return t('odakSiparis.shipments.dialog.lineHint', {
    quantity: opt.quantity,
    shipped: opt.shippedQuantity,
    remaining,
  });
}

async function save() {
  saving.value = true;
  errorMessage.value = '';
  try {
    if (internalMode.value === 'create') {
      await createOdakShipment(props.packageId, form);
    } else {
      const id = props.shipmentId;
      if (!id) throw new Error(t('odakSiparis.shipments.dialog.missingId'));
      await updateOdakShipment(id, props.packageId, form);
    }
    emit('saved');
    emit('update:modelValue', false);
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

function switchToEdit() {
  internalMode.value = 'edit';
}

watch(
  () => props.modelValue,
  async (open) => {
    if (!open) {
      lineOptions.value = [];
      return;
    }
    internalMode.value = props.mode;
    await loadLineOptions();
    await loadShipment();
  }
);

watch(
  () => props.mode,
  (mode) => {
    if (props.modelValue) internalMode.value = mode;
  }
);
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="920"
    scrollable
    @update:model-value="emit('update:modelValue', $event)"
  >
    <v-card>
      <v-card-title class="d-flex align-center">
        <span>{{ dialogTitle }}</span>
        <v-spacer />
        <v-btn icon variant="text" @click="emit('update:modelValue', false)">
          <span class="text-h6">×</span>
        </v-btn>
      </v-card-title>

      <v-card-text>
        <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
          {{ errorMessage }}
        </v-alert>
        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

        <v-row dense>
          <v-col v-if="fieldVisible('waybillNo')" cols="12" sm="6">
            <v-text-field
              v-model="form.waybillNo"
              :label="t('odakSiparis.shipments.fields.waybillNo')"
              :readonly="isFieldReadonly('waybillNo')"
              density="compact"
              variant="outlined"
            />
          </v-col>
          <v-col v-if="fieldVisible('shipmentDate')" cols="12" sm="6">
            <v-text-field
              v-model="form.shipmentDate"
              :label="t('odakSiparis.shipments.fields.shipmentDate')"
              type="date"
              :readonly="isFieldReadonly('shipmentDate')"
              density="compact"
              variant="outlined"
            />
          </v-col>
          <v-col v-if="fieldVisible('status')" cols="12" sm="6">
            <v-select
              v-model="form.status"
              :items="statusItems"
              item-title="title"
              item-value="value"
              :label="t('odakSiparis.shipments.fields.status')"
              :readonly="isFieldReadonly('status')"
              density="compact"
              variant="outlined"
            />
          </v-col>
          <v-col v-if="fieldVisible('controlType')" cols="12" sm="6">
            <v-text-field
              v-model="form.controlType"
              :label="t('odakSiparis.shipments.fields.controlType')"
              :readonly="isFieldReadonly('controlType')"
              density="compact"
              variant="outlined"
            />
          </v-col>
          <v-col v-if="fieldVisible('shipmentAddress')" cols="12">
            <v-textarea
              v-model="form.shipmentAddress"
              :label="t('odakSiparis.shipments.fields.shipmentAddress')"
              :readonly="isFieldReadonly('shipmentAddress')"
              rows="2"
              density="compact"
              variant="outlined"
            />
          </v-col>
          <v-col v-if="fieldVisible('notes')" cols="12">
            <v-textarea
              v-model="form.notes"
              :label="t('odakSiparis.shipments.fields.notes')"
              :readonly="isFieldReadonly('notes')"
              rows="2"
              density="compact"
              variant="outlined"
            />
          </v-col>
        </v-row>

        <v-divider v-if="showQcfGroup" class="my-4" />
        <div v-if="showQcfGroup" class="text-subtitle-2 font-weight-medium mb-2">
          {{ t('odakSiparis.shipments.fields.qcfGroup') }}
        </div>
        <v-row v-if="showQcfGroup" dense>
          <v-col v-if="fieldVisible('qcfStatus')" cols="12" sm="4">
            <v-select
              v-model="form.qcfStatus"
              :items="qcfItems"
              item-title="title"
              item-value="value"
              :label="t('odakSiparis.shipments.fields.qcfStatus')"
              :readonly="isFieldReadonly('qcfStatus')"
              density="compact"
              variant="outlined"
            />
          </v-col>
          <v-col v-if="fieldVisible('qcfReferenceNo')" cols="12" sm="4">
            <v-text-field
              v-model="form.qcfReferenceNo"
              :label="t('odakSiparis.shipments.fields.qcfReferenceNo')"
              :readonly="isFieldReadonly('qcfReferenceNo')"
              density="compact"
              variant="outlined"
            />
          </v-col>
          <v-col v-if="fieldVisible('qcfNotes')" cols="12" sm="4">
            <v-text-field
              v-model="form.qcfNotes"
              :label="t('odakSiparis.shipments.fields.qcfNotes')"
              :readonly="isFieldReadonly('qcfNotes')"
              density="compact"
              variant="outlined"
            />
          </v-col>
        </v-row>

        <v-divider class="my-4" />
        <div class="d-flex align-center mb-2">
          <div class="text-subtitle-2 font-weight-medium">
            {{ t('odakSiparis.shipments.dialog.linesTitle') }}
          </div>
          <v-spacer />
          <v-btn v-if="!readonly" size="small" variant="tonal" color="primary" @click="addLineRow">
            <PlusIcon class="mr-1" size="16" />
            {{ t('odakSiparis.shipments.dialog.addLine') }}
          </v-btn>
        </div>

        <div v-for="(row, index) in form.lines" :key="index" class="mb-3 pa-3 border rounded-md">
          <v-row dense align="center">
            <v-col cols="12" md="7">
              <v-select
                :key="`line-select-${index}-${packageId}`"
                v-model="row.parentLineId"
                :items="lineOptions"
                item-title="title"
                item-value="value"
                :label="t('odakSiparis.shipments.dialog.lineSelect')"
                :readonly="readonly"
                density="compact"
                variant="outlined"
              />
              <div v-if="row.parentLineId && !readonly" class="text-caption text-medium-emphasis mt-1">
                {{ lineHint(row.parentLineId) }}
              </div>
            </v-col>
            <v-col cols="12" md="3">
              <v-text-field
                v-model.number="row.shippedQuantity"
                :label="t('odakSiparis.shipments.dialog.shippedQty')"
                type="number"
                min="0"
                step="1"
                :readonly="readonly"
                density="compact"
                variant="outlined"
              />
            </v-col>
            <v-col cols="12" md="2" class="text-end">
              <v-btn
                v-if="!readonly"
                icon
                variant="text"
                color="error"
                size="small"
                @click="removeLineRow(index)"
              >
                <TrashIcon size="18" />
              </v-btn>
            </v-col>
          </v-row>
        </div>
      </v-card-text>

      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">
          {{ readonly ? t('odakSiparis.lines.dialog.close') : t('odakSiparis.lines.dialog.cancel') }}
        </v-btn>
        <v-btn v-if="readonly" color="primary" variant="flat" @click="switchToEdit">
          {{ t('odakSiparis.lines.dialog.edit') }}
        </v-btn>
        <v-btn v-else color="primary" variant="flat" :loading="saving" @click="save">
          {{ internalMode === 'create' ? t('odakSiparis.lines.dialog.create') : t('odakSiparis.lines.dialog.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
