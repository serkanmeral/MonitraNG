<script setup lang="ts">
import { onMounted, ref } from 'vue';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  defaultOdakPackagePersonnelConfig,
  type OdakPackagePersonnelConfig,
} from '@/utils/odakSiparisPackagePersonnel';
import {
  invalidateOdakPackageHubSettingsCache,
  loadOdakPackagePersonnelConfig,
  saveOdakPackagePersonnelConfig,
} from '@/utils/odakSiparisHubSettingsService';

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(true);
const saving = ref(false);
const errorMessage = ref('');
const successMessage = ref('');
const rowId = ref<string | null>(null);
const config = ref<OdakPackagePersonnelConfig>(defaultOdakPackagePersonnelConfig());

async function load() {
  loading.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    const resp = await loadOdakPackagePersonnelConfig();
    config.value = resp.config;
    rowId.value = resp.rowId;
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    loading.value = false;
  }
}

async function save() {
  saving.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    rowId.value = await saveOdakPackagePersonnelConfig(config.value, rowId.value);
    invalidateOdakPackageHubSettingsCache();
    successMessage.value = t('odakSiparis.packages.settings.saved');
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    saving.value = false;
  }
}

onMounted(() => {
  void load();
});
</script>

<template>
  <div>
    <p class="text-body-2 text-medium-emphasis mb-4">
      {{ t('odakSiparis.packages.settings.personnel.hint') }}
    </p>

    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>
    <v-alert v-if="successMessage" type="success" variant="tonal" density="compact" class="mb-3">
      {{ successMessage }}
    </v-alert>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <MngDirectoryPickerField
      v-model="config.designPersonnelIds"
      entity="user"
      :label="t('odakSiparis.packages.settings.personnel.designPersonnel')"
      multiple
      :disabled="loading || saving"
      class="mb-2"
    />
    <p class="text-caption text-medium-emphasis mb-4">
      {{ t('odakSiparis.packages.settings.personnel.designPersonnelHint') }}
    </p>

    <MngDirectoryPickerField
      v-model="config.manufacturePersonnelIds"
      entity="user"
      :label="t('odakSiparis.packages.settings.personnel.manufacturePersonnel')"
      multiple
      :disabled="loading || saving"
      class="mb-2"
    />
    <p class="text-caption text-medium-emphasis mb-4">
      {{ t('odakSiparis.packages.settings.personnel.manufacturePersonnelHint') }}
    </p>

    <div class="d-flex justify-end">
      <v-btn color="primary" variant="flat" :loading="saving" :disabled="loading" @click="save">
        {{ t('odakSiparis.packages.settings.save') }}
      </v-btn>
    </div>
  </div>
</template>
