<script setup lang="ts">
import { computed, ref } from 'vue';
import { storeToRefs } from 'pinia';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import { useOdakSiparisHubSettingsStore } from '@/stores/apps/odakSiparisHubSettings';

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const hubStore = useOdakSiparisHubSettingsStore();
const { bootstrapStatus } = storeToRefs(hubStore);

const errorMessage = ref('');
const successMessage = ref('');

const restrictedViewerGroups = computed({
  get: () => hubStore.poDocumentAccessConfig.restrictedViewerGroups,
  set: (groups: string[]) => hubStore.setPoDocumentRestrictedGroups(groups),
});

const loading = computed(
  () => bootstrapStatus.value === 'loading' || !hubStore.scopeReady('package_po_document_access')
);
const saving = computed(() => hubStore.scopeSaving('package_po_document_access'));
const canSave = computed(() => hubStore.canSaveScope('package_po_document_access'));
const canEdit = computed(() => hubStore.canEditScope('package_po_document_access'));

async function save() {
  if (!canSave.value) return;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    await hubStore.saveScope('package_po_document_access');
    successMessage.value = t('odakSiparis.packages.settings.saved');
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  }
}
</script>

<template>
  <div>
    <p class="text-body-2 text-medium-emphasis mb-4">
      {{ t('odakSiparis.packages.settings.poDocumentAccess.hint') }}
    </p>

    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>
    <v-alert v-if="successMessage" type="success" variant="tonal" density="compact" class="mb-3">
      {{ successMessage }}
    </v-alert>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <MngDirectoryPickerField
      v-model="restrictedViewerGroups"
      entity="group"
      group-value-key="name"
      multiple
      :label="t('odakSiparis.packages.settings.poDocumentAccess.groupsLabel')"
      :disabled="!canEdit"
      class="mb-4"
    />

    <div class="d-flex justify-end">
      <v-btn color="primary" variant="flat" :loading="saving" :disabled="!canSave" @click="save">
        {{ t('odakSiparis.packages.settings.save') }}
      </v-btn>
    </div>
  </div>
</template>
