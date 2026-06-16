<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakPackageRow } from '@/utils/odakSiparisConfig';
import {
  createOdakPackage,
  emptyPackageFormModel,
  loadPackageForEdit,
  loadPackageFormContext,
  packageRowToFormModel,
  updateOdakPackage,
  type OdakPackageDialogMode,
  type OdakPackageFormModel,
} from '@/utils/odakSiparisPackageService';
import { packageDisplayNo } from '@/utils/odakSiparisService';

const props = defineProps<{
  modelValue: boolean;
  mode: OdakPackageDialogMode;
  packageId?: string;
  seedRow?: OdakPackageRow | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  saved: [packageId?: string];
}>();

const { t } = useAppI18n();

const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const customerItems = ref<{ value: string; title: string }[]>([]);
const form = reactive<OdakPackageFormModel>(emptyPackageFormModel());

const isEdit = computed(() => props.mode === 'edit');

const dialogTitle = computed(() =>
  isEdit.value
    ? t('odakSiparis.packages.dialog.editTitle', { no: packageDisplayNo(props.seedRow ?? { packageNo: form.packageNo }) })
    : t('odakSiparis.packages.dialog.createTitle')
);

const statusItems = computed(() => [
  { value: 'open', title: t('odakSiparis.packages.tabs.open') },
  { value: 'closed', title: t('odakSiparis.packages.tabs.closed') },
]);

async function loadDialog() {
  if (!props.modelValue) return;
  errorMessage.value = '';
  loading.value = true;
  try {
    customerItems.value = await loadPackageFormContext();
    if (props.mode === 'create') {
      Object.assign(form, emptyPackageFormModel());
      return;
    }
    const id = props.packageId;
    if (!id) throw new Error(t('odakSiparis.packages.dialog.missingId'));
    if (props.seedRow) {
      Object.assign(form, packageRowToFormModel(props.seedRow));
    }
    const full = await loadPackageForEdit(id);
    if (!full) throw new Error(t('odakSiparis.packages.dialog.notFound'));
    Object.assign(form, packageRowToFormModel(full));
    const cid = form.customerId;
    if (cid && !customerItems.value.some((c) => c.value === cid)) {
      const label = props.seedRow ? String(props.seedRow.name ?? cid) : cid;
      customerItems.value = [{ value: cid, title: label }, ...customerItems.value];
    }
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

function closeDialog() {
  emit('update:modelValue', false);
}

async function savePackage() {
  errorMessage.value = '';
  if (!form.packageNo.trim()) {
    errorMessage.value = t('odakSiparis.packages.validation.packageNoRequired');
    return;
  }
  if (!form.name.trim()) {
    errorMessage.value = t('odakSiparis.packages.validation.nameRequired');
    return;
  }

  saving.value = true;
  try {
    if (props.mode === 'create') {
      const id = await createOdakPackage(form);
      emit('saved', id ?? undefined);
    } else if (props.packageId) {
      await updateOdakPackage(props.packageId, form);
      emit('saved', props.packageId);
    }
    closeDialog();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

watch(
  () => [props.modelValue, props.mode, props.packageId] as const,
  () => {
    if (props.modelValue) void loadDialog();
  }
);
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="780"
    scrollable
    @update:model-value="emit('update:modelValue', $event)"
  >
    <v-card>
      <v-card-title class="d-flex align-center py-4 px-5">
        <span class="text-h6">{{ dialogTitle }}</span>
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

        <template v-if="!loading">
          <v-row dense>
            <v-col cols="12" sm="4">
              <v-text-field
                v-model="form.packageNo"
                :label="t('odakSiparis.detail.fields.packageNo')"
                :readonly="isEdit"
                variant="outlined"
                density="comfortable"
                hide-details="auto"
              />
            </v-col>
            <v-col cols="12" sm="8">
              <v-text-field
                v-model="form.name"
                :label="t('odakSiparis.detail.fields.name')"
                variant="outlined"
                density="comfortable"
                hide-details="auto"
              />
            </v-col>
            <v-col cols="12" sm="8">
              <v-autocomplete
                v-model="form.customerId"
                :items="customerItems"
                item-title="title"
                item-value="value"
                :label="t('odakSiparis.detail.fields.customer')"
                variant="outlined"
                density="comfortable"
                clearable
                hide-details
              />
            </v-col>
            <v-col cols="12" sm="4">
              <v-select
                v-model="form.status"
                :items="statusItems"
                item-title="title"
                item-value="value"
                :label="t('odakSiparis.detail.fields.status')"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field
                v-model="form.beginDate"
                :label="t('odakSiparis.detail.fields.beginDate')"
                type="date"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field
                v-model="form.deliveryDate"
                :label="t('odakSiparis.detail.fields.deliveryDate')"
                type="date"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field
                v-model="form.poVersion"
                :label="t('odakSiparis.packages.fields.poVersion')"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col cols="12">
              <v-textarea
                v-model="form.deliveryAddress"
                :label="t('odakSiparis.detail.fields.deliveryAddress')"
                variant="outlined"
                density="comfortable"
                rows="2"
                auto-grow
                hide-details
              />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field
                v-model.number="form.partCount"
                :label="t('odakSiparis.detail.fields.partCount')"
                type="number"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field
                v-model.number="form.stockCount"
                :label="t('odakSiparis.detail.fields.stockCount')"
                type="number"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field
                v-model.number="form.shippedCount"
                :label="t('odakSiparis.detail.fields.shippedCount')"
                type="number"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col cols="12">
              <v-textarea
                v-model="form.notes"
                :label="t('odakSiparis.detail.fields.notes')"
                variant="outlined"
                density="comfortable"
                rows="2"
                hide-details
              />
            </v-col>
            <v-col cols="12">
              <v-text-field
                v-model="form.paymentDetail"
                :label="t('odakSiparis.detail.fields.paymentDetail')"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
          </v-row>
        </template>
      </v-card-text>

      <v-divider />
      <v-card-actions class="px-5 py-3">
        <v-spacer />
        <v-btn variant="text" @click="closeDialog">{{ t('odakSiparis.lines.dialog.cancel') }}</v-btn>
        <v-btn color="primary" variant="flat" :loading="saving" @click="savePackage">
          {{ isEdit ? t('odakSiparis.lines.dialog.save') : t('odakSiparis.lines.dialog.create') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
