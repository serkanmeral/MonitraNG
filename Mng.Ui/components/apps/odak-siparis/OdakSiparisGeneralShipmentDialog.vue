<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  ODAK_QCF_STATUS_OPTIONS,
  ODAK_SHIPMENT_STATUS_OPTIONS,
  type OdakShipmentRow,
} from '@/utils/odakSiparisConfig';
import { fetchCustomerRelationOptions } from '@/utils/odakSiparisService';
import {
  createGeneralOdakShipment,
  emptyGeneralShipmentFormModel,
  fetchOdakShipmentById,
  loadGeneralShipmentFormModel,
  shipmentHeaderToFormModel,
  type OdakShipmentDialogMode,
  updateGeneralOdakShipment,
  type OdakGeneralShipmentFormModel,
} from '@/utils/odakSiparisShipmentService';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  modelValue: boolean;
  mode: OdakShipmentDialogMode;
  shipmentId?: string;
  seedRow?: OdakShipmentRow | null;
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
const internalMode = ref<OdakShipmentDialogMode>('view');
const loadedRow = ref<OdakShipmentRow | null>(null);
const form = reactive<OdakGeneralShipmentFormModel>(emptyGeneralShipmentFormModel());
const customerOptions = ref<{ value: string; title: string }[]>([]);

const readonly = computed(() => internalMode.value === 'view');
const statusItems = computed(() =>
  ODAK_SHIPMENT_STATUS_OPTIONS.map((o) => ({ value: o.value, title: o.title }))
);
const qcfItems = computed(() => ODAK_QCF_STATUS_OPTIONS.map((o) => ({ value: o.value, title: o.title })));

const dialogTitle = computed(() => {
  if (internalMode.value === 'create') return t('odakSiparis.globalShipments.dialog.createTitle');
  if (internalMode.value === 'edit') return t('odakSiparis.globalShipments.dialog.editTitle');
  const no = loadedRow.value?.waybillNo || props.seedRow?.waybillNo || '—';
  return t('odakSiparis.shipments.dialog.viewTitle', { waybillNo: no });
});

async function loadDialog() {
  if (!props.modelValue) return;
  internalMode.value = props.mode;
  errorMessage.value = '';
  loading.value = true;
  try {
    customerOptions.value = await fetchCustomerRelationOptions();
    if (internalMode.value === 'create') {
      Object.assign(form, emptyGeneralShipmentFormModel());
      loadedRow.value = null;
      return;
    }
    const id = props.shipmentId;
    if (!id) {
      if (props.seedRow) {
        Object.assign(form, emptyGeneralShipmentFormModel({
          ...shipmentHeaderToFormModel(props.seedRow),
          customerId: '',
          headerDescription: props.seedRow.headerDescription ?? '',
        }));
        loadedRow.value = props.seedRow;
      }
      return;
    }
    const model = await loadGeneralShipmentFormModel(id);
    if (!model) throw new Error(t('odakSiparis.shipments.dialog.notFound'));
    Object.assign(form, model);
    loadedRow.value = (await fetchOdakShipmentById(id)) ?? props.seedRow ?? null;
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    loading.value = false;
  }
}

function addLineRow() {
  form.lines.push({ lineDescription: '', shippedQuantity: 1 });
}

function removeLineRow(index: number) {
  if (form.lines.length <= 1) {
    form.lines[0] = { lineDescription: '', shippedQuantity: 1 };
    return;
  }
  form.lines.splice(index, 1);
}

async function save() {
  saving.value = true;
  errorMessage.value = '';
  try {
    if (internalMode.value === 'create') {
      await createGeneralOdakShipment(form);
    } else {
      const id = props.shipmentId;
      if (!id) throw new Error(t('odakSiparis.shipments.dialog.missingId'));
      await updateGeneralOdakShipment(id, form);
    }
    emit('saved');
    emit('update:modelValue', false);
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    saving.value = false;
  }
}

function switchToEdit() {
  internalMode.value = 'edit';
}

watch(
  () => [props.modelValue, props.mode, props.shipmentId] as const,
  () => {
    if (props.modelValue) void loadDialog();
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
      <v-card-title class="d-flex align-center py-4 px-5">
        <span class="text-h6">{{ dialogTitle }}</span>
        <v-spacer />
        <v-btn icon variant="text" size="small" @click="emit('update:modelValue', false)">
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
          <v-col cols="12" sm="6">
            <v-autocomplete
              v-model="form.customerId"
              :items="customerOptions"
              item-title="title"
              item-value="value"
              :label="t('odakSiparis.globalShipments.fields.customerId')"
              :readonly="readonly"
              clearable
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.waybillNo"
              :label="t('odakSiparis.shipments.fields.waybillNo')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-textarea
              v-model="form.headerDescription"
              :label="t('odakSiparis.globalShipments.fields.headerDescription')"
              :readonly="readonly"
              rows="2"
              auto-grow
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.shipmentDate"
              type="date"
              :label="t('odakSiparis.shipments.fields.shipmentDate')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-select
              v-model="form.status"
              :items="statusItems"
              item-title="title"
              item-value="value"
              :label="t('odakSiparis.shipments.fields.status')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.controlType"
              :label="t('odakSiparis.shipments.fields.controlType')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-text-field
              v-model="form.shipmentAddress"
              :label="t('odakSiparis.shipments.fields.shipmentAddress')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-textarea
              v-model="form.notes"
              :label="t('odakSiparis.shipments.fields.notes')"
              :readonly="readonly"
              rows="2"
              auto-grow
              variant="outlined"
              density="compact"
            />
          </v-col>
        </v-row>

        <div class="text-subtitle-2 mt-4 mb-2">{{ t('odakSiparis.globalShipments.dialog.linesTitle') }}</div>
        <div v-for="(line, index) in form.lines" :key="index" class="d-flex ga-2 mb-2 align-start">
          <v-text-field
            v-model="line.lineDescription"
            :label="t('odakSiparis.globalShipments.fields.lineDescription')"
            :readonly="readonly"
            class="flex-grow-1"
            variant="outlined"
            density="compact"
          />
          <v-text-field
            v-model.number="line.shippedQuantity"
            type="number"
            min="0"
            :label="t('odakSiparis.shipments.dialog.shippedQty')"
            :readonly="readonly"
            style="max-width: 120px"
            variant="outlined"
            density="compact"
          />
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
        </div>
        <v-btn v-if="!readonly" variant="tonal" size="small" @click="addLineRow">
          <PlusIcon size="16" class="mr-1" />
          {{ t('odakSiparis.globalShipments.dialog.addLine') }}
        </v-btn>
      </v-card-text>
      <v-divider />
      <v-card-actions class="px-5 py-3">
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">
          {{ t('odakSiparis.packages.cancel') }}
        </v-btn>
        <v-btn v-if="readonly && internalMode !== 'create'" color="primary" variant="tonal" @click="switchToEdit">
          {{ t('odakSiparis.packages.edit') }}
        </v-btn>
        <v-btn v-if="!readonly" color="primary" variant="flat" :loading="saving" @click="save">
          {{ t('odakSiparis.packages.settings.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
