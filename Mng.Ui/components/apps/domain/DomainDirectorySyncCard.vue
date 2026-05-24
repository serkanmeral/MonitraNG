<script setup lang="ts">
import { ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { fetchFromMngKeeper } from '@/services/apiService';
import { RefreshIcon } from 'vue-tabler-icons';

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string) => {
  if (i18n?.t) return i18n.t(key);
  if (i18n?.global?.t) return i18n.global.t(key);
  return key;
};

const authStore = useAuthStore();

const syncing = ref(false);
const error = ref<string | null>(null);
const success = ref<string | null>(null);
const lastResult = ref<Record<string, unknown> | null>(null);

const runDirectorySync = async () => {
  syncing.value = true;
  error.value = null;
  success.value = null;

  try {
    // Keeper: DirectorySyncTrigger enum — Manual=0, Scheduled=1, Login=2 (string değil)
    const body: Record<string, string | number> = {
      triggeredBy: 0,
    };
    if (authStore.domainId) {
      body.domainId = authStore.domainId;
    }

    const response = await fetchFromMngKeeper('/directory/sync', 'POST', body);

    if (response?.code === 'sync_in_progress' || response?.Code === 'sync_in_progress') {
      error.value = t('domain.directorySync.inProgress');
      return;
    }

    if (response?.isSuccess === false || response?.IsSuccess === false) {
      error.value =
        response?.message ||
        response?.Message ||
        response?.errorMessage ||
        t('domain.directorySync.failed');
      return;
    }

    lastResult.value = response;
    success.value = t('domain.directorySync.success');
  } catch (e: unknown) {
    const err = e as {
      message?: string;
      data?: { message?: string; title?: string; errors?: Record<string, string[]> };
    };
    const validation =
      err?.data?.errors &&
      Object.entries(err.data.errors)
        .map(([k, v]) => `${k}: ${(v || []).join(', ')}`)
        .join(' ');
    error.value =
      validation ||
      err?.data?.message ||
      err?.data?.title ||
      err?.message ||
      t('domain.directorySync.failed');
  } finally {
    syncing.value = false;
  }
};
</script>

<template>
  <v-card elevation="10" class="mb-4">
    <v-card-item>
      <div class="d-flex align-center flex-wrap ga-3 mb-3">
        <RefreshIcon size="24" />
        <h5 class="text-h5 mb-0">{{ t('domain.directorySync.title') }}</h5>
      </div>
      <p class="text-body-2 text-medium-emphasis mb-4">
        {{ t('domain.directorySync.description') }}
      </p>

      <v-alert
        v-if="error"
        type="error"
        variant="tonal"
        density="compact"
        class="mb-3"
        closable
        @click:close="error = null"
      >
        {{ error }}
      </v-alert>

      <v-alert
        v-if="success"
        type="success"
        variant="tonal"
        density="compact"
        class="mb-3"
        closable
        @click:close="success = null"
      >
        {{ success }}
        <div v-if="lastResult" class="text-caption mt-2">
          {{ t('domain.directorySync.resultUsers') }}:
          +{{ lastResult.usersCreated ?? lastResult.UsersCreated ?? 0 }} /
          ~{{ lastResult.usersUpdated ?? lastResult.UsersUpdated ?? 0 }}
          · {{ t('domain.directorySync.resultGroups') }}:
          +{{ lastResult.groupsCreated ?? lastResult.GroupsCreated ?? 0 }} /
          ~{{ lastResult.groupsUpdated ?? lastResult.GroupsUpdated ?? 0 }}
        </div>
      </v-alert>

      <v-btn
        color="primary"
        variant="flat"
        :loading="syncing"
        :disabled="syncing"
        @click="runDirectorySync"
      >
        <RefreshIcon class="mr-2" size="18" />
        {{ t('domain.directorySync.run') }}
      </v-btn>
    </v-card-item>
  </v-card>
</template>
