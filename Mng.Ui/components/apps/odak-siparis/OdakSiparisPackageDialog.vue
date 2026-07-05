<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useOdakPackageFieldAccess } from '@/composables/useOdakPackageFieldAccess';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import type { OdakPackageRow } from '@/utils/odakSiparisConfig';
import type { OdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import { loadOdakPackageHubRuntimeSettings } from '@/utils/odakSiparisHubSettingsService';
import {
  createOdakPackage,
  emptyPackageFormModel,
  loadPackageForEdit,
  loadPackageFormContext,
  loadCustomerContactOptions,
  packageRowToFormModel,
  updateOdakPackage,
  type OdakPackageDialogMode,
  type OdakPackageFormModel,
} from '@/utils/odakSiparisPackageService';
import {
  loadDesignPersonnelSelectOptions,
  loadManufacturePersonnelSelectOptions,
  ensurePersonnelOptionsIncludeSelected,
} from '@/utils/odakSiparisPackagePersonnel';
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
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const customerItems = ref<{ value: string; title: string }[]>([]);
const customerContactItems = ref<{ value: string; title: string }[]>([]);
const designPersonnelItems = ref<{ value: string; title: string }[]>([]);
const manufacturePersonnelItems = ref<{ value: string; title: string }[]>([]);
const skipContactReset = ref(false);
const form = reactive<OdakPackageFormModel>(emptyPackageFormModel());
const fieldPolicies = ref<OdakFieldPoliciesBlob>({ policiesByField: {} });
const existingRow = ref<OdakPackageRow | null>(null);
const { canViewField, canEditField } = useOdakPackageFieldAccess(fieldPolicies);

const policyRow = computed((): OdakPackageRow | null => {
  if (existingRow.value) return existingRow.value;
  if (props.mode === 'create') {
    return { ...form, status: form.status } as unknown as OdakPackageRow;
  }
  return null;
});

function fieldVisible(fieldKey: string): boolean {
  return canViewField(fieldKey, policyRow.value);
}

function fieldReadonly(fieldKey: string): boolean {
  return !canEditField(fieldKey, policyRow.value);
}

const showCustomerContactsGroup = computed(
  () => fieldVisible('customerContactId')
);

const showOdakPersonnelGroup = computed(
  () => fieldVisible('designContactId') || fieldVisible('manufactureContactId')
);

const designPersonnelPoolEmpty = computed(() => designPersonnelItems.value.length === 0);
const manufacturePersonnelPoolEmpty = computed(() => manufacturePersonnelItems.value.length === 0);

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

async function loadCustomerContacts(customerId: string | null, keepSelection = false) {
  customerContactItems.value = await loadCustomerContactOptions(customerId);
  if (!keepSelection) {
    form.customerContactId = null;
    return;
  }
  const selectedId = form.customerContactId;
  if (selectedId && !customerContactItems.value.some((c) => c.value === selectedId)) {
    customerContactItems.value = [{ value: selectedId, title: selectedId }, ...customerContactItems.value];
  }
}

async function loadOdakPersonnelOptions(keepSelection = false) {
  let designItems = await loadDesignPersonnelSelectOptions();
  let manufactureItems = await loadManufacturePersonnelSelectOptions();
  if (keepSelection) {
    designItems = await ensurePersonnelOptionsIncludeSelected(designItems, form.designContactId);
    manufactureItems = await ensurePersonnelOptionsIncludeSelected(
      manufactureItems,
      form.manufactureContactId
    );
  } else {
    form.designContactId = null;
    form.manufactureContactId = null;
  }
  designPersonnelItems.value = designItems;
  manufacturePersonnelItems.value = manufactureItems;
}

async function loadDialog() {
  if (!props.modelValue) return;
  errorMessage.value = '';
  loading.value = true;
  skipContactReset.value = true;
  try {
    const hub = await loadOdakPackageHubRuntimeSettings();
    fieldPolicies.value = hub.fieldPolicies;
    customerItems.value = await loadPackageFormContext();
    if (props.mode === 'create') {
      Object.assign(form, emptyPackageFormModel());
      existingRow.value = null;
      customerContactItems.value = [];
      await loadOdakPersonnelOptions(false);
      return;
    }
    const id = props.packageId;
    if (!id) throw new Error(t('odakSiparis.packages.dialog.missingId'));
    if (props.seedRow) {
      Object.assign(form, packageRowToFormModel(props.seedRow));
      existingRow.value = props.seedRow;
    }
    const full = await loadPackageForEdit(id);
    if (!full) throw new Error(t('odakSiparis.packages.dialog.notFound'));
    Object.assign(form, packageRowToFormModel(full));
    existingRow.value = full;
    await Promise.all([
      loadCustomerContacts(form.customerId, true),
      loadOdakPersonnelOptions(true),
    ]);
    const cid = form.customerId;
    if (cid && !customerItems.value.some((c) => c.value === cid)) {
      const label = props.seedRow ? String(props.seedRow.name ?? cid) : cid;
      customerItems.value = [{ value: cid, title: label }, ...customerItems.value];
    }
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    loading.value = false;
    skipContactReset.value = false;
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
      await updateOdakPackage(props.packageId, form, existingRow.value);
      emit('saved', props.packageId);
    }
    closeDialog();
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
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

watch(
  () => form.customerId,
  (customerId, prev) => {
    if (skipContactReset.value || !props.modelValue) return;
    if (customerId === prev) return;
    void loadCustomerContacts(customerId ?? null);
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
            <v-col v-if="fieldVisible('packageNo')" cols="12" sm="4">
              <v-text-field
                v-model="form.packageNo"
                :label="t('odakSiparis.detail.fields.packageNo')"
                :readonly="isEdit || fieldReadonly('packageNo')"
                variant="outlined"
                density="comfortable"
                hide-details="auto"
              />
            </v-col>
            <v-col v-if="fieldVisible('name')" cols="12" sm="8">
              <v-text-field
                v-model="form.name"
                :label="t('odakSiparis.detail.fields.name')"
                :readonly="fieldReadonly('name')"
                variant="outlined"
                density="comfortable"
                hide-details="auto"
              />
            </v-col>
            <v-col v-if="fieldVisible('customerId')" cols="12" md="6">
              <v-autocomplete
                v-model="form.customerId"
                :items="customerItems"
                item-title="title"
                item-value="value"
                :label="t('odakSiparis.detail.fields.customer')"
                :disabled="fieldReadonly('customerId')"
                variant="outlined"
                density="comfortable"
                clearable
                hide-details
              />
            </v-col>
            <v-col v-if="fieldVisible('status')" cols="12" md="6">
              <v-select
                v-model="form.status"
                :items="statusItems"
                item-title="title"
                item-value="value"
                :label="t('odakSiparis.detail.fields.status')"
                :disabled="fieldReadonly('status')"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <template v-if="showCustomerContactsGroup">
              <v-col cols="12">
                <div class="text-subtitle-2 font-weight-medium mb-1">
                  {{ t('odakSiparis.packages.dialog.customerContactsGroup') }}
                </div>
              </v-col>
              <v-col v-if="fieldVisible('customerContactId')" cols="12" md="6">
                <v-autocomplete
                  v-model="form.customerContactId"
                  :items="customerContactItems"
                  item-title="title"
                  item-value="value"
                  :label="t('odakSiparis.detail.fields.customerContact')"
                  :disabled="!form.customerId || fieldReadonly('customerContactId')"
                  variant="outlined"
                  density="comfortable"
                  clearable
                  hide-details="auto"
                />
              </v-col>
              <v-col v-if="!form.customerId && fieldVisible('customerContactId')" cols="12">
                <div class="text-caption text-medium-emphasis">
                  {{ t('odakSiparis.packages.dialog.selectCustomerFirst') }}
                </div>
              </v-col>
            </template>
            <template v-if="showOdakPersonnelGroup">
              <v-col cols="12">
                <div class="text-subtitle-2 font-weight-medium mb-1">
                  {{ t('odakSiparis.packages.dialog.odakPersonnelGroup') }}
                </div>
              </v-col>
              <v-col v-if="fieldVisible('designContactId')" cols="12" md="6">
                <v-select
                  v-model="form.designContactId"
                  :items="designPersonnelItems"
                  item-title="title"
                  item-value="value"
                  :label="t('odakSiparis.detail.fields.designResponsible')"
                  :disabled="designPersonnelPoolEmpty || fieldReadonly('designContactId')"
                  variant="outlined"
                  density="comfortable"
                  clearable
                  hide-details="auto"
                />
              </v-col>
              <v-col v-if="fieldVisible('manufactureContactId')" cols="12" md="6">
                <v-select
                  v-model="form.manufactureContactId"
                  :items="manufacturePersonnelItems"
                  item-title="title"
                  item-value="value"
                  :label="t('odakSiparis.detail.fields.manufactureResponsible')"
                  :disabled="manufacturePersonnelPoolEmpty || fieldReadonly('manufactureContactId')"
                  variant="outlined"
                  density="comfortable"
                  clearable
                  hide-details="auto"
                />
              </v-col>
              <v-col
                v-if="designPersonnelPoolEmpty || manufacturePersonnelPoolEmpty"
                cols="12"
              >
                <div class="text-caption text-medium-emphasis">
                  {{ t('odakSiparis.packages.dialog.personnelPoolEmpty') }}
                </div>
              </v-col>
            </template>
            <v-col v-if="fieldVisible('beginDate')" cols="12" sm="4">
              <v-text-field
                v-model="form.beginDate"
                :label="t('odakSiparis.detail.fields.beginDate')"
                type="date"
                :readonly="fieldReadonly('beginDate')"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col v-if="fieldVisible('deliveryDate')" cols="12" sm="4">
              <v-text-field
                v-model="form.deliveryDate"
                :label="t('odakSiparis.detail.fields.deliveryDate')"
                type="date"
                :readonly="fieldReadonly('deliveryDate')"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col v-if="fieldVisible('poVersion')" cols="12" sm="4">
              <v-text-field
                v-model="form.poVersion"
                :label="t('odakSiparis.packages.fields.poVersion')"
                :readonly="fieldReadonly('poVersion')"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col v-if="fieldVisible('deliveryAddress')" cols="12">
              <v-textarea
                v-model="form.deliveryAddress"
                :label="t('odakSiparis.detail.fields.deliveryAddress')"
                :readonly="fieldReadonly('deliveryAddress')"
                variant="outlined"
                density="comfortable"
                rows="2"
                auto-grow
                hide-details
              />
            </v-col>
            <v-col v-if="fieldVisible('partCount')" cols="12" sm="4">
              <v-text-field
                v-model.number="form.partCount"
                :label="t('odakSiparis.detail.fields.partCount')"
                type="number"
                :readonly="fieldReadonly('partCount')"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col v-if="fieldVisible('stockCount')" cols="12" sm="4">
              <v-text-field
                v-model.number="form.stockCount"
                :label="t('odakSiparis.detail.fields.stockCount')"
                type="number"
                :readonly="fieldReadonly('stockCount')"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col v-if="fieldVisible('shippedCount')" cols="12" sm="4">
              <v-text-field
                v-model.number="form.shippedCount"
                :label="t('odakSiparis.detail.fields.shippedCount')"
                type="number"
                :readonly="fieldReadonly('shippedCount')"
                variant="outlined"
                density="comfortable"
                hide-details
              />
            </v-col>
            <v-col v-if="fieldVisible('notes')" cols="12">
              <v-textarea
                v-model="form.notes"
                :label="t('odakSiparis.detail.fields.notes')"
                :readonly="fieldReadonly('notes')"
                variant="outlined"
                density="comfortable"
                rows="2"
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
