<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useDisplay } from 'vuetify';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  loadSiemLogSourceDetail,
  logSourceCoverageColor,
  logSourceEventsLink,
} from '@/composables/useSiemDiscoveryLogSources';
import type { SiemLogSource, SiemLogSourceDetailSummary } from '@/types/apps/siemLogSource';
import {
  resolveActionLabel,
  resolveOutcomeLabel,
} from '@/composables/useSecEventList';

const props = defineProps<{
  modelValue: boolean;
  source: SiemLogSource | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
}>();

const { t, locale } = useAppI18n();
const { mdAndUp } = useDisplay();

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const loading = ref(false);
const error = ref<string | null>(null);
const summary = ref<SiemLogSourceDetailSummary | null>(null);

const drawerWidth = computed(() => (mdAndUp.value ? 420 : '100%'));

async function loadDetail() {
  if (!props.source) {
    summary.value = null;
    return;
  }
  loading.value = true;
  error.value = null;
  try {
    summary.value = await loadSiemLogSourceDetail(props.source);
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
    summary.value = null;
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.modelValue, props.source?.id] as const,
  ([isOpen]) => {
    if (isOpen && props.source) void loadDetail();
    if (!isOpen) {
      error.value = null;
    }
  },
);

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

function coverageLabel(c: string): string {
  return t(`siemCenter.discovery.logSources.coverage.${c}`);
}

function netLine(src?: string | null, dst?: string | null): string {
  const a = (src ?? '').trim();
  const b = (dst ?? '').trim();
  if (a && b) return `${a} → ${b}`;
  return a || b || '—';
}
</script>

<template>
  <v-navigation-drawer
    v-model="open"
    location="right"
    temporary
    :width="drawerWidth"
    class="log-source-detail"
  >
    <div v-if="source" class="pa-4">
      <div class="d-flex align-start ga-2 mb-3">
        <v-avatar :color="logSourceCoverageColor(source.coverage)" variant="tonal" size="42">
          <v-icon icon="mdi-firewall" />
        </v-avatar>
        <div class="min-w-0 flex-grow-1">
          <div class="text-subtitle-1 font-weight-bold text-truncate">{{ source.displayName }}</div>
          <div class="text-caption text-medium-emphasis">
            {{ t(`siemCenter.discovery.logSources.kind.${source.kind}`) }}
            <span v-if="source.siteLabel"> · {{ source.siteLabel }}</span>
          </div>
        </div>
        <v-btn icon="mdi-close" variant="text" size="small" @click="open = false" />
      </div>

      <div class="d-flex flex-wrap ga-2 mb-4">
        <v-chip size="small" :color="logSourceCoverageColor(source.coverage)" variant="flat">
          {{ coverageLabel(source.coverage) }}
        </v-chip>
        <v-chip size="small" variant="tonal" class="font-mono">{{ source.sensorHost }}</v-chip>
      </div>

      <p class="text-caption text-medium-emphasis mb-4">
        {{ t('siemCenter.discovery.logSources.detail.hint') }}
      </p>

      <v-alert v-if="error" type="warning" variant="tonal" density="compact" class="mb-3">
        {{ error }}
      </v-alert>

      <div v-if="loading" class="d-flex justify-center py-8">
        <v-progress-circular indeterminate color="primary" size="28" />
      </div>

      <template v-else-if="summary">
        <div class="d-flex flex-wrap ga-2 mb-4">
          <v-card variant="tonal" class="log-source-detail__stat flex-grow-1">
            <v-card-text class="pa-3">
              <div class="text-caption text-medium-emphasis">
                {{ t('siemCenter.discovery.logSources.detail.count1h') }}
              </div>
              <div class="text-h6 font-weight-bold">{{ summary.eventCount1h }}</div>
            </v-card-text>
          </v-card>
          <v-card variant="tonal" class="log-source-detail__stat flex-grow-1">
            <v-card-text class="pa-3">
              <div class="text-caption text-medium-emphasis">
                {{ t('siemCenter.discovery.logSources.detail.count24h') }}
              </div>
              <div class="text-h6 font-weight-bold">{{ summary.eventCount24h }}</div>
            </v-card-text>
          </v-card>
        </div>

        <div class="text-caption font-weight-bold text-medium-emphasis text-uppercase mb-2">
          {{ t('siemCenter.discovery.logSources.detail.topActions') }}
        </div>
        <div v-if="summary.topActions.length" class="d-flex flex-wrap ga-1 mb-4">
          <v-chip
            v-for="row in summary.topActions"
            :key="row.action"
            size="small"
            variant="outlined"
          >
            {{ resolveActionLabel(row.action, t) }}
            <span class="ms-1 text-medium-emphasis">×{{ row.count }}</span>
          </v-chip>
        </div>
        <div v-else class="text-body-2 text-medium-emphasis mb-4">
          {{ t('siemCenter.discovery.logSources.detail.noActions') }}
        </div>

        <div class="text-caption font-weight-bold text-medium-emphasis text-uppercase mb-2">
          {{ t('siemCenter.discovery.logSources.detail.recent') }}
        </div>
        <v-list v-if="summary.recent.length" density="compact" class="bg-transparent pa-0 mb-4">
          <v-list-item
            v-for="row in summary.recent"
            :key="row.id"
            class="px-0"
          >
            <v-list-item-title class="text-body-2">
              {{ resolveActionLabel(row.action, t) }}
              <v-chip
                v-if="row.outcome"
                size="x-small"
                class="ms-1"
                variant="tonal"
              >
                {{ resolveOutcomeLabel(row.outcome, t) }}
              </v-chip>
            </v-list-item-title>
            <v-list-item-subtitle class="text-caption">
              {{ formatWhen(row.timestamp) }}
              · {{ netLine(row.srcIp, row.dstIp) }}
            </v-list-item-subtitle>
          </v-list-item>
        </v-list>
        <div v-else class="text-body-2 text-medium-emphasis mb-4">
          {{ t('siemCenter.discovery.logSources.detail.noRecent') }}
        </div>
      </template>

      <div class="d-flex flex-wrap ga-2">
        <v-btn
          color="primary"
          variant="flat"
          prepend-icon="mdi-shield-search"
          :to="logSourceEventsLink(source)"
          class="text-none"
        >
          {{ t('siemCenter.discovery.logSources.openEvents') }}
        </v-btn>
        <v-btn
          variant="text"
          prepend-icon="mdi-refresh"
          :loading="loading"
          class="text-none"
          @click="loadDetail"
        >
          {{ t('siemCenter.discovery.refreshCoverage') }}
        </v-btn>
      </div>
    </div>
  </v-navigation-drawer>
</template>

<style scoped>
.log-source-detail__stat {
  min-width: 7.5rem;
}
</style>
