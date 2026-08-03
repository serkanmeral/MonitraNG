<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  fetchEventLogHostAssignment,
  fetchEventLogPackageManageList,
  saveEventLogHostAssignment,
} from '@/services/eventLogPackageCatalogService';
import type { EventLogPackageManageItem } from '@/types/apps/eventLogPackageCatalog';

const props = defineProps<{
  hostname: string;
  active: boolean;
}>();

const { t, locale } = useAppI18n();

const loading = ref(false);
const saving = ref(false);
const error = ref<string | null>(null);
const flash = ref<string | null>(null);
const optionals = ref<EventLogPackageManageItem[]>([]);
const enabledOptional = ref<string[]>([]);
const updatedAtUtc = ref<string | null>(null);
const hostKey = ref('');
const loadedFor = ref('');

const dateLocale = computed(() => (locale.value === 'tr' ? 'tr-TR' : 'en-GB'));

const headers = computed(() => [
  { title: t('siemCenter.discovery.hostDetail.packagesColName'), key: 'name', sortable: true },
  { title: t('siemCenter.discovery.hostDetail.packagesColChannel'), key: 'channel', sortable: true },
  { title: t('siemCenter.discovery.hostDetail.packagesColEventIds'), key: 'eventIds', sortable: false },
  {
    title: t('siemCenter.discovery.hostDetail.packagesColEnabled'),
    key: 'enabled',
    sortable: false,
    align: 'end' as const,
    width: '120px',
  },
]);

function formatUtc(iso?: string | null): string {
  if (!iso) return '—';
  try {
    return new Intl.DateTimeFormat(dateLocale.value, {
      dateStyle: 'short',
      timeStyle: 'medium',
      timeZone: 'UTC',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

function formatPackageIds(item: EventLogPackageManageItem): string {
  if (item.selectionMode === 'all') {
    const exclude = item.excludedEventIds.length
      ? t('siemCenter.settings.catalog.idsSummaryExclude', {
          ids: item.excludedEventIds.join(', '),
        })
      : '';
    return t('siemCenter.settings.catalog.idsSummaryAll', { exclude });
  }
  return item.eventIds.join(', ');
}

function isOptionalEnabled(name: string): boolean {
  return enabledOptional.value.some((n) => n.toLowerCase() === name.toLowerCase());
}

function toggleOptional(name: string, on: boolean) {
  const key = name.toLowerCase();
  const next = enabledOptional.value.filter((n) => n.toLowerCase() !== key);
  if (on) next.push(name.toLowerCase());
  enabledOptional.value = next;
}

async function load() {
  const host = props.hostname?.trim();
  if (!host) return;
  loading.value = true;
  error.value = null;
  try {
    const [list, assignment] = await Promise.all([
      fetchEventLogPackageManageList(),
      fetchEventLogHostAssignment(host),
    ]);
    optionals.value = list.items.filter((i) => !i.isDefault);
    enabledOptional.value = [...assignment.enabledOptionalPackages];
    updatedAtUtc.value = assignment.updatedAtUtc;
    hostKey.value = assignment.hostKey;
    loadedFor.value = host;
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

async function save() {
  const host = props.hostname?.trim();
  if (!host) return;
  saving.value = true;
  error.value = null;
  flash.value = null;
  try {
    const res = await saveEventLogHostAssignment(host, {
      enabledOptionalPackages: [...enabledOptional.value],
      // Fleet defaults are never disabled from host modal.
      disabledServerPackages: [],
    });
    enabledOptional.value = [...res.enabledOptionalPackages];
    updatedAtUtc.value = res.updatedAtUtc;
    hostKey.value = res.hostKey;
    flash.value = t('siemCenter.discovery.hostDetail.packagesSaved');
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

watch(
  () => [props.active, props.hostname] as const,
  ([active, host]) => {
    if (active && host && loadedFor.value !== host) {
      void load();
    } else if (active && host && loadedFor.value === host) {
      // refresh when revisiting tab
    }
  },
  { immediate: true },
);

defineExpose({ refresh: load });
</script>

<template>
  <div class="siem-host-packages">
    <div class="d-flex flex-wrap align-center ga-2 mb-2">
      <p class="text-caption text-medium-emphasis mb-0 flex-grow-1">
        {{ t('siemCenter.discovery.hostDetail.packagesOptionalOnlyHint') }}
      </p>
      <v-btn
        size="small"
        variant="tonal"
        prepend-icon="mdi-refresh"
        :loading="loading"
        @click="load"
      >
        {{ t('siemCenter.discovery.hostDetail.packagesRefresh') }}
      </v-btn>
      <v-btn
        size="small"
        color="primary"
        prepend-icon="mdi-content-save"
        :loading="saving"
        :disabled="loading || !hostname"
        @click="save"
      >
        {{ t('siemCenter.discovery.hostDetail.packagesSave') }}
      </v-btn>
    </div>

    <div class="d-flex flex-wrap ga-3 mb-2 text-caption text-medium-emphasis">
      <span v-if="hostKey" class="font-mono">
        {{ t('siemCenter.discovery.hostDetail.packagesHostKey') }}: {{ hostKey }}
      </span>
      <span>
        {{ t('siemCenter.discovery.hostDetail.packagesUpdated') }}:
        {{ formatUtc(updatedAtUtc) }} UTC
      </span>
    </div>

    <v-alert v-if="flash" type="success" variant="tonal" density="compact" class="mb-2" closable>
      {{ flash }}
    </v-alert>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-2">
      {{ t('siemCenter.discovery.hostDetail.packagesError') }}
      <div class="text-caption mt-1">{{ error }}</div>
    </v-alert>

    <v-skeleton-loader v-if="loading && !optionals.length" type="table" />

    <v-data-table
      v-else
      :headers="headers"
      :items="optionals"
      :loading="loading"
      item-value="name"
      density="compact"
      class="rounded-lg packages-assign-table"
      hide-default-footer
      :items-per-page="-1"
      :no-data-text="t('siemCenter.discovery.hostDetail.packagesEmptyOptional')"
    >
      <template #item.name="{ item }">
        <span class="font-mono text-body-2">{{ item.name }}</span>
      </template>
      <template #item.channel="{ item }">
        <span class="text-body-2 text-truncate d-inline-block packages-channel">{{ item.channel }}</span>
      </template>
      <template #item.eventIds="{ item }">
        <span class="font-mono text-caption">{{ formatPackageIds(item) }}</span>
      </template>
      <template #item.enabled="{ item }">
        <v-switch
          :model-value="isOptionalEnabled(item.name)"
          color="primary"
          density="compact"
          hide-details
          class="d-inline-flex justify-end"
          @update:model-value="(v: boolean | null) => toggleOptional(item.name, !!v)"
        />
      </template>
    </v-data-table>
  </div>
</template>

<style scoped>
.packages-channel {
  max-width: 14rem;
}

.packages-assign-table :deep(td) {
  vertical-align: middle;
}
</style>
