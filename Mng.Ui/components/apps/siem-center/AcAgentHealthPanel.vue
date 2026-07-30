<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { secEventQuery } from '@/services/secEventService';
import type { SecEventListItem } from '@/types/apps/secEvent';

const { t } = useAppI18n();

const loading = ref(true);
const error = ref<string | null>(null);
const hostUpItems = ref<SecEventListItem[]>([]);
const inventoryItems = ref<SecEventListItem[]>([]);

const hostUpLink = '/apps/siem-center/events?sourceType=metric&eventAction=host.up';
const inventoryLink = '/apps/siem-center/events?sourceType=metric&eventAction=watch.inventory';

const uniqueHosts = computed(() => {
  const map = new Map<string, SecEventListItem>();
  for (const item of hostUpItems.value) {
    const key = (item.sourceHost || item.id || '').trim();
    if (!key) continue;
    if (!map.has(key)) map.set(key, item);
  }
  return [...map.values()].slice(0, 8);
});

const latestInventory = computed(() => inventoryItems.value[0] ?? null);

function from24h(): string {
  return new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString();
}

function formatTime(iso?: string | null): string {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

async function load() {
  loading.value = true;
  error.value = null;
  try {
    const from = from24h();
    const [ups, inv] = await Promise.all([
      secEventQuery({ from, sourceType: 'metric', eventAction: 'host.up', limit: 50, excludeUnknown: false }),
      secEventQuery({ from, sourceType: 'metric', eventAction: 'watch.inventory', limit: 20, excludeUnknown: false }),
    ]);
    hostUpItems.value = ups.items ?? [];
    inventoryItems.value = inv.items ?? [];
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
    hostUpItems.value = [];
    inventoryItems.value = [];
  } finally {
    loading.value = false;
  }
}

onMounted(load);

defineExpose({ refresh: load });
</script>

<template>
  <v-card variant="outlined" class="agent-health-panel mb-4">
    <v-card-title class="d-flex align-center flex-wrap ga-2 text-subtitle-1">
      <v-icon icon="mdi-desktop-classic" size="20" class="me-1" />
      {{ t('siemCenter.dashboard.agentHealthTitle') }}
      <v-spacer />
      <v-btn size="small" variant="text" :loading="loading" prepend-icon="mdi-refresh" @click="load">
        {{ t('siemCenter.dashboard.agentHealthRefresh') }}
      </v-btn>
    </v-card-title>
    <v-card-subtitle class="pb-2">
      {{ t('siemCenter.dashboard.agentHealthHint') }}
    </v-card-subtitle>

    <v-alert v-if="error" type="warning" variant="tonal" density="compact" class="ma-3 mt-0">
      {{ error }}
    </v-alert>

    <v-row dense class="pa-3 pt-0">
      <v-col cols="12" md="6">
        <div class="text-caption text-medium-emphasis mb-2">
          {{ t('siemCenter.dashboard.agentHealthHosts') }}
          <v-btn :to="hostUpLink" size="x-small" variant="text" class="ms-1">
            {{ t('siemCenter.dashboard.agentHealthOpenEvents') }}
          </v-btn>
        </div>
        <v-skeleton-loader v-if="loading" type="list-item@3" />
        <div v-else-if="uniqueHosts.length === 0" class="text-body-2 text-medium-emphasis">
          {{ t('siemCenter.dashboard.agentHealthEmptyHosts') }}
        </div>
        <v-list v-else density="compact" class="bg-transparent py-0">
          <v-list-item
            v-for="item in uniqueHosts"
            :key="item.id"
            :title="item.sourceHost || '—'"
            :subtitle="formatTime(item.timestamp)"
            :to="`${hostUpLink}`"
          >
            <template #prepend>
              <v-icon icon="mdi-heart-pulse" color="success" size="18" />
            </template>
          </v-list-item>
        </v-list>
      </v-col>

      <v-col cols="12" md="6">
        <div class="text-caption text-medium-emphasis mb-2">
          {{ t('siemCenter.dashboard.agentHealthInventory') }}
          <v-btn :to="inventoryLink" size="x-small" variant="text" class="ms-1">
            {{ t('siemCenter.dashboard.agentHealthOpenEvents') }}
          </v-btn>
        </div>
        <v-skeleton-loader v-if="loading" type="list-item@2" />
        <div v-else-if="!latestInventory" class="text-body-2 text-medium-emphasis">
          {{ t('siemCenter.dashboard.agentHealthEmptyInventory') }}
        </div>
        <v-list v-else density="compact" class="bg-transparent py-0">
          <v-list-item
            :title="latestInventory.sourceHost || '—'"
            :subtitle="formatTime(latestInventory.timestamp)"
            :to="inventoryLink"
          >
            <template #prepend>
              <v-icon icon="mdi-eye-check" color="info" size="18" />
            </template>
            <template #append>
              <v-chip size="x-small" variant="tonal" color="info">
                watch.inventory
              </v-chip>
            </template>
          </v-list-item>
          <div class="text-caption text-medium-emphasis px-4 pb-2">
            {{ latestInventory.rawPreview || t('siemCenter.dashboard.agentHealthInventoryHint') }}
          </div>
        </v-list>
      </v-col>
    </v-row>
  </v-card>
</template>

<style scoped>
.agent-health-panel {
  border-radius: 12px;
}
</style>
