<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { useDisplay } from 'vuetify';
import { useAppI18n } from '@/composables/useAppI18n';
import type { SecEventListItem } from '@/types/apps/secEvent';
import type { SecEventTargetFieldDefinition } from '@/types/apps/secEventParseRules';
import {
  actionColor,
  alarmRulesLinkForAction,
  displayEventAction,
  formatRelativeTime,
  getScenarioIdForAction,
  hasKnownActionLabel,
  outcomeColor,
  productAccentColor,
  resolveActionLabel,
  resolveOutcomeLabel,
  resolveProductLabel,
  sourceTypeLabelKey,
} from '@/composables/useSecEventList';
import {
  eventLogDetailFieldsJson,
  eventLogDetailMessageText,
} from '@/utils/windowsSecurityLogonParse';
import { copyTextToClipboard } from '@/utils/clipboard';
import { fetchSecEventTargetFields } from '@/services/secEventParseRuleCatalogService';
import AppJsonViewer from '@/components/shared/AppJsonViewer.vue';
import {
  firewallFlowEndpoint,
  isFirewallSecEvent,
  secEventBagField,
} from '@/utils/secEventFirewallDisplay';

const props = defineProps<{
  event: SecEventListItem | null;
  loading?: boolean;
}>();

const emit = defineEmits<{
  close: [];
  filterBy: [
    payload: {
      scope?: { type?: string | null; product?: string | null; host?: string | null };
      field?: string;
      value?: string;
    },
  ];
}>();

/** Already shown in the summary block — skip in extracted list. */
const SUMMARY_FIELD_KEYS = new Set([
  'actor.user',
  'network.srcIp',
  'network.dstIp',
  'network.dstPort',
  'network.protocol',
  'event.action',
  'event.outcome',
  'event.code',
  'source.host',
  'source.product',
  'source.type',
  'message',
  'custom.policy_id',
  'custom.service',
  'custom.log_type',
  'custom.log_subtype',
  'custom.src_port',
  'custom.cfg_path',
  'eventDataText',
  'Message',
  'EventID',
  'EventId',
  'Level',
  'Channel',
  'Provider',
  'Computer',
  'TimeCreated',
]);

const { t, locale } = useAppI18n();
const { mdAndUp } = useDisplay();
const copyHint = ref<string | null>(null);
const bodyTab = ref<'message' | 'fields'>('message');
const catalogByKey = ref<Map<string, SecEventTargetFieldDefinition>>(new Map());

const scenarioId = computed(() => (props.event ? getScenarioIdForAction(props.event.eventAction) : null));

const scenarioTitle = computed(() =>
  scenarioId.value ? t(`siemCenter.scenarios.${scenarioId.value}.title`) : null,
);

const scenarioDesc = computed(() =>
  scenarioId.value ? t(`siemCenter.scenarios.${scenarioId.value}.desc`) : null,
);

const actionLabel = computed(() =>
  props.event ? resolveActionLabel(props.event.eventAction, t) : '',
);

const outcomeLabel = computed(() =>
  props.event ? resolveOutcomeLabel(props.event.eventOutcome, t) : '',
);

const productLabel = computed(() =>
  props.event ? resolveProductLabel(props.event.sourceProduct, t) : '',
);

const isFirewall = computed(() => isFirewallSecEvent(props.event));

const firewallPolicyId = computed(() =>
  secEventBagField(props.event?.fields, 'custom.policy_id'),
);
const firewallService = computed(() =>
  secEventBagField(props.event?.fields, 'custom.service'),
);
const firewallLogType = computed(() =>
  secEventBagField(props.event?.fields, 'custom.log_type'),
);
const firewallLogSubtype = computed(() =>
  secEventBagField(props.event?.fields, 'custom.log_subtype'),
);
const firewallCfgPath = computed(() =>
  secEventBagField(props.event?.fields, 'custom.cfg_path'),
);
const firewallFlowLine = computed(() =>
  props.event ? firewallFlowEndpoint(props.event) : null,
);
const firewallProto = computed(() => props.event?.networkProtocol?.trim() || null);

const headerSubtitle = computed(() => {
  if (!props.event) return '';
  const host = props.event.sourceHost?.trim();
  const product = productLabel.value;
  const when = relativeTime(props.event.timestamp);
  const parts = [host, product].filter(Boolean);
  if (parts.length && when) return `${parts.join(' · ')} · ${when}`;
  if (parts.length) return parts.join(' · ');
  return when || '';
});

const accentClass = computed(() => {
  const p = (props.event?.sourceProduct ?? '').toLowerCase();
  if (p.includes('rdp') || p.includes('windows')) return 'ac-sec-event-detail--accent-primary';
  if (p.includes('forti')) return 'ac-sec-event-detail--accent-warning';
  if (p) return 'ac-sec-event-detail--accent-info';
  return '';
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

interface ExtractedFieldRow {
  key: string;
  label: string;
  value: string;
  fullValue: string;
  filterField: string;
  isCustom: boolean;
}

const extractedFields = computed((): ExtractedFieldRow[] => {
  const bag = props.event?.fields;
  if (!bag || typeof bag !== 'object') return [];

  const rows: ExtractedFieldRow[] = [];
  for (const [key, raw] of Object.entries(bag)) {
    if (SUMMARY_FIELD_KEYS.has(key)) continue;
    if (raw == null) continue;
    if (typeof raw === 'object') continue;
    const fullValue = String(raw).trim();
    if (!fullValue) continue;
    // Skip very long blobs (message-like) from the summary list
    if (fullValue.length > 500 && !key.startsWith('custom.')) continue;

    const isCustom = key.startsWith('custom.');
    const catalog = catalogByKey.value.get(key);
    const label = catalog?.label?.trim() || key;

    rows.push({
      key,
      label,
      value: fullValue.length > 160 ? `${fullValue.slice(0, 160)}…` : fullValue,
      fullValue,
      filterField: key,
      isCustom,
    });
  }

  rows.sort((a, b) => {
    if (a.isCustom !== b.isCustom) return a.isCustom ? -1 : 1;
    return a.label.localeCompare(b.label);
  });
  return rows.slice(0, 30);
});

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

function flashHint(message: string) {
  copyHint.value = message;
  window.setTimeout(() => {
    copyHint.value = null;
  }, 2000);
}

async function copyValue(label: string, value?: string | null) {
  if (!value) return;
  const ok = await copyTextToClipboard(value);
  flashHint(ok ? t('siemCenter.events.copied', { label }) : t('siemCenter.events.copyFailed'));
}

async function copyActiveTab() {
  await copyValue(activeTabCopyLabel.value, activeTabCopyValue.value);
}

function filterByField(field: string, value: string) {
  emit('filterBy', { field, value });
  flashHint(t('siemCenter.events.filterAppliedHint'));
}

function filterByHost() {
  if (!props.event?.sourceHost) return;
  emit('filterBy', { scope: { host: props.event.sourceHost } });
  flashHint(t('siemCenter.events.filterAppliedHint'));
}

function filterByProduct() {
  if (!props.event?.sourceProduct) return;
  emit('filterBy', { scope: { product: props.event.sourceProduct } });
  flashHint(t('siemCenter.events.filterAppliedHint'));
}

function filterByUser() {
  if (!props.event?.actorUser) return;
  filterByField('actor.user', props.event.actorUser);
}

function filterBySrcIp() {
  if (!props.event?.networkSrcIp) return;
  filterByField('network.srcIp', props.event.networkSrcIp);
}

function filterByDstIp() {
  if (!props.event?.networkDstIp) return;
  filterByField('network.dstIp', props.event.networkDstIp);
}

function filterByPolicy() {
  if (!firewallPolicyId.value) return;
  filterByField('custom.policy_id', firewallPolicyId.value);
}

function filterByService() {
  if (!firewallService.value) return;
  filterByField('custom.service', firewallService.value);
}

function filterByCode() {
  if (!props.event?.eventCode) return;
  filterByField('event.code', props.event.eventCode);
}

function filterByAction() {
  if (!props.event?.eventAction) return;
  filterByField('event.action', props.event.eventAction);
}

function filterByOutcome() {
  if (!props.event?.eventOutcome) return;
  filterByField('event.outcome', props.event.eventOutcome);
}

async function loadCatalogLabels() {
  try {
    const res = await fetchSecEventTargetFields();
    const map = new Map<string, SecEventTargetFieldDefinition>();
    for (const f of res.fields ?? []) {
      if (f.key) map.set(f.key, f);
    }
    catalogByKey.value = map;
  } catch {
    catalogByKey.value = new Map();
  }
}

onMounted(() => {
  void loadCatalogLabels();
});

watch(
  () => props.event?.id,
  () => {
    bodyTab.value = 'message';
    copyHint.value = null;
  },
);
</script>

<template>
  <v-card
    v-if="event"
    variant="outlined"
    class="rounded-lg ac-sec-event-detail h-100"
    :class="accentClass"
  >
    <v-card-title class="d-flex align-start gap-2 pa-4 pb-2">
      <v-icon icon="mdi-shield-search" color="primary" size="22" class="mt-1" />
      <div class="min-w-0 flex-grow-1">
        <div class="text-subtitle-1 font-weight-bold text-truncate" :title="actionLabel">
          {{ actionLabel || t('siemCenter.events.detailTitle') }}
        </div>
        <div v-if="headerSubtitle" class="text-body-2 text-medium-emphasis mt-1 text-truncate">
          {{ headerSubtitle }}
        </div>
        <div v-else-if="scenarioTitle" class="text-body-2 text-medium-emphasis mt-1">{{ scenarioTitle }}</div>
      </div>
      <v-btn v-if="!mdAndUp" icon="mdi-close" variant="text" size="small" @click="emit('close')" />
    </v-card-title>

    <v-card-text class="pa-4 pt-0">
      <v-alert v-if="copyHint" type="success" variant="tonal" density="compact" class="mb-3">{{ copyHint }}</v-alert>

      <div class="d-flex flex-wrap gap-2 mb-3">
        <v-chip v-if="scenarioId" size="small" color="primary" variant="flat">{{ scenarioId }}</v-chip>
        <v-chip
          size="small"
          :color="actionColor(event.eventAction)"
          variant="tonal"
          class="ac-detail-chip--clickable"
          :title="t('siemCenter.events.filterByAction')"
          @click="filterByAction"
        >
          {{ actionLabel }}
        </v-chip>
        <v-chip
          v-if="event.eventOutcome"
          size="small"
          :color="outcomeColor(event.eventOutcome)"
          variant="flat"
          class="ac-detail-chip--clickable"
          :title="t('siemCenter.events.filterByOutcome')"
          @click="filterByOutcome"
        >
          {{ outcomeLabel }}
        </v-chip>
        <v-chip
          v-if="event.eventCode"
          size="small"
          variant="outlined"
          class="ac-detail-chip--clickable"
          :title="t('siemCenter.events.filterByCode')"
          @click="filterByCode"
        >
          {{ event.eventCode }}
        </v-chip>
        <v-chip
          v-if="event.sourceProduct"
          size="small"
          :color="productAccentColor(event.sourceProduct)"
          variant="tonal"
          class="ac-detail-chip--clickable"
          :title="t('siemCenter.events.filterByField')"
          @click="filterByProduct"
        >
          {{ productLabel }}
        </v-chip>
        <v-chip v-if="event.baselineNewFlowPair" size="x-small" color="info" variant="flat">new_flow</v-chip>
        <v-tooltip v-if="!hasKnownActionLabel(event.eventAction, t)" location="top">
          <template #activator="{ props: tip }">
            <v-chip v-bind="tip" size="x-small" variant="text" color="warning">
              {{ t('siemCenter.events.rawActionHint') }}
            </v-chip>
          </template>
          <span>{{ event.eventAction }}</span>
        </v-tooltip>
      </div>

      <p v-if="scenarioDesc" class="text-caption text-medium-emphasis mb-4">{{ scenarioDesc }}</p>

      <!-- Primary summary: Host · Product · Actor · Network -->
      <div class="ac-detail-section mb-4">
        <div class="ac-detail-section__title">{{ t('siemCenter.events.sectionSummary') }}</div>
        <v-list density="compact" class="bg-transparent pa-0">
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colHost') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2 font-weight-medium">{{ event.sourceHost || '—' }}</v-list-item-subtitle>
            <template v-if="event.sourceHost" #append>
              <div class="d-flex">
                <v-btn
                  icon="mdi-filter-outline"
                  size="x-small"
                  variant="text"
                  :title="t('siemCenter.events.filterByField')"
                  @click="filterByHost"
                />
                <v-btn
                  icon="mdi-content-copy"
                  size="x-small"
                  variant="text"
                  @click="copyValue(t('siemCenter.events.colHost'), event.sourceHost)"
                />
              </div>
            </template>
          </v-list-item>

          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colProduct') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ productLabel || '—' }}</v-list-item-subtitle>
            <template v-if="event.sourceProduct" #append>
              <v-btn
                icon="mdi-filter-outline"
                size="x-small"
                variant="text"
                :title="t('siemCenter.events.filterByField')"
                @click="filterByProduct"
              />
            </template>
          </v-list-item>

          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colTime') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ formatDate(event.timestamp) }}</v-list-item-subtitle>
            <template #append>
              <span class="text-caption text-medium-emphasis">{{ relativeTime(event.timestamp) }}</span>
            </template>
          </v-list-item>

          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colUser') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ event.actorUser || '—' }}</v-list-item-subtitle>
            <template v-if="event.actorUser" #append>
              <div class="d-flex">
                <v-btn
                  icon="mdi-filter-outline"
                  size="x-small"
                  variant="text"
                  :title="t('siemCenter.events.filterByField')"
                  @click="filterByUser"
                />
                <v-btn
                  icon="mdi-content-copy"
                  size="x-small"
                  variant="text"
                  @click="copyValue(t('siemCenter.events.colUser'), event.actorUser)"
                />
              </div>
            </template>
          </v-list-item>

          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colSrcIp') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2 font-weight-medium">{{ event.networkSrcIp || '—' }}</v-list-item-subtitle>
            <template v-if="event.networkSrcIp" #append>
              <div class="d-flex">
                <v-btn
                  icon="mdi-filter-outline"
                  size="x-small"
                  variant="text"
                  :title="t('siemCenter.events.filterByField')"
                  @click="filterBySrcIp"
                />
                <v-btn
                  icon="mdi-content-copy"
                  size="x-small"
                  variant="text"
                  @click="copyValue(t('siemCenter.events.colSrcIp'), event.networkSrcIp)"
                />
              </div>
            </template>
          </v-list-item>

          <v-list-item v-if="event.networkDstIp" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colDstIp') }}</v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ event.networkDstIp }}</v-list-item-subtitle>
            <template #append>
              <div class="d-flex">
                <v-btn
                  icon="mdi-filter-outline"
                  size="x-small"
                  variant="text"
                  :title="t('siemCenter.events.filterByField')"
                  @click="filterByDstIp"
                />
                <v-btn
                  icon="mdi-content-copy"
                  size="x-small"
                  variant="text"
                  @click="copyValue(t('siemCenter.events.colDstIp'), event.networkDstIp)"
                />
              </div>
            </template>
          </v-list-item>
        </v-list>
      </div>

      <!-- Firewall-specific flow summary -->
      <div v-if="isFirewall" class="ac-detail-section mb-4">
        <div class="ac-detail-section__title">{{ t('siemCenter.events.sectionFirewallFlow') }}</div>
        <v-list density="compact" class="bg-transparent pa-0">
          <v-list-item v-if="firewallFlowLine" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('siemCenter.events.colFirewallEndpoints') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2 font-weight-medium font-mono">
              {{ firewallFlowLine }}
            </v-list-item-subtitle>
            <template #append>
              <v-btn
                icon="mdi-content-copy"
                size="x-small"
                variant="text"
                @click="copyValue(t('siemCenter.events.colFirewallEndpoints'), firewallFlowLine)"
              />
            </template>
          </v-list-item>
          <v-list-item v-if="firewallPolicyId" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('siemCenter.events.colFirewallPolicy') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ firewallPolicyId }}</v-list-item-subtitle>
            <template #append>
              <div class="d-flex">
                <v-btn
                  icon="mdi-filter-outline"
                  size="x-small"
                  variant="text"
                  :title="t('siemCenter.events.filterByField')"
                  @click="filterByPolicy"
                />
                <v-btn
                  icon="mdi-content-copy"
                  size="x-small"
                  variant="text"
                  @click="copyValue(t('siemCenter.events.colFirewallPolicy'), firewallPolicyId)"
                />
              </div>
            </template>
          </v-list-item>
          <v-list-item v-if="firewallService" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('siemCenter.events.colFirewallService') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ firewallService }}</v-list-item-subtitle>
            <template #append>
              <div class="d-flex">
                <v-btn
                  icon="mdi-filter-outline"
                  size="x-small"
                  variant="text"
                  :title="t('siemCenter.events.filterByField')"
                  @click="filterByService"
                />
                <v-btn
                  icon="mdi-content-copy"
                  size="x-small"
                  variant="text"
                  @click="copyValue(t('siemCenter.events.colFirewallService'), firewallService)"
                />
              </div>
            </template>
          </v-list-item>
          <v-list-item v-if="firewallProto || event.networkDstPort" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('siemCenter.events.colFirewallProtoPort') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">
              {{ [firewallProto, event.networkDstPort != null ? `:${event.networkDstPort}` : null].filter(Boolean).join(' ') || '—' }}
            </v-list-item-subtitle>
          </v-list-item>
          <v-list-item v-if="firewallLogType || firewallLogSubtype" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('siemCenter.events.colFirewallLogKind') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">
              {{ [firewallLogType, firewallLogSubtype].filter(Boolean).join(' / ') }}
            </v-list-item-subtitle>
          </v-list-item>
          <v-list-item v-if="firewallCfgPath" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('siemCenter.events.colFirewallCfgPath') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2 font-mono">{{ firewallCfgPath }}</v-list-item-subtitle>
            <template #append>
              <v-btn
                icon="mdi-content-copy"
                size="x-small"
                variant="text"
                @click="copyValue(t('siemCenter.events.colFirewallCfgPath'), firewallCfgPath)"
              />
            </template>
          </v-list-item>
        </v-list>
      </div>

      <!-- Extra parse / custom fields (deduped) -->
      <div v-if="extractedFields.length" class="ac-detail-section mb-4">
        <div class="ac-detail-section__title">{{ t('siemCenter.events.sectionExtracted') }}</div>
        <v-list density="compact" class="bg-transparent pa-0">
          <v-list-item v-for="row in extractedFields" :key="row.key" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ row.label }}
              <span v-if="row.isCustom" class="text-disabled"> · custom</span>
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2 text-truncate" :title="row.fullValue">
              {{ row.value }}
            </v-list-item-subtitle>
            <template #append>
              <div class="d-flex">
                <v-btn
                  icon="mdi-filter-outline"
                  size="x-small"
                  variant="text"
                  :title="t('siemCenter.events.filterByField')"
                  @click="filterByField(row.filterField, row.fullValue)"
                />
                <v-btn
                  icon="mdi-content-copy"
                  size="x-small"
                  variant="text"
                  @click="copyValue(row.label, row.fullValue)"
                />
              </div>
            </template>
          </v-list-item>
        </v-list>
      </div>

      <!-- Message / Fields -->
      <div class="ac-detail-section mb-3">
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
            <AppJsonViewer
              v-if="displayFieldsJson.trim()"
              :model-value="displayFieldsJson"
              max-height="360px"
            />
            <div v-else class="text-body-2 text-medium-emphasis">{{ t('siemCenter.events.noFields') }}</div>
          </v-tabs-window-item>
        </v-tabs-window>
      </div>

      <!-- Advanced: type, action key, parser, id -->
      <v-expansion-panels variant="accordion" class="mb-3 ac-detail-advanced">
        <v-expansion-panel elevation="0">
          <v-expansion-panel-title class="text-caption font-weight-medium px-0">
            {{ t('siemCenter.events.sectionAdvanced') }}
          </v-expansion-panel-title>
          <v-expansion-panel-text class="px-0">
            <v-list density="compact" class="bg-transparent pa-0">
              <v-list-item class="px-0">
                <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colSource') }}</v-list-item-title>
                <v-list-item-subtitle class="text-body-2">
                  {{ event.sourceType ? t(sourceTypeLabelKey(event.sourceType)) : '—' }}
                </v-list-item-subtitle>
              </v-list-item>
              <v-list-item class="px-0">
                <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.detailActionKey') }}</v-list-item-title>
                <v-list-item-subtitle class="text-body-2 font-weight-medium">{{ displayEventAction(event) }}</v-list-item-subtitle>
                <template #append>
                  <v-btn
                    icon="mdi-filter-outline"
                    size="x-small"
                    variant="text"
                    :title="t('siemCenter.events.filterByAction')"
                    @click="filterByAction"
                  />
                </template>
              </v-list-item>
              <v-list-item v-if="event.ingestedAt" class="px-0">
                <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.ingestedAt') }}</v-list-item-title>
                <v-list-item-subtitle class="text-body-2">{{ formatDate(event.ingestedAt) }}</v-list-item-subtitle>
              </v-list-item>
              <v-list-item v-if="event.parserId" class="px-0">
                <v-list-item-title class="text-caption text-medium-emphasis">{{ t('siemCenter.events.colParser') }}</v-list-item-title>
                <v-list-item-subtitle class="text-body-2">{{ event.parserId }}</v-list-item-subtitle>
              </v-list-item>
              <v-list-item class="px-0">
                <v-list-item-title class="text-caption text-medium-emphasis">ID</v-list-item-title>
                <v-list-item-subtitle class="text-body-2 text-truncate">{{ event.id }}</v-list-item-subtitle>
                <template #append>
                  <v-btn icon="mdi-content-copy" size="x-small" variant="text" @click="copyValue('ID', event.id)" />
                </template>
              </v-list-item>
            </v-list>
          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>

      <div class="d-flex flex-wrap gap-2 mt-2">
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
  border-left-width: 3px !important;
  border-left-style: solid;
  border-left-color: transparent;
}

.ac-sec-event-detail--accent-primary {
  border-left-color: rgb(var(--v-theme-primary)) !important;
}

.ac-sec-event-detail--accent-warning {
  border-left-color: rgb(var(--v-theme-warning)) !important;
}

.ac-sec-event-detail--accent-info {
  border-left-color: rgb(var(--v-theme-info)) !important;
}

.ac-detail-section__title {
  font-size: 0.8125rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgba(var(--v-theme-on-surface), 0.55);
  margin-bottom: 8px;
}

.ac-detail-chip--clickable {
  cursor: pointer;
}

.ac-detail-advanced :deep(.v-expansion-panel) {
  background: transparent;
}

.ac-detail-advanced :deep(.v-expansion-panel-title) {
  min-height: 36px;
  padding-inline: 0;
}

.ac-detail-advanced :deep(.v-expansion-panel-text__wrapper) {
  padding: 0;
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
