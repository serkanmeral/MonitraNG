<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { fetchEventLogPackageCatalog } from '@/services/eventLogPackageCatalogService';
import type { EventLogPackageCatalogResponse } from '@/types/apps/eventLogPackageCatalog';

const { t, locale } = useAppI18n();

const loading = ref(true);
const error = ref<string | null>(null);
const catalog = ref<EventLogPackageCatalogResponse | null>(null);

function formatUtc(iso?: string): string {
  if (!iso) return '—';
  try {
    return new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      dateStyle: 'short',
      timeStyle: 'medium',
      timeZone: 'UTC',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

async function load() {
  loading.value = true;
  error.value = null;
  try {
    catalog.value = await fetchEventLogPackageCatalog();
  } catch (e: unknown) {
    catalog.value = null;
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

onMounted(load);

defineExpose({ refresh: load });
</script>

<template>
  <div class="siem-settings-catalog">
    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      {{ t('siemCenter.settings.catalog.readOnlyHint') }}
    </v-alert>

    <div class="d-flex flex-wrap align-center ga-2 mb-4">
      <v-btn
        size="small"
        variant="tonal"
        color="primary"
        prepend-icon="mdi-refresh"
        :loading="loading"
        @click="load"
      >
        {{ t('siemCenter.settings.catalog.refresh') }}
      </v-btn>
      <template v-if="catalog">
        <v-chip size="small" variant="tonal">
          {{ t('siemCenter.settings.catalog.version') }}: {{ catalog.version || '—' }}
        </v-chip>
        <v-chip size="small" variant="outlined">
          {{ t('siemCenter.settings.catalog.source') }}: {{ catalog.source || '—' }}
        </v-chip>
        <span class="text-caption text-medium-emphasis">
          {{ t('siemCenter.settings.catalog.generated') }}: {{ formatUtc(catalog.generatedUtc) }} UTC
        </span>
      </template>
    </div>

    <v-alert v-if="error" type="error" variant="tonal" class="mb-4">
      {{ t('siemCenter.settings.catalog.loadError') }}
      <div class="text-caption mt-1">{{ error }}</div>
    </v-alert>

    <v-skeleton-loader v-if="loading && !catalog" type="table" />

    <template v-else-if="catalog">
      <h3 class="text-subtitle-1 font-weight-bold mb-2">
        {{ t('siemCenter.settings.catalog.packagesTitle') }}
      </h3>
      <p class="text-body-2 text-medium-emphasis mb-3">
        {{ t('siemCenter.settings.catalog.packagesHint') }}
      </p>
      <v-table
        v-if="catalog.packages.length"
        density="comfortable"
        class="mb-2"
      >
        <thead>
          <tr>
            <th>{{ t('siemCenter.settings.catalog.colName') }}</th>
            <th>{{ t('siemCenter.settings.catalog.colChannel') }}</th>
            <th>{{ t('siemCenter.settings.catalog.colEventIds') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="pkg in catalog.packages" :key="pkg.name">
            <td class="text-body-2 font-weight-medium">{{ pkg.name }}</td>
            <td class="text-body-2">{{ pkg.channel }}</td>
            <td class="text-body-2">
              <div class="d-flex flex-wrap ga-1">
                <v-chip
                  v-for="id in pkg.eventIds"
                  :key="id"
                  size="x-small"
                  variant="tonal"
                >
                  {{ id }}
                </v-chip>
              </div>
            </td>
          </tr>
        </tbody>
      </v-table>
      <div v-else class="text-body-2 text-medium-emphasis mb-2">
        {{ t('siemCenter.settings.catalog.emptyPackages') }}
      </div>

      <h3 class="text-subtitle-1 font-weight-bold mb-2 mt-6">
        {{ t('siemCenter.settings.catalog.optionalTitle') }}
      </h3>
      <p class="text-body-2 text-medium-emphasis mb-3">
        {{ t('siemCenter.settings.catalog.optionalHint') }}
      </p>
      <v-table
        v-if="catalog.optionalPackages.length"
        density="comfortable"
        class="mb-2"
      >
        <thead>
          <tr>
            <th>{{ t('siemCenter.settings.catalog.colName') }}</th>
            <th>{{ t('siemCenter.settings.catalog.colChannel') }}</th>
            <th>{{ t('siemCenter.settings.catalog.colEventIds') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="pkg in catalog.optionalPackages" :key="pkg.name">
            <td class="text-body-2 font-weight-medium">{{ pkg.name }}</td>
            <td class="text-body-2">{{ pkg.channel }}</td>
            <td class="text-body-2">
              <div class="d-flex flex-wrap ga-1">
                <v-chip
                  v-for="id in pkg.eventIds"
                  :key="id"
                  size="x-small"
                  variant="tonal"
                >
                  {{ id }}
                </v-chip>
              </div>
            </td>
          </tr>
        </tbody>
      </v-table>
      <div v-else class="text-body-2 text-medium-emphasis mb-2">
        {{ t('siemCenter.settings.catalog.emptyPackages') }}
      </div>
    </template>
  </div>
</template>
