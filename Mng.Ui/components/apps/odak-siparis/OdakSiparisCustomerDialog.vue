<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ODAK_CUSTOMER_SEKTOR_OPTIONS, type OdakCustomerRow } from '@/utils/odakSiparisConfig';
import {
  createOdakCustomer,
  customerRowToFormModel,
  emptyCustomerFormModel,
  fetchOdakCustomerById,
  updateOdakCustomer,
  type OdakCustomerDialogMode,
  type OdakCustomerFormModel,
} from '@/utils/odakSiparisCustomerService';

const props = defineProps<{
  modelValue: boolean;
  mode: OdakCustomerDialogMode;
  customerId?: string;
  seedRow?: OdakCustomerRow | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  saved: [customerId?: string];
}>();

const { t } = useAppI18n();

const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const form = reactive<OdakCustomerFormModel>(emptyCustomerFormModel());

const isEdit = computed(() => props.mode === 'edit');

const dialogTitle = computed(() =>
  isEdit.value
    ? t('odakSiparis.customers.dialog.editTitle', { name: form.unvan || form.kod || '—' })
    : t('odakSiparis.customers.dialog.createTitle')
);

const sektorItems = computed(() =>
  ODAK_CUSTOMER_SEKTOR_OPTIONS.map((o) => ({ value: o.value, title: o.title }))
);

async function loadDialog() {
  if (!props.modelValue) return;
  errorMessage.value = '';
  loading.value = true;
  try {
    if (props.mode === 'create') {
      Object.assign(form, emptyCustomerFormModel());
      return;
    }
    const id = props.customerId;
    if (!id) throw new Error(t('odakSiparis.customers.dialog.missingId'));
    if (props.seedRow) {
      Object.assign(form, customerRowToFormModel(props.seedRow));
    }
    const full = await fetchOdakCustomerById(id);
    if (!full) throw new Error(t('odakSiparis.customers.dialog.notFound'));
    Object.assign(form, customerRowToFormModel(full));
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

function closeDialog() {
  emit('update:modelValue', false);
}

async function saveCustomer() {
  errorMessage.value = '';
  if (!form.kod.trim()) {
    errorMessage.value = t('odakSiparis.customers.validation.kodRequired');
    return;
  }
  if (!form.unvan.trim()) {
    errorMessage.value = t('odakSiparis.customers.validation.unvanRequired');
    return;
  }
  if (!form.isMusteri && !form.isTedarikci) {
    errorMessage.value = t('odakSiparis.customers.validation.roleRequired');
    return;
  }

  saving.value = true;
  try {
    if (props.mode === 'create') {
      const id = await createOdakCustomer(form);
      emit('saved', id ?? undefined);
    } else if (props.customerId) {
      await updateOdakCustomer(props.customerId, form);
      emit('saved', props.customerId);
    }
    closeDialog();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

watch(
  () => [props.modelValue, props.mode, props.customerId] as const,
  () => {
    if (props.modelValue) void loadDialog();
  }
);
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="640"
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
                v-model="form.kod"
                :label="t('odakSiparis.customers.fields.kod')"
                :readonly="isEdit"
                variant="outlined"
                density="comfortable"
                placeholder="MUS-001"
                hide-details="auto"
              />
            </v-col>
            <v-col cols="12" sm="8">
              <v-text-field
                v-model="form.unvan"
                :label="t('odakSiparis.customers.fields.unvan')"
                variant="outlined"
                density="comfortable"
                hide-details="auto"
              />
            </v-col>
            <v-col cols="12" sm="6">
              <v-switch
                v-model="form.isMusteri"
                :label="t('odakSiparis.customers.fields.isMusteri')"
                color="primary"
                hide-details
              />
            </v-col>
            <v-col cols="12" sm="6">
              <v-switch
                v-model="form.isTedarikci"
                :label="t('odakSiparis.customers.fields.isTedarikci')"
                color="warning"
                hide-details
              />
            </v-col>
            <v-col cols="12" sm="6">
              <v-select
                v-model="form.sektor"
                :items="sektorItems"
                item-title="title"
                item-value="value"
                :label="t('odakSiparis.customers.fields.sektor')"
                variant="outlined"
                density="comfortable"
                clearable
                hide-details
              />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field
                v-model="form.ulke"
                :label="t('odakSiparis.customers.fields.ulke')"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col cols="12" sm="4">
              <v-switch
                v-model="form.aktif"
                :label="t('odakSiparis.customers.fields.aktif')"
                color="primary"
                hide-details
              />
            </v-col>
            <v-col cols="12">
              <v-textarea
                v-model="form.notlar"
                :label="t('odakSiparis.customers.fields.notlar')"
                variant="outlined"
                density="comfortable"
                rows="3"
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
        <v-btn color="primary" variant="flat" :loading="saving" @click="saveCustomer">
          {{ isEdit ? t('odakSiparis.lines.dialog.save') : t('odakSiparis.lines.dialog.create') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
