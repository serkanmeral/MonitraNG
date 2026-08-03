<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useDisplay } from 'vuetify';
import { useAppI18n } from '@/composables/useAppI18n';
import type { SecEventListItem } from '@/types/apps/secEvent';
import {
  actionColor,
  alarmRulesLinkForAction,
  displayEventAction,
  formatRelativeTime,
  getScenarioIdForAction,
  outcomeColor,
  sourceTypeLabelKey,
} from '@/composables/useSecEventList';
import {
  eventLogDetailFieldsJson,
  eventLogDetailMessageText,
} from '@/utils/windowsSecurityLogonParse';
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
const bodyTab = ref<'message' | 'fields'>('message');

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

/** Mesaj = düz metin; Alanlar = fields JSON. */
const displayMessage = computed(() => {
  if (!props.event) return '';
  return eventLogDetailMessageText(
    props.event.fields,
    props.event.raw,
    props.event.rawPreview,
    null,
  );
});

const displayFieldsJson = computed(() => {
  if (!props.event) return '';
  return eventLogDetailFieldsJson(props.event.fields, props.event.raw, props.event.rawPreview);
});

const activeTabCopyValue = computed(() =>
  bodyTab.value === 'message' ? displayMessage.value : displayFieldsJson.value,
);

const activeTabCopyLabel = computed(() =>
  bodyTab.value === 'message'
    ? t('siemCenter.events.tabMessage')
    : t('siemCenter.events.tabFields'),
);

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

async function copyActiveTab() {
  await copyValue(activeTabCopyLabel.value, activeTabCopyValue.value);
}

watch(
  () => props.event?.id,
  () => {
    bodyTab.value = 'message';
    copyHint.value = null;
  },
);
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

      <!-- Message / Fields / Raw -->
      <div class="ac-detail-section">
        <div class="d-flex align-center flex-wrap ga-2 mb-2">
          <v-tabs v-model="bodyTab" density="compact" color="primary" class="flex-grow-1">
            <v-tab value="message">{{ t('siemCenter.events.tabMessage') }}</v-tab>
            <v-tab value="fields">{{ t('siemCenter.events.tabFields') }}</v-tab>
          </v-tabs>
          <v-btn
            size="x-small"
            variant="text"
            prepend-icon="mdi-content-copy"
            :disabled="!activeTabCopyValue?.trim()"
            @click="copyActiveTab"
          >
            {{ t('siemCenter.events.copy') }}
          </v-btn>
        </div>

        <div v-if="loading" class="d-flex justify-center py-6">
          <v-progress-circular indeterminate color="primary" size="28" />
        </div>
        <v-tabs-window v-else v-model="bodyTab">
          <v-tabs-window-item value="message">
            <pre v-if="displayMessage.trim()" class="ac-raw-block">{{ displayMessage }}</pre>
            <div v-else class="text-body-2 text-medium-emphasis">{{ t('siemCenter.events.noMessage') }}</div>
          </v-tabs-window-item>
          <v-tabs-window-item value="fields">
            <pre v-if="displayFieldsJson.trim()" class="ac-raw-block">{{ displayFieldsJson }}</pre>
            <div v-else class="text-body-2 text-medium-emphasis">{{ t('siemCenter.events.noFields') }}</div>
          </v-tabs-window-item>
        </v-tabs-window>
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
