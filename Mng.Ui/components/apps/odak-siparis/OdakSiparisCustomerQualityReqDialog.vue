<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakCustomerQualityReqRow } from '@/utils/odakSiparisConfig';
import {
  createOdakCustomerQualityReq,
  emptyCustomerQualityReqFormModel,
  fetchOdakCustomerQualityReqById,
  listQualityReqsForCustomer,
  qualityReqRowToFormModel,
  updateOdakCustomerQualityReq,
  validateCustomerQualityReqForm,
  type OdakCustomerQualityReqDialogMode,
  type OdakCustomerQualityReqFormModel,
} from '@/utils/odakSiparisCustomerQualityReqService';

const props = defineProps<{
  modelValue: boolean;
  mode: OdakCustomerQualityReqDialogMode;
  customerId: string;
  reqId?: string;
  seedRow?: OdakCustomerQualityReqRow | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  saved: [];
}>();

const { t } = useAppI18n();

const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const form = reactive<OdakCustomerQualityReqFormModel>(emptyCustomerQualityReqFormModel());
const existingRows = ref<OdakCustomerQualityReqRow[]>([]);

const isEdit = computed(() => props.mode === 'edit');

const dialogTitle = computed(() =>
  isEdit.value
    ? t('odakSiparis.customers.qualityReqs.dialog.editTitle', { name: form.ad || form.kod || '—' })
    : t('odakSiparis.customers.qualityReqs.dialog.createTitle')
);

async function loadDialog() {
  if (!props.modelValue) return;
  errorMessage.value = '';
  loading.value = true;
  try {
    existingRows.value = await listQualityReqsForCustomer(props.customerId);
    if (props.mode === 'create') {
      Object.assign(form, emptyCustomerQualityReqFormModel());
      return;
    }
    const id = props.reqId;
    if (!id) throw new Error(t('odakSiparis.customers.qualityReqs.dialog.missingId'));
    if (props.seedRow) {
      Object.assign(form, qualityReqRowToFormModel(props.seedRow));
    }
    const full = await fetchOdakCustomerQualityReqById(id);
    if (!full) throw new Error(t('odakSiparis.customers.qualityReqs.dialog.notFound'));
    Object.assign(form, qualityReqRowToFormModel(full));
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

function closeDialog() {
  emit('update:modelValue', false);
}

async function saveReq() {
  errorMessage.value = '';
  const validationKey = validateCustomerQualityReqForm(form, existingRows.value, props.reqId);
  if (validationKey) {
    errorMessage.value = t(`odakSiparis.customers.qualityReqs.validation.${validationKey}`);
    return;
  }
  if (!props.customerId) {
    errorMessage.value = t('odakSiparis.customers.qualityReqs.dialog.missingCustomer');
    return;
  }
  saving.value = true;
  try {
    if (props.mode === 'create') {
      await createOdakCustomerQualityReq(props.customerId, form);
    } else if (props.reqId) {
      await updateOdakCustomerQualityReq(props.reqId, props.customerId, form);
    }
    emit('saved');
    closeDialog();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

watch(
  () => [props.modelValue, props.reqId, props.mode] as const,
  ([open]) => {
    if (open) void loadDialog();
  }
);
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="640" persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="py-4">{{ dialogTitle }}</v-card-title>
      <v-divider />
      <v-card-text class="pt-4">
        <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
          {{ errorMessage }}
        </v-alert>
        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

        <v-row dense>
          <v-col cols="12" sm="4">
            <v-text-field
              v-model="form.kod"
              :label="t('odakSiparis.customers.qualityReqs.fields.kod')"
              variant="outlined"
              density="comfortable"
              :disabled="loading || saving"
            />
          </v-col>
          <v-col cols="12" sm="8">
            <v-text-field
              v-model="form.ad"
              :label="t('odakSiparis.customers.qualityReqs.fields.ad')"
              variant="outlined"
              density="comfortable"
              :disabled="loading || saving"
            />
          </v-col>
          <v-col cols="12">
            <v-textarea
              v-model="form.aciklama"
              :label="t('odakSiparis.customers.qualityReqs.fields.aciklama')"
              variant="outlined"
              density="comfortable"
              rows="2"
              auto-grow
              :disabled="loading || saving"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-switch
              v-model="form.faiUygulanacak"
              :label="t('odakSiparis.customers.qualityReqs.fields.faiUygulanacak')"
              color="primary"
              hide-details
              :disabled="loading || saving"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-switch
              v-model="form.aktif"
              :label="t('odakSiparis.customers.qualityReqs.fields.aktif')"
              color="primary"
              hide-details
              :disabled="loading || saving"
            />
          </v-col>
        </v-row>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="closeDialog">
          {{ t('odakSiparis.packages.cancel') }}
        </v-btn>
        <v-btn color="primary" variant="flat" :loading="saving" :disabled="loading" @click="saveReq">
          {{ t('odakSiparis.packages.settings.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
