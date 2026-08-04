<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  logSourceEventsLink,
  useSiemDiscoveryLogSources,
} from '@/composables/useSiemDiscoveryLogSources';
import AcSiemDiscoveryLogSourceDetailDrawer from '@/components/apps/siem-center/AcSiemDiscoveryLogSourceDetailDrawer.vue';
import type { SiemLogSource, SiemLogSourceCoverage } from '@/types/apps/siemLogSource';

const { t, locale } = useAppI18n();
const { loading, error, sources, kpis, lastRefreshedAt, refresh, coverageColor } =
  useSiemDiscoveryLogSources();

const coverageFilter = ref<SiemLogSourceCoverage | 'all'>('all');
const detailOpen = ref(false);
const selectedSource = ref<SiemLogSource | null>(null);

const filtered = computed(() => {
  if (coverageFilter.value === 'all') return sources.value;
  return sources.value.filter((s) => s.coverage === coverageFilter.value);
});

const lastRefreshedLabel = computed(() => {
  if (!lastRefreshedAt.value) return '';
  try {
    return new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    }).format(new Date(lastRefreshedAt.value));
  } catch {
    return '';
  }
});

function formatWhen(iso?: string | null): string {
  if (!iso) return '—';
  try {
    return new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      dateStyle: 'short',
      timeStyle: 'medium',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

function coverageLabel(c: SiemLogSourceCoverage): string {
  return t(`siemCenter.discovery.logSources.coverage.${c}`);
}

function selectKpi(coverage: SiemLogSourceCoverage | 'all' | undefined) {
  coverageFilter.value = coverage ?? 'all';
}

function openDetail(src: SiemLogSource) {
  selectedSource.value = src;
  detailOpen.value = true;
}

watch(sources, () => {
  if (
    coverageFilter.value !== 'all'
    && !sources.value.some((s) => s.coverage === coverageFilter.value)
  ) {
    coverageFilter.value = 'all';
  }
});

defineExpose({ refresh });
</script>

<template>
  <div class="log-sources">
    <v-alert
      type="info"
      variant="tonal"
      density="compact"
      class="mb-3"
    >
      {{ t('siemCenter.discovery.logSources.banner') }}
    </v-alert>

    <v-alert v-if="error" type="warning" variant="tonal" density="compact" class="mb-3">
      {{ error }}
    </v-alert>

    <div class="d-flex flex-wrap align-center justify-space-between ga-2 mb-3">
      <div class="d-flex flex-wrap ga-2">
        <v-chip
          v-for="k in kpis"
          :key="k.id"
          :color="k.color"
          :variant="coverageFilter === (k.coverage ?? 'all') ? 'flat' : 'tonal'"
          size="small"
          class="log-sources__kpi"
          @click="selectKpi(k.coverage)"
        >
          <strong class="me-1">{{ k.value }}</strong>
          {{ t(k.labelKey) }}
        </v-chip>
      </div>
      <div class="d-flex align-center ga-2">
        <span v-if="lastRefreshedLabel" class="text-caption text-medium-emphasis">
          {{ t('siemCenter.discovery.logSources.refreshedAt', { time: lastRefreshedLabel }) }}
        </span>
        <v-btn
          size="small"
          variant="tonal"
          prepend-icon="mdi-refresh"
          :loading="loading"
          @click="refresh"
        >
          {{ t('siemCenter.discovery.refreshCoverage') }}
        </v-btn>
      </div>
    </div>

    <div v-if="loading && !filtered.length" class="d-flex justify-center py-10">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <v-row v-else dense>
      <v-col
        v-for="src in filtered"
        :key="src.id"
        cols="12"
        sm="6"
        lg="4"
      >
        <v-card
          variant="outlined"
          class="log-sources__card h-100 rounded-lg"
          hover
          @click="openDetail(src)"
        >
          <v-card-text class="pa-4">
            <div class="d-flex align-start justify-space-between ga-2 mb-2">
              <div class="d-flex align-center ga-2 min-w-0">
                <v-avatar :color="coverageColor(src.coverage)" variant="tonal" size="40">
                  <v-icon icon="mdi-firewall" />
                </v-avatar>
                <div class="min-w-0">
                  <div class="text-subtitle-2 font-weight-bold text-truncate">{{ src.displayName }}</div>
                  <div class="text-caption text-medium-emphasis">
                    {{ t(`siemCenter.discovery.logSources.kind.${src.kind}`) }}
                    <span v-if="src.siteLabel"> · {{ src.siteLabel }}</span>
                  </div>
                </div>
              </div>
              <v-chip size="x-small" :color="coverageColor(src.coverage)" variant="flat">
                {{ coverageLabel(src.coverage) }}
              </v-chip>
            </div>

            <v-list density="compact" class="bg-transparent pa-0 mb-3">
              <v-list-item class="px-0 min-h-0">
                <v-list-item-title class="text-caption text-medium-emphasis">
                  {{ t('siemCenter.discovery.logSources.sensor') }}
                </v-list-item-title>
                <v-list-item-subtitle class="text-body-2 font-mono">
                  {{ src.sensorHost }}
                </v-list-item-subtitle>
              </v-list-item>
              <v-list-item class="px-0 min-h-0">
                <v-list-item-title class="text-caption text-medium-emphasis">
                  {{ t('siemCenter.discovery.logSources.lastEvent') }}
                </v-list-item-title>
                <v-list-item-subtitle class="text-body-2">
                  {{ formatWhen(src.lastEventAt) }}
                  <span v-if="src.lastAction" class="text-medium-emphasis"> · {{ src.lastAction }}</span>
                </v-list-item-subtitle>
              </v-list-item>
              <v-list-item class="px-0 min-h-0">
                <v-list-item-title class="text-caption text-medium-emphasis">
                  {{ t('siemCenter.discovery.logSources.events24h') }}
                </v-list-item-title>
                <v-list-item-subtitle class="text-body-2">
                  {{ src.eventCount24h }}
                  <span v-if="src.fromSeed && src.eventCount24h === 0" class="text-medium-emphasis">
                    · {{ t('siemCenter.discovery.logSources.fromSeed') }}
                  </span>
                </v-list-item-subtitle>
              </v-list-item>
            </v-list>

            <div class="d-flex flex-wrap ga-2" @click.stop>
              <v-btn
                size="small"
                variant="tonal"
                color="primary"
                prepend-icon="mdi-information-outline"
                class="text-none"
                @click="openDetail(src)"
              >
                {{ t('siemCenter.discovery.logSources.openDetail') }}
              </v-btn>
              <v-btn
                size="small"
                variant="text"
                prepend-icon="mdi-shield-search"
                :to="logSourceEventsLink(src)"
                class="text-none"
              >
                {{ t('siemCenter.discovery.logSources.openEvents') }}
              </v-btn>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-card
      v-if="!loading && filtered.length === 0"
      variant="outlined"
      class="pa-8 text-center rounded-lg"
    >
      <v-icon icon="mdi-firewall-off" size="40" class="mb-2 opacity-60" />
      <div class="text-body-1 font-weight-medium">{{ t('siemCenter.discovery.logSources.empty') }}</div>
    </v-card>

    <AcSiemDiscoveryLogSourceDetailDrawer
      v-model="detailOpen"
      :source="selectedSource"
    />
  </div>
</template>

<style scoped>
.log-sources__kpi {
  cursor: pointer;
}

.log-sources__card {
  border-left: 3px solid rgba(var(--v-theme-warning), 0.85);
  cursor: pointer;
}
</style>
