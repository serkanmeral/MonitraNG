<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakCustomerContactRow } from '@/utils/odakSiparisConfig';
import {
  contactRowToFormModel,
  createOdakCustomerContact,
  emptyCustomerContactFormModel,
  fetchOdakCustomerContactById,
  updateOdakCustomerContact,
  validateCustomerContactForm,
  type OdakCustomerContactDialogMode,
  type OdakCustomerContactFormModel,
} from '@/utils/odakSiparisCustomerContactService';

const props = defineProps<{
  modelValue: boolean;
  mode: OdakCustomerContactDialogMode;
  customerId: string;
  contactId?: string;
  seedRow?: OdakCustomerContactRow | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  saved: [];
}>();

const { t } = useAppI18n();

const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const form = reactive<OdakCustomerContactFormModel>(emptyCustomerContactFormModel());

const isEdit = computed(() => props.mode === 'edit');

const dialogTitle = computed(() =>
  isEdit.value
    ? t('odakSiparis.customers.contacts.dialog.editTitle', { name: form.ad || '—' })
    : t('odakSiparis.customers.contacts.dialog.createTitle')
);

async function loadDialog() {
  if (!props.modelValue) return;
  errorMessage.value = '';
  loading.value = true;
  try {
    if (props.mode === 'create') {
      Object.assign(form, emptyCustomerContactFormModel());
      return;
    }
    const id = props.contactId;
    if (!id) throw new Error(t('odakSiparis.customers.contacts.dialog.missingId'));
    if (props.seedRow) {
      Object.assign(form, contactRowToFormModel(props.seedRow));
    }
    const full = await fetchOdakCustomerContactById(id);
    if (!full) throw new Error(t('odakSiparis.customers.contacts.dialog.notFound'));
    Object.assign(form, contactRowToFormModel(full));
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

function closeDialog() {
  emit('update:modelValue', false);
}

async function saveContact() {
  errorMessage.value = '';
  const validationKey = validateCustomerContactForm(form);
  if (validationKey) {
    errorMessage.value = t(`odakSiparis.customers.contacts.validation.${validationKey}`);
    return;
  }
  if (!props.customerId) {
    errorMessage.value = t('odakSiparis.customers.contacts.dialog.missingCustomer');
    return;
  }

  saving.value = true;
  try {
    if (props.mode === 'create') {
      await createOdakCustomerContact(props.customerId, form);
    } else if (props.contactId) {
      await updateOdakCustomerContact(props.contactId, props.customerId, form);
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
  () => [props.modelValue, props.mode, props.contactId] as const,
  () => {
    if (props.modelValue) void loadDialog();
  }
);
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="560"
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
            <v-col cols="12">
              <v-text-field
                v-model="form.ad"
                :label="t('odakSiparis.customers.contacts.fields.ad')"
                variant="outlined"
                density="comfortable"
                hide-details="auto"
              />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field
                v-model="form.email"
                :label="t('odakSiparis.customers.contacts.fields.email')"
                type="email"
                variant="outlined"
                density="comfortable"
                hide-details="auto"
              />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field
                v-model="form.telefon"
                :label="t('odakSiparis.customers.contacts.fields.telefon')"
                variant="outlined"
                density="comfortable"
                hide-details="auto"
              />
            </v-col>
            <v-col cols="12">
              <v-text-field
                v-model="form.gorevUnvani"
                :label="t('odakSiparis.customers.contacts.fields.gorevUnvani')"
                variant="outlined"
                density="comfortable"
                hide-details="auto"
              />
            </v-col>
            <v-col cols="12" sm="6">
              <v-switch
                v-model="form.birincilKisi"
                :label="t('odakSiparis.customers.contacts.fields.birincilKisi')"
                color="primary"
                hide-details
              />
            </v-col>
            <v-col cols="12" sm="6">
              <v-switch
                v-model="form.aktif"
                :label="t('odakSiparis.customers.contacts.fields.aktif')"
                color="primary"
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
        <v-btn color="primary" variant="flat" :loading="saving" @click="saveContact">
          {{ isEdit ? t('odakSiparis.lines.dialog.save') : t('odakSiparis.lines.dialog.create') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
