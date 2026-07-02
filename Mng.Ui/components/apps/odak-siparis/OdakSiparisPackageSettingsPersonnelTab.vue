<script setup lang="ts">
import { computed, ref } from 'vue';
import { storeToRefs } from 'pinia';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useOdakSiparisHubSettingsStore } from '@/stores/apps/odakSiparisHubSettings';

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const hubStore = useOdakSiparisHubSettingsStore();
const { bootstrapStatus } = storeToRefs(hubStore);

const errorMessage = ref('');
const successMessage = ref('');

const designPersonnelIds = computed({
  get: () => hubStore.personnelConfig.designPersonnelIds,
  set: (ids: string[]) => hubStore.setDesignPersonnelIds(ids),
});

const manufacturePersonnelIds = computed({
  get: () => hubStore.personnelConfig.manufacturePersonnelIds,
  set: (ids: string[]) => hubStore.setManufacturePersonnelIds(ids),
});

const loading = computed(
  () => bootstrapStatus.value === 'loading' || !hubStore.scopeReady('package_odak_personnel')
);
const saving = computed(() => hubStore.scopeSaving('package_odak_personnel'));
const canSave = computed(() => hubStore.canSaveScope('package_odak_personnel'));
const canEdit = computed(() => hubStore.canEditScope('package_odak_personnel'));

async function save() {
  if (!canSave.value) return;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    await hubStore.saveScope('package_odak_personnel');
    successMessage.value = t('odakSiparis.packages.settings.saved');
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  }
}
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
      v-model="designPersonnelIds"
      entity="user"
      :label="t('odakSiparis.packages.settings.personnel.designPersonnel')"
      multiple
      :disabled="!canEdit"
      class="mb-2"
    />
    <p class="text-caption text-medium-emphasis mb-4">
      {{ t('odakSiparis.packages.settings.personnel.designPersonnelHint') }}
    </p>

    <MngDirectoryPickerField
      v-model="manufacturePersonnelIds"
      entity="user"
      :label="t('odakSiparis.packages.settings.personnel.manufacturePersonnel')"
      multiple
      :disabled="!canEdit"
      class="mb-2"
    />
    <p class="text-caption text-medium-emphasis mb-4">
      {{ t('odakSiparis.packages.settings.personnel.manufacturePersonnelHint') }}
    </p>

    <div class="d-flex justify-end">
      <v-btn color="primary" variant="flat" :loading="saving" :disabled="!canSave" @click="save">
        {{ t('odakSiparis.packages.settings.save') }}
      </v-btn>
    </div>
  </div>
</template>
