<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useGroupStore } from '@/stores/apps/group';
import {
  defaultOdakPackagePoDocumentAccessConfig,
  type OdakPackagePoDocumentAccessConfig,
} from '@/utils/odakSiparisPoDocumentAccess';
import {
  invalidateOdakPackageHubSettingsCache,
  loadOdakPackagePoDocumentAccessConfig,
  saveOdakPackagePoDocumentAccessConfig,
} from '@/utils/odakSiparisHubSettingsService';

const { t } = useAppI18n();
const groupStore = useGroupStore();

const loading = ref(true);
const saving = ref(false);
const errorMessage = ref('');
const successMessage = ref('');
const rowId = ref<string | null>(null);
const config = ref<OdakPackagePoDocumentAccessConfig>(defaultOdakPackagePoDocumentAccessConfig());

const groupItems = computed(() =>
  (groupStore.groups ?? [])
    .map((g) => ({
      value: g.name ?? g.groupName ?? '',
      title: g.name ?? g.groupName ?? '',
    }))
    .filter((g) => g.value)
);

async function load() {
  loading.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    if (!groupStore.groups?.length) {
      await groupStore.fetchGroups({ page: 1, pageSize: 500 });
    }
    const resp = await loadOdakPackagePoDocumentAccessConfig();
    config.value = resp.config;
    rowId.value = resp.rowId;
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

async function save() {
  saving.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    rowId.value = await saveOdakPackagePoDocumentAccessConfig(config.value, rowId.value);
    invalidateOdakPackageHubSettingsCache();
    successMessage.value = t('odakSiparis.packages.settings.saved');
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
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
      {{ t('odakSiparis.packages.settings.poDocumentAccess.hint') }}
    </p>

    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>
    <v-alert v-if="successMessage" type="success" variant="tonal" density="compact" class="mb-3">
      {{ successMessage }}
    </v-alert>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <v-autocomplete
      v-model="config.restrictedViewerGroups"
      :items="groupItems"
      :label="t('odakSiparis.packages.settings.poDocumentAccess.groupsLabel')"
      :hint="t('odakSiparis.packages.settings.poDocumentAccess.groupsHint')"
      persistent-hint
      multiple
      chips
      closable-chips
      variant="outlined"
      density="comfortable"
      :disabled="loading || saving"
      class="mb-4"
    />

    <div class="d-flex justify-end">
      <v-btn color="primary" variant="flat" :loading="saving" :disabled="loading" @click="save">
        {{ t('odakSiparis.packages.settings.save') }}
      </v-btn>
    </div>
  </div>
</template>
