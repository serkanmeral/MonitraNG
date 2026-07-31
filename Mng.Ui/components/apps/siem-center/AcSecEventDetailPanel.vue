<script setup lang="ts">
import { ref, computed } from 'vue';
import { useDisplay } from 'vuetify';
import { useAppI18n } from '@/composables/useAppI18n';
import type { SecEventListItem } from '@/types/apps/secEvent';
import {
  actionColor,
  alarmRulesLinkForAction,
  displayEventAction,
  formatRawForDisplay,
  formatRelativeTime,
  getScenarioIdForAction,
  outcomeColor,
  sourceTypeLabelKey,
} from '@/composables/useSecEventList';
import { copyTextToClipboard } from '@/utils/clipboard';

const props = defineProps<{
  event: SecEventListItem | null;
  loading?: boolean;
}>();

const emit = defineEmits<{
  close: [];
}>();

const { t, locale } = useAppI18n();
const { mdAndUp } = useDisplay();
const copyHint = ref<string | null>(null);

const scenarioId = computed(() => (props.event ? getScenarioIdForAction(props.event.eventAction) : null));

const scenarioTitle = computed(() =>
  scenarioId.value ? t(`siemCenter.scenarios.${scenarioId.value}.title`) : null,
);

const scenarioDesc = computed(() =>
  scenarioId.value ? t(`siemCenter.scenarios.${scenarioId.value}.desc`) : null,
);

const actionLabel = computed(() => {
  if (!props.event) return '';
  const key = `siemCenter.events.actions.${props.event.eventAction}`;
  const translated = t(key);
  return translated !== key ? translated : props.event.eventAction;
});

const displayRaw = computed(() => props.event?.raw ?? props.event?.rawPreview ?? '');

const formattedRaw = computed(() => (displayRaw.value ? formatRawForDisplay(displayRaw.value) : null));

function formatDate(value?: string | null): string {
  if (!value) return '—';
  try {
    return new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      dateStyle: 'short',
      timeStyle: 'medium',
    }).format(new Date(value));
  } catch {
    return value ?? '—';
  }
}

function relativeTime(value?: string | null): string {
  return formatRelativeTime(value, locale.value, t);
}

async function copyValue(label: string, value?: string | null) {
  if (!value) return;
  const ok = await copyTextToClipboard(value);
  copyHint.value = ok ? t('siemCenter.events.copied', { label }) : t('siemCenter.events.copyFailed');
  window.setTimeout(() => {
    copyHint.value = null;
  }, 2000);
}
</script>

<template>
  <v-card v-if="event" variant="outlined" class="rounded-lg ac-sec-event-detail h-100">
    <v-card-title class="d-flex align-start gap-2 pa-4 pb-2">
      <v-icon icon="mdi-shield-search" color="primary" size="22" class="mt-1" />
      <div class="min-w-0 flex-grow-1">
        <div class="text-subtitle-1 font-weight-bold">{{ t('siemCenter.events.detailTitle') }}</div>
        <div v-if="scenarioTitle" class="text-body-2 text-medium-emphasis mt-1">{{ scenarioTitle }}</div>
      </div>
      <v-btn v-if="!mdAndUp" icon="mdi-close" variant="text" size="small" @click="emit('close')" />
    </v-card-title>

    <v-card-text class="pa-4 pt-0">
      <v-alert v-if="copyHint" type="success" variant="tonal" density="compact" class="mb-3">{{ copyHint }}</v-alert>

      <div class="d-flex flex-wrap gap-2 mb-4">
        <v-chip v-if="scenarioId" size="small" color="primary" variant="flat">{{ scenarioId }}</v-chip>
        <v-chip size="small" :color="actionColor(event.eventAction)" variant="tonal">{{ actionLabel }}</v-chip>
        <v-chip v-if="event.eventOutcome" size="small" :color="outcomeColor(event.eventOutcome)" variant="tonal">
          {{ event.eventOutcome }}
        </v-chip>
        <v-chip v-if="event.baselineNewFlowPair" size="x-small" color="info" variant="flat">new_flow</v-chip>
      </div>

      <p v-if="scenarioDesc" class="text-body-2 text-medium-emphasis mb-4">{{ scenarioDesc }}</p>

      <!-- Overview -->
      <div class="ac-detail-section mb-4">
        <div class="ac-detail-section__title">{{ t('siemCenter.events.sectionOverview') }}</div>
        <v-list density="compact" class="bg-transparent pa-0">
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colTime') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ formatDate(event.timestamp) }}</v-list-item-subtitle>
            <template #append>
              <span class="text-caption text-medium-emphasis">{{ relativeTime(event.timestamp) }}</span>
            </template>
          </v-list-item>
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.detailActionKey') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2 font-weight-medium">{{ displayEventAction(event) }}</v-list-item-subtitle>
          </v-list-item>
          <v-list-item v-if="event.ingestedAt" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.ingestedAt') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ formatDate(event.ingestedAt) }}</v-list-item-subtitle>
          </v-list-item>
        </v-list>
      </div>

      <!-- Actor & network -->
      <div class="ac-detail-section mb-4">
        <div class="ac-detail-section__title">{{ t('siemCenter.events.sectionActor') }}</div>
        <v-list density="compact" class="bg-transparent pa-0">
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colUser') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ event.actorUser || '—' }}</v-list-item-subtitle>
            <template v-if="event.actorUser" #append>
              <v-btn icon="mdi-content-copy" size="x-small" variant="text" @click="copyValue(t('siemCenter.events.colUser'), event.actorUser)" />
            </template>
          </v-list-item>
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colSrcIp') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2 font-weight-medium">{{ event.networkSrcIp || '—' }}</v-list-item-subtitle>
            <template v-if="event.networkSrcIp" #append>
              <v-btn icon="mdi-content-copy" size="x-small" variant="text" @click="copyValue(t('siemCenter.events.colSrcIp'), event.networkSrcIp)" />
            </template>
          </v-list-item>
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colDstIp') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ event.networkDstIp || '—' }}</v-list-item-subtitle>
            <template v-if="event.networkDstIp" #append>
              <v-btn icon="mdi-content-copy" size="x-small" variant="text" @click="copyValue(t('siemCenter.events.colDstIp'), event.networkDstIp)" />
            </template>
          </v-list-item>
        </v-list>
      </div>

      <!-- Source -->
      <div class="ac-detail-section mb-4">
        <div class="ac-detail-section__title">{{ t('siemCenter.events.sectionSource') }}</div>
        <v-list density="compact" class="bg-transparent pa-0">
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colSource') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2">
              {{ event.sourceType ? t(sourceTypeLabelKey(event.sourceType)) : '—' }}
              <span v-if="event.sourceProduct" class="text-medium-emphasis"> · {{ event.sourceProduct }}</span>
            </v-list-item-subtitle>
          </v-list-item>
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colHost') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ event.sourceHost || '—' }}</v-list-item-subtitle>
          </v-list-item>
        </v-list>
      </div>

      <!-- Technical -->
      <div class="ac-detail-section mb-4">
        <div class="ac-detail-section__title">{{ t('siemCenter.events.sectionTechnical') }}</div>
        <v-list density="compact" class="bg-transparent pa-0">
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">ID</v-list-item-title>
            <v-list-item-subtitle class="text-body-2 text-truncate">{{ event.id }}</v-list-item-subtitle>
            <template #append>
              <v-btn icon="mdi-content-copy" size="x-small" variant="text" @click="copyValue('ID', event.id)" />
            </template>
          </v-list-item>
          <v-list-item v-if="event.eventCode" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colCode') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ event.eventCode }}</v-list-item-subtitle>
          </v-list-item>
          <v-list-item v-if="event.parserId" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colParser') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ event.parserId }}</v-list-item-subtitle>
          </v-list-item>
        </v-list>
      </div>

      <!-- Raw log -->
      <div class="ac-detail-section">
        <div class="d-flex align-center justify-space-between mb-2">
          <div class="ac-detail-section__title mb-0">
            {{ event.raw ? t('siemCenter.events.rawFull') : t('siemCenter.events.rawPreview') }}
          </div>
          <v-btn
            v-if="displayRaw"
            size="x-small"
            variant="text"
            prepend-icon="mdi-content-copy"
            @click="copyValue(t('siemCenter.events.rawFull'), displayRaw)"
          >
            {{ t('siemCenter.events.copy') }}
          </v-btn>
        </div>

        <div v-if="loading" class="d-flex justify-center py-6">
          <v-progress-circular indeterminate color="primary" size="28" />
        </div>
        <pre v-else-if="formattedRaw" class="ac-raw-block">{{ formattedRaw.text }}</pre>
        <div v-else class="text-body-2 text-medium-emphasis">{{ t('siemCenter.events.rawUnavailable') }}</div>
      </div>

      <div class="d-flex flex-wrap gap-2 mt-4">
        <v-btn
          v-if="scenarioId"
          size="small"
          variant="tonal"
          color="primary"
          prepend-icon="mdi-shield-link-variant"
          :to="alarmRulesLinkForAction(event.eventAction)"
        >
          {{ t('siemCenter.events.relatedRules') }}
        </v-btn>
        <v-btn size="small" variant="text" prepend-icon="mdi-view-dashboard" to="/apps/siem-center">
          {{ t('siemCenter.events.backToDashboard') }}
        </v-btn>
      </div>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.ac-sec-event-detail {
  position: sticky;
  top: 12px;
  max-height: calc(100vh - 96px);
  overflow-y: auto;
}

.ac-detail-section__title {
  font-size: 0.8125rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgba(var(--v-theme-on-surface), 0.55);
  margin-bottom: 8px;
}

.ac-raw-block {
  font-family: ui-monospace, 'Cascadia Code', 'Consolas', monospace;
  font-size: 0.75rem;
  line-height: 1.45;
  padding: 12px;
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.06);
  max-height: 280px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
  margin: 0;
}
</style>
